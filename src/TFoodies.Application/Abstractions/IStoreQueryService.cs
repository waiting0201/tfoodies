namespace TFoodies.Application.Abstractions;

public interface IStoreQueryService
{
    Task<HomeData> GetHomeAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ProductListItem>> GetProductsAsync(string? typeTitle, CancellationToken ct = default);
    Task<ProductDetail?> GetProductDetailAsync(string title, CancellationToken ct = default);
    Task<BrandDetail?> GetBrandDetailAsync(string brandTitle, CancellationToken ct = default);
    Task<(IReadOnlyList<NewsListItem> Items, int TotalCount)> GetNewsAsync(int page, int pageSize, CancellationToken ct = default);
    Task<NewsDetail?> GetNewsDetailAsync(Guid newId, CancellationToken ct = default);
    Task<(IReadOnlyList<RecipeListItem> Items, int TotalCount)> GetRecipesAsync(int page, int pageSize, string? keyword, CancellationToken ct = default);
    Task<RecipeDetail?> GetRecipeDetailAsync(Guid recipeId, CancellationToken ct = default);
    Task<(IReadOnlyList<IssueListItem> Items, int TotalCount)> GetIssuesAsync(int page, int pageSize, string? keyword, CancellationToken ct = default);
    Task<IssueDetail?> GetIssueDetailAsync(string issueTitle, CancellationToken ct = default);
    Task<(IReadOnlyList<EventListItem> Items, int TotalCount)> GetEventsAsync(int page, int pageSize, CancellationToken ct = default);
    Task<EventDetail?> GetEventDetailAsync(Guid eventId, CancellationToken ct = default);
}

// ── Home ───────────────────────────────────────────────────────────────────────
public sealed record HomeData(
    IReadOnlyList<BannerItem> Banners,
    IReadOnlyList<ProductListItem> HotProducts,
    IReadOnlyList<ProductListItem> NewProducts,
    IReadOnlyList<NewsListItem> LatestNews,
    IReadOnlyList<RecipeListItem> LatestRecipes,
    IReadOnlyList<IssueListItem> LatestIssues,
    EventListItem? LatestEvent);

public sealed record BannerItem(Guid BannerId, string? Title, string? Subtitle, string? Url, string Photo, int Style, int Sort);

// ── Products ───────────────────────────────────────────────────────────────────
public sealed record ProductListItem(
    Guid ProductId, string Title, string? EnTitle, int Price, int? FixPrice,
    string Photo, bool IsHot, bool IsNew, bool IsSet, int Added, string BrandTitle, string TypeTitle, string? Shortener);

public sealed record ProductDetail(
    Guid ProductId, string Title, string? EnTitle, string? Intro, string Memo,
    int Price, int? FixPrice, string? Capacity, string? Unit,
    string Photo, bool IsHot, bool IsNew, bool IsSet, bool IsGroupBuy, int Added,
    string? Keyword, string? Description, string? Shortener,
    BrandSummary Brand, string TypeTitle,
    IReadOnlyList<string> Photos,
    IReadOnlyList<ProductListItem> SetProducts);

public sealed record BrandSummary(Guid BrandId, string Title, string? Logo);

// ── Brand ──────────────────────────────────────────────────────────────────────
public sealed record BrandDetail(
    Guid BrandId, string Title, string? Subtitle, string? Logo, string? Banner,
    string? PatternEnTitle, string? PatternChTitle, string? PartnerVideo, string? PatternMemo, string? PatternClass,
    string? ILogo, string? Slogan, string? Intro,
    string? StoryBgClass, string? StoryEnTitle, string? StoryChTitle, string? StoryMemo,
    string? PeopleTitle, string? PeopleSlogan, string? PeopleMemo, string? PeoplePhoto,
    string? Keyword, string? Description,
    IReadOnlyList<string> BrandPhotos,
    IReadOnlyList<ProductListItem> Products);

// ── News ───────────────────────────────────────────────────────────────────────
public sealed record NewsListItem(
    Guid NewId, string Title, string? Summary, string Photo, DateTime PublishDate, string? Shortener);

public sealed record NewsDetail(
    Guid NewId, string Title, string? Summary, string Photo, string Intro,
    string? ActivityDate, string? ActivitySchedule, DateTime PublishDate, string? Shortener,
    IReadOnlyList<string> MediaUrls);

// ── Recipes ───────────────────────────────────────────────────────────────────
public sealed record RecipeListItem(
    Guid RecipeId, string Title, int Duration, int Portion, string Intro,
    string RPhoto, string Photo, string? Shortener);

public sealed record RecipeDetail(
    Guid RecipeId, string Title, int Duration, int Portion, string Intro,
    string RPhoto, string Photo, string? Youtube, string? V,
    string? Keyword, string? Description, string? Shortener,
    IReadOnlyList<RecipeIngredient> Ingredients,
    IReadOnlyList<RecipeSeasoning> Seasonings,
    IReadOnlyList<RecipeStep> Steps);

public sealed record RecipeIngredient(string Name, string? Quantity);
public sealed record RecipeSeasoning(string Name, string? Quantity);
public sealed record RecipeStep(int Sort, string? Photo, string? Content);

// ── Issues ────────────────────────────────────────────────────────────────────
public sealed record IssueListItem(
    Guid IssueId, string Title, string Photo, DateTime CreateDate, bool IsPublish, string? Shortener);

public sealed record IssueDetail(
    Guid IssueId, string Title, string Photo, string? Intro,
    string? Keyword, string? Description, DateTime CreateDate, bool IsPublish, string? Shortener,
    IReadOnlyList<string> MediaUrls);

// ── Events ────────────────────────────────────────────────────────────────────
public sealed record EventListItem(
    Guid EventId, string Title, string Summary, string Photo, DateTime EventDate, DateTime CreateDate, string? Shortener);

public sealed record EventDetail(
    Guid EventId, string Title, string Summary, string Intro, string Photo,
    string? Keyword, string? Description, DateTime EventDate, DateTime CreateDate, string? Shortener,
    IReadOnlyList<string> EventPhotos);
