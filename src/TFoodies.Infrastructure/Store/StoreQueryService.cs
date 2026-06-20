using Dapper;
using TFoodies.Application.Abstractions;

namespace TFoodies.Infrastructure.Store;

// Slug convention: DB stores '/' in titles; URLs use '-'. Convert on the way in.
file static class Slug
{
    public static string ToTitle(string slug) => slug.Replace('-', '/');
}

public sealed class StoreQueryService : IStoreQueryService
{
    private readonly IDbConnectionFactory _db;

    public StoreQueryService(IDbConnectionFactory db) => _db = db;

    // Shared product projection (matches ProductRow / MapProduct below).
    private const string ProductColumns = @"
    p.productid, p.title, p.entitle, p.price, p.fixprice, p.photo, p.capacity,
    p.ishot, p.isnew, p.isset, p.isdisabled, p.added, p.sort, p.shortener,
    b.title AS brandTitle, pt.title AS typeTitle";

    private const string ProductJoins = @"
FROM Products p
JOIN Brands b ON b.brandid = p.brandid
JOIN Producttypes pt ON pt.producttypeid = p.producttypeid";

    // ── Home ─────────────────────────────────────────────────────────────────────

    public async Task<HomeData> GetHomeAsync(CancellationToken ct = default)
    {
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var sql = $@"
SELECT bannerid, title, subtitle, url, photo, style, sort FROM Banners ORDER BY sort;

SELECT TOP 8 {ProductColumns}
{ProductJoins}
WHERE p.ishot = 1 AND p.isdisabled = 0
ORDER BY p.sort DESC;

SELECT TOP 9 {ProductColumns}
{ProductJoins}
WHERE p.isnew = 1 AND p.isdisabled = 0
ORDER BY NEWID();

SELECT TOP 2 newid, title, summary, photo, publishdate, shortener
FROM News ORDER BY publishdate DESC;

SELECT TOP 3 recipeid, title, duration, portion, intro, rphoto, photo, v, shortener
FROM Recipes ORDER BY sort;

SELECT TOP 3 issueid, title, photo, createdate, ispublish, shortener
FROM Issues WHERE ispublish = 1 ORDER BY createdate DESC;

SELECT TOP 1 eventid, title, summary, photo, eventdate, createdate, shortener
FROM Events ORDER BY eventdate DESC;";

        using var multi = await conn.QueryMultipleAsync(sql);

        var banners = (await multi.ReadAsync<BannerRow>()).Select(r =>
            new BannerItem(r.bannerid, r.title, r.subtitle, r.url, r.photo, r.style, r.sort))
            .ToList();

        var hot = (await multi.ReadAsync<ProductRow>()).Select(MapProduct).ToList();
        var newp = (await multi.ReadAsync<ProductRow>()).Select(MapProduct).ToList();

        var news = (await multi.ReadAsync<NewsRow>()).Select(r =>
            new NewsListItem(r.newid, r.title, r.summary, r.photo, r.publishdate, r.shortener))
            .ToList();

        var recipes = (await multi.ReadAsync<RecipeRow>()).Select(MapRecipeListItem).ToList();

        var issues = (await multi.ReadAsync<IssueRow>()).Select(r =>
            new IssueListItem(r.issueid, r.title, r.photo, r.createdate, r.ispublish, r.shortener))
            .ToList();

        var eventRow = (await multi.ReadAsync<EventRow>()).FirstOrDefault();
        var latestEvent = eventRow is null ? null
            : new EventListItem(eventRow.eventid, eventRow.title, eventRow.summary,
                eventRow.photo, eventRow.eventdate, eventRow.createdate, eventRow.shortener);

        return new HomeData(banners, hot, newp, news, recipes, issues, latestEvent);
    }

    // ── Products ──────────────────────────────────────────────────────────────────

    public async Task<ProductsPage> GetProductsAsync(string? typeTitle, CancellationToken ct = default)
    {
        // The type tabs link with encodeURIComponent(title) (raw DB title, '/' intact),
        // not the hyphen slug used for product/brand/issue detail — so match as-is.
        var dbTypeTitle = string.IsNullOrWhiteSpace(typeTitle) ? null : typeTitle;
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var sql = $@"
SELECT producttypeid, title, memo, keyword, description
FROM Producttypes WHERE isenable = 1 ORDER BY sort;

SELECT {ProductColumns}
{ProductJoins}
WHERE p.isdisabled = 0
  AND (@dbTypeTitle IS NULL OR pt.title = @dbTypeTitle)
ORDER BY p.sort DESC;";

        using var multi = await conn.QueryMultipleAsync(sql, new { dbTypeTitle });

        var types = (await multi.ReadAsync<ProductTypeRow>())
            .Select(r => new ProductTypeItem(r.producttypeid, r.title, r.memo, r.keyword, r.description))
            .ToList();
        var products = (await multi.ReadAsync<ProductRow>()).Select(MapProduct).ToList();

        var currentType = dbTypeTitle is null
            ? null
            : types.FirstOrDefault(t => t.Title == dbTypeTitle);

        return new ProductsPage(types, currentType, products);
    }

    public async Task<ProductDetail?> GetProductDetailAsync(string titleSlug, CancellationToken ct = default)
    {
        var dbTitle = Slug.ToTitle(titleSlug);
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var sql = @"
SELECT p.productid, p.title, p.entitle, p.intro, p.memo, p.price, p.fixprice,
       p.capacity, p.unit, p.photo, p.ishot, p.isnew, p.isset, p.isgroupbuy,
       p.added, p.keyword, p.description, p.shortener,
       b.brandid, b.title AS brandTitle, b.logo AS brandLogo,
       b.intro AS brandIntro, b.storybgclass AS brandStoryBgClass, b.isdisplay AS brandIsDisplay,
       pt.title AS typeTitle
FROM Products p
JOIN Brands b ON b.brandid = p.brandid
JOIN Producttypes pt ON pt.producttypeid = p.producttypeid
WHERE p.title = @dbTitle AND p.isdisabled = 0;

SELECT pp.photo FROM Productphotos pp
JOIN Products p ON p.productid = pp.productid
WHERE p.title = @dbTitle AND p.isdisabled = 0
ORDER BY pp.sort;

SELECT r.recipeid, r.title, r.rphoto
FROM Recipeproducts rp
JOIN Recipes r ON r.recipeid = rp.recipeid
JOIN Products p ON p.productid = rp.productid
WHERE p.title = @dbTitle AND p.isdisabled = 0
ORDER BY r.sort;";

        using var multi = await conn.QueryMultipleAsync(sql, new { dbTitle });

        var row = (await multi.ReadAsync<ProductDetailRow>()).FirstOrDefault();
        if (row is null) return null;

        var photos = (await multi.ReadAsync<string>()).ToList();
        var recipes = (await multi.ReadAsync<RecipeRefRow>())
            .Select(r => new RecipeRef(r.recipeid, r.title, r.rphoto)).ToList();

        return new ProductDetail(
            row.productid, row.title, row.entitle, row.intro, row.memo,
            row.price, row.fixprice, row.capacity, row.unit,
            row.photo, row.ishot, row.isnew, row.isset, row.isgroupbuy, row.added,
            row.keyword, row.description, row.shortener,
            new BrandSummary(row.brandid, row.brandTitle, row.brandLogo, row.brandIntro, row.brandStoryBgClass, row.brandIsDisplay),
            row.typeTitle, photos, recipes);
    }

    // ── Brand ─────────────────────────────────────────────────────────────────────

    // 導覽列下拉用：上線中的品牌（isdisplay=1），依 sort 排序。對應舊 BaseController.OnActionExecuted。
    public async Task<IReadOnlyList<BrandMenuItem>> GetBrandsAsync(CancellationToken ct = default)
    {
        using var conn = await _db.CreateOpenConnectionAsync(ct);
        var brands = await conn.QueryAsync<BrandMenuItem>(
            "SELECT brandid AS BrandId, title AS Title, logo AS Logo FROM Brands WHERE isdisplay = 1 ORDER BY sort;");
        return brands.ToList();
    }

    // 品牌頁「More」無限捲動：跳過 skip 筆後取 take 筆，多抓 1 筆判斷是否還有更多。
    // 對應舊 AjaxController.GetBrandMoreProducts（ORDER BY sort DESC、isdisabled=0）。
    public async Task<(IReadOnlyList<ProductListItem> Products, bool HasMore)> GetBrandProductsAsync(
        string brandTitleSlug, int skip, int take, CancellationToken ct = default)
    {
        var dbTitle = Slug.ToTitle(brandTitleSlug);
        skip = Math.Max(0, skip);
        take = Math.Clamp(take, 1, 48);
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var sql = $@"
SELECT {ProductColumns}
{ProductJoins}
WHERE b.title = @dbTitle AND p.isdisabled = 0
ORDER BY p.sort DESC
OFFSET @skip ROWS FETCH NEXT @takePlus ROWS ONLY;";

        var rows = (await conn.QueryAsync<ProductRow>(sql, new { dbTitle, skip, takePlus = take + 1 })).ToList();
        var hasMore = rows.Count > take;
        var products = rows.Take(take).Select(MapProduct).ToList();
        return (products, hasMore);
    }

    public async Task<BrandDetail?> GetBrandDetailAsync(string brandTitleSlug, CancellationToken ct = default)
    {
        var dbTitle = Slug.ToTitle(brandTitleSlug);
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var sql = $@"
SELECT brandid, title, subtitle, logo, banner,
       patternentitle, patternchtitle, parttnervideo, patternmemo, patternclass,
       ilogo, slogan, intro, storybgclass, storyentitle, storychtitle, storymemo,
       peopletitle, peopleslogan, peoplememo, peoplephoto,
       keyword, description
FROM Brands WHERE title = @dbTitle AND isdisplay = 1;

SELECT bp.photo FROM Brandphotos bp
JOIN Brands b ON b.brandid = bp.brandid
WHERE b.title = @dbTitle
ORDER BY bp.sort;

SELECT COUNT(*)
FROM Products p
JOIN Brands b ON b.brandid = p.brandid
WHERE b.title = @dbTitle AND p.isdisabled = 0;

SELECT TOP 4 {ProductColumns}
{ProductJoins}
WHERE b.title = @dbTitle AND p.isdisabled = 0
ORDER BY p.sort DESC;";

        using var multi = await conn.QueryMultipleAsync(sql, new { dbTitle });

        var row = (await multi.ReadAsync<BrandDetailRow>()).FirstOrDefault();
        if (row is null) return null;

        var photos = (await multi.ReadAsync<string>()).ToList();
        var productCount = await multi.ReadSingleAsync<int>();
        var products = (await multi.ReadAsync<ProductRow>()).Select(MapProduct).ToList();

        return new BrandDetail(
            row.brandid, row.title, row.subtitle, row.logo, row.banner,
            row.patternentitle, row.patternchtitle, row.parttnervideo, row.patternmemo, row.patternclass,
            row.ilogo, row.slogan, row.intro,
            row.storybgclass, row.storyentitle, row.storychtitle, row.storymemo,
            row.peopletitle, row.peopleslogan, row.peoplememo, row.peoplephoto,
            row.keyword, row.description,
            productCount, photos, products);
    }

    // ── News ──────────────────────────────────────────────────────────────────────

    public async Task<(IReadOnlyList<NewsListItem> Items, int TotalCount)> GetNewsAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var sql = @"
SELECT COUNT(*) FROM News;
SELECT newid, title, summary, photo, publishdate, shortener
FROM News ORDER BY publishdate DESC
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;";

        using var multi = await conn.QueryMultipleAsync(sql,
            new { offset = (page - 1) * pageSize, pageSize });

        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<NewsRow>())
            .Select(r => new NewsListItem(r.newid, r.title, r.summary, r.photo, r.publishdate, r.shortener))
            .ToList();

        return (items, total);
    }

    public async Task<NewsDetail?> GetNewsDetailAsync(Guid newId, CancellationToken ct = default)
    {
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var sql = @"
SELECT newid, title, summary, photo, intro, activitydate, activityschedule, publishdate, shortener
FROM News WHERE newid = @newId;
SELECT photo FROM Newmedias WHERE newid = @newId AND photo IS NOT NULL;
SELECT TOP 3 newid, title, photo FROM News WHERE newid <> @newId ORDER BY NEWID();";

        using var multi = await conn.QueryMultipleAsync(sql, new { newId });

        var row = (await multi.ReadAsync<NewsDetailRow>()).FirstOrDefault();
        if (row is null) return null;

        var media = (await multi.ReadAsync<string>()).ToList();
        var others = (await multi.ReadAsync<NewsRefRow>())
            .Select(r => new NewsRef(r.newid, r.title, r.photo)).ToList();

        return new NewsDetail(row.newid, row.title, row.summary, row.photo, row.intro,
            row.activitydate, row.activityschedule, row.publishdate, row.shortener, media, others);
    }

    // ── Recipes ───────────────────────────────────────────────────────────────────

    public async Task<(IReadOnlyList<RecipeListItem> Items, int TotalCount)> GetRecipesAsync(
        int page, int pageSize, string? keyword, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        var like = string.IsNullOrWhiteSpace(keyword) ? null : $"%{keyword}%";
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var sql = @"
SELECT COUNT(*) FROM Recipes WHERE @like IS NULL OR title LIKE @like;
SELECT recipeid, title, duration, portion, intro, rphoto, photo, v, shortener
FROM Recipes
WHERE @like IS NULL OR title LIKE @like
ORDER BY sort
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;";

        using var multi = await conn.QueryMultipleAsync(sql,
            new { like, offset = (page - 1) * pageSize, pageSize });

        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<RecipeRow>()).Select(MapRecipeListItem).ToList();

        return (items, total);
    }

    public async Task<RecipeDetail?> GetRecipeDetailAsync(Guid recipeId, CancellationToken ct = default)
    {
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var sql = $@"
SELECT recipeid, title, duration, portion, intro, rphoto, photo, youtube, v,
       keyword, description, shortener
FROM Recipes WHERE recipeid = @recipeId;

SELECT sort, title, value FROM Recipeingredients
WHERE recipeid = @recipeId ORDER BY sort;

SELECT sort, title, value FROM Recipeseasonings
WHERE recipeid = @recipeId ORDER BY sort;

SELECT sort, title, value FROM Recipesteps
WHERE recipeid = @recipeId ORDER BY sort;

SELECT {ProductColumns}
FROM Recipeproducts rp
JOIN Products p ON p.productid = rp.productid
JOIN Brands b ON b.brandid = p.brandid
JOIN Producttypes pt ON pt.producttypeid = p.producttypeid
WHERE rp.recipeid = @recipeId AND p.isdisabled = 0
ORDER BY p.sort DESC;";

        using var multi = await conn.QueryMultipleAsync(sql, new { recipeId });

        var row = (await multi.ReadAsync<RecipeDetailRow>()).FirstOrDefault();
        if (row is null) return null;

        var ingredients = (await multi.ReadAsync<RecipePartRow>())
            .Select(r => new RecipeIngredient(r.sort, r.title, r.value)).ToList();
        var seasonings = (await multi.ReadAsync<RecipePartRow>())
            .Select(r => new RecipeSeasoning(r.sort, r.title, r.value)).ToList();
        var steps = (await multi.ReadAsync<RecipePartRow>())
            .Select(r => new RecipeStep(r.sort, r.title, r.value)).ToList();
        var products = (await multi.ReadAsync<ProductRow>()).Select(MapProduct).ToList();

        return new RecipeDetail(
            row.recipeid, row.title, row.duration, row.portion, row.intro,
            row.rphoto, row.photo, row.youtube, row.v,
            row.keyword, row.description, row.shortener,
            ingredients, seasonings, steps, products);
    }

    // ── Issues ────────────────────────────────────────────────────────────────────

    public async Task<(IReadOnlyList<IssueListItem> Items, int TotalCount)> GetIssuesAsync(
        int page, int pageSize, string? keyword, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        var like = string.IsNullOrWhiteSpace(keyword) ? null : $"%{keyword}%";
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var sql = @"
SELECT COUNT(*) FROM Issues WHERE ispublish = 1
  AND (@like IS NULL OR title LIKE @like);
SELECT issueid, title, photo, createdate, ispublish, shortener
FROM Issues
WHERE ispublish = 1 AND (@like IS NULL OR title LIKE @like)
ORDER BY sort
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;";

        using var multi = await conn.QueryMultipleAsync(sql,
            new { like, offset = (page - 1) * pageSize, pageSize });

        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<IssueRow>())
            .Select(r => new IssueListItem(r.issueid, r.title, r.photo, r.createdate, r.ispublish, r.shortener))
            .ToList();

        return (items, total);
    }

    public async Task<IssueDetail?> GetIssueDetailAsync(string issueTitleSlug, CancellationToken ct = default)
    {
        var dbTitle = Slug.ToTitle(issueTitleSlug);
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var sql = $@"
SELECT issueid, title, photo, intro, keyword, description, createdate, ispublish, shortener
FROM Issues WHERE title = @dbTitle AND ispublish = 1;

DECLARE @iid uniqueidentifier = (SELECT issueid FROM Issues WHERE title = @dbTitle AND ispublish = 1);

SELECT photo FROM Issuemedias WHERE issueid = @iid AND photo IS NOT NULL;

SELECT {ProductColumns}
FROM Issueproducts ip
JOIN Products p ON p.productid = ip.productid
JOIN Brands b ON b.brandid = p.brandid
JOIN Producttypes pt ON pt.producttypeid = p.producttypeid
WHERE ip.issueid = @iid AND p.isdisabled = 0
ORDER BY p.sort DESC;

SELECT r.recipeid, r.title, r.rphoto
FROM Issuerecipes ir
JOIN Recipes r ON r.recipeid = ir.recipeid
WHERE ir.issueid = @iid
ORDER BY r.sort;

SELECT TOP 3 issueid, title, photo FROM Issues
WHERE ispublish = 1 AND issueid <> @iid ORDER BY NEWID();";

        using var multi = await conn.QueryMultipleAsync(sql, new { dbTitle });

        var row = (await multi.ReadAsync<IssueDetailRow>()).FirstOrDefault();
        if (row is null) return null;

        var media = (await multi.ReadAsync<string>()).ToList();
        var products = (await multi.ReadAsync<ProductRow>()).Select(MapProduct).ToList();
        var recipes = (await multi.ReadAsync<RecipeRefRow>())
            .Select(r => new RecipeRef(r.recipeid, r.title, r.rphoto)).ToList();
        var others = (await multi.ReadAsync<IssueRefRow>())
            .Select(r => new IssueRef(r.issueid, r.title, r.photo)).ToList();

        return new IssueDetail(row.issueid, row.title, row.photo, row.intro,
            row.keyword, row.description, row.createdate, row.ispublish, row.shortener,
            media, products, recipes, others);
    }

    // ── Knowledges (小知識) ─────────────────────────────────────────────────────────

    public async Task<(IReadOnlyList<KnowledgeListItem> Items, int TotalCount)> GetKnowledgesAsync(
        int page, int pageSize, string? keyword, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        var like = string.IsNullOrWhiteSpace(keyword) ? null : $"%{keyword}%";
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var sql = @"
SELECT COUNT(*) FROM Knowledges WHERE ispublish = 1
  AND (@like IS NULL OR question LIKE @like);
SELECT knowledgeid, question, photo, createdate, ispublish, shortener
FROM Knowledges
WHERE ispublish = 1 AND (@like IS NULL OR question LIKE @like)
ORDER BY sort
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;";

        using var multi = await conn.QueryMultipleAsync(sql,
            new { like, offset = (page - 1) * pageSize, pageSize });

        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<KnowledgeRow>())
            .Select(r => new KnowledgeListItem(r.knowledgeid, r.question, r.photo, r.createdate, r.ispublish, r.shortener))
            .ToList();

        return (items, total);
    }

    public async Task<KnowledgeDetail?> GetKnowledgeDetailAsync(Guid knowledgeId, CancellationToken ct = default)
    {
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var sql = $@"
SELECT knowledgeid, question, photo, answer, keyword, description, createdate, ispublish, shortener
FROM Knowledges WHERE knowledgeid = @knowledgeId AND ispublish = 1;

SELECT {ProductColumns}
FROM Knowledgeproducts kp
JOIN Products p ON p.productid = kp.productid
JOIN Brands b ON b.brandid = p.brandid
JOIN Producttypes pt ON pt.producttypeid = p.producttypeid
WHERE kp.knowledgeid = @knowledgeId AND p.isdisabled = 0
ORDER BY p.sort DESC;

SELECT TOP 3 knowledgeid, question, photo FROM Knowledges
WHERE ispublish = 1 AND knowledgeid <> @knowledgeId ORDER BY NEWID();";

        using var multi = await conn.QueryMultipleAsync(sql, new { knowledgeId });

        var row = (await multi.ReadAsync<KnowledgeDetailRow>()).FirstOrDefault();
        if (row is null) return null;

        var products = (await multi.ReadAsync<ProductRow>()).Select(MapProduct).ToList();
        var others = (await multi.ReadAsync<KnowledgeRefRow>())
            .Select(r => new KnowledgeRef(r.knowledgeid, r.question, r.photo)).ToList();

        return new KnowledgeDetail(row.knowledgeid, row.question, row.photo, row.answer,
            row.keyword, row.description, row.createdate, row.ispublish, row.shortener, products, others);
    }

    // ── Blogs (部落客分享) ───────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<BlogItem>> GetBlogsAsync(CancellationToken ct = default)
    {
        using var conn = await _db.CreateOpenConnectionAsync(ct);
        var rows = await conn.QueryAsync<BlogRow>(
            "SELECT blogid, title, photo, link FROM Blogs ORDER BY sort;");
        return rows.Select(r => new BlogItem(r.blogid, r.title, r.photo, r.link)).ToList();
    }

    // ── Events ────────────────────────────────────────────────────────────────────

    public async Task<(IReadOnlyList<EventListItem> Items, int TotalCount)> GetEventsAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var sql = @"
SELECT COUNT(*) FROM Events;
SELECT eventid, title, summary, photo, eventdate, createdate, shortener
FROM Events ORDER BY eventdate DESC
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;";

        using var multi = await conn.QueryMultipleAsync(sql,
            new { offset = (page - 1) * pageSize, pageSize });

        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<EventRow>())
            .Select(r => new EventListItem(r.eventid, r.title, r.summary, r.photo, r.eventdate, r.createdate, r.shortener))
            .ToList();

        return (items, total);
    }

    public async Task<EventDetail?> GetEventDetailAsync(Guid eventId, CancellationToken ct = default)
    {
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var sql = @"
SELECT eventid, title, summary, intro, photo, keyword, description, eventdate, createdate, shortener
FROM Events WHERE eventid = @eventId;
SELECT photo FROM Eventphotos WHERE eventid = @eventId ORDER BY sort;";

        using var multi = await conn.QueryMultipleAsync(sql, new { eventId });

        var row = (await multi.ReadAsync<EventDetailRow>()).FirstOrDefault();
        if (row is null) return null;

        var photos = (await multi.ReadAsync<string>()).ToList();
        return new EventDetail(row.eventid, row.title, row.summary, row.intro, row.photo,
            row.keyword, row.description, row.eventdate, row.createdate, row.shortener, photos);
    }

    // ── Shopping guide (購物說明 / 會員常見問題) ──────────────────────────────────
    // 全部分類 + 其底下問答，一次撈回（資料量小、無分頁，對應舊 PageMs/Howtobuy）。
    // answer 為 ntext，需 CAST 為 nvarchar(max) 才能由 Dapper 正確讀取。
    public async Task<IReadOnlyList<ShoppingGuideType>> GetShoppingGuideAsync(CancellationToken ct = default)
    {
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var sql = @"
SELECT questiontypeid, title, sort FROM Questiontypes ORDER BY sort, title;
SELECT questionid, questiontypeid, title, CAST(answer AS nvarchar(max)) AS answer, sort
FROM Questions ORDER BY sort, title;";

        using var multi = await conn.QueryMultipleAsync(sql);

        var types = (await multi.ReadAsync<QuestiontypeRow>()).ToList();
        var questions = (await multi.ReadAsync<QuestionRow>()).ToList();

        return types
            .Select(t => new ShoppingGuideType(
                t.questiontypeid, t.title, t.sort,
                questions
                    .Where(q => q.questiontypeid == t.questiontypeid)
                    .Select(q => new ShoppingGuideQuestion(q.questionid, q.title, q.answer, q.sort))
                    .ToList()))
            .ToList();
    }

    // ── Mapping helpers ───────────────────────────────────────────────────────────

    private static ProductListItem MapProduct(ProductRow r) =>
        new(r.productid, r.title, r.entitle, r.price, r.fixprice, r.photo, r.capacity,
            r.ishot, r.isnew, r.isset, r.isdisabled, r.added, r.sort, r.brandTitle, r.typeTitle, r.shortener);

    private static RecipeListItem MapRecipeListItem(RecipeRow r) =>
        new(r.recipeid, r.title, r.duration, r.portion, r.intro, r.rphoto, r.photo, r.v, r.shortener);

    // ── Private row types (Dapper columns → named tuples) ─────────────────────────

    private sealed record BannerRow(Guid bannerid, string? title, string? subtitle, string? url, string photo, int style, int sort);
    private sealed record ProductRow(Guid productid, string title, string? entitle, int price, int? fixprice, string photo, string? capacity, bool ishot, bool isnew, bool isset, bool isdisabled, int added, int sort, string? shortener, string brandTitle, string typeTitle);
    private sealed record ProductTypeRow(Guid producttypeid, string title, string? memo, string? keyword, string? description);
    private sealed record ProductDetailRow(Guid productid, string title, string? entitle, string? intro, string memo, int price, int? fixprice, string? capacity, string? unit, string photo, bool ishot, bool isnew, bool isset, bool isgroupbuy, int added, string? keyword, string? description, string? shortener, Guid brandid, string brandTitle, string? brandLogo, string? brandIntro, string? brandStoryBgClass, int brandIsDisplay, string typeTitle);
    private sealed record BrandDetailRow(Guid brandid, string title, string? subtitle, string? logo, string? banner, string? patternentitle, string? patternchtitle, string? parttnervideo, string? patternmemo, string? patternclass, string? ilogo, string? slogan, string? intro, string? storybgclass, string? storyentitle, string? storychtitle, string? storymemo, string? peopletitle, string? peopleslogan, string? peoplememo, string? peoplephoto, string? keyword, string? description);
    private sealed record NewsRow(Guid newid, string title, string? summary, string photo, DateTime publishdate, string? shortener);
    private sealed record NewsDetailRow(Guid newid, string title, string? summary, string photo, string intro, string? activitydate, string? activityschedule, DateTime publishdate, string? shortener);
    private sealed record NewsRefRow(Guid newid, string title, string photo);
    private sealed record RecipeRow(Guid recipeid, string title, int duration, int portion, string intro, string rphoto, string photo, string? v, string? shortener);
    private sealed record RecipeDetailRow(Guid recipeid, string title, int duration, int portion, string intro, string rphoto, string photo, string? youtube, string? v, string? keyword, string? description, string? shortener);
    private sealed record RecipePartRow(int sort, string title, string? value);
    private sealed record RecipeRefRow(Guid recipeid, string title, string? rphoto);
    private sealed record IssueRow(Guid issueid, string title, string photo, DateTime createdate, bool ispublish, string? shortener);
    private sealed record IssueDetailRow(Guid issueid, string title, string photo, string? intro, string? keyword, string? description, DateTime createdate, bool ispublish, string? shortener);
    private sealed record IssueRefRow(Guid issueid, string title, string photo);
    private sealed record KnowledgeRow(Guid knowledgeid, string question, string photo, DateTime createdate, bool ispublish, string? shortener);
    private sealed record KnowledgeDetailRow(Guid knowledgeid, string question, string photo, string answer, string? keyword, string? description, DateTime createdate, bool ispublish, string? shortener);
    private sealed record KnowledgeRefRow(Guid knowledgeid, string question, string photo);
    private sealed record BlogRow(Guid blogid, string title, string photo, string link);
    private sealed record EventRow(Guid eventid, string title, string summary, string photo, DateTime eventdate, DateTime createdate, string? shortener);
    private sealed record EventDetailRow(Guid eventid, string title, string summary, string intro, string photo, string? keyword, string? description, DateTime eventdate, DateTime createdate, string? shortener);
    private sealed record QuestiontypeRow(Guid questiontypeid, string title, int sort);
    private sealed record QuestionRow(Guid questionid, Guid questiontypeid, string title, string answer, int sort);
}
