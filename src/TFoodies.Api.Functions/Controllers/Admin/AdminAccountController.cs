using System.Data;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using TFoodies.Api.Functions.Helpers;
using TFoodies.Api.Functions.Router;
using TFoodies.Application.Abstractions;

namespace TFoodies.Api.Functions.Controllers.Admin;

/// <summary>
/// 後台管理員帳號管理。
///   GET  /admin/admin-accounts              — 管理員列表（排除 ID=888）
///   POST /admin/admin-accounts              — 新增管理員
///   PUT  /admin/admin-accounts/{id}         — 更新管理員資訊
///   GET  /admin/admin-accounts/{id}/permissions — 取得管理員模組授權
///   PUT  /admin/admin-accounts/{id}/permissions — 覆寫管理員模組授權
/// </summary>
public sealed class AdminAccountController
{
    private readonly IAdminPermissionService _perms;
    private readonly IDbConnectionFactory _db;

    public AdminAccountController(IAdminPermissionService perms, IDbConnectionFactory db)
    {
        _perms = perms;
        _db = db;
    }

    // GET /admin/admin-accounts
    public async Task<IActionResult> List(RouteContext ctx)
    {
        var guard = AdminGuard.RequireAdmin(ctx);
        if (guard.Result is not null) return guard.Result;

        var ct = ctx.Request.HttpContext.RequestAborted;
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var rows = await conn.QueryAsync(@"
SELECT adminid, username, name, email, isenable
FROM Admins
WHERE adminid != 888
ORDER BY adminid");

        return ctx.Ok(rows);
    }

    // POST /admin/admin-accounts
    public async Task<IActionResult> Create(RouteContext ctx)
    {
        var guard = AdminGuard.RequireAdmin(ctx);
        if (guard.Result is not null) return guard.Result;

        var body = await ctx.TryReadBodyAsync<CreateAdminRequest>();
        if (body is null || string.IsNullOrWhiteSpace(body.Username))
            return ctx.BadRequest("缺少 username 欄位。");
        if (string.IsNullOrWhiteSpace(body.Password))
            return ctx.BadRequest("缺少 password 欄位。");

        var hashed = HashPassword(body.Password);
        var ct = ctx.Request.HttpContext.RequestAborted;
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        // 檢查 username 是否已存在
        var exists = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Admins WHERE username = @username",
            new { username = body.Username });
        if (exists > 0) return ctx.Conflict("此帳號名稱已存在。");

        var adminId = await conn.ExecuteScalarAsync<int>(@"
INSERT INTO Admins (username, password, name, email, isenable)
OUTPUT INSERTED.adminid
VALUES (@username, @password, @name, @email, 1)",
            new
            {
                username = body.Username,
                password = hashed,
                name = body.Name ?? string.Empty,
                email = body.Email,
            });

        return ctx.Created(new { adminId });
    }

    // PUT /admin/admin-accounts/{id}
    public async Task<IActionResult> Update(RouteContext ctx)
    {
        var guard = AdminGuard.RequireAdmin(ctx);
        if (guard.Result is not null) return guard.Result;

        if (!int.TryParse(ctx.RequirePathParam("id"), out var adminId))
            return ctx.BadRequest("無效的管理員 ID。");
        if (adminId == 888) return ctx.Forbidden("無法修改保留帳號。");

        var body = await ctx.TryReadBodyAsync<UpdateAdminRequest>();
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");

        var setClauses = new List<string>();
        var p = new DynamicParameters();
        p.Add("adminId", adminId);

        if (body.Name is not null) { setClauses.Add("name=@name"); p.Add("name", body.Name); }
        if (body.Email is not null) { setClauses.Add("email=@email"); p.Add("email", body.Email); }
        if (!string.IsNullOrWhiteSpace(body.Password))
        {
            setClauses.Add("password=@password");
            p.Add("password", HashPassword(body.Password));
        }
        if (body.IsEnable.HasValue) { setClauses.Add("isenable=@isenable"); p.Add("isenable", body.IsEnable.Value); }

        if (setClauses.Count == 0) return ctx.BadRequest("沒有可更新的欄位。");

        var ct = ctx.Request.HttpContext.RequestAborted;
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var rows = await conn.ExecuteAsync(
            $"UPDATE Admins SET {string.Join(", ", setClauses)} WHERE adminid=@adminId AND adminid!=888",
            p);

        if (rows == 0) return ctx.NotFound("找不到管理員帳號。");
        return ctx.Ok(new { message = "管理員資訊已更新" });
    }

    // GET /admin/admin-accounts/{id}/permissions
    public async Task<IActionResult> GetPermissions(RouteContext ctx)
    {
        var guard = AdminGuard.RequireAdmin(ctx);
        if (guard.Result is not null) return guard.Result;

        if (!int.TryParse(ctx.RequirePathParam("id"), out var adminId))
            return ctx.BadRequest("無效的管理員 ID。");

        var ct = ctx.Request.HttpContext.RequestAborted;
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var rows = await conn.QueryAsync(@"
SELECT l.limid, l.key, l.label, l.parentid,
       ISNULL(al.isadd,  0) AS canAdd,
       ISNULL(al.isupdate, 0) AS canUpdate,
       ISNULL(al.isdelete, 0) AS canDelete
FROM Lims l
LEFT JOIN AdminLims al ON al.limid = l.limid AND al.adminid = @adminId
ORDER BY l.limid",
            new { adminId });

        return ctx.Ok(rows);
    }

    // PUT /admin/admin-accounts/{id}/permissions
    public async Task<IActionResult> SetPermissions(RouteContext ctx)
    {
        var guard = AdminGuard.RequireAdmin(ctx);
        if (guard.Result is not null) return guard.Result;

        if (!int.TryParse(ctx.RequirePathParam("id"), out var adminId))
            return ctx.BadRequest("無效的管理員 ID。");
        if (adminId == 888) return ctx.Forbidden("無法修改保留帳號的權限。");

        var body = await ctx.TryReadBodyAsync<SetPermissionsRequest>();
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");

        var ct = ctx.Request.HttpContext.RequestAborted;
        using var conn = (SqlConnection)await _db.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            await conn.ExecuteAsync(
                "DELETE AdminLims WHERE adminid = @adminId",
                new { adminId }, tx);

            if (body.Grants is not null)
            {
                foreach (var grant in body.Grants)
                {
                    await conn.ExecuteAsync(@"
INSERT INTO AdminLims (adminid, limid, isadd, isupdate, isdelete)
VALUES (@adminId, @limId, @canAdd, @canUpdate, @canDelete)",
                        new
                        {
                            adminId,
                            limId = grant.LimId,
                            canAdd = grant.CanAdd,
                            canUpdate = grant.CanUpdate,
                            canDelete = grant.CanDelete,
                        }, tx);
                }
            }

            tx.Commit();
            return ctx.Ok(new { message = "權限已更新" });
        }
        catch { tx.Rollback(); throw; }
    }

    // ── Password helper (mirrors AuthService.HashPassword) ────────────────────────

    private static string HashPassword(string password)
    {
        const int Iterations = 260_000;
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            32);
        return $"pbkdf2:{Iterations}:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    // ── Row / DTO types ───────────────────────────────────────────────────────────

    private sealed record CreateAdminRequest(
        string? Username,
        string? Password,
        string? Name,
        string? Email);

    private sealed record UpdateAdminRequest(
        string? Name,
        string? Email,
        string? Password,
        bool? IsEnable);

    private sealed record SetPermissionsRequest(List<PermissionGrant>? Grants);

    private sealed record PermissionGrant(int LimId, bool CanAdd, bool CanUpdate, bool CanDelete);
}
