using Dapper;
using Microsoft.AspNetCore.Mvc;
using TFoodies.Api.Functions.Helpers;
using TFoodies.Api.Functions.Router;
using TFoodies.Application.Abstractions;
using TFoodies.Contracts.Common;

namespace TFoodies.Api.Functions.Controllers.Admin;

/// <summary>
/// 後台簡訊維護（隸屬 MemberMs 會員管理模組）。對齊舊系統 MemberMsController 的 Sms/Smsdetails。
///   GET    /admin/sms                          — 簡訊列表（分頁）
///   POST   /admin/sms                          — 新增簡訊
///   PUT    /admin/sms/{id}                      — 編輯簡訊
///   DELETE /admin/sms/{id}                      — 刪除簡訊（已有收訊人則拒絕）
///   GET    /admin/sms/{id}/recipients?issend=  — 收訊人列表
///   GET    /admin/sms/{id}/available-members    — 可加入的會員（尚非收訊人）
///   POST   /admin/sms/{id}/recipients          — 加入收訊人（memberIds[]）
///   DELETE /admin/sms/recipients/{detailId}    — 移除收訊人（已發送則拒絕）
///   POST   /admin/sms/{id}/send                — 開始發送（三竹簡訊閘道）
/// </summary>
public sealed class SmsAdminController
{
    private readonly IAdminPermissionService _perms;
    private readonly IDbConnectionFactory _db;
    private readonly ISmsService _sms;

    public SmsAdminController(IAdminPermissionService perms, IDbConnectionFactory db, ISmsService sms)
    {
        _perms = perms;
        _db = db;
        _sms = sms;
    }

    // ── 簡訊主檔 ────────────────────────────────────────────────────────────────

    // GET /admin/sms?page=&pageSize=
    public async Task<IActionResult> List(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "MemberMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        var q = ctx.Request.Query;
        var page = Math.Max(1, int.TryParse(q["page"], out var pg) ? pg : 1);
        var pageSize = Math.Clamp(int.TryParse(q["pageSize"], out var sz) ? sz : 20, 1, 100);
        var offset = (page - 1) * pageSize;

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        var total = await conn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Sms");

        var rows = await conn.QueryAsync<SmsRow>(@"
SELECT s.smsid, s.title, s.smbody, s.dlvtime,
       (SELECT COUNT(1) FROM Smsdetails d WHERE d.smsid = s.smsid) AS recipientCount
FROM Sms s
ORDER BY s.dlvtime DESC
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY",
            new { offset, pageSize });

        var list = rows.Select(r => (object)new
        {
            id = r.smsid,
            r.title,
            r.smbody,
            dlvtime = r.dlvtime,
            r.recipientCount
        }).ToList();

        return ctx.OkPaged(PaginatedResponse<object>.Create(list, total, page, pageSize));
    }

    // POST /admin/sms
    public async Task<IActionResult> Create(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "MemberMs", AdminOperation.Add);
        if (guard.Result is not null) return guard.Result;

        var body = await ctx.TryReadBodyAsync<UpsertSmsRequest>();
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");
        var err = Validate(body);
        if (err is not null) return ctx.BadRequest(err);

        var newId = Guid.NewGuid();
        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        await conn.ExecuteAsync(
            "INSERT INTO Sms (smsid, title, smbody, dlvtime) VALUES (@smsid, @title, @smbody, @dlvtime)",
            new { smsid = newId, body.Title, body.Smbody, body.Dlvtime });

        return ctx.Created(new { id = newId });
    }

    // PUT /admin/sms/{id}
    public async Task<IActionResult> Update(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "MemberMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var smsId))
            return ctx.BadRequest("無效的簡訊 ID。");

        var body = await ctx.TryReadBodyAsync<UpsertSmsRequest>();
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");
        var err = Validate(body);
        if (err is not null) return ctx.BadRequest(err);

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.ExecuteAsync(
            "UPDATE Sms SET title=@title, smbody=@smbody, dlvtime=@dlvtime WHERE smsid=@smsId",
            new { smsId, body.Title, body.Smbody, body.Dlvtime });

        if (rows == 0) return ctx.NotFound("找不到簡訊。");
        return ctx.Ok(new { message = "簡訊已更新" });
    }

    // DELETE /admin/sms/{id} — 已有收訊人則不可刪（對齊舊系統）
    public async Task<IActionResult> Delete(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "MemberMs", AdminOperation.Delete);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var smsId))
            return ctx.BadRequest("無效的簡訊 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        var hasRecipients = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Smsdetails WHERE smsid = @smsId", new { smsId });
        if (hasRecipients > 0) return ctx.BadRequest("此簡訊已有收訊人，無法刪除。");

        var rows = await conn.ExecuteAsync("DELETE FROM Sms WHERE smsid = @smsId", new { smsId });
        if (rows == 0) return ctx.NotFound("找不到簡訊。");
        return ctx.Ok(new { message = "簡訊已刪除" });
    }

    // ── 收訊人（Smsdetails）────────────────────────────────────────────────────

    // GET /admin/sms/{id}/recipients?issend=
    public async Task<IActionResult> Recipients(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "MemberMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var smsId))
            return ctx.BadRequest("無效的簡訊 ID。");

        int? issend = int.TryParse(ctx.Request.Query["issend"], out var s) ? s : null;

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        var sms = await conn.QuerySingleOrDefaultAsync<SmsRow>(
            "SELECT smsid, title, smbody, dlvtime, 0 AS recipientCount FROM Sms WHERE smsid = @smsId",
            new { smsId });
        if (sms is null) return ctx.NotFound("找不到簡訊。");

        var rows = await conn.QueryAsync<RecipientRow>(@"
SELECT d.smsdetailid, d.section, d.statuscode, d.issend,
       m.ismember, m.name, m.mobile, z.city AS city
FROM Smsdetails d
JOIN Members m  ON m.memberid = d.memberid
LEFT JOIN Zipcodes z ON z.zipcodeid = m.zipcodeid
WHERE d.smsid = @smsId AND (@issend IS NULL OR d.issend = @issend)
ORDER BY d.section",
            new { smsId, issend });

        var recipients = rows.Select(r => (object)new
        {
            id = r.smsdetailid,
            section = r.section,
            r.ismember,
            city = r.city,
            r.name,
            r.mobile,
            statusCode = r.statuscode,
            issend = r.issend
        }).ToList();

        return ctx.Ok(new
        {
            sms = new { id = sms.smsid, sms.title, sms.smbody, dlvtime = sms.dlvtime },
            recipients
        });
    }

    // GET /admin/sms/{id}/available-members — 尚非此簡訊收訊人的會員
    public async Task<IActionResult> AvailableMembers(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "MemberMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var smsId))
            return ctx.BadRequest("無效的簡訊 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.QueryAsync<AvailableMemberRow>(@"
SELECT m.memberid, m.name, m.mobile, m.ismember, z.city AS city
FROM Members m
LEFT JOIN Zipcodes z ON z.zipcodeid = m.zipcodeid
WHERE m.isenable = 1
  AND NOT EXISTS (SELECT 1 FROM Smsdetails d WHERE d.smsid = @smsId AND d.memberid = m.memberid)
ORDER BY m.name",
            new { smsId });

        var members = rows.Select(r => (object)new
        {
            id = r.memberid,
            r.name,
            r.mobile,
            r.ismember,
            city = r.city
        }).ToList();

        return ctx.Ok(new { members });
    }

    // POST /admin/sms/{id}/recipients — body { memberIds: [guid] }
    public async Task<IActionResult> AddRecipients(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "MemberMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var smsId))
            return ctx.BadRequest("無效的簡訊 ID。");

        var body = await ctx.TryReadBodyAsync<AddRecipientsRequest>();
        if (body is null || body.MemberIds is null || body.MemberIds.Length == 0)
            return ctx.BadRequest("尚未選擇會員。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        var exists = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Sms WHERE smsid = @smsId", new { smsId });
        if (exists == 0) return ctx.NotFound("找不到簡訊。");

        // 起始 section 接續目前最大值（對齊舊系統 AddSmsMembers）。
        var maxSection = await conn.ExecuteScalarAsync<int?>(
            "SELECT MAX(section) FROM Smsdetails WHERE smsid = @smsId", new { smsId }) ?? 0;

        var added = 0;
        foreach (var memberId in body.MemberIds.Distinct())
        {
            var dup = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM Smsdetails WHERE smsid = @smsId AND memberid = @memberId",
                new { smsId, memberId });
            if (dup > 0) continue;

            await conn.ExecuteAsync(@"
INSERT INTO Smsdetails (smsdetailid, smsid, memberid, section, issend)
VALUES (@smsdetailid, @smsId, @memberId, @section, 0)",
                new { smsdetailid = Guid.NewGuid(), smsId, memberId, section = ++maxSection });
            added++;
        }

        return ctx.Ok(new { message = $"已加入 {added} 位收訊人", added });
    }

    // DELETE /admin/sms/recipients/{detailId} — 已發送則不可刪（對齊舊系統）
    public async Task<IActionResult> DeleteRecipient(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "MemberMs", AdminOperation.Delete);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("detailId"), out var detailId))
            return ctx.BadRequest("無效的收訊人 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.ExecuteAsync(
            "DELETE FROM Smsdetails WHERE smsdetailid = @detailId AND issend <> 1",
            new { detailId });

        if (rows == 0) return ctx.BadRequest("找不到收訊人，或已發送無法移除。");
        return ctx.Ok(new { message = "收訊人已移除" });
    }

    // POST /admin/sms/{id}/send — 對未發送收訊人逐筆發送（三竹簡訊閘道）
    public async Task<IActionResult> Send(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "MemberMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var smsId))
            return ctx.BadRequest("無效的簡訊 ID。");

        var ct = ctx.Request.HttpContext.RequestAborted;
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var smbody = await conn.ExecuteScalarAsync<string?>(
            "SELECT smbody FROM Sms WHERE smsid = @smsId", new { smsId });
        if (smbody is null) return ctx.NotFound("找不到簡訊。");

        var pending = (await conn.QueryAsync<PendingRecipientRow>(@"
SELECT d.smsdetailid, m.mobile
FROM Smsdetails d
JOIN Members m ON m.memberid = d.memberid
WHERE d.smsid = @smsId AND d.issend = 0
ORDER BY d.section",
            new { smsId })).ToList();

        if (pending.Count == 0) return ctx.BadRequest("沒有待發送的收訊人。");

        int sent = 0, failed = 0;
        foreach (var r in pending)
        {
            var ok = await _sms.SendAsync(r.mobile, smbody, ct);
            if (ok)
            {
                await conn.ExecuteAsync(
                    "UPDATE Smsdetails SET issend = 1, statuscode = '0' WHERE smsdetailid = @id",
                    new { id = r.smsdetailid });
                sent++;
            }
            else
            {
                await conn.ExecuteAsync(
                    "UPDATE Smsdetails SET statuscode = 'e' WHERE smsdetailid = @id",
                    new { id = r.smsdetailid });
                failed++;
            }
        }

        return ctx.Ok(new { message = $"發送完成：成功 {sent} 筆、失敗 {failed} 筆", sent, failed });
    }

    // ── Validation & DTOs ──────────────────────────────────────────────────────

    private static string? Validate(UpsertSmsRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Title)) return "標題為必填欄位。";
        if (r.Title.Length > 50) return "標題長度不可超過 50 個字元。";
        if (string.IsNullOrWhiteSpace(r.Smbody)) return "簡訊內容為必填欄位。";
        if (r.Smbody.Length > 160) return "簡訊內容不可超過 160 個字元。";
        return null;
    }

    private sealed record SmsRow(Guid smsid, string title, string smbody, DateTime? dlvtime, int recipientCount);

    private sealed record RecipientRow(
        Guid smsdetailid, int? section, string? statuscode, int issend,
        int ismember, string name, string mobile, string? city);

    private sealed record AvailableMemberRow(
        Guid memberid, string name, string mobile, int ismember, string? city);

    private sealed record PendingRecipientRow(Guid smsdetailid, string mobile);

    private sealed record UpsertSmsRequest(string Title, string Smbody, DateTime? Dlvtime);

    private sealed record AddRecipientsRequest(Guid[] MemberIds);
}
