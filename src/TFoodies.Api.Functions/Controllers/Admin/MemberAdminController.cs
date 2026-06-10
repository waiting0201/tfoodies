using Dapper;
using Microsoft.AspNetCore.Mvc;
using TFoodies.Api.Functions.Helpers;
using TFoodies.Api.Functions.Router;
using TFoodies.Application.Abstractions;
using TFoodies.Contracts.Common;

namespace TFoodies.Api.Functions.Controllers.Admin;

/// <summary>
/// 後台會員管理（MemberMs 模組）。欄位與行為對齊舊系統 MemberMsController。
///   GET    /admin/members               — 會員列表（型態/縣市/姓名/性別/電話/Email/開通）
///   GET    /admin/members/check-mobile  — 手機號碼查重（對應舊系統 Ajax/Checkmobile）
///   POST   /admin/members               — 新增會員
///   GET    /admin/members/{id}          — 會員明細（含訂單摘要）
///   PUT    /admin/members/{id}          — 更新會員資料
///   DELETE /admin/members/{id}          — 軟刪除（isenable=false）
/// </summary>
public sealed class MemberAdminController
{
    private readonly IAdminPermissionService _perms;
    private readonly IDbConnectionFactory _db;

    public MemberAdminController(IAdminPermissionService perms, IDbConnectionFactory db)
    {
        _perms = perms;
        _db = db;
    }

    // GET /admin/members?keyword=&page=1&pageSize=20
    public async Task<IActionResult> List(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "MemberMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        var q = ctx.Request.Query;
        var keyword = q["keyword"].ToString().Trim();
        var page = Math.Max(1, int.TryParse(q["page"], out var pg) ? pg : 1);
        var pageSize = Math.Clamp(int.TryParse(q["pageSize"], out var sz) ? sz : 20, 1, 100);
        var offset = (page - 1) * pageSize;

        var (whereSql, whereParams) = BuildMemberWhere(keyword);
        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        var total = await conn.ExecuteScalarAsync<int>(
            $"SELECT COUNT(1) FROM Members m WHERE {whereSql}", whereParams);

        var dp = new DynamicParameters(whereParams);
        dp.Add("offset", offset); dp.Add("pageSize", pageSize);

        // 舊系統欄位：型態(ismember) / 縣市(Zipcodes.city) / 姓名 / 性別 / 電話 / Email / 開通(isenable)
        var items = await conn.QueryAsync<MemberListRow>($@"
SELECT m.memberid, m.name, m.mobile, m.email,
       m.gender, m.ismember, m.isenable,
       z.city AS city
FROM Members m
LEFT JOIN Zipcodes z ON z.zipcodeid = m.zipcodeid
WHERE {whereSql}
ORDER BY m.name
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY", dp);

        var list = items.Select(r => (object)new
        {
            id = r.memberid,
            r.ismember,
            city = r.city,
            r.name,
            gender = r.gender,
            r.mobile,
            r.email,
            isEnable = r.isenable
        }).ToList();

        return ctx.OkPaged(PaginatedResponse<object>.Create(list, total, page, pageSize));
    }

    // GET /admin/members/check-mobile?mobile=&excludeId=
    // valid=true 代表手機可用（無重複）；對齊舊系統 Ajax/Checkmobile。
    public async Task<IActionResult> CheckMobile(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "MemberMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        var mobile = ctx.Request.Query["mobile"].ToString().Trim();
        if (string.IsNullOrWhiteSpace(mobile)) return ctx.Ok(new { valid = false });

        Guid? excludeId = Guid.TryParse(ctx.Request.Query["excludeId"], out var eid) ? eid : null;

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var count = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Members WHERE mobile = @mobile AND (@excludeId IS NULL OR memberid <> @excludeId)",
            new { mobile, excludeId });

        return ctx.Ok(new { valid = count == 0 });
    }

    // POST /admin/members
    public async Task<IActionResult> Create(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "MemberMs", AdminOperation.Add);
        if (guard.Result is not null) return guard.Result;

        var body = await ctx.TryReadBodyAsync<CreateMemberRequest>();
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");
        if (string.IsNullOrWhiteSpace(body.Name)) return ctx.BadRequest("缺少 name 欄位。");
        if (string.IsNullOrWhiteSpace(body.Mobile)) return ctx.BadRequest("缺少 mobile 欄位。");
        if (string.IsNullOrWhiteSpace(body.Password)) return ctx.BadRequest("缺少 password 欄位。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        var dup = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Members WHERE mobile = @mobile", new { body.Mobile });
        if (dup > 0) return ctx.BadRequest("手機號碼已存在。");

        var newId = Guid.NewGuid();
        var ismember = body.Ismember == 1 ? 1 : 2;   // 預設客戶(2)，對齊舊系統

        // isagent=0、agentdiscount=1、isenable=1 為新增會員固定值（對齊舊系統 AddMembers）。
        await conn.ExecuteAsync(@"
INSERT INTO Members
    (memberid, name, mobile, password, gender, birthday, email,
     zipcodeid, address, isagent, agentdiscount, memo, createdate, ismember, isenable)
VALUES
    (@memberid, @name, @mobile, @password, @gender, @birthday, @email,
     @zipcodeid, @address, 0, 1, @memo, @createdate, @ismember, 1)",
            new
            {
                memberid = newId,
                body.Name,
                body.Mobile,
                password = body.Password,
                body.Gender,
                body.Birthday,
                body.Email,
                zipcodeid = body.ZipcodeId,
                body.Address,
                body.Memo,
                createdate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8)),
                ismember
            });

        return ctx.Created(new { id = newId });
    }

    // GET /admin/members/{id}
    public async Task<IActionResult> Detail(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "MemberMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var memberId))
            return ctx.BadRequest("無效的會員 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        var row = await conn.QuerySingleOrDefaultAsync<MemberDetailRow>(@"
SELECT m.memberid, m.name, m.mobile, m.email, m.gender, m.birthday,
       m.isagent, m.agentdiscount, m.zipcodeid, m.address, m.memo,
       m.isenable, m.ismember, m.createdate,
       z.city AS city, z.area AS area,
       (SELECT COUNT(1)        FROM Orders o WHERE o.memberid = m.memberid) AS orderCount,
       (SELECT ISNULL(SUM(o.total), 0) FROM Orders o WHERE o.memberid = m.memberid) AS totalSpend
FROM Members m
LEFT JOIN Zipcodes z ON z.zipcodeid = m.zipcodeid
WHERE m.memberid = @memberId", new { memberId });

        if (row is null) return ctx.NotFound("找不到會員");

        var recentOrders = (await conn.QueryAsync<OrderSummaryRow>(@"
SELECT TOP 10 ordercode, orderdate, total, paystatus, deliverstatus
FROM Orders WHERE memberid = @memberId
ORDER BY createdate DESC", new { memberId }))
            .Select(o => (object)new
            {
                o.ordercode, o.orderdate, o.total, o.paystatus, o.deliverstatus
            }).ToList();

        return ctx.Ok(new
        {
            id = row.memberid,
            row.name,
            row.mobile,
            row.email,
            gender = row.gender,
            birthday = row.birthday,
            ismember = row.ismember,
            isEnable = row.isenable,
            isAgent = row.isagent,
            agentDiscount = row.agentdiscount,
            zipcodeId = row.zipcodeid,
            city = row.city,
            area = row.area,
            address = row.address,
            note = row.memo,
            createdAt = row.createdate,
            totalOrders = row.orderCount,
            totalSpend = row.totalSpend,
            recentOrders
        });
    }

    // PUT /admin/members/{id}
    public async Task<IActionResult> Update(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "MemberMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var memberId))
            return ctx.BadRequest("無效的會員 ID。");

        var body = await ctx.TryReadBodyAsync<UpdateMemberRequest>();
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");
        if (string.IsNullOrWhiteSpace(body.Name)) return ctx.BadRequest("缺少 name 欄位。");
        if (string.IsNullOrWhiteSpace(body.Mobile)) return ctx.BadRequest("缺少 mobile 欄位。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        var dup = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Members WHERE mobile = @mobile AND memberid <> @memberId",
            new { body.Mobile, memberId });
        if (dup > 0) return ctx.BadRequest("手機號碼已存在。");

        var ismember = body.Ismember == 1 ? 1 : 2;

        // 白名單對齊舊系統 EditMembers：isenable/name/mobile/email/gender/birthday/zipcodeid/address/ismember/memo。
        // 不更新 isagent/agentdiscount（保留代理商狀態）。只在明確提供 password 時更新。
        var sql = @"
UPDATE Members SET
    name=@name, mobile=@mobile, email=@email, gender=@gender, birthday=@birthday,
    zipcodeid=@zipcodeid, address=@address, ismember=@ismember, isenable=@isenable, memo=@memo"
            + (string.IsNullOrWhiteSpace(body.Password) ? "" : ", password=@password")
            + " WHERE memberid=@memberId";

        var rows = await conn.ExecuteAsync(sql, new
        {
            memberId,
            body.Name,
            body.Mobile,
            body.Email,
            body.Gender,
            body.Birthday,
            zipcodeid = body.ZipcodeId,
            body.Address,
            ismember,
            isenable = body.IsEnable,
            body.Memo,
            password = body.Password
        });

        if (rows == 0) return ctx.NotFound("找不到會員");

        return ctx.Ok(new
        {
            id = memberId,
            body.Name,
            body.Mobile,
            body.Email,
            gender = body.Gender,
            birthday = body.Birthday,
            ismember,
            isEnable = body.IsEnable,
            zipcodeId = body.ZipcodeId,
            address = body.Address,
            note = body.Memo,
            message = "會員資料已更新"
        });
    }

    // DELETE /admin/members/{id} — 軟刪除（isenable=false）
    public async Task<IActionResult> Delete(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "MemberMs", AdminOperation.Delete);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var memberId))
            return ctx.BadRequest("無效的會員 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        var rows = await conn.ExecuteAsync(
            "UPDATE Members SET isenable=0 WHERE memberid=@memberId AND isenable=1",
            new { memberId });

        if (rows == 0) return ctx.NotFound("找不到會員或會員已停用。");
        return ctx.Ok(new { message = "會員已停用" });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static (string Sql, object Params) BuildMemberWhere(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return ("1=1", new { });

        return (
            "(m.name LIKE @kw OR m.mobile LIKE @kw OR m.email LIKE @kw)",
            new { kw = $"%{keyword}%" } as object);
    }

    // ── Row / DTO types ───────────────────────────────────────────────────────────

    private sealed record MemberListRow(
        Guid memberid, string name, string mobile, string? email,
        int? gender, int ismember, bool isenable, string? city);

    // Dapper 以 DateTime 回傳 SQL date 欄位，故 birthday/createdate 用 DateTime（見 feedback-dapper-datetime）
    private sealed record MemberDetailRow(
        Guid memberid, string name, string mobile, string? email,
        int? gender, DateTime? birthday, bool isagent, decimal agentdiscount,
        int? zipcodeid, string? address, string? memo,
        bool isenable, int ismember, DateTime createdate,
        string? city, string? area,
        int orderCount, int totalSpend);

    private sealed record OrderSummaryRow(
        string ordercode, DateTime orderdate, int total, int paystatus, int deliverstatus);

    private sealed record CreateMemberRequest(
        string Name, string Mobile, string Password,
        string? Email, int? Gender, DateTime? Birthday,
        int? ZipcodeId, string? Address, int? Ismember, string? Memo);

    // isagent/agentdiscount 編輯時保留不變（與舊系統一致），故不在請求 DTO 中。
    private sealed record UpdateMemberRequest(
        string Name, string Mobile, string? Email, int? Gender, DateTime? Birthday,
        int? ZipcodeId, string? Address, int? Ismember, bool IsEnable, string? Memo,
        string? Password);
}
