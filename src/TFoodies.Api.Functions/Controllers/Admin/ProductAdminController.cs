using Dapper;
using Microsoft.AspNetCore.Mvc;
using TFoodies.Api.Functions.Helpers;
using TFoodies.Api.Functions.Router;
using TFoodies.Application.Abstractions;
using TFoodies.Contracts.Common;

namespace TFoodies.Api.Functions.Controllers.Admin;

/// <summary>
/// 後台商品型錄管理。
///   GET    /admin/products           — 商品列表（篩選/分頁）
///   GET    /admin/products/{id}      — 商品明細
///   POST   /admin/products           — 新增商品
///   PUT    /admin/products/{id}      — 更新商品
///   DELETE /admin/products/{id}      — 軟刪除（isdisabled=true）
///   GET    /admin/brands             — 品牌列表（select 用）
///   GET    /admin/producttypes       — 品類列表（select 用）
/// </summary>
public sealed class ProductAdminController
{
    private readonly IAdminPermissionService _perms;
    private readonly IDbConnectionFactory _db;

    public ProductAdminController(IAdminPermissionService perms, IDbConnectionFactory db)
    {
        _perms = perms;
        _db = db;
    }

    // GET /admin/products?keyword=&brandId=&typeId=&disabled=false&page=1&pageSize=20
    public async Task<IActionResult> List(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "ProductMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        var q = ctx.Request.Query;
        var keyword = q["keyword"].ToString().Trim();
        var brandId = Guid.TryParse(q["brandId"], out var bid) ? bid : (Guid?)null;
        var typeId = Guid.TryParse(q["typeId"], out var tid) ? tid : (Guid?)null;
        bool? disabled = bool.TryParse(q["disabled"], out var d) ? d : null;
        var page = Math.Max(1, int.TryParse(q["page"], out var pg) ? pg : 1);
        var pageSize = Math.Clamp(int.TryParse(q["pageSize"], out var sz) ? sz : 20, 1, 100);

        var (whereSql, whereParams) = BuildProductWhere(keyword, brandId, typeId, disabled);
        var offset = (page - 1) * pageSize;

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        var total = await conn.ExecuteScalarAsync<int>(
            $"SELECT COUNT(1) FROM Products p WHERE {whereSql}", whereParams);

        var dp = new DynamicParameters(whereParams);
        dp.Add("offset", offset); dp.Add("pageSize", pageSize);

        var items = await conn.QueryAsync<ProductListRow>($@"
SELECT p.productid, p.productnum, p.title, p.price, p.fixprice,
       p.photo, p.ishot, p.isnew, p.isdisabled, p.isset, p.sort,
       b.title AS brandTitle, pt.title AS typeTitle
FROM Products p
JOIN Brands b ON b.brandid = p.brandid
JOIN Producttypes pt ON pt.producttypeid = p.producttypeid
WHERE {whereSql}
ORDER BY p.sort ASC, p.createdate DESC
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY", dp);

        var list = items.Select(r => (object)new
        {
            productId = r.productid,
            productNum = r.productnum,
            r.title,
            r.price,
            fixPrice = r.fixprice,
            r.photo,
            isHot = r.ishot,
            isNew = r.isnew,
            r.isdisabled,
            r.sort,
            brandName = r.brandTitle,
            typeName = r.typeTitle
        }).ToList();

        return ctx.OkPaged(PaginatedResponse<object>.Create(list, total, page, pageSize));
    }

    // GET /admin/products/{id}
    public async Task<IActionResult> Detail(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "ProductMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var productId))
            return ctx.BadRequest("無效的商品 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        using var multi = await conn.QueryMultipleAsync(@"
SELECT p.*, b.title AS brandTitle, pt.title AS typeTitle
FROM Products p
JOIN Brands b ON b.brandid=p.brandid
JOIN Producttypes pt ON pt.producttypeid=p.producttypeid
WHERE p.productid = @productId;

SELECT t.tagid, t.title FROM Tags t
JOIN ProductTags pt2 ON pt2.tagid=t.tagid
WHERE pt2.productid = @productId;

SELECT pp.photoid, pp.photo, pp.sort FROM Productphotos pp
WHERE pp.productid = @productId ORDER BY pp.sort;",
            new { productId });

        var product = await multi.ReadSingleOrDefaultAsync<dynamic>();
        if (product is null) return ctx.NotFound("找不到商品");

        var tags = (await multi.ReadAsync<dynamic>()).ToList();
        var photos = (await multi.ReadAsync<dynamic>()).ToList();

        return ctx.Ok(new { product, tags, photos });
    }

    // POST /admin/products
    public async Task<IActionResult> Create(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "ProductMs", AdminOperation.Add);
        if (guard.Result is not null) return guard.Result;

        var body = await ctx.TryReadBodyAsync<UpsertProductRequest>();
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");
        var validation = ValidateProduct(body);
        if (validation is not null) return ctx.BadRequest(validation);

        var newId = Guid.NewGuid();
        var now = DateTime.UtcNow.AddHours(8);

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        await conn.ExecuteAsync(@"
INSERT INTO Products (productid, producttypeid, brandid, productnum, title, entitle,
    intro, memo, fixprice, price, capacity, photo, added, ishot, isnew, isdisabled,
    keyword, description, unit, conversion, weight, isset, isgroupbuy, sort, createdate, shortener)
VALUES (@productid, @producttypeid, @brandid, @productnum, @title, @entitle,
    @intro, @memo, @fixprice, @price, @capacity, @photo, 0, @ishot, @isnew, 0,
    @keyword, @description, @unit, @conversion, @weight, @isset, @isgroupbuy, @sort, @createdate, @shortener)",
            MapProductParams(newId, body, now));

        return ctx.Created(new { productId = newId });
    }

    // PUT /admin/products/{id}
    public async Task<IActionResult> Update(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "ProductMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var productId))
            return ctx.BadRequest("無效的商品 ID。");

        var body = await ctx.TryReadBodyAsync<UpsertProductRequest>();
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");
        var validation = ValidateProduct(body);
        if (validation is not null) return ctx.BadRequest(validation);

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        var rows = await conn.ExecuteAsync(@"
UPDATE Products SET
    producttypeid=@producttypeid, brandid=@brandid, productnum=@productnum,
    title=@title, entitle=@entitle, intro=@intro, memo=@memo,
    fixprice=@fixprice, price=@price, capacity=@capacity, photo=@photo,
    ishot=@ishot, isnew=@isnew, keyword=@keyword, description=@description,
    unit=@unit, conversion=@conversion, weight=@weight,
    isset=@isset, isgroupbuy=@isgroupbuy, sort=@sort, shortener=@shortener
WHERE productid=@productid AND isdisabled=0",
            MapProductParams(productId, body, DateTime.UtcNow.AddHours(8)));

        if (rows == 0) return ctx.NotFound("找不到商品或商品已下架。");
        return ctx.Ok(new { message = "商品已更新" });
    }

    // DELETE /admin/products/{id} — 軟刪除
    public async Task<IActionResult> Delete(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "ProductMs", AdminOperation.Delete);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var productId))
            return ctx.BadRequest("無效的商品 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        var rows = await conn.ExecuteAsync(
            "UPDATE Products SET isdisabled=1 WHERE productid=@productId AND isdisabled=0",
            new { productId });

        if (rows == 0) return ctx.NotFound("找不到商品或商品已下架。");
        return ctx.Ok(new { message = "商品已下架" });
    }

    // GET /admin/brands
    public async Task<IActionResult> Brands(RouteContext ctx)
    {
        var guard = AdminGuard.RequireAdmin(ctx);
        if (guard.Result is not null) return guard.Result;

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var brands = await conn.QueryAsync(
            "SELECT brandid, title, subtitle, sort, isdisplay FROM Brands ORDER BY sort, title");
        return ctx.Ok(brands);
    }

    // POST /admin/brands
    public async Task<IActionResult> CreateBrand(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "ProductMs", AdminOperation.Add);
        if (guard.Result is not null) return guard.Result;

        var body = await ctx.TryReadBodyAsync<UpsertBrandRequest>();
        if (body is null || string.IsNullOrWhiteSpace(body.Title)) return ctx.BadRequest("缺少 title。");

        var id = Guid.NewGuid();
        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        await conn.ExecuteAsync(@"
INSERT INTO Brands (brandid, title, subtitle, sort, isdisplay,
    logo, banner, ilogo, slogan, intro, keyword, description, patternentitle, patternchtitle,
    patternmemo, patternclass, storybgclass, storyentitle, storychtitle, storymemo,
    peopletitle, peopleslogan, peoplememo, peoplephoto, parttnervideo)
VALUES (@id, @title, @subtitle, @sort, @isdisplay,
    '', '', '', '', '', '', '', '', '', '', '', '', '', '', '', '', '', '', '', '')",
            new { id, body.Title, subtitle = body.Subtitle ?? string.Empty, sort = body.Sort, isdisplay = body.IsDisplay ? 1 : 0 });

        return ctx.Created(new { brandId = id });
    }

    // PUT /admin/brands/{id}
    public async Task<IActionResult> UpdateBrand(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "ProductMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var id)) return ctx.BadRequest("無效的 ID。");
        var body = await ctx.TryReadBodyAsync<UpsertBrandRequest>();
        if (body is null || string.IsNullOrWhiteSpace(body.Title)) return ctx.BadRequest("缺少 title。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.ExecuteAsync(
            "UPDATE Brands SET title=@title, subtitle=@subtitle, sort=@sort, isdisplay=@isdisplay WHERE brandid=@id",
            new { id, body.Title, subtitle = body.Subtitle ?? string.Empty, sort = body.Sort, isdisplay = body.IsDisplay ? 1 : 0 });

        if (rows == 0) return ctx.NotFound("找不到品牌。");
        return ctx.Ok(new { message = "品牌已更新" });
    }

    // DELETE /admin/brands/{id}
    public async Task<IActionResult> DeleteBrand(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "ProductMs", AdminOperation.Delete);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var id)) return ctx.BadRequest("無效的 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var inUse = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Products WHERE brandid=@id AND isdisabled=0", new { id });
        if (inUse > 0) return ctx.UnprocessableEntity("此品牌仍有上架商品，無法刪除。");

        var rows = await conn.ExecuteAsync("DELETE FROM Brands WHERE brandid=@id", new { id });
        if (rows == 0) return ctx.NotFound("找不到品牌。");
        return ctx.Ok(new { message = "品牌已刪除" });
    }

    // GET /admin/producttypes
    public async Task<IActionResult> ProductTypes(RouteContext ctx)
    {
        var guard = AdminGuard.RequireAdmin(ctx);
        if (guard.Result is not null) return guard.Result;

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var types = await conn.QueryAsync(
            "SELECT producttypeid, title, sort, isenable FROM Producttypes ORDER BY sort");
        return ctx.Ok(types);
    }

    // POST /admin/producttypes
    public async Task<IActionResult> CreateProductType(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "ProductMs", AdminOperation.Add);
        if (guard.Result is not null) return guard.Result;

        var body = await ctx.TryReadBodyAsync<UpsertProductTypeRequest>();
        if (body is null || string.IsNullOrWhiteSpace(body.Title)) return ctx.BadRequest("缺少 title。");

        var id = Guid.NewGuid();
        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        await conn.ExecuteAsync(@"
INSERT INTO Producttypes (producttypeid, title, sort, isenable, keyword, description, memo)
VALUES (@id, @title, @sort, @isenable, '', '', '')",
            new { id, body.Title, sort = body.Sort, isenable = body.IsEnable });

        return ctx.Created(new { producttypeId = id });
    }

    // PUT /admin/producttypes/{id}
    public async Task<IActionResult> UpdateProductType(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "ProductMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var id)) return ctx.BadRequest("無效的 ID。");
        var body = await ctx.TryReadBodyAsync<UpsertProductTypeRequest>();
        if (body is null || string.IsNullOrWhiteSpace(body.Title)) return ctx.BadRequest("缺少 title。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.ExecuteAsync(
            "UPDATE Producttypes SET title=@title, sort=@sort, isenable=@isenable WHERE producttypeid=@id",
            new { id, body.Title, sort = body.Sort, isenable = body.IsEnable });

        if (rows == 0) return ctx.NotFound("找不到商品分類。");
        return ctx.Ok(new { message = "商品分類已更新" });
    }

    // DELETE /admin/producttypes/{id}
    public async Task<IActionResult> DeleteProductType(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "ProductMs", AdminOperation.Delete);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var id)) return ctx.BadRequest("無效的 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var inUse = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Products WHERE producttypeid=@id AND isdisabled=0", new { id });
        if (inUse > 0) return ctx.UnprocessableEntity("此分類仍有上架商品，無法刪除。");

        var rows = await conn.ExecuteAsync("DELETE FROM Producttypes WHERE producttypeid=@id", new { id });
        if (rows == 0) return ctx.NotFound("找不到商品分類。");
        return ctx.Ok(new { message = "商品分類已刪除" });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static (string Sql, object Params) BuildProductWhere(
        string keyword, Guid? brandId, Guid? typeId, bool? disabled)
    {
        var clauses = new List<string>();
        var p = new Dictionary<string, object?>();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            clauses.Add("(p.title LIKE @kw OR p.productnum LIKE @kw)");
            p["kw"] = $"%{keyword}%";
        }
        if (brandId.HasValue) { clauses.Add("p.brandid = @brandId"); p["brandId"] = brandId.Value; }
        if (typeId.HasValue) { clauses.Add("p.producttypeid = @typeId"); p["typeId"] = typeId.Value; }
        if (disabled.HasValue) { clauses.Add("p.isdisabled = @disabled"); p["disabled"] = disabled.Value; }

        return (clauses.Count > 0 ? string.Join(" AND ", clauses) : "1=1", p);
    }

    private static string? ValidateProduct(UpsertProductRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Title)) return "缺少 title 欄位。";
        if (r.Price <= 0) return "price 必須大於 0。";
        if (r.BrandId == Guid.Empty) return "缺少 brandId 欄位。";
        if (r.ProductTypeId == Guid.Empty) return "缺少 productTypeId 欄位。";
        return null;
    }

    private static object MapProductParams(Guid id, UpsertProductRequest r, DateTime now) => new
    {
        productid = id,
        producttypeid = r.ProductTypeId,
        brandid = r.BrandId,
        productnum = r.ProductNum,
        title = r.Title,
        entitle = r.EnTitle,
        intro = r.Intro,
        memo = r.Memo ?? string.Empty,
        fixprice = r.FixPrice,
        price = r.Price,
        capacity = r.Capacity,
        photo = r.Photo ?? string.Empty,
        ishot = r.IsHot,
        isnew = r.IsNew,
        keyword = r.Keyword,
        description = r.Description,
        unit = r.Unit,
        conversion = r.Conversion,
        weight = r.Weight,
        isset = r.IsSet,
        isgroupbuy = r.IsGroupBuy,
        sort = r.Sort,
        shortener = r.Shortener,
        createdate = now,
    };

    // ── Row / DTO types ───────────────────────────────────────────────────────────

    private sealed record ProductListRow(
        Guid productid, string? productnum, string title, int price, int? fixprice,
        string photo, bool ishot, bool isnew, bool isdisabled, bool isset, int sort,
        string brandTitle, string typeTitle);

    private sealed record UpsertProductRequest(
        Guid ProductTypeId, Guid BrandId,
        string? ProductNum, string Title, string? EnTitle,
        string? Intro, string? Memo,
        int? FixPrice, int Price,
        string? Capacity, string? Photo,
        bool IsHot, bool IsNew,
        string? Keyword, string? Description,
        string? Unit, int? Conversion, decimal? Weight,
        bool IsSet, bool IsGroupBuy,
        int Sort, string? Shortener);

    // ── Tags ─────────────────────────────────────────────────────────────────────

    // GET /admin/tags
    public async Task<IActionResult> ListTags(RouteContext ctx)
    {
        var guard = AdminGuard.RequireAdmin(ctx);
        if (guard.Result is not null) return guard.Result;

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.QueryAsync("SELECT tagid, title FROM Tags ORDER BY title");
        return ctx.Ok(rows);
    }

    // POST /admin/tags
    public async Task<IActionResult> CreateTag(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "ProductMs", AdminOperation.Add);
        if (guard.Result is not null) return guard.Result;
        var body = await ctx.TryReadBodyAsync<UpsertTagRequest>();
        if (body is null || string.IsNullOrWhiteSpace(body.Title)) return ctx.BadRequest("缺少 title。");

        var id = Guid.NewGuid();
        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        await conn.ExecuteAsync("INSERT INTO Tags (tagid, title) VALUES (@id, @title)",
            new { id, title = body.Title.Trim() });
        return ctx.Created(new { tagId = id });
    }

    // PUT /admin/tags/{id}
    public async Task<IActionResult> UpdateTag(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "ProductMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var id)) return ctx.BadRequest("無效的 ID。");
        var body = await ctx.TryReadBodyAsync<UpsertTagRequest>();
        if (body is null || string.IsNullOrWhiteSpace(body.Title)) return ctx.BadRequest("缺少 title。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.ExecuteAsync("UPDATE Tags SET title=@title WHERE tagid=@id",
            new { title = body.Title.Trim(), id });
        if (rows == 0) return ctx.NotFound("找不到標籤。");
        return ctx.Ok(new { message = "標籤已更新" });
    }

    // DELETE /admin/tags/{id}
    public async Task<IActionResult> DeleteTag(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "ProductMs", AdminOperation.Delete);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var id)) return ctx.BadRequest("無效的 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var usedCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Producttags WHERE tagid=@id", new { id });
        if (usedCount > 0) return ctx.UnprocessableEntity($"此標籤已被 {usedCount} 件商品使用，無法刪除。");

        var rows = await conn.ExecuteAsync("DELETE FROM Tags WHERE tagid=@id", new { id });
        if (rows == 0) return ctx.NotFound("找不到標籤。");
        return ctx.Ok(new { message = "標籤已刪除" });
    }

    private sealed record UpsertBrandRequest(
        string Title, string? Subtitle, int Sort, bool IsDisplay);

    private sealed record UpsertProductTypeRequest(
        string Title, int Sort, bool IsEnable);

    private sealed record UpsertTagRequest(string Title);
}
