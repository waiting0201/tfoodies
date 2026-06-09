using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TFoodies.Contracts.Common;

namespace TFoodies.Api.Functions.Router;

/// <summary>
/// 每個 HTTP 請求共享的上下文，在 middleware pipeline 和 controller 之間傳遞。
/// 由 ApiFunction 在每次 Function 呼叫時建立（per-request），不可為 Singleton。
/// </summary>
public sealed class RouteContext
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public HttpRequest Request { get; }

    /// <summary>
    /// 去除 Function route prefix 後的路由字串，例如 "store/products" 或 "auth/login"
    /// </summary>
    public string Route { get; }

    /// <summary>
    /// RouteHandler 從 Regex 擷取的路徑參數，例如 { "id": "abc-123" }
    /// </summary>
    public Dictionary<string, string> PathParams { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 由 JwtAuthMiddleware 設定；未驗證路由為 null
    /// </summary>
    public ClaimsPrincipal? CurrentUser { get; set; }

    /// <summary>
    /// middleware 短路時設定此值；ApiFunction 回傳給 Functions host
    /// </summary>
    public IActionResult? Result { get; set; }

    public RouteContext(HttpRequest request, string route)
    {
        Request = request;
        Route = route.Trim('/');
    }

    // ── 回應輔助方法 ──────────────────────────────────────────────────────────

    public IActionResult Ok<T>(T data)
        => new OkObjectResult(data);

    public IActionResult OkPaged<T>(PaginatedResponse<T> paged)
        => new OkObjectResult(paged);

    public IActionResult Created<T>(T data)
        => new ObjectResult(data) { StatusCode = 201 };

    public IActionResult NoContent()
        => new NoContentResult();

    public IActionResult BadRequest(string message, string? details = null)
        => new BadRequestObjectResult(ApiErrorResponse.Create("BAD_REQUEST", message, details));

    public IActionResult NotFound(string message = "找不到資源")
        => new NotFoundObjectResult(ApiErrorResponse.Create("NOT_FOUND", message));

    public IActionResult Unauthorized(string message = "請先登入")
        => new UnauthorizedObjectResult(ApiErrorResponse.Create("UNAUTHORIZED", message));

    public IActionResult Forbidden(string message = "無訪問權限")
        => new ObjectResult(ApiErrorResponse.Create("FORBIDDEN", message)) { StatusCode = 403 };

    public IActionResult Conflict(string message)
        => new ConflictObjectResult(ApiErrorResponse.Create("CONFLICT", message));

    public IActionResult UnprocessableEntity(string message)
        => new ObjectResult(ApiErrorResponse.Create("UNPROCESSABLE_ENTITY", message)) { StatusCode = 422 };

    public IActionResult InternalServerError(string? correlationId = null)
        => new ObjectResult(ApiErrorResponse.Create("INTERNAL_ERROR", "伺服器發生未預期的錯誤", correlationId)) { StatusCode = 500 };

    public IActionResult File(byte[] content, string contentType, string fileName)
        => new FileContentResult(content, contentType) { FileDownloadName = fileName };

    // ── 請求輔助方法 ──────────────────────────────────────────────────────────

    public async Task<T> ReadBodyAsync<T>(CancellationToken ct = default)
    {
        try
        {
            var result = await JsonSerializer.DeserializeAsync<T>(Request.Body, JsonOptions, ct);
            if (result == null) throw new ArgumentException("Request body 為空或格式不正確。");
            return result;
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"JSON 格式錯誤：{ex.Message}");
        }
    }

    public async Task<T?> TryReadBodyAsync<T>(CancellationToken ct = default) where T : class
    {
        try { return await JsonSerializer.DeserializeAsync<T>(Request.Body, JsonOptions, ct); }
        catch { return null; }
    }

    public string RequirePathParam(string key)
    {
        if (PathParams.TryGetValue(key, out var value)) return value;
        throw new KeyNotFoundException($"路徑參數 '{key}' 不存在。");
    }
}
