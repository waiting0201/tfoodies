using Dapper;
using Microsoft.AspNetCore.Mvc;
using TFoodies.Api.Functions.Helpers;
using TFoodies.Api.Functions.Router;
using TFoodies.Application.Abstractions;

namespace TFoodies.Api.Functions.Controllers.Admin;

/// <summary>
/// 郵遞區號參照資料（縣市 / 鄉鎮市區）。供會員、訂單等地址欄位的縣市→區域連動使用。
/// 對齊舊系統 Ajax/GetZipcodeByCity。僅需登入（不綁特定模組權限）。
///   GET /admin/zipcodes/cities          — 縣市清單（distinct）
///   GET /admin/zipcodes/areas?city=     — 指定縣市的鄉鎮市區（含 zipcodeid）
/// </summary>
public sealed class ZipcodeAdminController
{
    private readonly IDbConnectionFactory _db;

    public ZipcodeAdminController(IDbConnectionFactory db) => _db = db;

    // GET /admin/zipcodes/cities
    public async Task<IActionResult> Cities(RouteContext ctx)
    {
        var guard = AdminGuard.RequireAdmin(ctx);
        if (guard.Result is not null) return guard.Result;

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var cities = (await conn.QueryAsync<string>(
            "SELECT DISTINCT city FROM Zipcodes ORDER BY city")).ToList();

        return ctx.Ok(new { cities });
    }

    // GET /admin/zipcodes/areas?city=台北市
    public async Task<IActionResult> Areas(RouteContext ctx)
    {
        var guard = AdminGuard.RequireAdmin(ctx);
        if (guard.Result is not null) return guard.Result;

        var city = ctx.Request.Query["city"].ToString().Trim();
        if (string.IsNullOrWhiteSpace(city)) return ctx.Ok(new { areas = Array.Empty<object>() });

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.QueryAsync<ZipcodeRow>(
            "SELECT zipcodeid, area, zipcode FROM Zipcodes WHERE city = @city ORDER BY zipcode",
            new { city });

        var areas = rows.Select(r => (object)new
        {
            zipcodeId = r.zipcodeid,
            r.area,
            r.zipcode
        }).ToList();

        return ctx.Ok(new { areas });
    }

    private sealed record ZipcodeRow(int zipcodeid, string area, string zipcode);
}
