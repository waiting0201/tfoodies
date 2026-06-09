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
    private const int DefaultPageSize = 10;

    private readonly IStoreQueryService _store;

    public StoreController(IStoreQueryService store) => _store = store;

    // GET /store/home
    public async Task<IActionResult> GetHome(RouteContext ctx)
    {
        var data = await _store.GetHomeAsync();
        return ctx.Ok(data);
    }

    // GET /store/products[?producttypetitle=]
    public async Task<IActionResult> GetProducts(RouteContext ctx)
    {
        var typeTitle = ctx.Request.Query["producttypetitle"].ToString();
        var items = await _store.GetProductsAsync(string.IsNullOrWhiteSpace(typeTitle) ? null : typeTitle);
        return ctx.Ok(items);
    }

    // GET /store/products/detail?title=
    public async Task<IActionResult> GetProductDetail(RouteContext ctx)
    {
        var title = ctx.Request.Query["title"].ToString();
        if (string.IsNullOrWhiteSpace(title)) return ctx.BadRequest("缺少 title 參數。");
        var detail = await _store.GetProductDetailAsync(title);
        return detail is null ? ctx.NotFound("找不到商品。") : ctx.Ok(detail);
    }

    // GET /store/brands/detail?brandtitle=
    public async Task<IActionResult> GetBrandDetail(RouteContext ctx)
    {
        var brandTitle = ctx.Request.Query["brandtitle"].ToString();
        if (string.IsNullOrWhiteSpace(brandTitle)) return ctx.BadRequest("缺少 brandtitle 參數。");
        var detail = await _store.GetBrandDetailAsync(brandTitle);
        return detail is null ? ctx.NotFound("找不到品牌。") : ctx.Ok(detail);
    }

    // GET /store/news[?p=1]
    public async Task<IActionResult> GetNews(RouteContext ctx)
    {
        var (page, pageSize) = ParsePaging(ctx);
        var (items, total) = await _store.GetNewsAsync(page, pageSize);
        return ctx.OkPaged(PaginatedResponse<NewsListItem>.Create(items.ToList(), page, pageSize, total));
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
        return ctx.OkPaged(PaginatedResponse<RecipeListItem>.Create(items.ToList(), page, pageSize, total));
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
        return ctx.OkPaged(PaginatedResponse<IssueListItem>.Create(items.ToList(), page, pageSize, total));
    }

    // GET /store/issues/detail?issuetitle=
    public async Task<IActionResult> GetIssueDetail(RouteContext ctx)
    {
        var issueTitle = ctx.Request.Query["issuetitle"].ToString();
        if (string.IsNullOrWhiteSpace(issueTitle)) return ctx.BadRequest("缺少 issuetitle 參數。");
        var detail = await _store.GetIssueDetailAsync(issueTitle);
        return detail is null ? ctx.NotFound("找不到綠誌。") : ctx.Ok(detail);
    }

    // GET /store/events[?p=1]
    public async Task<IActionResult> GetEvents(RouteContext ctx)
    {
        var (page, pageSize) = ParsePaging(ctx);
        var (items, total) = await _store.GetEventsAsync(page, pageSize);
        return ctx.OkPaged(PaginatedResponse<EventListItem>.Create(items.ToList(), page, pageSize, total));
    }

    // GET /store/events/detail?eventid=
    public async Task<IActionResult> GetEventDetail(RouteContext ctx)
    {
        var raw = ctx.Request.Query["eventid"].ToString();
        if (!Guid.TryParse(raw, out var eventId)) return ctx.BadRequest("eventid 格式不正確。");
        var detail = await _store.GetEventDetailAsync(eventId);
        return detail is null ? ctx.NotFound("找不到活動。") : ctx.Ok(detail);
    }

    private static (int page, int pageSize) ParsePaging(RouteContext ctx)
    {
        int.TryParse(ctx.Request.Query["p"].ToString(), out var p);
        int.TryParse(ctx.Request.Query["pageSize"].ToString(), out var ps);
        return (Math.Max(1, p), ps > 0 ? Math.Min(ps, 100) : DefaultPageSize);
    }
}
