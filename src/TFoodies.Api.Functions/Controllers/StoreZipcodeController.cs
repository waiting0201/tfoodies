using Dapper;
using Microsoft.AspNetCore.Mvc;
using TFoodies.Api.Functions.Router;
using TFoodies.Application.Abstractions;

namespace TFoodies.Api.Functions.Controllers;

/// <summary>
/// 前台結帳地址用的郵遞區號參照（縣市 → 鄉鎮市區）。對齊舊系統 Ajax/GetZipcodeByCity，
/// 供訪客結帳時的縣市/區域連動選單解析出 zipcodeid。公開端點（不需登入）。
///   GET /store/zipcodes/cities          — 縣市清單（distinct）
///   GET /store/zipcodes/areas?city=     — 指定縣市的鄉鎮市區（含 zipcodeid）
/// </summary>
public sealed class StoreZipcodeController
{
    private readonly IDbConnectionFactory _db;

    public StoreZipcodeController(IDbConnectionFactory db) => _db = db;

    // GET /store/zipcodes/cities
    public async Task<IActionResult> Cities(RouteContext ctx)
    {
        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var cities = (await conn.QueryAsync<string>(
            "SELECT DISTINCT city FROM Zipcodes ORDER BY city")).ToList();

        return ctx.Ok(new { cities });
    }

    // GET /store/zipcodes/areas?city=台北市
    public async Task<IActionResult> Areas(RouteContext ctx)
    {
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
