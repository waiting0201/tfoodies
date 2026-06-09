using Dapper;
using Microsoft.AspNetCore.Mvc;
using TFoodies.Api.Functions.Helpers;
using TFoodies.Api.Functions.Router;
using TFoodies.Application.Abstractions;
using TFoodies.Contracts.Common;

namespace TFoodies.Api.Functions.Controllers.Admin;

/// <summary>
/// 後台 CMS 內容管理（HomeMs 模組）。
///   GET    /admin/cms/banners                              — 橫幅列表（全量）
///   POST   /admin/cms/banners                              — 新增橫幅
///   PUT    /admin/cms/banners/{id}                         — 更新橫幅
///   DELETE /admin/cms/banners/{id}                         — 刪除橫幅（硬刪）
///
///   GET    /admin/cms/news                                 — 新聞列表（分頁）
///   GET    /admin/cms/news/{id}                            — 新聞明細
///   POST   /admin/cms/news                                 — 新增新聞
///   PUT    /admin/cms/news/{id}                            — 更新新聞
///   DELETE /admin/cms/news/{id}                            — 刪除新聞（硬刪）
///
///   GET    /admin/cms/recipes                              — 食譜列表（分頁）
///   GET    /admin/cms/recipes/{id}                         — 食譜明細（含食材/調味料/步驟）
///   POST   /admin/cms/recipes                              — 新增食譜（含子表）
///   PUT    /admin/cms/recipes/{id}                         — 更新食譜（含子表）
///   DELETE /admin/cms/recipes/{id}                         — 刪除食譜（硬刪）
///
///   GET    /admin/cms/issues                               — 期刊列表（分頁）
///   GET    /admin/cms/issues/{id}                          — 期刊明細
///   POST   /admin/cms/issues                               — 新增期刊
///   PUT    /admin/cms/issues/{id}                          — 更新期刊
///   DELETE /admin/cms/issues/{id}                          — 軟刪除（ispublish=0）
///
///   GET    /admin/cms/events                               — 活動列表（分頁）
///   GET    /admin/cms/events/{id}                          — 活動明細
///   POST   /admin/cms/events                               — 新增活動
///   PUT    /admin/cms/events/{id}                          — 更新活動
///   DELETE /admin/cms/events/{id}                          — 刪除活動（硬刪）
///   GET    /admin/cms/events/{eventId}/photos              — 活動圖片列表
///   POST   /admin/cms/events/{eventId}/photos              — 新增活動圖片
///   PUT    /admin/cms/events/{eventId}/photos/{id}         — 更新活動圖片
///   DELETE /admin/cms/events/{eventId}/photos/{id}         — 刪除活動圖片（硬刪）
///
///   GET    /admin/cms/knowledges                           — FAQ 列表（分頁）
///   GET    /admin/cms/knowledges/{id}                      — FAQ 明細
///   POST   /admin/cms/knowledges                           — 新增 FAQ
///   PUT    /admin/cms/knowledges/{id}                      — 更新 FAQ
///   DELETE /admin/cms/knowledges/{id}                      — 軟刪除（ispublish=0）
///
///   GET    /admin/cms/blogs                                — Blog 列表（全量）
///   GET    /admin/cms/blogs/{id}                           — Blog 明細
///   POST   /admin/cms/blogs                                — 新增 Blog
///   PUT    /admin/cms/blogs/{id}                           — 更新 Blog
///   DELETE /admin/cms/blogs/{id}                           — 刪除 Blog（硬刪）
/// </summary>
public sealed class CmsAdminController
{
    private readonly IAdminPermissionService _perms;
    private readonly IDbConnectionFactory _db;

    public CmsAdminController(IAdminPermissionService perms, IDbConnectionFactory db)
    {
        _perms = perms;
        _db = db;
    }

    // ══════════════════════════════════════════════════════════════════
    // BANNERS（全量，無 isdisable 欄位，硬刪）
    // ══════════════════════════════════════════════════════════════════

    // GET /admin/cms/banners
    public async Task<IActionResult> BannerList(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "HomeMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.QueryAsync(
            "SELECT bannerid AS bannerId, title, subtitle, url, photo AS photoUrl, style, sort FROM Banners ORDER BY sort");
        return ctx.Ok(rows);
    }

    // GET /admin/cms/banners/{id}
    public async Task<IActionResult> BannerDetail(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "HomeMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var bannerId))
            return ctx.BadRequest("無效的橫幅 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var row = await conn.QuerySingleOrDefaultAsync(
            "SELECT bannerid AS bannerId, title, subtitle, url, photo AS photoUrl, style, sort FROM Banners WHERE bannerid=@bannerId",
            new { bannerId });

        if (row is null) return ctx.NotFound("找不到橫幅。");
        return ctx.Ok(row);
    }

    // POST /admin/cms/banners
    public async Task<IActionResult> BannerCreate(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "HomeMs", AdminOperation.Add);
        if (guard.Result is not null) return guard.Result;

        var body = await ctx.TryReadBodyAsync<UpsertBannerRequest>();
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");
        var validation = ValidateBanner(body);
        if (validation is not null) return ctx.BadRequest(validation);

        var newId = Guid.NewGuid();

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        await conn.ExecuteAsync(@"
INSERT INTO Banners (bannerid, title, subtitle, url, photo, style, sort)
VALUES (@bannerid, @title, @subtitle, @url, @photo, @style, @sort)",
            new
            {
                bannerid = newId,
                title    = body.Title,
                subtitle = body.Subtitle ?? string.Empty,
                url      = body.Url ?? string.Empty,
                photo    = body.PhotoUrl ?? string.Empty,
                style    = body.Style,
                sort     = body.Sort,
            });

        return ctx.Created(new { bannerId = newId });
    }

    // PUT /admin/cms/banners/{id}
    public async Task<IActionResult> BannerUpdate(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "HomeMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var bannerId))
            return ctx.BadRequest("無效的橫幅 ID。");

        var body = await ctx.TryReadBodyAsync<UpsertBannerRequest>();
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");
        var validation = ValidateBanner(body);
        if (validation is not null) return ctx.BadRequest(validation);

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.ExecuteAsync(@"
UPDATE Banners SET title=@title, subtitle=@subtitle, url=@url, photo=@photo, style=@style, sort=@sort
WHERE bannerid=@bannerid",
            new
            {
                bannerid = bannerId,
                title    = body.Title,
                subtitle = body.Subtitle ?? string.Empty,
                url      = body.Url ?? string.Empty,
                photo    = body.PhotoUrl ?? string.Empty,
                style    = body.Style,
                sort     = body.Sort,
            });

        if (rows == 0) return ctx.NotFound("找不到橫幅。");
        return ctx.Ok(new { message = "橫幅已更新" });
    }

    // DELETE /admin/cms/banners/{id}
    public async Task<IActionResult> BannerDelete(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "HomeMs", AdminOperation.Delete);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var bannerId))
            return ctx.BadRequest("無效的橫幅 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.ExecuteAsync(
            "DELETE FROM Banners WHERE bannerid=@bannerId",
            new { bannerId });

        if (rows == 0) return ctx.NotFound("找不到橫幅。");
        return ctx.Ok(new { message = "橫幅已刪除" });
    }

    // ══════════════════════════════════════════════════════════════════
    // NEWS（分頁，硬刪；實際欄位: newid/title/summary/photo/intro/publishdate/shortener）
    // ══════════════════════════════════════════════════════════════════

    // GET /admin/cms/news?page=&pageSize=20
    public async Task<IActionResult> NewsList(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "HomeMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        var q        = ctx.Request.Query;
        var page     = Math.Max(1, int.TryParse(q["page"], out var pg) ? pg : 1);
        var pageSize = Math.Clamp(int.TryParse(q["pageSize"], out var sz) ? sz : 20, 1, 100);
        var offset   = (page - 1) * pageSize;

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        var total = await conn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM News");

        var items = await conn.QueryAsync(@"
SELECT newid AS newsId, title, photo, publishdate AS publishDate, shortener
FROM News
ORDER BY publishdate DESC
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY",
            new { offset, pageSize });

        return ctx.OkPaged(PaginatedResponse<object>.Create(items.Cast<object>().ToList(), total, page, pageSize));
    }

    // GET /admin/cms/news/{id}
    public async Task<IActionResult> NewsDetail(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "HomeMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var newId))
            return ctx.BadRequest("無效的新聞 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var row = await conn.QuerySingleOrDefaultAsync(
            "SELECT newid AS newsId, title, summary, intro, photo, publishdate AS publishDate, shortener, activitydate AS activityDate, activityschedule AS activitySchedule FROM News WHERE newid=@newId",
            new { newId });

        if (row is null) return ctx.NotFound("找不到新聞。");
        return ctx.Ok(row);
    }

    // POST /admin/cms/news
    public async Task<IActionResult> NewsCreate(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "HomeMs", AdminOperation.Add);
        if (guard.Result is not null) return guard.Result;

        var body = await ctx.TryReadBodyAsync<UpsertNewsRequest>();
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");
        var validation = ValidateNews(body);
        if (validation is not null) return ctx.BadRequest(validation);

        var newId = Guid.NewGuid();

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        await conn.ExecuteAsync(@"
INSERT INTO News (newid, title, summary, photo, intro, publishdate, shortener, activitydate, activityschedule)
VALUES (@newid, @title, @summary, @photo, @intro, @publishdate, @shortener, @activitydate, @activityschedule)",
            new
            {
                newid            = newId,
                title            = body.Title,
                summary          = body.Summary ?? string.Empty,
                photo            = body.Photo ?? string.Empty,
                intro            = body.Intro ?? string.Empty,
                publishdate      = body.PublishDate ?? DateTime.UtcNow.AddHours(8),
                shortener        = body.Shortener ?? string.Empty,
                activitydate     = body.ActivityDate ?? string.Empty,
                activityschedule = body.ActivitySchedule ?? string.Empty,
            });

        return ctx.Created(new { newId });
    }

    // PUT /admin/cms/news/{id}
    public async Task<IActionResult> NewsUpdate(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "HomeMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var newId))
            return ctx.BadRequest("無效的新聞 ID。");

        var body = await ctx.TryReadBodyAsync<UpsertNewsRequest>();
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");
        var validation = ValidateNews(body);
        if (validation is not null) return ctx.BadRequest(validation);

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.ExecuteAsync(@"
UPDATE News SET title=@title, summary=@summary, photo=@photo, intro=@intro,
    publishdate=@publishdate, shortener=@shortener,
    activitydate=@activitydate, activityschedule=@activityschedule
WHERE newid=@newid",
            new
            {
                newid            = newId,
                title            = body.Title,
                summary          = body.Summary ?? string.Empty,
                photo            = body.Photo ?? string.Empty,
                intro            = body.Intro ?? string.Empty,
                publishdate      = body.PublishDate ?? DateTime.UtcNow.AddHours(8),
                shortener        = body.Shortener ?? string.Empty,
                activitydate     = body.ActivityDate ?? string.Empty,
                activityschedule = body.ActivitySchedule ?? string.Empty,
            });

        if (rows == 0) return ctx.NotFound("找不到新聞。");
        return ctx.Ok(new { message = "新聞已更新" });
    }

    // DELETE /admin/cms/news/{id}
    public async Task<IActionResult> NewsDelete(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "HomeMs", AdminOperation.Delete);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var newId))
            return ctx.BadRequest("無效的新聞 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.ExecuteAsync(
            "DELETE FROM News WHERE newid=@newId",
            new { newId });

        if (rows == 0) return ctx.NotFound("找不到新聞。");
        return ctx.Ok(new { message = "新聞已刪除" });
    }

    // ══════════════════════════════════════════════════════════════════
    // RECIPES（分頁，硬刪；實際欄位: recipeid/title/photo/rphoto/intro/duration/portion/youtube/v/keyword/description/sort/shortener）
    // 子表：Recipeingredients / Recipeseasonings / Recipesteps（delete-insert on save）
    // ══════════════════════════════════════════════════════════════════

    // GET /admin/cms/recipes?page=&pageSize=20
    public async Task<IActionResult> RecipeList(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "HomeMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        var (page, pageSize, offset) = ExtractPaging(ctx);

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        var total = await conn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Recipes");

        var items = await conn.QueryAsync(@"
SELECT recipeid AS recipeId, title, photo, rphoto, intro, duration, portion, youtube, keyword, description, sort, shortener
FROM Recipes
ORDER BY sort ASC, recipeid ASC
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY",
            new { offset, pageSize });

        return ctx.OkPaged(PaginatedResponse<object>.Create(items.Cast<object>().ToList(), total, page, pageSize));
    }

    // GET /admin/cms/recipes/{id}
    public async Task<IActionResult> RecipeDetail(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "HomeMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var recipeId))
            return ctx.BadRequest("無效的食譜 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        var recipe = await conn.QuerySingleOrDefaultAsync(@"
SELECT recipeid AS recipeId, title, photo, rphoto, intro, duration, portion,
       youtube, v, keyword, description, sort, shortener
FROM Recipes
WHERE recipeid=@recipeId",
            new { recipeId });

        if (recipe is null) return ctx.NotFound("找不到食譜。");

        var ingredients = await conn.QueryAsync(@"
SELECT recipeingredientid AS id, title, value, sort
FROM Recipeingredients
WHERE recipeid=@recipeId
ORDER BY sort",
            new { recipeId });

        var seasonings = await conn.QueryAsync(@"
SELECT recipeseasoningid AS id, title, value, sort
FROM Recipeseasonings
WHERE recipeid=@recipeId
ORDER BY sort",
            new { recipeId });

        var steps = await conn.QueryAsync(@"
SELECT recipestepid AS id, title, value, sort
FROM Recipesteps
WHERE recipeid=@recipeId
ORDER BY sort",
            new { recipeId });

        // Dapper 回傳 dynamic ExpandoObject；使用匿名型別組合後回傳
        var r = (IDictionary<string, object?>)recipe!;
        return ctx.Ok(new
        {
            recipeId    = r["recipeId"],
            title       = r["title"],
            photo       = r["photo"],
            rphoto      = r["rphoto"],
            intro       = r["intro"],
            duration    = r["duration"],
            portion     = r["portion"],
            youtube     = r["youtube"],
            v           = r["v"],
            keyword     = r["keyword"],
            description = r["description"],
            sort        = r["sort"],
            shortener   = r["shortener"],
            ingredients = ingredients,
            seasonings  = seasonings,
            steps       = steps,
        });
    }

    // POST /admin/cms/recipes
    public async Task<IActionResult> RecipeCreate(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "HomeMs", AdminOperation.Add);
        if (guard.Result is not null) return guard.Result;

        var body = await ctx.TryReadBodyAsync<UpsertRecipeRequest>();
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");
        var validation = ValidateRecipe(body);
        if (validation is not null) return ctx.BadRequest(validation);

        var newId   = Guid.NewGuid();
        var videoId = ExtractYoutubeVideoId(body.Youtube);

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        using var tx   = conn.BeginTransaction();
        try
        {
            await conn.ExecuteAsync(@"
INSERT INTO Recipes (recipeid, title, photo, rphoto, intro, duration, portion, youtube, v, keyword, description, sort, shortener)
VALUES (@recipeid, @title, @photo, @rphoto, @intro, @duration, @portion, @youtube, @v, @keyword, @description, @sort, @shortener)",
                new
                {
                    recipeid    = newId,
                    title       = body.Title,
                    photo       = body.Photo ?? string.Empty,
                    rphoto      = body.Rphoto ?? string.Empty,
                    intro       = body.Intro ?? string.Empty,
                    duration    = body.Duration,
                    portion     = body.Portion,
                    youtube     = body.Youtube ?? string.Empty,
                    v           = videoId,
                    keyword     = body.Keyword ?? string.Empty,
                    description = body.Description ?? string.Empty,
                    sort        = body.Sort,
                    shortener   = body.Shortener ?? string.Empty,
                }, transaction: tx);

            await SaveRecipeChildrenAsync(conn, tx, newId, body);

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }

        return ctx.Created(new { recipeId = newId });
    }

    // PUT /admin/cms/recipes/{id}
    public async Task<IActionResult> RecipeUpdate(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "HomeMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var recipeId))
            return ctx.BadRequest("無效的食譜 ID。");

        var body = await ctx.TryReadBodyAsync<UpsertRecipeRequest>();
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");
        var validation = ValidateRecipe(body);
        if (validation is not null) return ctx.BadRequest(validation);

        var videoId = ExtractYoutubeVideoId(body.Youtube);

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        using var tx   = conn.BeginTransaction();
        try
        {
            var rows = await conn.ExecuteAsync(@"
UPDATE Recipes SET title=@title, photo=@photo, rphoto=@rphoto, intro=@intro,
    duration=@duration, portion=@portion, youtube=@youtube, v=@v,
    keyword=@keyword, description=@description, sort=@sort, shortener=@shortener
WHERE recipeid=@recipeid",
                new
                {
                    recipeid    = recipeId,
                    title       = body.Title,
                    photo       = body.Photo ?? string.Empty,
                    rphoto      = body.Rphoto ?? string.Empty,
                    intro       = body.Intro ?? string.Empty,
                    duration    = body.Duration,
                    portion     = body.Portion,
                    youtube     = body.Youtube ?? string.Empty,
                    v           = videoId,
                    keyword     = body.Keyword ?? string.Empty,
                    description = body.Description ?? string.Empty,
                    sort        = body.Sort,
                    shortener   = body.Shortener ?? string.Empty,
                }, transaction: tx);

            if (rows == 0)
            {
                tx.Rollback();
                return ctx.NotFound("找不到食譜。");
            }

            await SaveRecipeChildrenAsync(conn, tx, recipeId, body);

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }

        return ctx.Ok(new { message = "食譜已更新" });
    }

    // DELETE /admin/cms/recipes/{id}
    public async Task<IActionResult> RecipeDelete(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "HomeMs", AdminOperation.Delete);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var recipeId))
            return ctx.BadRequest("無效的食譜 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.ExecuteAsync(
            "DELETE FROM Recipes WHERE recipeid=@recipeId",
            new { recipeId });

        if (rows == 0) return ctx.NotFound("找不到食譜。");
        return ctx.Ok(new { message = "食譜已刪除" });
    }

    /// <summary>
    /// Delete-then-insert 子表（食材/調味料/步驟）。
    /// 必須在已開啟的 transaction 中呼叫。
    /// </summary>
    private static async Task SaveRecipeChildrenAsync(
        System.Data.IDbConnection conn,
        System.Data.IDbTransaction tx,
        Guid recipeId,
        UpsertRecipeRequest body)
    {
        // Ingredients
        await conn.ExecuteAsync(
            "DELETE FROM Recipeingredients WHERE recipeid=@recipeId",
            new { recipeId }, transaction: tx);

        if (body.Ingredients is { Count: > 0 })
        {
            for (var i = 0; i < body.Ingredients.Count; i++)
            {
                var item = body.Ingredients[i];
                await conn.ExecuteAsync(@"
INSERT INTO Recipeingredients (recipeingredientid, recipeid, title, value, sort)
VALUES (@id, @recipeid, @title, @value, @sort)",
                    new { id = Guid.NewGuid(), recipeid = recipeId, title = item.Title, value = item.Value, sort = i + 1 },
                    transaction: tx);
            }
        }

        // Seasonings
        await conn.ExecuteAsync(
            "DELETE FROM Recipeseasonings WHERE recipeid=@recipeId",
            new { recipeId }, transaction: tx);

        if (body.Seasonings is { Count: > 0 })
        {
            for (var i = 0; i < body.Seasonings.Count; i++)
            {
                var item = body.Seasonings[i];
                await conn.ExecuteAsync(@"
INSERT INTO Recipeseasonings (recipeseasoningid, recipeid, title, value, sort)
VALUES (@id, @recipeid, @title, @value, @sort)",
                    new { id = Guid.NewGuid(), recipeid = recipeId, title = item.Title, value = item.Value, sort = i + 1 },
                    transaction: tx);
            }
        }

        // Steps
        await conn.ExecuteAsync(
            "DELETE FROM Recipesteps WHERE recipeid=@recipeId",
            new { recipeId }, transaction: tx);

        if (body.Steps is { Count: > 0 })
        {
            for (var i = 0; i < body.Steps.Count; i++)
            {
                var item = body.Steps[i];
                await conn.ExecuteAsync(@"
INSERT INTO Recipesteps (recipestepid, recipeid, title, value, sort)
VALUES (@id, @recipeid, @title, @value, @sort)",
                    new { id = Guid.NewGuid(), recipeid = recipeId, title = item.Title, value = item.Value, sort = i + 1 },
                    transaction: tx);
            }
        }
    }

    /// <summary>
    /// 從 YouTube URL 提取影片 ID。
    /// 支援 https://www.youtube.com/watch?v=XXXX 和 https://youtu.be/XXXX 兩種格式。
    /// 無法解析時回傳空字串，不丟例外。
    /// </summary>
    private static string ExtractYoutubeVideoId(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return string.Empty;

        // youtu.be/{id} 短網址格式
        if (uri.Host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase))
        {
            var segment = uri.AbsolutePath.TrimStart('/');
            // 去掉可能的額外路徑，只取第一段
            var slash = segment.IndexOf('/');
            return slash >= 0 ? segment[..slash] : segment;
        }

        // 標準 watch?v= 格式，手動解析 query string 避免 System.Web 依賴
        var query = uri.Query;
        if (string.IsNullOrEmpty(query)) return string.Empty;

        // 逐項解析 key=value，找 v= 參數
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            if (eq <= 0) continue;
            var key = Uri.UnescapeDataString(pair[..eq]);
            if (key.Equals("v", StringComparison.Ordinal))
                return Uri.UnescapeDataString(pair[(eq + 1)..]);
        }

        return string.Empty;
    }

    // ══════════════════════════════════════════════════════════════════
    // ISSUES（分頁，軟刪 ispublish=0；實際欄位: issueid/title/photo/intro/keyword/description/sort/createdate/ispublish/shortener）
    // ══════════════════════════════════════════════════════════════════

    // GET /admin/cms/issues?page=&pageSize=20
    public async Task<IActionResult> IssueList(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "HomeMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        var (page, pageSize, offset) = ExtractPaging(ctx);

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        var total = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Issues WHERE ispublish=1");

        var items = await conn.QueryAsync(@"
SELECT issueid AS issueId, title, photo, intro, keyword, description, sort, shortener, ispublish
FROM Issues
WHERE ispublish=1
ORDER BY createdate DESC
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY",
            new { offset, pageSize });

        return ctx.OkPaged(PaginatedResponse<object>.Create(items.Cast<object>().ToList(), total, page, pageSize));
    }

    // GET /admin/cms/issues/{id}
    public async Task<IActionResult> IssueDetail(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "HomeMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var issueId))
            return ctx.BadRequest("無效的期刊 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        // 後台編輯不過濾 ispublish，允許編輯已下架的期刊
        var row = await conn.QuerySingleOrDefaultAsync(
            "SELECT issueid AS issueId, title, photo, intro, keyword, description, sort, shortener, ispublish FROM Issues WHERE issueid=@issueId",
            new { issueId });

        if (row is null) return ctx.NotFound("找不到期刊。");
        return ctx.Ok(row);
    }

    // POST /admin/cms/issues
    public async Task<IActionResult> IssueCreate(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "HomeMs", AdminOperation.Add);
        if (guard.Result is not null) return guard.Result;

        var body = await ctx.TryReadBodyAsync<UpsertIssueRequest>();
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");
        var validation = ValidateIssue(body);
        if (validation is not null) return ctx.BadRequest(validation);

        var newId = Guid.NewGuid();
        var now   = DateTime.UtcNow.AddHours(8);

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        await conn.ExecuteAsync(@"
INSERT INTO Issues (issueid, title, photo, intro, keyword, description, sort, createdate, ispublish, shortener)
VALUES (@issueid, @title, @photo, @intro, @keyword, @description, @sort, @createdate, @ispublish, @shortener)",
            new
            {
                issueid     = newId,
                title       = body.Title,
                photo       = body.Photo ?? string.Empty,
                intro       = body.Intro ?? string.Empty,
                keyword     = body.Keyword ?? string.Empty,
                description = body.Description ?? string.Empty,
                sort        = body.Sort,
                createdate  = now,
                ispublish   = body.IsPublish,
                shortener   = body.Shortener ?? string.Empty,
            });

        return ctx.Created(new { issueId = newId });
    }

    // PUT /admin/cms/issues/{id}
    public async Task<IActionResult> IssueUpdate(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "HomeMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var issueId))
            return ctx.BadRequest("無效的期刊 ID。");

        var body = await ctx.TryReadBodyAsync<UpsertIssueRequest>();
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");
        var validation = ValidateIssue(body);
        if (validation is not null) return ctx.BadRequest(validation);

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.ExecuteAsync(@"
UPDATE Issues SET title=@title, photo=@photo, intro=@intro, keyword=@keyword,
    description=@description, sort=@sort, shortener=@shortener, ispublish=@ispublish
WHERE issueid=@issueid",
            new
            {
                issueid     = issueId,
                title       = body.Title,
                photo       = body.Photo ?? string.Empty,
                intro       = body.Intro ?? string.Empty,
                keyword     = body.Keyword ?? string.Empty,
                description = body.Description ?? string.Empty,
                sort        = body.Sort,
                shortener   = body.Shortener ?? string.Empty,
                ispublish   = body.IsPublish,
            });

        if (rows == 0) return ctx.NotFound("找不到期刊。");
        return ctx.Ok(new { message = "期刊已更新" });
    }

    // DELETE /admin/cms/issues/{id}
    public async Task<IActionResult> IssueDelete(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "HomeMs", AdminOperation.Delete);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var issueId))
            return ctx.BadRequest("無效的期刊 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.ExecuteAsync(
            "UPDATE Issues SET ispublish=0 WHERE issueid=@issueId",
            new { issueId });

        if (rows == 0) return ctx.NotFound("找不到期刊。");
        return ctx.Ok(new { message = "期刊已下架" });
    }

    // ══════════════════════════════════════════════════════════════════
    // EVENTS（分頁，硬刪；實際欄位: eventid/title/summary/intro/eventdate/photo/keyword/description/createdate/sort/shortener）
    // ══════════════════════════════════════════════════════════════════

    // GET /admin/cms/events?page=&pageSize=20
    public async Task<IActionResult> EventList(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "HomeMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        var (page, pageSize, offset) = ExtractPaging(ctx);

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        var total = await conn.ExecuteScalarAsync<int>("SELECT COUNT(1) FROM Events");

        var items = await conn.QueryAsync(@"
SELECT eventid AS eventId, title, summary, photo, intro, eventdate AS eventDate, keyword, description, sort, shortener
FROM Events
ORDER BY eventdate DESC
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY",
            new { offset, pageSize });

        return ctx.OkPaged(PaginatedResponse<object>.Create(items.Cast<object>().ToList(), total, page, pageSize));
    }

    // GET /admin/cms/events/{id}
    public async Task<IActionResult> EventDetail(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "HomeMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var eventId))
            return ctx.BadRequest("無效的活動 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var row = await conn.QuerySingleOrDefaultAsync(
            "SELECT eventid AS eventId, title, summary, photo, intro, eventdate AS eventDate, keyword, description, sort, shortener FROM Events WHERE eventid=@eventId",
            new { eventId });

        if (row is null) return ctx.NotFound("找不到活動。");
        return ctx.Ok(row);
    }

    // POST /admin/cms/events
    public async Task<IActionResult> EventCreate(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "HomeMs", AdminOperation.Add);
        if (guard.Result is not null) return guard.Result;

        var body = await ctx.TryReadBodyAsync<UpsertEventRequest>();
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");
        var validation = ValidateEvent(body);
        if (validation is not null) return ctx.BadRequest(validation);

        var newId = Guid.NewGuid();
        var now   = DateTime.UtcNow.AddHours(8);

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        await conn.ExecuteAsync(@"
INSERT INTO Events (eventid, title, summary, photo, intro, eventdate, createdate, keyword, description, sort, shortener)
VALUES (@eventid, @title, @summary, @photo, @intro, @eventdate, @createdate, @keyword, @description, @sort, @shortener)",
            new
            {
                eventid     = newId,
                title       = body.Title,
                summary     = body.Summary ?? string.Empty,
                photo       = body.Photo ?? string.Empty,
                intro       = body.Intro ?? string.Empty,
                eventdate   = body.EventDate ?? now,
                createdate  = now,
                keyword     = body.Keyword ?? string.Empty,
                description = body.Description ?? string.Empty,
                sort        = body.Sort,
                shortener   = body.Shortener ?? string.Empty,
            });

        return ctx.Created(new { eventId = newId });
    }

    // PUT /admin/cms/events/{id}
    public async Task<IActionResult> EventUpdate(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "HomeMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var eventId))
            return ctx.BadRequest("無效的活動 ID。");

        var body = await ctx.TryReadBodyAsync<UpsertEventRequest>();
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");
        var validation = ValidateEvent(body);
        if (validation is not null) return ctx.BadRequest(validation);

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.ExecuteAsync(@"
UPDATE Events SET title=@title, summary=@summary, photo=@photo, intro=@intro,
    eventdate=@eventdate, keyword=@keyword, description=@description, sort=@sort, shortener=@shortener
WHERE eventid=@eventid",
            new
            {
                eventid     = eventId,
                title       = body.Title,
                summary     = body.Summary ?? string.Empty,
                photo       = body.Photo ?? string.Empty,
                intro       = body.Intro ?? string.Empty,
                eventdate   = body.EventDate ?? DateTime.UtcNow.AddHours(8),
                keyword     = body.Keyword ?? string.Empty,
                description = body.Description ?? string.Empty,
                sort        = body.Sort,
                shortener   = body.Shortener ?? string.Empty,
            });

        if (rows == 0) return ctx.NotFound("找不到活動。");
        return ctx.Ok(new { message = "活動已更新" });
    }

    // DELETE /admin/cms/events/{id}
    public async Task<IActionResult> EventDelete(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "HomeMs", AdminOperation.Delete);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var eventId))
            return ctx.BadRequest("無效的活動 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.ExecuteAsync(
            "DELETE FROM Events WHERE eventid=@eventId",
            new { eventId });

        if (rows == 0) return ctx.NotFound("找不到活動。");
        return ctx.Ok(new { message = "活動已刪除" });
    }

    // ══════════════════════════════════════════════════════════════════
    // EVENTPHOTOS（活動圖片子表；硬刪；實際欄位: eventphotoid/eventid/photo/sort）
    // ══════════════════════════════════════════════════════════════════

    // GET /admin/cms/events/{eventId}/photos
    public async Task<IActionResult> EventphotoList(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "HomeMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("eventId"), out var eventId))
            return ctx.BadRequest("無效的活動 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.QueryAsync(@"
SELECT eventphotoid AS eventphotoId, photo, sort
FROM Eventphotos
WHERE eventid=@eventId
ORDER BY sort",
            new { eventId });

        return ctx.Ok(rows);
    }

    // POST /admin/cms/events/{eventId}/photos
    public async Task<IActionResult> EventphotoCreate(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "HomeMs", AdminOperation.Add);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("eventId"), out var eventId))
            return ctx.BadRequest("無效的活動 ID。");

        var body = await ctx.TryReadBodyAsync<UpsertEventphotoRequest>();
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");
        var validation = ValidateEventphoto(body);
        if (validation is not null) return ctx.BadRequest(validation);

        var newId = Guid.NewGuid();

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        await conn.ExecuteAsync(@"
INSERT INTO Eventphotos (eventphotoid, eventid, photo, sort)
VALUES (@eventphotoid, @eventid, @photo, @sort)",
            new
            {
                eventphotoid = newId,
                eventid      = eventId,
                photo        = body.Photo,
                sort         = body.Sort,
            });

        return ctx.Created(new { eventphotoId = newId });
    }

    // PUT /admin/cms/events/{eventId}/photos/{id}
    public async Task<IActionResult> EventphotoUpdate(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "HomeMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("eventId"), out var eventId))
            return ctx.BadRequest("無效的活動 ID。");
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var eventphotoId))
            return ctx.BadRequest("無效的圖片 ID。");

        var body = await ctx.TryReadBodyAsync<UpsertEventphotoRequest>();
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");
        var validation = ValidateEventphoto(body);
        if (validation is not null) return ctx.BadRequest(validation);

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.ExecuteAsync(@"
UPDATE Eventphotos SET photo=@photo, sort=@sort
WHERE eventphotoid=@eventphotoid AND eventid=@eventid",
            new
            {
                eventphotoid = eventphotoId,
                eventid      = eventId,
                photo        = body.Photo,
                sort         = body.Sort,
            });

        if (rows == 0) return ctx.NotFound("找不到活動圖片。");
        return ctx.Ok(new { message = "活動圖片已更新" });
    }

    // DELETE /admin/cms/events/{eventId}/photos/{id}
    public async Task<IActionResult> EventphotoDelete(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "HomeMs", AdminOperation.Delete);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("eventId"), out var eventId))
            return ctx.BadRequest("無效的活動 ID。");
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var eventphotoId))
            return ctx.BadRequest("無效的圖片 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.ExecuteAsync(
            "DELETE FROM Eventphotos WHERE eventphotoid=@eventphotoid AND eventid=@eventid",
            new { eventphotoid = eventphotoId, eventid = eventId });

        if (rows == 0) return ctx.NotFound("找不到活動圖片。");
        return ctx.Ok(new { message = "活動圖片已刪除" });
    }

    // ══════════════════════════════════════════════════════════════════
    // KNOWLEDGES / FAQ（分頁，軟刪 ispublish=0；實際欄位: knowledgeid/question/photo/answer/keyword/description/sort/createdate/ispublish/shortener；無 title 欄位）
    // ══════════════════════════════════════════════════════════════════

    // GET /admin/cms/knowledges?page=&pageSize=20
    public async Task<IActionResult> KnowledgeList(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "HomeMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        var (page, pageSize, offset) = ExtractPaging(ctx);

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        var total = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Knowledges WHERE ispublish=1");

        var items = await conn.QueryAsync(@"
SELECT knowledgeid AS knowledgeId, question, photo, answer, keyword, description, sort, shortener, ispublish
FROM Knowledges
WHERE ispublish=1
ORDER BY sort ASC, knowledgeid ASC
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY",
            new { offset, pageSize });

        return ctx.OkPaged(PaginatedResponse<object>.Create(items.Cast<object>().ToList(), total, page, pageSize));
    }

    // GET /admin/cms/knowledges/{id}
    public async Task<IActionResult> KnowledgeDetail(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "HomeMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var knowledgeId))
            return ctx.BadRequest("無效的 FAQ ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        // 後台編輯不過濾 ispublish，允許編輯已下架的 FAQ
        var row = await conn.QuerySingleOrDefaultAsync(
            "SELECT knowledgeid AS knowledgeId, question, photo, answer, keyword, description, sort, shortener, ispublish FROM Knowledges WHERE knowledgeid=@knowledgeId",
            new { knowledgeId });

        if (row is null) return ctx.NotFound("找不到 FAQ。");
        return ctx.Ok(row);
    }

    // POST /admin/cms/knowledges
    public async Task<IActionResult> KnowledgeCreate(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "HomeMs", AdminOperation.Add);
        if (guard.Result is not null) return guard.Result;

        var body = await ctx.TryReadBodyAsync<UpsertKnowledgeRequest>();
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");
        var validation = ValidateKnowledge(body);
        if (validation is not null) return ctx.BadRequest(validation);

        var newId = Guid.NewGuid();
        var now   = DateTime.UtcNow.AddHours(8);

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        await conn.ExecuteAsync(@"
INSERT INTO Knowledges (knowledgeid, question, photo, answer, keyword, description, sort, createdate, ispublish, shortener)
VALUES (@knowledgeid, @question, @photo, @answer, @keyword, @description, @sort, @createdate, @ispublish, @shortener)",
            new
            {
                knowledgeid = newId,
                question    = body.Question,
                photo       = body.Photo ?? string.Empty,
                answer      = body.Answer,
                keyword     = body.Keyword ?? string.Empty,
                description = body.Description ?? string.Empty,
                sort        = body.Sort,
                createdate  = now,
                ispublish   = body.IsPublish,
                shortener   = body.Shortener ?? string.Empty,
            });

        return ctx.Created(new { knowledgeId = newId });
    }

    // PUT /admin/cms/knowledges/{id}
    public async Task<IActionResult> KnowledgeUpdate(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "HomeMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var knowledgeId))
            return ctx.BadRequest("無效的 FAQ ID。");

        var body = await ctx.TryReadBodyAsync<UpsertKnowledgeRequest>();
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");
        var validation = ValidateKnowledge(body);
        if (validation is not null) return ctx.BadRequest(validation);

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.ExecuteAsync(@"
UPDATE Knowledges SET question=@question, photo=@photo, answer=@answer, keyword=@keyword,
    description=@description, sort=@sort, shortener=@shortener, ispublish=@ispublish
WHERE knowledgeid=@knowledgeid",
            new
            {
                knowledgeid = knowledgeId,
                question    = body.Question,
                photo       = body.Photo ?? string.Empty,
                answer      = body.Answer,
                keyword     = body.Keyword ?? string.Empty,
                description = body.Description ?? string.Empty,
                sort        = body.Sort,
                shortener   = body.Shortener ?? string.Empty,
                ispublish   = body.IsPublish,
            });

        if (rows == 0) return ctx.NotFound("找不到 FAQ。");
        return ctx.Ok(new { message = "FAQ 已更新" });
    }

    // DELETE /admin/cms/knowledges/{id}
    public async Task<IActionResult> KnowledgeDelete(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "HomeMs", AdminOperation.Delete);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var knowledgeId))
            return ctx.BadRequest("無效的 FAQ ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.ExecuteAsync(
            "UPDATE Knowledges SET ispublish=0 WHERE knowledgeid=@knowledgeId",
            new { knowledgeId });

        if (rows == 0) return ctx.NotFound("找不到 FAQ。");
        return ctx.Ok(new { message = "FAQ 已下架" });
    }

    // ══════════════════════════════════════════════════════════════════
    // BLOGS（全量，硬刪；實際欄位: blogid/title/photo/link/sort）
    // ══════════════════════════════════════════════════════════════════

    // GET /admin/cms/blogs
    public async Task<IActionResult> BlogList(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "HomeMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.QueryAsync(
            "SELECT blogid AS blogId, title, photo, link, sort FROM Blogs ORDER BY sort");

        return ctx.Ok(rows);
    }

    // GET /admin/cms/blogs/{id}
    public async Task<IActionResult> BlogDetail(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "HomeMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var blogId))
            return ctx.BadRequest("無效的 Blog ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var row = await conn.QuerySingleOrDefaultAsync(
            "SELECT blogid AS blogId, title, photo, link, sort FROM Blogs WHERE blogid=@blogId",
            new { blogId });

        if (row is null) return ctx.NotFound("找不到 Blog。");
        return ctx.Ok(row);
    }

    // POST /admin/cms/blogs
    public async Task<IActionResult> BlogCreate(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "HomeMs", AdminOperation.Add);
        if (guard.Result is not null) return guard.Result;

        var body = await ctx.TryReadBodyAsync<UpsertBlogRequest>();
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");
        var validation = ValidateBlog(body);
        if (validation is not null) return ctx.BadRequest(validation);

        var newId = Guid.NewGuid();

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        await conn.ExecuteAsync(@"
INSERT INTO Blogs (blogid, title, photo, link, sort)
VALUES (@blogid, @title, @photo, @link, @sort)",
            new
            {
                blogid = newId,
                title  = body.Title,
                photo  = body.Photo ?? string.Empty,
                link   = body.Link ?? string.Empty,
                sort   = body.Sort,
            });

        return ctx.Created(new { blogId = newId });
    }

    // PUT /admin/cms/blogs/{id}
    public async Task<IActionResult> BlogUpdate(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "HomeMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var blogId))
            return ctx.BadRequest("無效的 Blog ID。");

        var body = await ctx.TryReadBodyAsync<UpsertBlogRequest>();
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");
        var validation = ValidateBlog(body);
        if (validation is not null) return ctx.BadRequest(validation);

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.ExecuteAsync(@"
UPDATE Blogs SET title=@title, photo=@photo, link=@link, sort=@sort
WHERE blogid=@blogid",
            new
            {
                blogid = blogId,
                title  = body.Title,
                photo  = body.Photo ?? string.Empty,
                link   = body.Link ?? string.Empty,
                sort   = body.Sort,
            });

        if (rows == 0) return ctx.NotFound("找不到 Blog。");
        return ctx.Ok(new { message = "Blog 已更新" });
    }

    // DELETE /admin/cms/blogs/{id}
    public async Task<IActionResult> BlogDelete(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "HomeMs", AdminOperation.Delete);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var blogId))
            return ctx.BadRequest("無效的 Blog ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.ExecuteAsync(
            "DELETE FROM Blogs WHERE blogid=@blogId",
            new { blogId });

        if (rows == 0) return ctx.NotFound("找不到 Blog。");
        return ctx.Ok(new { message = "Blog 已刪除" });
    }

    // ══════════════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════════════

    private static (int page, int pageSize, int offset) ExtractPaging(RouteContext ctx)
    {
        var q        = ctx.Request.Query;
        var page     = Math.Max(1, int.TryParse(q["page"], out var pg) ? pg : 1);
        var pageSize = Math.Clamp(int.TryParse(q["pageSize"], out var sz) ? sz : 20, 1, 100);
        return (page, pageSize, (page - 1) * pageSize);
    }

    private static string? ValidateBanner(UpsertBannerRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Title)) return "缺少 title 欄位。";
        return null;
    }

    private static string? ValidateNews(UpsertNewsRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Title)) return "缺少 title 欄位。";
        return null;
    }

    private static string? ValidateRecipe(UpsertRecipeRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Title)) return "缺少 title 欄位。";
        return null;
    }

    private static string? ValidateIssue(UpsertIssueRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Title)) return "缺少 title 欄位。";
        return null;
    }

    private static string? ValidateEvent(UpsertEventRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Title)) return "缺少 title 欄位。";
        return null;
    }

    private static string? ValidateKnowledge(UpsertKnowledgeRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Question)) return "缺少 question 欄位。";
        if (string.IsNullOrWhiteSpace(r.Answer))   return "缺少 answer 欄位。";
        return null;
    }

    private static string? ValidateBlog(UpsertBlogRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Title)) return "缺少 title 欄位。";
        return null;
    }

    private static string? ValidateEventphoto(UpsertEventphotoRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Photo)) return "缺少 photo 欄位。";
        return null;
    }

    // ══════════════════════════════════════════════════════════════════
    // Request DTOs
    // ══════════════════════════════════════════════════════════════════

    private sealed record UpsertBannerRequest(
        string Title, string? Subtitle, string? Url, string? PhotoUrl, int Style, int Sort);

    private sealed record UpsertNewsRequest(
        string Title, string? Summary, string? Photo, string? Intro,
        DateTime? PublishDate, string? Shortener,
        string? ActivityDate, string? ActivitySchedule);

    private sealed record UpsertRecipeRequest(
        string Title, string? Photo, string? Rphoto, string? Intro,
        int Duration, int Portion, int Sort, string? Shortener,
        string? Youtube, string? Keyword, string? Description,
        List<RecipeChildItem>? Ingredients,
        List<RecipeChildItem>? Seasonings,
        List<RecipeStepItem>?  Steps);

    private sealed record RecipeChildItem(string Title, string Value);
    private sealed record RecipeStepItem(string Title, string Value);

    private sealed record UpsertIssueRequest(
        string Title, string? Photo, string? Intro,
        string? Keyword, string? Description, int Sort, string? Shortener,
        bool IsPublish);

    private sealed record UpsertEventRequest(
        string Title, string? Summary, string? Photo, string? Intro,
        DateTime? EventDate, int Sort, string? Shortener,
        string? Keyword, string? Description);

    private sealed record UpsertKnowledgeRequest(
        string Question, string Answer, int Sort,
        string? Photo, string? Keyword, string? Description,
        string? Shortener, bool IsPublish);

    private sealed record UpsertBlogRequest(
        string Title, string? Photo, string? Link, int Sort);

    private sealed record UpsertEventphotoRequest(
        string Photo, int Sort);
}
