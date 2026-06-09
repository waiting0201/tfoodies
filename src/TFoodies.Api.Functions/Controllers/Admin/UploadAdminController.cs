using Microsoft.AspNetCore.Mvc;
using TFoodies.Application.Abstractions;
using TFoodies.Api.Functions.Helpers;
using TFoodies.Api.Functions.Router;

namespace TFoodies.Api.Functions.Controllers.Admin;

/// <summary>
/// 後台共用圖片上傳端點。
///   POST /admin/upload   multipart/form-data，field 名稱 "file"
///   回傳: { "url": "https://..." }
///
/// 檔名格式與舊系統一致：yyyyMMddHHmmssff + 原始副檔名（小寫）。
/// Container 由 AzureBlob:ContainerName 設定，預設 "tfoodies"（與舊系統相同）。
/// </summary>
public sealed class UploadAdminController
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp"
    };

    private readonly IBlobService _blob;

    public UploadAdminController(IBlobService blob)
    {
        _blob = blob;
    }

    // POST /admin/upload
    public async Task<IActionResult> Upload(RouteContext ctx)
    {
        var guard = AdminGuard.RequireAdmin(ctx);
        if (guard.Result is not null) return guard.Result;

        var ct = ctx.Request.HttpContext.RequestAborted;

        if (!ctx.Request.HasFormContentType)
            return ctx.BadRequest("請使用 multipart/form-data 上傳。");

        var form = await ctx.Request.ReadFormAsync(ct);
        var file = form.Files["file"];

        if (file is null || file.Length == 0)
            return ctx.BadRequest("缺少 file 欄位或檔案為空。");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return ctx.BadRequest($"不支援的檔案格式 {ext}，允許：jpg、png、gif、webp。");

        // 檔名格式與舊系統一致：yyyyMMddHHmmssff + 副檔名
        var fileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + ext;

        await using var stream = file.OpenReadStream();
        await _blob.UploadAsync(stream, fileName, file.ContentType, ct);

        return ctx.Ok(new { fileName });
    }
}
