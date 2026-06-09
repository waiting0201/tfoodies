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

    // ── Home ─────────────────────────────────────────────────────────────────────

    public async Task<HomeData> GetHomeAsync(CancellationToken ct = default)
    {
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var sql = @"
SELECT bannerid, title, subtitle, url, photo, style, sort FROM Banners ORDER BY sort;

SELECT TOP 8
    p.productid, p.title, p.entitle, p.price, p.fixprice, p.photo,
    p.ishot, p.isnew, p.isset, p.added, p.shortener,
    b.title AS brandTitle, pt.title AS typeTitle
FROM Products p
JOIN Brands b ON b.brandid = p.brandid
JOIN Producttypes pt ON pt.producttypeid = p.producttypeid
WHERE p.ishot = 1 AND p.isdisabled = 0
ORDER BY p.sort;

SELECT TOP 9
    p.productid, p.title, p.entitle, p.price, p.fixprice, p.photo,
    p.ishot, p.isnew, p.isset, p.added, p.shortener,
    b.title AS brandTitle, pt.title AS typeTitle
FROM Products p
JOIN Brands b ON b.brandid = p.brandid
JOIN Producttypes pt ON pt.producttypeid = p.producttypeid
WHERE p.isnew = 1 AND p.isdisabled = 0
ORDER BY NEWID();

SELECT TOP 2 newid, title, summary, photo, publishdate, shortener
FROM News ORDER BY publishdate DESC;

SELECT TOP 3 recipeid, title, duration, portion, intro, rphoto, photo, shortener
FROM Recipes ORDER BY sort;

SELECT TOP 3 issueid, title, photo, createdate, ispublish, shortener
FROM Issues WHERE ispublish = 1 ORDER BY sort;

SELECT TOP 1 eventid, title, summary, photo, eventdate, createdate, shortener
FROM Events ORDER BY sort;";

        using var multi = await conn.QueryMultipleAsync(sql);

        var banners = (await multi.ReadAsync<BannerRow>()).Select(r =>
            new BannerItem(r.bannerid, r.title, r.subtitle, r.url, r.photo, r.style, r.sort))
            .ToList();

        var hot = (await multi.ReadAsync<ProductRow>()).Select(MapProduct).ToList();
        var newp = (await multi.ReadAsync<ProductRow>()).Select(MapProduct).ToList();

        var news = (await multi.ReadAsync<NewsRow>()).Select(r =>
            new NewsListItem(r.newid, r.title, r.summary, r.photo, r.publishdate, r.shortener))
            .ToList();

        var recipes = (await multi.ReadAsync<RecipeRow>()).Select(r =>
            new RecipeListItem(r.recipeid, r.title, r.duration, r.portion, r.intro, r.rphoto, r.photo, r.shortener))
            .ToList();

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

    public async Task<IReadOnlyList<ProductListItem>> GetProductsAsync(string? typeTitle, CancellationToken ct = default)
    {
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var sql = @"
SELECT p.productid, p.title, p.entitle, p.price, p.fixprice, p.photo,
       p.ishot, p.isnew, p.isset, p.added, p.shortener,
       b.title AS brandTitle, pt.title AS typeTitle
FROM Products p
JOIN Brands b ON b.brandid = p.brandid
JOIN Producttypes pt ON pt.producttypeid = p.producttypeid
WHERE p.isdisabled = 0
  AND (@typeTitle IS NULL OR pt.title = @typeTitle)
ORDER BY p.sort";

        var rows = await conn.QueryAsync<ProductRow>(sql, new { typeTitle = typeTitle });
        return rows.Select(MapProduct).ToList();
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
       pt.title AS typeTitle
FROM Products p
JOIN Brands b ON b.brandid = p.brandid
JOIN Producttypes pt ON pt.producttypeid = p.producttypeid
WHERE p.title = @dbTitle AND p.isdisabled = 0;

SELECT pp.photo FROM Productphotos pp
JOIN Products p ON p.productid = pp.productid
WHERE p.title = @dbTitle AND p.isdisabled = 0
ORDER BY pp.sort;

SELECT sub.productid, sub.title, sub.entitle, sub.price, sub.fixprice, sub.photo,
       sub.ishot, sub.isnew, sub.isset, sub.added, sub.shortener,
       b2.title AS brandTitle, pt2.title AS typeTitle
FROM Setproducts sp
JOIN Products main ON main.productid = sp.oproductid
JOIN Products sub ON sub.productid = sp.productid
JOIN Brands b2 ON b2.brandid = sub.brandid
JOIN Producttypes pt2 ON pt2.producttypeid = sub.producttypeid
WHERE main.title = @dbTitle AND main.isdisabled = 0 AND sub.isdisabled = 0;";

        using var multi = await conn.QueryMultipleAsync(sql, new { dbTitle });

        var row = (await multi.ReadAsync<ProductDetailRow>()).FirstOrDefault();
        if (row is null) return null;

        var photos = (await multi.ReadAsync<string>()).ToList();
        var setProducts = (await multi.ReadAsync<ProductRow>()).Select(MapProduct).ToList();

        return new ProductDetail(
            row.productid, row.title, row.entitle, row.intro, row.memo,
            row.price, row.fixprice, row.capacity, row.unit,
            row.photo, row.ishot, row.isnew, row.isset, row.isgroupbuy, row.added,
            row.keyword, row.description, row.shortener,
            new BrandSummary(row.brandid, row.brandTitle, row.brandLogo),
            row.typeTitle, photos, setProducts);
    }

    // ── Brand ─────────────────────────────────────────────────────────────────────

    public async Task<BrandDetail?> GetBrandDetailAsync(string brandTitleSlug, CancellationToken ct = default)
    {
        var dbTitle = Slug.ToTitle(brandTitleSlug);
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var sql = @"
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

SELECT TOP 4
    p.productid, p.title, p.entitle, p.price, p.fixprice, p.photo,
    p.ishot, p.isnew, p.isset, p.added, p.shortener,
    b.title AS brandTitle, pt.title AS typeTitle
FROM Products p
JOIN Brands b ON b.brandid = p.brandid
JOIN Producttypes pt ON pt.producttypeid = p.producttypeid
WHERE b.title = @dbTitle AND p.isdisabled = 0
ORDER BY p.sort;";

        using var multi = await conn.QueryMultipleAsync(sql, new { dbTitle });

        var row = (await multi.ReadAsync<BrandDetailRow>()).FirstOrDefault();
        if (row is null) return null;

        var photos = (await multi.ReadAsync<string>()).ToList();
        var products = (await multi.ReadAsync<ProductRow>()).Select(MapProduct).ToList();

        return new BrandDetail(
            row.brandid, row.title, row.subtitle, row.logo, row.banner,
            row.patternentitle, row.patternchtitle, row.parttnervideo, row.patternmemo, row.patternclass,
            row.ilogo, row.slogan, row.intro,
            row.storybgclass, row.storyentitle, row.storychtitle, row.storymemo,
            row.peopletitle, row.peopleslogan, row.peoplememo, row.peoplephoto,
            row.keyword, row.description,
            photos, products);
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
SELECT photo FROM Newmedias WHERE newid = @newId AND photo IS NOT NULL;";

        using var multi = await conn.QueryMultipleAsync(sql, new { newId });

        var row = (await multi.ReadAsync<NewsDetailRow>()).FirstOrDefault();
        if (row is null) return null;

        var media = (await multi.ReadAsync<string>()).ToList();
        return new NewsDetail(row.newid, row.title, row.summary, row.photo, row.intro,
            row.activitydate, row.activityschedule, row.publishdate, row.shortener, media);
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
SELECT recipeid, title, duration, portion, intro, rphoto, photo, shortener
FROM Recipes
WHERE @like IS NULL OR title LIKE @like
ORDER BY sort
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;";

        using var multi = await conn.QueryMultipleAsync(sql,
            new { like, offset = (page - 1) * pageSize, pageSize });

        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<RecipeRow>())
            .Select(r => new RecipeListItem(r.recipeid, r.title, r.duration, r.portion, r.intro, r.rphoto, r.photo, r.shortener))
            .ToList();

        return (items, total);
    }

    public async Task<RecipeDetail?> GetRecipeDetailAsync(Guid recipeId, CancellationToken ct = default)
    {
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        var sql = @"
SELECT recipeid, title, duration, portion, intro, rphoto, photo, youtube, v,
       keyword, description, shortener
FROM Recipes WHERE recipeid = @recipeId;

SELECT title AS name, value AS quantity FROM Recipeingredients
WHERE recipeid = @recipeId ORDER BY sort;

SELECT title AS name, value AS quantity FROM Recipeseasonings
WHERE recipeid = @recipeId ORDER BY sort;

SELECT sort, NULL AS photo, value AS content FROM Recipesteps
WHERE recipeid = @recipeId ORDER BY sort;";

        using var multi = await conn.QueryMultipleAsync(sql, new { recipeId });

        var row = (await multi.ReadAsync<RecipeDetailRow>()).FirstOrDefault();
        if (row is null) return null;

        var ingredients = (await multi.ReadAsync<RecipeIngredientRow>())
            .Select(r => new RecipeIngredient(r.name, r.quantity)).ToList();
        var seasonings = (await multi.ReadAsync<RecipeIngredientRow>())
            .Select(r => new RecipeSeasoning(r.name, r.quantity)).ToList();
        var steps = (await multi.ReadAsync<RecipeStepRow>())
            .Select(r => new RecipeStep(r.sort, r.photo, r.content)).ToList();

        return new RecipeDetail(
            row.recipeid, row.title, row.duration, row.portion, row.intro,
            row.rphoto, row.photo, row.youtube, row.v,
            row.keyword, row.description, row.shortener,
            ingredients, seasonings, steps);
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

        var sql = @"
SELECT issueid, title, photo, intro, keyword, description, createdate, ispublish, shortener
FROM Issues WHERE title = @dbTitle AND ispublish = 1;

SELECT photo FROM Issuemedias WHERE issueid = (
    SELECT issueid FROM Issues WHERE title = @dbTitle AND ispublish = 1
) AND photo IS NOT NULL;";

        using var multi = await conn.QueryMultipleAsync(sql, new { dbTitle });

        var row = (await multi.ReadAsync<IssueDetailRow>()).FirstOrDefault();
        if (row is null) return null;

        var media = (await multi.ReadAsync<string>()).ToList();
        return new IssueDetail(row.issueid, row.title, row.photo, row.intro,
            row.keyword, row.description, row.createdate, row.ispublish, row.shortener, media);
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
FROM Events ORDER BY sort
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

    // ── Mapping helpers ───────────────────────────────────────────────────────────

    private static ProductListItem MapProduct(ProductRow r) =>
        new(r.productid, r.title, r.entitle, r.price, r.fixprice,
            r.photo, r.ishot, r.isnew, r.isset, r.added, r.brandTitle, r.typeTitle, r.shortener);

    // ── Private row types (Dapper columns → named tuples) ─────────────────────────

    private sealed record BannerRow(Guid bannerid, string? title, string? subtitle, string? url, string photo, int style, int sort);
    private sealed record ProductRow(Guid productid, string title, string? entitle, int price, int? fixprice, string photo, bool ishot, bool isnew, bool isset, int added, string? shortener, string brandTitle, string typeTitle);
    private sealed record ProductDetailRow(Guid productid, string title, string? entitle, string? intro, string memo, int price, int? fixprice, string? capacity, string? unit, string photo, bool ishot, bool isnew, bool isset, bool isgroupbuy, int added, string? keyword, string? description, string? shortener, Guid brandid, string brandTitle, string? brandLogo, string typeTitle);
    private sealed record BrandDetailRow(Guid brandid, string title, string? subtitle, string? logo, string? banner, string? patternentitle, string? patternchtitle, string? parttnervideo, string? patternmemo, string? patternclass, string? ilogo, string? slogan, string? intro, string? storybgclass, string? storyentitle, string? storychtitle, string? storymemo, string? peopletitle, string? peopleslogan, string? peoplememo, string? peoplephoto, string? keyword, string? description);
    private sealed record NewsRow(Guid newid, string title, string? summary, string photo, DateTime publishdate, string? shortener);
    private sealed record NewsDetailRow(Guid newid, string title, string? summary, string photo, string intro, string? activitydate, string? activityschedule, DateTime publishdate, string? shortener);
    private sealed record RecipeRow(Guid recipeid, string title, int duration, int portion, string intro, string rphoto, string photo, string? shortener);
    private sealed record RecipeDetailRow(Guid recipeid, string title, int duration, int portion, string intro, string rphoto, string photo, string? youtube, string? v, string? keyword, string? description, string? shortener);
    private sealed record RecipeIngredientRow(string name, string? quantity);
    private sealed record RecipeStepRow(int sort, string? photo, string? content);
    private sealed record IssueRow(Guid issueid, string title, string photo, DateTime createdate, bool ispublish, string? shortener);
    private sealed record IssueDetailRow(Guid issueid, string title, string photo, string? intro, string? keyword, string? description, DateTime createdate, bool ispublish, string? shortener);
    private sealed record EventRow(Guid eventid, string title, string summary, string photo, DateTime eventdate, DateTime createdate, string? shortener);
    private sealed record EventDetailRow(Guid eventid, string title, string summary, string intro, string photo, string? keyword, string? description, DateTime eventdate, DateTime createdate, string? shortener);
}
