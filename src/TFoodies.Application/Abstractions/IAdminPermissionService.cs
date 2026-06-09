namespace TFoodies.Application.Abstractions;

/// <summary>
/// 後台 RBAC：對應舊系統 Lims/AdminLims 樹狀授權。
///
/// 舊系統：Controller 名 → 頂層 Lim.Key；動作基底名 → 子層 Lim.Key。
///         AdminLims 連結 AdminID+LimID，附 IsAdd/IsUpdate/IsDelete 旗標。
/// 新系統：以 module (e.g. "OrderMs") 為授權粒度；
///         operation 對應 IsAdd/IsUpdate/IsDelete / 唯讀。
/// </summary>
public interface IAdminPermissionService
{
    /// <summary>
    /// 確認管理員是否存在且已啟用（不檢查模組權限）。
    /// </summary>
    Task<bool> IsActiveAdminAsync(int adminId, CancellationToken ct = default);

    /// <summary>
    /// 確認管理員是否可對指定模組執行指定操作。
    /// AdminID=888 (itadmin 後門) 永遠回傳 false。
    /// </summary>
    Task<bool> HasPermissionAsync(
        int adminId, string module, AdminOperation operation, CancellationToken ct = default);

    /// <summary>
    /// 取得管理員可存取的所有模組 + 操作（供前端側欄渲染）。
    /// </summary>
    Task<IReadOnlyList<AdminModulePermission>> GetPermissionsAsync(
        int adminId, CancellationToken ct = default);
}

public enum AdminOperation
{
    Read,
    Write,    // IsAdd or IsUpdate
    Add,      // IsAdd
    Update,   // IsUpdate
    Delete,   // IsDelete
}

public sealed record AdminModulePermission(
    string Module,
    string Label,
    bool CanAdd,
    bool CanUpdate,
    bool CanDelete);
