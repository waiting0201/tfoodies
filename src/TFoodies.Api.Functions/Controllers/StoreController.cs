using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using TFoodies.Application.Abstractions;
using TFoodies.Api.Functions.Router;
using TFoodies.Contracts.Common;

namespace TFoodies.Api.Functions.Controllers;

/// <summary>
/// 前台公開端點（無需登入）。Scoped，由 HandlerFactory 延遲解析。
/// </summary>
public sealed class StoreController
{
    // 前台內容清單一律三欄網格，每頁 9 筆（3×3）才不會多出落單的第 10 筆。
    private const int DefaultPageSize = 9;

    private readonly IStoreQueryService _store;
    private readonly IDbConnectionFactory _db;
    private readonly ICaptchaVerifier _captcha;

    public StoreController(IStoreQueryService store, IDbConnectionFactory db, ICaptchaVerifier captcha)
    {
        _store = store;
        _db = db;
        _captcha = captcha;
    }

    // GET /store/home
    public async Task<IActionResult> GetHome(RouteContext ctx)
    {
        var data = await _store.GetHomeAsync();
        return ctx.Ok(data);
    }

    // GET /store/products[?producttypetitle=]
    // Returns { productTypes, currentType, products } so the listing page can render the
    // type tabs + the current type's SEO copy alongside the products.
    public async Task<IActionResult> GetProducts(RouteContext ctx)
    {
        var typeTitle = ctx.Request.Query["producttypetitle"].ToString();
        var page = await _store.GetProductsAsync(string.IsNullOrWhiteSpace(typeTitle) ? null : typeTitle);
        return ctx.Ok(page);
    }

    // GET /store/products/detail?title=
    public async Task<IActionResult> GetProductDetail(RouteContext ctx)
    {
        var title = ctx.Request.Query["title"].ToString();
        if (string.IsNullOrWhiteSpace(title)) return ctx.BadRequest("缺少 title 參數。");
        var detail = await _store.GetProductDetailAsync(title);
        return detail is null ? ctx.NotFound("找不到商品。") : ctx.Ok(detail);
    }

    // GET /store/brands — 導覽列「品牌系列」下拉清單（isdisplay=1，依 sort）。
    public async Task<IActionResult> GetBrands(RouteContext ctx)
    {
        var brands = await _store.GetBrandsAsync();
        return ctx.Ok(brands);
    }

    // GET /store/brands/detail?brandtitle=
    public async Task<IActionResult> GetBrandDetail(RouteContext ctx)
    {
        var brandTitle = ctx.Request.Query["brandtitle"].ToString();
        if (string.IsNullOrWhiteSpace(brandTitle)) return ctx.BadRequest("缺少 brandtitle 參數。");
        var detail = await _store.GetBrandDetailAsync(brandTitle);
        return detail is null ? ctx.NotFound("找不到品牌。") : ctx.Ok(detail);
    }

    // GET /store/brands/products?brandtitle=&skip=4&take=4 — 品牌頁「More」分頁載入系列商品。
    public async Task<IActionResult> GetBrandProducts(RouteContext ctx)
    {
        var brandTitle = ctx.Request.Query["brandtitle"].ToString();
        if (string.IsNullOrWhiteSpace(brandTitle)) return ctx.BadRequest("缺少 brandtitle 參數。");
        int.TryParse(ctx.Request.Query["skip"].ToString(), out var skip);
        int.TryParse(ctx.Request.Query["take"].ToString(), out var take);
        var (products, hasMore) = await _store.GetBrandProductsAsync(brandTitle, skip, take > 0 ? take : 4);
        return ctx.Ok(new { products, hasMore });
    }

    // GET /store/news[?p=1]
    public async Task<IActionResult> GetNews(RouteContext ctx)
    {
        var (page, pageSize) = ParsePaging(ctx);
        var (items, total) = await _store.GetNewsAsync(page, pageSize);
        return ctx.OkPaged(PaginatedResponse<NewsListItem>.Create(items.ToList(), total, page, pageSize));
    }

    // GET /store/news/detail?newid=
    public async Task<IActionResult> GetNewsDetail(RouteContext ctx)
    {
        var raw = ctx.Request.Query["newid"].ToString();
        if (!Guid.TryParse(raw, out var newId)) return ctx.BadRequest("newid 格式不正確。");
        var detail = await _store.GetNewsDetailAsync(newId);
        return detail is null ? ctx.NotFound("找不到消息。") : ctx.Ok(detail);
    }

    // GET /store/recipes[?p=1&k=]
    public async Task<IActionResult> GetRecipes(RouteContext ctx)
    {
        var (page, pageSize) = ParsePaging(ctx);
        var keyword = ctx.Request.Query["k"].ToString();
        var (items, total) = await _store.GetRecipesAsync(page, pageSize, string.IsNullOrWhiteSpace(keyword) ? null : keyword);
        return ctx.OkPaged(PaginatedResponse<RecipeListItem>.Create(items.ToList(), total, page, pageSize));
    }

    // GET /store/recipes/detail?recipeid=
    public async Task<IActionResult> GetRecipeDetail(RouteContext ctx)
    {
        var raw = ctx.Request.Query["recipeid"].ToString();
        if (!Guid.TryParse(raw, out var recipeId)) return ctx.BadRequest("recipeid 格式不正確。");
        var detail = await _store.GetRecipeDetailAsync(recipeId);
        return detail is null ? ctx.NotFound("找不到食譜。") : ctx.Ok(detail);
    }

    // GET /store/issues[?p=1&k=]
    public async Task<IActionResult> GetIssues(RouteContext ctx)
    {
        var (page, pageSize) = ParsePaging(ctx);
        var keyword = ctx.Request.Query["k"].ToString();
        var (items, total) = await _store.GetIssuesAsync(page, pageSize, string.IsNullOrWhiteSpace(keyword) ? null : keyword);
        return ctx.OkPaged(PaginatedResponse<IssueListItem>.Create(items.ToList(), total, page, pageSize));
    }

    // GET /store/issues/detail?issuetitle=
    public async Task<IActionResult> GetIssueDetail(RouteContext ctx)
    {
        var issueTitle = ctx.Request.Query["issuetitle"].ToString();
        if (string.IsNullOrWhiteSpace(issueTitle)) return ctx.BadRequest("缺少 issuetitle 參數。");
        var detail = await _store.GetIssueDetailAsync(issueTitle);
        return detail is null ? ctx.NotFound("找不到綠誌。") : ctx.Ok(detail);
    }

    // GET /store/knowledges[?p=1&k=]
    public async Task<IActionResult> GetKnowledges(RouteContext ctx)
    {
        var (page, pageSize) = ParsePaging(ctx);
        var keyword = ctx.Request.Query["k"].ToString();
        var (items, total) = await _store.GetKnowledgesAsync(page, pageSize, string.IsNullOrWhiteSpace(keyword) ? null : keyword);
        return ctx.OkPaged(PaginatedResponse<KnowledgeListItem>.Create(items.ToList(), total, page, pageSize));
    }

    // GET /store/knowledges/detail?knowledgeid=
    public async Task<IActionResult> GetKnowledgeDetail(RouteContext ctx)
    {
        var raw = ctx.Request.Query["knowledgeid"].ToString();
        if (!Guid.TryParse(raw, out var knowledgeId)) return ctx.BadRequest("knowledgeid 格式不正確。");
        var detail = await _store.GetKnowledgeDetailAsync(knowledgeId);
        return detail is null ? ctx.NotFound("找不到小知識。") : ctx.Ok(detail);
    }

    // GET /store/blogs
    public async Task<IActionResult> GetBlogs(RouteContext ctx)
    {
        var items = await _store.GetBlogsAsync();
        return ctx.Ok(items);
    }

    // GET /store/events[?p=1]
    public async Task<IActionResult> GetEvents(RouteContext ctx)
    {
        var (page, pageSize) = ParsePaging(ctx);
        var (items, total) = await _store.GetEventsAsync(page, pageSize);
        return ctx.OkPaged(PaginatedResponse<EventListItem>.Create(items.ToList(), total, page, pageSize));
    }

    // GET /store/events/detail?eventid=
    public async Task<IActionResult> GetEventDetail(RouteContext ctx)
    {
        var raw = ctx.Request.Query["eventid"].ToString();
        if (!Guid.TryParse(raw, out var eventId)) return ctx.BadRequest("eventid 格式不正確。");
        var detail = await _store.GetEventDetailAsync(eventId);
        return detail is null ? ctx.NotFound("找不到活動。") : ctx.Ok(detail);
    }

    // GET /store/shopping-guide — 購物說明 / 會員常見問題（依分類分組，前台 /PageMs/Howtobuy）
    public async Task<IActionResult> GetShoppingGuide(RouteContext ctx)
    {
        var types = await _store.GetShoppingGuideAsync();
        return ctx.Ok(types);
    }

    // POST /store/outofnotices — 缺貨商品「到貨通知我」登記（公開；對齊舊系統 Ajax/PostOutofnotice）。
    // 顧客在商品缺貨時留下姓名/Email/電話，到貨後由後台（OrderMs → 缺貨通知）通知。
    // 防濫用：reCAPTCHA v3 + 同 Email/同商品「未通知」者去重（冪等）。登入會員自動帶 memberid。
    public async Task<IActionResult> CreateOutofnotice(RouteContext ctx)
    {
        var ct = ctx.Request.HttpContext.RequestAborted;

        var body = await ctx.TryReadBodyAsync<OutofnoticeRequest>(ct);
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");

        if (body.ProductId is null || body.ProductId == Guid.Empty)
            return ctx.BadRequest("缺少 productId 欄位。");
        if (string.IsNullOrWhiteSpace(body.Name)) return ctx.BadRequest("缺少 name 欄位。");
        if (string.IsNullOrWhiteSpace(body.Email)) return ctx.BadRequest("缺少 email 欄位。");
        if (string.IsNullOrWhiteSpace(body.Mobile)) return ctx.BadRequest("缺少 mobile 欄位。");

        // 人機驗證（未設定金鑰時 verifier 會放行）。
        if (!await _captcha.VerifyAsync(body.CaptchaToken, "outofnotice", ct))
            return ctx.BadRequest("人機驗證失敗，請重新整理後再試。");

        var productId = body.ProductId.Value;
        var email = body.Email!.Trim();

        using var conn = await _db.CreateOpenConnectionAsync(ct);

        // 商品必須存在，避免登記到不存在的商品。
        var productExists = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Products WHERE productid = @productId", new { productId });
        if (productExists == 0) return ctx.NotFound("找不到商品。");

        // 去重：同商品 + 同 Email 且尚未通知者，視為已登記（冪等，不重複寫入）。
        var pending = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Outofnotices WHERE productid = @productId AND email = @email AND isnotice = 0",
            new { productId, email });
        if (pending > 0) return ctx.Ok(new { message = "您已登記過此商品的到貨通知。", duplicated = true });

        await conn.ExecuteAsync(@"
INSERT INTO Outofnotices (outofnoticeid, productid, memberid, name, email, mobile, createdate, isnotice)
VALUES (@outofnoticeid, @productid, @memberid, @name, @email, @mobile, @createdate, 0)",
            new
            {
                outofnoticeid = Guid.NewGuid(),
                productid     = productId,
                memberid      = ExtractMemberId(ctx.CurrentUser),
                name          = body.Name!.Trim(),
                email,
                mobile        = body.Mobile!.Trim(),
                createdate    = DateTime.UtcNow.AddHours(8),
            });

        return ctx.Created(new { message = "感謝您，到貨時將通知您！" });
    }

    // 登入會員帶 Bearer 時自動關聯 memberid；匿名登記則為 null（對齊 StoreOrderController）。
    private static Guid? ExtractMemberId(ClaimsPrincipal? user)
    {
        if (user is null) return null;
        var role = user.FindFirstValue(ClaimTypes.Role);
        if (!string.Equals(role, "member", StringComparison.OrdinalIgnoreCase)) return null;
        var id = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out var guid) ? guid : null;
    }

    private sealed record OutofnoticeRequest(
        Guid? ProductId, string? Name, string? Email, string? Mobile, string? CaptchaToken);

    private static (int page, int pageSize) ParsePaging(RouteContext ctx)
    {
        int.TryParse(ctx.Request.Query["p"].ToString(), out var p);
        int.TryParse(ctx.Request.Query["pageSize"].ToString(), out var ps);
        return (Math.Max(1, p), ps > 0 ? Math.Min(ps, 100) : DefaultPageSize);
    }
}
