using Dapper;
using TFoodies.Application.Abstractions;

namespace TFoodies.Infrastructure.Permissions;

/// <summary>
/// 移植舊系統 CheckSessionAttribute 的 Lims/AdminLims 樹狀 RBAC。
///
/// Lims 樹：頂層（ParentID IS NULL）Key = 模組名（OrderMs, ProductMs...）
///          子層 Key = 功能名（Orders, Shipments...）
/// AdminLims：AdminID ↔ LimID，含 IsAdd/IsUpdate/IsDelete。
///
/// 新系統簡化：只做頂層模組授權，不再用動作字串推導。
/// </summary>
public sealed class SqlAdminPermissionService : IAdminPermissionService
{
    private readonly IDbConnectionFactory _db;

    public SqlAdminPermissionService(IDbConnectionFactory db) => _db = db;

    public async Task<bool> IsActiveAdminAsync(int adminId, CancellationToken ct = default)
    {
        if (adminId == 888) return true;   // itadmin 超級管理員，繞過 DB 查詢
        using var conn = await _db.CreateOpenConnectionAsync(ct);
        var count = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Admins WHERE AdminID = @adminId AND Isenable = 1",
            new { adminId });
        return count > 0;
    }

    public async Task<bool> HasPermissionAsync(
        int adminId, string module, AdminOperation operation, CancellationToken ct = default)
    {
        if (adminId == 888) return true;   // itadmin 超級管理員，擁有所有模組完整權限
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        // AdminLims 以「子層 Lim」為單位儲存（對齊舊系統與權限編輯器）。
        // 模組授權 = 該模組（頂層 Lim）底下任一子項有授予記錄；
        // 操作旗標取所有子項的聯集（OR），對應舊系統「父層有任一子項即視為有權」。
        var grant = await conn.QuerySingleOrDefaultAsync<GrantRow>(
            @"SELECT
                  CAST(MAX(CAST(al.IsAdd    AS INT)) AS BIT) AS IsAdd,
                  CAST(MAX(CAST(al.IsUpdate AS INT)) AS BIT) AS IsUpdate,
                  CAST(MAX(CAST(al.IsDelete AS INT)) AS BIT) AS IsDelete
              FROM AdminLims al
              JOIN Lims child  ON child.LimID = al.LimID
              JOIN Lims parent ON parent.LimID = child.ParentID
              JOIN Admins a    ON a.AdminID = al.AdminID
              WHERE al.AdminID = @adminId
                AND parent.[Key] = @module AND parent.ParentID IS NULL
                AND a.Isenable = 1
              HAVING COUNT(1) > 0",
            new { adminId, module });

        if (grant is null) return false;

        return operation switch
        {
            AdminOperation.Read => true,           // 只要有任一子項授予即可讀
            AdminOperation.Add => grant.IsAdd,
            AdminOperation.Update => grant.IsUpdate,
            AdminOperation.Write => grant.IsAdd || grant.IsUpdate,
            AdminOperation.Delete => grant.IsDelete,
            _ => false,
        };
    }

    // itadmin 超級管理員擁有的完整模組清單（對應 Lims.[Key]，含新增模組）
    private static readonly IReadOnlyList<AdminModulePermission> SuperAdminPermissions =
    [
        new("HomeMs",       "網頁管理",   true, true, true),
        new("ProductMs",    "產品管理",   true, true, true),
        new("OrderMs",      "訂單管理",   true, true, true),
        new("MemberMs",     "會員管理",   true, true, true),
        new("InventoryMs",  "庫存管理",   true, true, true),
        new("PurchaseMs",   "採購管理",   true, true, true),
        new("AccountingMs", "會計帳管理", true, true, true),
        new("ReportMs",     "報表管理",   true, true, true),
        new("InvoiceMs",    "發票管理",   true, true, true),
        new("DiscountMs",   "折扣管理",   true, true, true),
        new("SettingMs",    "系統管理",   true, true, true),
    ];

    public async Task<IReadOnlyList<AdminModulePermission>> GetPermissionsAsync(
        int adminId, CancellationToken ct = default)
    {
        if (adminId == 888) return SuperAdminPermissions;

        using var conn = await _db.CreateOpenConnectionAsync(ct);

        // AdminLims 以「子層 Lim」為單位儲存；彙整回「頂層模組」供前端側欄渲染。
        // 父層（模組）只要底下任一子項有授予即列入；操作旗標取子項聯集（OR）。
        // 對應舊系統 buildMenuItems 第一層判定 siteLink.Lims1.Any(...)。
        var rows = await conn.QueryAsync<PermissionRow>(@"
SELECT parent.[Key] AS Module, parent.[Value] AS Label,
       CAST(MAX(CAST(al.IsAdd    AS INT)) AS BIT) AS IsAdd,
       CAST(MAX(CAST(al.IsUpdate AS INT)) AS BIT) AS IsUpdate,
       CAST(MAX(CAST(al.IsDelete AS INT)) AS BIT) AS IsDelete
FROM AdminLims al
JOIN Lims child  ON child.LimID = al.LimID
JOIN Lims parent ON parent.LimID = child.ParentID
JOIN Admins a    ON a.AdminID = al.AdminID
WHERE al.AdminID = @adminId
  AND parent.ParentID IS NULL
  AND a.Isenable = 1
GROUP BY parent.[Key], parent.[Value], parent.Sort
ORDER BY parent.Sort",
            new { adminId });

        return rows
            .Select(r => new AdminModulePermission(r.Module ?? string.Empty, r.Label ?? string.Empty,
                r.IsAdd, r.IsUpdate, r.IsDelete))
            .ToList();
    }

    private sealed record GrantRow(bool IsAdd, bool IsUpdate, bool IsDelete);
    private sealed record PermissionRow(string? Module, string? Label, bool IsAdd, bool IsUpdate, bool IsDelete);
}
