namespace TFoodies.Application.Abstractions;

public interface IStoreQueryService
{
    Task<HomeData> GetHomeAsync(CancellationToken ct = default);
    Task<ProductsPage> GetProductsAsync(string? typeTitle, CancellationToken ct = default);
    Task<ProductDetail?> GetProductDetailAsync(string title, CancellationToken ct = default);
    Task<IReadOnlyList<BrandMenuItem>> GetBrandsAsync(CancellationToken ct = default);
    Task<BrandDetail?> GetBrandDetailAsync(string brandTitle, CancellationToken ct = default);
    Task<(IReadOnlyList<ProductListItem> Products, bool HasMore)> GetBrandProductsAsync(string brandTitle, int skip, int take, CancellationToken ct = default);
    Task<(IReadOnlyList<NewsListItem> Items, int TotalCount)> GetNewsAsync(int page, int pageSize, CancellationToken ct = default);
    Task<NewsDetail?> GetNewsDetailAsync(Guid newId, CancellationToken ct = default);
    Task<(IReadOnlyList<RecipeListItem> Items, int TotalCount)> GetRecipesAsync(int page, int pageSize, string? keyword, CancellationToken ct = default);
    Task<RecipeDetail?> GetRecipeDetailAsync(Guid recipeId, CancellationToken ct = default);
    Task<(IReadOnlyList<IssueListItem> Items, int TotalCount)> GetIssuesAsync(int page, int pageSize, string? keyword, CancellationToken ct = default);
    Task<IssueDetail?> GetIssueDetailAsync(string issueTitle, CancellationToken ct = default);
    Task<(IReadOnlyList<KnowledgeListItem> Items, int TotalCount)> GetKnowledgesAsync(int page, int pageSize, string? keyword, CancellationToken ct = default);
    Task<KnowledgeDetail?> GetKnowledgeDetailAsync(Guid knowledgeId, CancellationToken ct = default);
    Task<IReadOnlyList<BlogItem>> GetBlogsAsync(CancellationToken ct = default);
    Task<(IReadOnlyList<EventListItem> Items, int TotalCount)> GetEventsAsync(int page, int pageSize, CancellationToken ct = default);
    Task<EventDetail?> GetEventDetailAsync(Guid eventId, CancellationToken ct = default);
    Task<IReadOnlyList<ShoppingGuideType>> GetShoppingGuideAsync(CancellationToken ct = default);
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
// IsDisabled/Sort carried so related-product sections (recipe/issue detail) can
// filter+order client-side exactly as the legacy views did.
public sealed record ProductListItem(
    Guid ProductId, string Title, string? EnTitle, int Price, int? FixPrice,
    string Photo, string? Capacity, bool IsHot, bool IsNew, bool IsSet, bool IsDisabled,
    int Added, int Sort, string BrandTitle, string TypeTitle, string? Shortener);

public sealed record ProductTypeItem(Guid ProductTypeId, string Title, string? Memo, string? Keyword, string? Description);

// Products listing page = the type tabs + the current type (for SEO) + the products.
public sealed record ProductsPage(
    IReadOnlyList<ProductTypeItem> ProductTypes,
    ProductTypeItem? CurrentType,
    IReadOnlyList<ProductListItem> Products);

public sealed record ProductDetail(
    Guid ProductId, string Title, string? EnTitle, string? Intro, string Memo,
    int Price, int? FixPrice, string? Capacity, string? Unit,
    string Photo, bool IsHot, bool IsNew, bool IsSet, bool IsGroupBuy, int Added,
    string? Keyword, string? Description, string? Shortener,
    BrandSummary Brand, string TypeTitle,
    IReadOnlyList<string> Photos,
    IReadOnlyList<RecipeRef> Recipes);

public sealed record BrandSummary(Guid BrandId, string Title, string? Logo, string? Intro, string? StoryBgClass, int IsDisplay);

// Compact recipe reference used by product/issue detail "適合料理 / 查看食譜" sections.
public sealed record RecipeRef(Guid RecipeId, string Title, string? RPhoto);

// ── Brand ──────────────────────────────────────────────────────────────────────
// 導覽列「品牌系列」下拉選單用：僅 isdisplay=1，依 sort 排序（對應舊 BaseController.ViewBag.Brands）。
public sealed record BrandMenuItem(Guid BrandId, string Title, string? Logo);

public sealed record BrandDetail(
    Guid BrandId, string Title, string? Subtitle, string? Logo, string? Banner,
    string? PatternEnTitle, string? PatternChTitle, string? PartnerVideo, string? PatternMemo, string? PatternClass,
    string? ILogo, string? Slogan, string? Intro,
    string? StoryBgClass, string? StoryEnTitle, string? StoryChTitle, string? StoryMemo,
    string? PeopleTitle, string? PeopleSlogan, string? PeopleMemo, string? PeoplePhoto,
    string? Keyword, string? Description,
    int ProductCount,
    IReadOnlyList<string> BrandPhotos,
    IReadOnlyList<ProductListItem> Products);

// ── News ───────────────────────────────────────────────────────────────────────
public sealed record NewsListItem(
    Guid NewId, string Title, string? Summary, string Photo, DateTime PublishDate, string? Shortener);

public sealed record NewsDetail(
    Guid NewId, string Title, string? Summary, string Photo, string Intro,
    string? ActivityDate, string? ActivitySchedule, DateTime PublishDate, string? Shortener,
    IReadOnlyList<string> MediaUrls,
    IReadOnlyList<NewsRef> Others);

public sealed record NewsRef(Guid NewId, string Title, string Photo);

// ── Recipes ─────────────────────────────────────────────────────────────────────
public sealed record RecipeListItem(
    Guid RecipeId, string Title, int Duration, int Portion, string Intro,
    string RPhoto, string Photo, string? V, string? Shortener);

public sealed record RecipeDetail(
    Guid RecipeId, string Title, int Duration, int Portion, string Intro,
    string RPhoto, string Photo, string? Youtube, string? V,
    string? Keyword, string? Description, string? Shortener,
    IReadOnlyList<RecipeIngredient> Ingredients,
    IReadOnlyList<RecipeSeasoning> Seasonings,
    IReadOnlyList<RecipeStep> Steps,
    IReadOnlyList<ProductListItem> Products);

public sealed record RecipeIngredient(int Sort, string Title, string? Value);
public sealed record RecipeSeasoning(int Sort, string Title, string? Value);
public sealed record RecipeStep(int Sort, string Title, string? Value);

// ── Issues ────────────────────────────────────────────────────────────────────
public sealed record IssueListItem(
    Guid IssueId, string Title, string Photo, DateTime CreateDate, bool IsPublish, string? Shortener);

public sealed record IssueDetail(
    Guid IssueId, string Title, string Photo, string? Intro,
    string? Keyword, string? Description, DateTime CreateDate, bool IsPublish, string? Shortener,
    IReadOnlyList<string> MediaUrls,
    IReadOnlyList<ProductListItem> Products,
    IReadOnlyList<RecipeRef> Recipes,
    IReadOnlyList<IssueRef> Others);

public sealed record IssueRef(Guid IssueId, string Title, string Photo);

// ── Knowledges (小知識) ─────────────────────────────────────────────────────────
public sealed record KnowledgeListItem(
    Guid KnowledgeId, string Question, string Photo, DateTime CreateDate, bool IsPublish, string? Shortener);

public sealed record KnowledgeDetail(
    Guid KnowledgeId, string Question, string Photo, string Answer,
    string? Keyword, string? Description, DateTime CreateDate, bool IsPublish, string? Shortener,
    IReadOnlyList<KnowledgeRef> Others);

public sealed record KnowledgeRef(Guid KnowledgeId, string Question, string Photo);

// ── Blogs (部落客分享) ───────────────────────────────────────────────────────────
// 單頁清單（無分頁、無詳情），每張卡連到外部部落格文章 Link。
public sealed record BlogItem(Guid BlogId, string Title, string Photo, string Link);

// ── Events ────────────────────────────────────────────────────────────────────
public sealed record EventListItem(
    Guid EventId, string Title, string Summary, string Photo, DateTime EventDate, DateTime CreateDate, string? Shortener);

public sealed record EventDetail(
    Guid EventId, string Title, string Summary, string Intro, string Photo,
    string? Keyword, string? Description, DateTime EventDate, DateTime CreateDate, string? Shortener,
    IReadOnlyList<string> EventPhotos);

// ── Shopping guide (購物說明 / 會員常見問題 FAQ) ─────────────────────────────────
// 前台 /PageMs/Howtobuy：依分類分組的問答。answer 為富文本 HTML（v-html 呈現）。
public sealed record ShoppingGuideType(
    Guid QuestiontypeId, string Title, int Sort,
    IReadOnlyList<ShoppingGuideQuestion> Questions);

public sealed record ShoppingGuideQuestion(
    Guid QuestionId, string Title, string Answer, int Sort);
