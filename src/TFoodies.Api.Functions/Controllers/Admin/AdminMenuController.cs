using Dapper;
using Microsoft.AspNetCore.Mvc;
using TFoodies.Api.Functions.Helpers;
using TFoodies.Api.Functions.Router;
using TFoodies.Application.Abstractions;

namespace TFoodies.Api.Functions.Controllers.Admin;

/// <summary>
/// 後台左側選單。直接由 Lims/AdminLims 樹動態產生（取代前端寫死的 navGroups），
/// 與舊系統 HtmlHelperExtensions.buildMenuItems 的可見性規則一致：
///
///   • itadmin (AdminID=888) → 顯示完整 Lims 樹。
///   • 一般管理員：
///       - 子項（leaf）：在 AdminLims 有授予記錄才顯示。
///       - 頂層（有子項）：底下任一子項可見即顯示，否則整組隱藏。
///       - 頂層（無子項，如 InvoiceMs/DiscountMs）：在 AdminLims 對自身 LimID 有授予才顯示。
///
///   GET /admin/menu — 回傳依權限過濾後的選單樹。
/// </summary>
public sealed class AdminMenuController
{
    private readonly IDbConnectionFactory _db;

    public AdminMenuController(IDbConnectionFactory db) => _db = db;

    public async Task<IActionResult> Get(RouteContext ctx)
    {
        var guard = AdminGuard.RequireAdmin(ctx);
        if (guard.Result is not null) return guard.Result;
        var adminId = guard.AdminId;

        var ct = ctx.Request.HttpContext.RequestAborted;
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var lims = (await conn.QueryAsync<LimRow>(
            "SELECT LimID, ParentID, [Key], [Value], Icon, Sort FROM Lims")).ToList();

        // 非 superadmin：取得該管理員所有「已啟用」的授予 LimID（leaf 與 childless-top 皆可能）。
        HashSet<int>? granted = null;
        if (adminId != 888)
        {
            var ids = await conn.QueryAsync<int>(
                @"SELECT al.LimID
                  FROM AdminLims al
                  JOIN Admins a ON a.AdminID = al.AdminID
                  WHERE al.AdminID = @adminId AND a.Isenable = 1",
                new { adminId });
            granted = ids.ToHashSet();
        }

        bool IsGranted(int limId) => granted is null || granted.Contains(limId);

        var items = new List<object>();
        var tops = lims.Where(l => l.ParentID is null)
                       .OrderBy(l => l.Sort).ThenBy(l => l.LimID);

        foreach (var top in tops)
        {
            var children = lims.Where(l => l.ParentID == top.LimID)
                               .OrderBy(l => l.Sort).ThenBy(l => l.LimID)
                               .ToList();

            if (children.Count > 0)
            {
                var visible = children.Where(c => IsGranted(c.LimID)).ToList();
                if (visible.Count == 0) continue;   // 無任何可見子項 → 整組隱藏

                items.Add(new
                {
                    key      = top.Key,
                    label    = top.Value,
                    icon     = top.Icon,
                    sort     = top.Sort,
                    children = visible.Select(c => (object)new
                    {
                        key   = c.Key,
                        label = c.Value,
                        sort  = c.Sort,
                    }).ToList(),
                });
            }
            else
            {
                if (!IsGranted(top.LimID)) continue;   // childless 頂層：需對自身有授予
                items.Add(new
                {
                    key      = top.Key,
                    label    = top.Value,
                    icon     = top.Icon,
                    sort     = top.Sort,
                    children = new List<object>(),
                });
            }
        }

        return ctx.Ok(new { items });
    }

    private sealed record LimRow(int LimID, int? ParentID, string? Key, string? Value, string? Icon, int Sort);
}
