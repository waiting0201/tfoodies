using Dapper;
using Microsoft.AspNetCore.Mvc;
using TFoodies.Api.Functions.Helpers;
using TFoodies.Api.Functions.Router;
using TFoodies.Application.Abstractions;
using TFoodies.Contracts.Common;

namespace TFoodies.Api.Functions.Controllers.Admin;

/// <summary>
/// 後台會員管理。
///   GET    /admin/members          — 會員列表（搜尋/分頁）
///   GET    /admin/members/{id}     — 會員明細（含訂單摘要）
///   PUT    /admin/members/{id}     — 更新會員資料
///   DELETE /admin/members/{id}     — 軟刪除（isenable=false）
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

        var items = await conn.QueryAsync<MemberListRow>($@"
SELECT m.memberid, m.name, m.mobile, m.email,
       m.gender, m.birthday, m.isagent, m.isenable,
       m.createdate,
       (SELECT COUNT(1) FROM Orders o WHERE o.memberid=m.memberid) AS orderCount
FROM Members m
WHERE {whereSql}
ORDER BY m.createdate DESC
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY", dp);

        var list = items.Select(r => (object)new
        {
            id = r.memberid,
            r.name,
            r.mobile,
            r.email,
            level = r.isagent ? "代理商" : "一般會員",
            createdAt = r.createdate,
            isEnable = r.isenable,
            r.orderCount
        }).ToList();

        return ctx.OkPaged(PaginatedResponse<object>.Create(list, total, page, pageSize));
    }

    // GET /admin/members/{id}
    public async Task<IActionResult> Detail(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "MemberMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var memberId))
            return ctx.BadRequest("無效的會員 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        using var multi = await conn.QueryMultipleAsync(@"
SELECT memberid, name, mobile, email, gender, birthday,
       isagent, agentdiscount, zipcodeid, address, memo,
       isenable, ismember, createdate
FROM Members WHERE memberid = @memberId;

SELECT TOP 10 ordercode, orderdate, total, paystatus, deliverstatus
FROM Orders WHERE memberid = @memberId
ORDER BY createdate DESC;",
            new { memberId });

        var member = await multi.ReadSingleOrDefaultAsync<dynamic>();
        if (member is null) return ctx.NotFound("找不到會員");
        var recentOrders = (await multi.ReadAsync<dynamic>()).ToList();

        return ctx.Ok(new { member, recentOrders });
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

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        // 只在明確提供 password 時更新（明文；登入時自動升級為 PBKDF2）
        var rows = string.IsNullOrWhiteSpace(body.Password)
            ? await conn.ExecuteAsync(@"
UPDATE Members SET name=@name, email=@email, gender=@gender, birthday=@birthday,
    isagent=@isagent, agentdiscount=@agentdiscount,
    zipcodeid=@zipcodeid, address=@address, memo=@memo
WHERE memberid=@memberId",
                new { memberId, body.Name, body.Email, body.Gender, body.Birthday,
                      body.IsAgent, body.AgentDiscount, body.ZipcodeId, body.Address, body.Memo })
            : await conn.ExecuteAsync(@"
UPDATE Members SET name=@name, email=@email, gender=@gender, birthday=@birthday,
    isagent=@isagent, agentdiscount=@agentdiscount,
    zipcodeid=@zipcodeid, address=@address, memo=@memo, password=@password
WHERE memberid=@memberId",
                new { memberId, body.Name, body.Email, body.Gender, body.Birthday,
                      body.IsAgent, body.AgentDiscount, body.ZipcodeId, body.Address, body.Memo,
                      password = body.Password });

        if (rows == 0) return ctx.NotFound("找不到會員");
        return ctx.Ok(new { message = "會員資料已更新" });
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
        int? gender, DateTime? birthday, bool isagent, bool isenable,
        DateTime createdate, int orderCount);

    private sealed record UpdateMemberRequest(
        string Name, string? Email, int? Gender, DateTime? Birthday,
        bool IsAgent, decimal AgentDiscount,
        int? ZipcodeId, string? Address, string? Memo,
        string? Password);
}
