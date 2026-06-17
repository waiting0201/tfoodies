using System.Data;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using TFoodies.Api.Functions.Helpers;
using TFoodies.Api.Functions.Router;
using TFoodies.Application.Abstractions;
using TFoodies.Contracts.Common;

namespace TFoodies.Api.Functions.Controllers.Admin;

/// <summary>
/// 後台商品型錄管理（ProductMs）。對應舊系統 ProductMsController，涵蓋 Products / Brands /
/// Producttypes / Tags 四個子模組與其圖庫（Productphotos / Brandphotos）。
///
/// 商品：
///   GET    /admin/products                    — 列表（篩選/分頁）
///   GET    /admin/products/check-num          — productnum 唯一性檢查
///   GET    /admin/products/check-name         — title 唯一性檢查
///   PUT    /admin/products/sort               — 排序
///   GET    /admin/products/{id}               — 明細（含 tags / photos）
///   POST   /admin/products                    — 新增（含標籤 M:N、套裝 Setproducts）
///   PUT    /admin/products/{id}               — 更新（標籤/套裝差異更新）
///   DELETE /admin/products/{id}               — 軟刪除（isdisabled=true）+ Blob 清理
///   GET/POST/PUT/DELETE /admin/products/{id}/photos[/{photoId}] — 商品圖庫
///   PUT    /admin/products/{id}/photos/sort   — 圖庫排序
/// 品牌：
///   GET    /admin/brands                      — 列表（select / 清單用）
///   GET    /admin/brands/{id}                 — 明細（全欄位，供表單載入）
///   POST/PUT/DELETE /admin/brands[/{id}]      — CRUD（全欄位）
///   PUT    /admin/brands/sort                 — 排序
///   GET/POST/PUT/DELETE /admin/brands/{id}/photos[/{photoId}] — 品牌圖庫
/// 品類：
///   GET    /admin/producttypes                — 列表
///   GET    /admin/producttypes/check-name     — title 唯一性檢查
///   PUT    /admin/producttypes/sort           — 排序
///   POST/PUT/DELETE /admin/producttypes[/{id}] — CRUD
/// 標籤：
///   GET/POST/PUT/DELETE /admin/tags[/{id}]    — CRUD
/// </summary>
public sealed class ProductAdminController
{
    private readonly IAdminPermissionService _perms;
    private readonly IDbConnectionFactory _db;
    private readonly IBlobService _blob;

    public ProductAdminController(IAdminPermissionService perms, IDbConnectionFactory db, IBlobService blob)
    {
        _perms = perms;
        _db = db;
        _blob = blob;
    }

    // ══════════════════════════════════════════════════════════════════
    // PRODUCTS
    // ══════════════════════════════════════════════════════════════════

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
       p.photo, p.ishot, p.isnew, p.isdisabled, p.isset, p.isgroupbuy, p.sort,
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
            isGroupBuy = r.isgroupbuy,
            r.isset,
            r.sort,
            brandName = r.brandTitle,
            typeName = r.typeTitle
        }).ToList();

        return ctx.OkPaged(PaginatedResponse<object>.Create(list, total, page, pageSize));
    }

    // GET /admin/products/check-num?value=&excludeId=
    public Task<IActionResult> CheckProductNum(RouteContext ctx)
        => CheckUniqueAsync(ctx, "Products", "productnum", "productid");

    // GET /admin/products/check-name?value=&excludeId=
    public Task<IActionResult> CheckProductName(RouteContext ctx)
        => CheckUniqueAsync(ctx, "Products", "title", "productid");

    // PUT /admin/products/sort   body: [{ id, sort }]
    public Task<IActionResult> SortProducts(RouteContext ctx)
        => SortAsync(ctx, "Products", "productid");

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
JOIN Producttags pt2 ON pt2.tagid=t.tagid
WHERE pt2.productid = @productId;

SELECT pp.productphotoid, pp.photo, pp.sort FROM Productphotos pp
WHERE pp.productid = @productId ORDER BY pp.sort;

SELECT sp.setproductid, sp.productid, sp.qty,
       cp.productnum, cp.title, cp.capacity
FROM Setproducts sp
JOIN Products cp ON cp.productid = sp.productid
WHERE sp.oproductid = @productId;",
            new { productId });

        var product = await multi.ReadSingleOrDefaultAsync<dynamic>();
        if (product is null) return ctx.NotFound("找不到商品");

        var tags = (await multi.ReadAsync<dynamic>()).ToList();
        var photos = (await multi.ReadAsync<dynamic>()).ToList();
        var setProducts = (await multi.ReadAsync<dynamic>()).ToList();

        return ctx.Ok(new { product, tags, photos, setProducts });
    }

    // POST /admin/products
    public async Task<IActionResult> Create(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "ProductMs", AdminOperation.Add);
        if (guard.Result is not null) return guard.Result;

        var body = await ctx.TryReadBodyAsync<UpsertProductRequest>();
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");
        var validation = ValidateProduct(body, isCreate: true);
        if (validation is not null) return ctx.BadRequest(validation);

        var newId = Guid.NewGuid();
        var now = DateTime.UtcNow.AddHours(8);
        var ct = ctx.Request.HttpContext.RequestAborted;

        using var conn = await _db.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        await conn.ExecuteAsync(@"
INSERT INTO Products (productid, producttypeid, brandid, productnum, title, entitle,
    intro, memo, fixprice, price, capacity, photo, added, ishot, isnew, isdisabled,
    keyword, description, unit, conversion, weight, isset, isgroupbuy, sort, createdate, shortener)
VALUES (@productid, @producttypeid, @brandid, @productnum, @title, @entitle,
    @intro, @memo, @fixprice, @price, @capacity, @photo, @added, @ishot, @isnew, 0,
    @keyword, @description, @unit, @conversion, @weight, @isset, @isgroupbuy, @sort, @createdate, @shortener)",
            MapProductParams(newId, body, now), tx);

        await ReplaceTagsAsync(conn, tx, newId, body.TagIds);
        await ReplaceSetProductsAsync(conn, tx, newId, body.IsSet, body.SetProducts);

        tx.Commit();
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
        var validation = ValidateProduct(body, isCreate: false);
        if (validation is not null) return ctx.BadRequest(validation);

        var ct = ctx.Request.HttpContext.RequestAborted;
        using var conn = await _db.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        // photo 為空字串時保留原圖（編輯未重新上傳代表圖的情境）
        var rows = await conn.ExecuteAsync(@"
UPDATE Products SET
    producttypeid=@producttypeid, brandid=@brandid, productnum=@productnum,
    title=@title, entitle=@entitle, intro=@intro, memo=@memo,
    fixprice=@fixprice, price=@price, capacity=@capacity,
    photo = CASE WHEN @photo = '' THEN photo ELSE @photo END,
    added=@added, ishot=@ishot, isnew=@isnew, isdisabled=@isdisabled, keyword=@keyword, description=@description,
    unit=@unit, conversion=@conversion, weight=@weight,
    isset=@isset, isgroupbuy=@isgroupbuy, sort=@sort, shortener=@shortener
WHERE productid=@productid",
            MapProductParams(productId, body, DateTime.UtcNow.AddHours(8)), tx);

        if (rows == 0) { tx.Rollback(); return ctx.NotFound("找不到商品。"); }

        await ReplaceTagsAsync(conn, tx, productId, body.TagIds);
        await ReplaceSetProductsAsync(conn, tx, productId, body.IsSet, body.SetProducts);

        tx.Commit();
        return ctx.Ok(new { message = "商品已更新" });
    }

    // DELETE /admin/products/{id} — 軟刪除 + Blob 清理
    public async Task<IActionResult> Delete(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "ProductMs", AdminOperation.Delete);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var productId))
            return ctx.BadRequest("無效的商品 ID。");

        var ct = ctx.Request.HttpContext.RequestAborted;
        using var conn = await _db.CreateOpenConnectionAsync(ct);

        // 收集待清理的 blob 檔名（代表圖 + 圖庫 + media 檔）
        var blobs = (await conn.QueryAsync<string>(@"
SELECT photo FROM Products WHERE productid=@productId AND isdisabled=0 AND photo <> ''
UNION ALL SELECT photo FROM Productphotos WHERE productid=@productId AND photo <> ''
UNION ALL SELECT filename FROM Productmedias WHERE productid=@productId AND mediatype=0 AND filename IS NOT NULL AND filename <> ''",
            new { productId })).ToList();

        var rows = await conn.ExecuteAsync(
            "UPDATE Products SET isdisabled=1 WHERE productid=@productId AND isdisabled=0",
            new { productId });

        if (rows == 0) return ctx.NotFound("找不到商品或商品已下架。");

        foreach (var name in blobs)
            await _blob.DeleteAsync(name, ct);

        return ctx.Ok(new { message = "商品已下架" });
    }

    // ── 商品圖庫 Productphotos ───────────────────────────────────────────

    // GET /admin/products/{id}/photos
    public async Task<IActionResult> ProductPhotos(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "ProductMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var productId)) return ctx.BadRequest("無效的商品 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.QueryAsync(
            "SELECT productphotoid AS productphotoId, photo, sort FROM Productphotos WHERE productid=@productId ORDER BY sort",
            new { productId });
        return ctx.Ok(rows);
    }

    // POST /admin/products/{id}/photos
    public async Task<IActionResult> CreateProductPhoto(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "ProductMs", AdminOperation.Add);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var productId)) return ctx.BadRequest("無效的商品 ID。");

        var body = await ctx.TryReadBodyAsync<UpsertPhotoRequest>();
        if (body is null || string.IsNullOrWhiteSpace(body.Photo)) return ctx.BadRequest("缺少圖片檔名。");

        var id = Guid.NewGuid();
        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        await conn.ExecuteAsync(
            "INSERT INTO Productphotos (productphotoid, productid, photo, sort) VALUES (@id, @productId, @photo, @sort)",
            new { id, productId, photo = body.Photo, sort = body.Sort });
        return ctx.Created(new { productphotoId = id });
    }

    // PUT /admin/products/{id}/photos/{photoId}
    public async Task<IActionResult> UpdateProductPhoto(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "ProductMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var productId)) return ctx.BadRequest("無效的商品 ID。");
        if (!Guid.TryParse(ctx.RequirePathParam("photoId"), out var photoId)) return ctx.BadRequest("無效的圖片 ID。");

        var body = await ctx.TryReadBodyAsync<UpsertPhotoRequest>();
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.ExecuteAsync(@"
UPDATE Productphotos SET photo = CASE WHEN @photo = '' THEN photo ELSE @photo END, sort=@sort
WHERE productphotoid=@photoId AND productid=@productId",
            new { photoId, productId, photo = body.Photo ?? string.Empty, sort = body.Sort });
        if (rows == 0) return ctx.NotFound("找不到商品圖片。");
        return ctx.Ok(new { message = "商品圖片已更新" });
    }

    // DELETE /admin/products/{id}/photos/{photoId}
    public async Task<IActionResult> DeleteProductPhoto(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "ProductMs", AdminOperation.Delete);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var productId)) return ctx.BadRequest("無效的商品 ID。");
        if (!Guid.TryParse(ctx.RequirePathParam("photoId"), out var photoId)) return ctx.BadRequest("無效的圖片 ID。");

        var ct = ctx.Request.HttpContext.RequestAborted;
        using var conn = await _db.CreateOpenConnectionAsync(ct);
        var photo = await conn.ExecuteScalarAsync<string?>(
            "SELECT photo FROM Productphotos WHERE productphotoid=@photoId AND productid=@productId",
            new { photoId, productId });
        var rows = await conn.ExecuteAsync(
            "DELETE FROM Productphotos WHERE productphotoid=@photoId AND productid=@productId",
            new { photoId, productId });
        if (rows == 0) return ctx.NotFound("找不到商品圖片。");
        if (!string.IsNullOrWhiteSpace(photo)) await _blob.DeleteAsync(photo!, ct);
        return ctx.Ok(new { message = "商品圖片已刪除" });
    }

    // PUT /admin/products/{id}/photos/sort   body: [{ id, sort }]
    public Task<IActionResult> SortProductPhotos(RouteContext ctx)
        => SortAsync(ctx, "Productphotos", "productphotoid");

    // ══════════════════════════════════════════════════════════════════
    // BRANDS
    // ══════════════════════════════════════════════════════════════════

    // GET /admin/brands — 清單 / select 用（精簡欄位）
    public async Task<IActionResult> Brands(RouteContext ctx)
    {
        var guard = AdminGuard.RequireAdmin(ctx);
        if (guard.Result is not null) return guard.Result;

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var brands = await conn.QueryAsync(
            "SELECT brandid, title, subtitle, logo, sort, isdisplay FROM Brands ORDER BY sort, title");
        return ctx.Ok(brands);
    }

    // GET /admin/brands/{id} — 全欄位（供表單載入）
    public async Task<IActionResult> BrandDetail(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "ProductMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var id)) return ctx.BadRequest("無效的品牌 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var brand = await conn.QuerySingleOrDefaultAsync(
            "SELECT * FROM Brands WHERE brandid=@id", new { id });
        if (brand is null) return ctx.NotFound("找不到品牌。");
        return ctx.Ok(brand);
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
INSERT INTO Brands (brandid, title, subtitle, logo, banner,
    patternentitle, patternchtitle, parttnervideo, patternmemo, patternclass,
    ilogo, slogan, intro, storybgclass, storyentitle, storychtitle, storymemo,
    peopletitle, peopleslogan, peoplememo, peoplephoto, keyword, description, sort, isdisplay)
VALUES (@brandid, @title, @subtitle, @logo, @banner,
    @patternentitle, @patternchtitle, @parttnervideo, @patternmemo, @patternclass,
    @ilogo, @slogan, @intro, @storybgclass, @storyentitle, @storychtitle, @storymemo,
    @peopletitle, @peopleslogan, @peoplememo, @peoplephoto, @keyword, @description, @sort, @isdisplay)",
            MapBrandParams(id, body));

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

        // 圖檔欄位：空字串時保留原值（未重新上傳）
        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.ExecuteAsync(@"
UPDATE Brands SET
    title=@title, subtitle=@subtitle,
    logo        = CASE WHEN @logo = '' THEN logo ELSE @logo END,
    banner      = CASE WHEN @banner = '' THEN banner ELSE @banner END,
    patternentitle=@patternentitle, patternchtitle=@patternchtitle, parttnervideo=@parttnervideo,
    patternmemo=@patternmemo,
    patternclass= CASE WHEN @patternclass = '' THEN patternclass ELSE @patternclass END,
    ilogo       = CASE WHEN @ilogo = '' THEN ilogo ELSE @ilogo END,
    slogan=@slogan, intro=@intro,
    storybgclass= CASE WHEN @storybgclass = '' THEN storybgclass ELSE @storybgclass END,
    storyentitle=@storyentitle, storychtitle=@storychtitle, storymemo=@storymemo,
    peopletitle=@peopletitle, peopleslogan=@peopleslogan, peoplememo=@peoplememo,
    peoplephoto = CASE WHEN @peoplephoto = '' THEN peoplephoto ELSE @peoplephoto END,
    keyword=@keyword, description=@description, sort=@sort, isdisplay=@isdisplay
WHERE brandid=@brandid",
            MapBrandParams(id, body));

        if (rows == 0) return ctx.NotFound("找不到品牌。");
        return ctx.Ok(new { message = "品牌已更新" });
    }

    // DELETE /admin/brands/{id}
    public async Task<IActionResult> DeleteBrand(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "ProductMs", AdminOperation.Delete);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var id)) return ctx.BadRequest("無效的 ID。");

        var ct = ctx.Request.HttpContext.RequestAborted;
        using var conn = await _db.CreateOpenConnectionAsync(ct);
        var inUse = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Products WHERE brandid=@id AND isdisabled=0", new { id });
        if (inUse > 0) return ctx.UnprocessableEntity("此品牌仍有上架商品，無法刪除。");

        // 收集 blob（品牌圖檔 + 圖庫）
        var blobs = (await conn.QueryAsync<string>(@"
SELECT v FROM (
    SELECT logo AS v FROM Brands WHERE brandid=@id
    UNION ALL SELECT banner FROM Brands WHERE brandid=@id
    UNION ALL SELECT ilogo FROM Brands WHERE brandid=@id
    UNION ALL SELECT patternclass FROM Brands WHERE brandid=@id
    UNION ALL SELECT storybgclass FROM Brands WHERE brandid=@id
    UNION ALL SELECT peoplephoto FROM Brands WHERE brandid=@id
    UNION ALL SELECT photo FROM Brandphotos WHERE brandid=@id
) t WHERE v IS NOT NULL AND v <> ''", new { id })).ToList();

        using (var tx = conn.BeginTransaction())
        {
            await conn.ExecuteAsync("DELETE FROM Brandphotos WHERE brandid=@id", new { id }, tx);
            var rows = await conn.ExecuteAsync("DELETE FROM Brands WHERE brandid=@id", new { id }, tx);
            if (rows == 0) { tx.Rollback(); return ctx.NotFound("找不到品牌。"); }
            tx.Commit();
        }

        foreach (var name in blobs)
            await _blob.DeleteAsync(name, ct);

        return ctx.Ok(new { message = "品牌已刪除" });
    }

    // PUT /admin/brands/sort   body: [{ id, sort }]
    public Task<IActionResult> SortBrands(RouteContext ctx)
        => SortAsync(ctx, "Brands", "brandid");

    // ── 品牌圖庫 Brandphotos ─────────────────────────────────────────────

    // GET /admin/brands/{id}/photos
    public async Task<IActionResult> BrandPhotos(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "ProductMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var brandId)) return ctx.BadRequest("無效的品牌 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.QueryAsync(
            "SELECT brandphotoid AS brandphotoId, photo, sort FROM Brandphotos WHERE brandid=@brandId ORDER BY sort",
            new { brandId });
        return ctx.Ok(rows);
    }

    // POST /admin/brands/{id}/photos
    public async Task<IActionResult> CreateBrandPhoto(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "ProductMs", AdminOperation.Add);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var brandId)) return ctx.BadRequest("無效的品牌 ID。");

        var body = await ctx.TryReadBodyAsync<UpsertPhotoRequest>();
        if (body is null || string.IsNullOrWhiteSpace(body.Photo)) return ctx.BadRequest("缺少圖片檔名。");

        var id = Guid.NewGuid();
        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        await conn.ExecuteAsync(
            "INSERT INTO Brandphotos (brandphotoid, brandid, photo, sort) VALUES (@id, @brandId, @photo, @sort)",
            new { id, brandId, photo = body.Photo, sort = body.Sort });
        return ctx.Created(new { brandphotoId = id });
    }

    // PUT /admin/brands/{id}/photos/{photoId}
    public async Task<IActionResult> UpdateBrandPhoto(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "ProductMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var brandId)) return ctx.BadRequest("無效的品牌 ID。");
        if (!Guid.TryParse(ctx.RequirePathParam("photoId"), out var photoId)) return ctx.BadRequest("無效的圖片 ID。");

        var body = await ctx.TryReadBodyAsync<UpsertPhotoRequest>();
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.ExecuteAsync(@"
UPDATE Brandphotos SET photo = CASE WHEN @photo = '' THEN photo ELSE @photo END, sort=@sort
WHERE brandphotoid=@photoId AND brandid=@brandId",
            new { photoId, brandId, photo = body.Photo ?? string.Empty, sort = body.Sort });
        if (rows == 0) return ctx.NotFound("找不到品牌圖片。");
        return ctx.Ok(new { message = "品牌圖片已更新" });
    }

    // DELETE /admin/brands/{id}/photos/{photoId}
    public async Task<IActionResult> DeleteBrandPhoto(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "ProductMs", AdminOperation.Delete);
        if (guard.Result is not null) return guard.Result;
        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var brandId)) return ctx.BadRequest("無效的品牌 ID。");
        if (!Guid.TryParse(ctx.RequirePathParam("photoId"), out var photoId)) return ctx.BadRequest("無效的圖片 ID。");

        var ct = ctx.Request.HttpContext.RequestAborted;
        using var conn = await _db.CreateOpenConnectionAsync(ct);
        var photo = await conn.ExecuteScalarAsync<string?>(
            "SELECT photo FROM Brandphotos WHERE brandphotoid=@photoId AND brandid=@brandId",
            new { photoId, brandId });
        var rows = await conn.ExecuteAsync(
            "DELETE FROM Brandphotos WHERE brandphotoid=@photoId AND brandid=@brandId",
            new { photoId, brandId });
        if (rows == 0) return ctx.NotFound("找不到品牌圖片。");
        if (!string.IsNullOrWhiteSpace(photo)) await _blob.DeleteAsync(photo!, ct);
        return ctx.Ok(new { message = "品牌圖片已刪除" });
    }

    // PUT /admin/brands/{id}/photos/sort   body: [{ id, sort }]
    public Task<IActionResult> SortBrandPhotos(RouteContext ctx)
        => SortAsync(ctx, "Brandphotos", "brandphotoid");

    // ══════════════════════════════════════════════════════════════════
    // PRODUCTTYPES
    // ══════════════════════════════════════════════════════════════════

    // GET /admin/producttypes
    public async Task<IActionResult> ProductTypes(RouteContext ctx)
    {
        var guard = AdminGuard.RequireAdmin(ctx);
        if (guard.Result is not null) return guard.Result;

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var types = await conn.QueryAsync(
            "SELECT producttypeid, title, memo, keyword, description, sort, isenable FROM Producttypes ORDER BY sort");
        return ctx.Ok(types);
    }

    // GET /admin/producttypes/check-name?value=&excludeId=
    public Task<IActionResult> CheckProductTypeName(RouteContext ctx)
        => CheckUniqueAsync(ctx, "Producttypes", "title", "producttypeid");

    // PUT /admin/producttypes/sort   body: [{ id, sort }]
    public Task<IActionResult> SortProductTypes(RouteContext ctx)
        => SortAsync(ctx, "Producttypes", "producttypeid");

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
INSERT INTO Producttypes (producttypeid, title, memo, keyword, description, sort, isenable)
VALUES (@id, @title, @memo, @keyword, @description, @sort, @isenable)",
            new
            {
                id,
                title = body.Title.Trim(),
                memo = body.Memo ?? string.Empty,
                keyword = body.Keyword ?? string.Empty,
                description = body.Description ?? string.Empty,
                sort = body.Sort,
                isenable = body.IsEnable
            });

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
        var rows = await conn.ExecuteAsync(@"
UPDATE Producttypes SET title=@title, memo=@memo, keyword=@keyword, description=@description, sort=@sort, isenable=@isenable
WHERE producttypeid=@id",
            new
            {
                id,
                title = body.Title.Trim(),
                memo = body.Memo ?? string.Empty,
                keyword = body.Keyword ?? string.Empty,
                description = body.Description ?? string.Empty,
                sort = body.Sort,
                isenable = body.IsEnable
            });

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

    // ══════════════════════════════════════════════════════════════════
    // TAGS
    // ══════════════════════════════════════════════════════════════════

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

    // ══════════════════════════════════════════════════════════════════
    // 共用 Helpers
    // ══════════════════════════════════════════════════════════════════

    /// <summary>唯一性檢查：GET ?value=&excludeId= → { available: bool }。</summary>
    private async Task<IActionResult> CheckUniqueAsync(RouteContext ctx, string table, string column, string keyColumn)
    {
        var guard = AdminGuard.RequireAdmin(ctx);
        if (guard.Result is not null) return guard.Result;

        var value = ctx.Request.Query["value"].ToString().Trim();
        if (string.IsNullOrWhiteSpace(value)) return ctx.BadRequest("缺少 value 參數。");
        Guid? excludeId = Guid.TryParse(ctx.Request.Query["excludeId"], out var ex) ? ex : null;

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var count = await conn.ExecuteScalarAsync<int>(
            $"SELECT COUNT(1) FROM {table} WHERE {column}=@value AND (@excludeId IS NULL OR {keyColumn}<>@excludeId)",
            new { value, excludeId });
        return ctx.Ok(new { available = count == 0 });
    }

    /// <summary>排序：PUT body [{ id, sort }] → 逐筆更新 sort。</summary>
    private async Task<IActionResult> SortAsync(RouteContext ctx, string table, string keyColumn)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "ProductMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;

        var items = await ctx.TryReadBodyAsync<List<SortItem>>();
        if (items is null) return ctx.BadRequest("Request body 格式不正確。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        using var tx = conn.BeginTransaction();
        foreach (var item in items)
            await conn.ExecuteAsync(
                $"UPDATE {table} SET sort=@sort WHERE {keyColumn}=@id",
                new { sort = item.Sort, id = item.Id }, tx);
        tx.Commit();
        return ctx.Ok(new { message = "排序已更新" });
    }

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

    private static string? ValidateProduct(UpsertProductRequest r, bool isCreate)
    {
        if (string.IsNullOrWhiteSpace(r.Title)) return "缺少 title 欄位。";
        if (r.Price <= 0) return "price 必須大於 0。";
        if (r.BrandId == Guid.Empty) return "缺少 brandId 欄位。";
        if (r.ProductTypeId == Guid.Empty) return "缺少 productTypeId 欄位。";
        if (isCreate && string.IsNullOrWhiteSpace(r.Photo)) return "代表圖為必填。";
        if (r.IsSet && (r.SetProducts is null || r.SetProducts.Count == 0))
            return "組合商品必須設定至少一項組件。";
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
        added = r.Added,
        ishot = r.IsHot,
        isnew = r.IsNew,
        isdisabled = r.IsDisabled,
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

    /// <summary>全刪重建商品標籤關聯（Producttags 為純 join table，無額外欄位，churn 無害）。</summary>
    private static async Task ReplaceTagsAsync(IDbConnection conn, IDbTransaction tx, Guid productId, IReadOnlyList<Guid>? tagIds)
    {
        await conn.ExecuteAsync("DELETE FROM Producttags WHERE productid=@productId", new { productId }, tx);
        if (tagIds is null || tagIds.Count == 0) return;
        foreach (var tagId in tagIds.Distinct())
            await conn.ExecuteAsync(
                "INSERT INTO Producttags (productid, tagid) VALUES (@productId, @tagId)",
                new { productId, tagId }, tx);
    }

    /// <summary>重建套裝組件；isset=false 時清空。oproductid = 套裝主商品。</summary>
    private static async Task ReplaceSetProductsAsync(
        IDbConnection conn, IDbTransaction tx, Guid productId, bool isSet, IReadOnlyList<SetProductItem>? items)
    {
        await conn.ExecuteAsync("DELETE FROM Setproducts WHERE oproductid=@productId", new { productId }, tx);
        if (!isSet || items is null) return;
        foreach (var item in items.Where(i => i.ProductId != Guid.Empty && i.Qty > 0))
            await conn.ExecuteAsync(
                "INSERT INTO Setproducts (oproductid, productid, qty) VALUES (@oproductid, @productid, @qty)",
                new { oproductid = productId, productid = item.ProductId, qty = item.Qty }, tx);
    }

    private static object MapBrandParams(Guid id, UpsertBrandRequest b) => new
    {
        brandid = id,
        title = b.Title,
        subtitle = b.Subtitle ?? string.Empty,
        logo = b.Logo ?? string.Empty,
        banner = b.Banner ?? string.Empty,
        patternentitle = b.PatternEnTitle ?? string.Empty,
        patternchtitle = b.PatternChTitle ?? string.Empty,
        parttnervideo = b.PartnerVideo ?? string.Empty,
        patternmemo = b.PatternMemo ?? string.Empty,
        patternclass = b.PatternClass ?? string.Empty,
        ilogo = b.ILogo ?? string.Empty,
        slogan = b.Slogan ?? string.Empty,
        intro = b.Intro ?? string.Empty,
        storybgclass = b.StoryBgClass ?? string.Empty,
        storyentitle = b.StoryEnTitle ?? string.Empty,
        storychtitle = b.StoryChTitle ?? string.Empty,
        storymemo = b.StoryMemo ?? string.Empty,
        peopletitle = b.PeopleTitle ?? string.Empty,
        peopleslogan = b.PeopleSlogan ?? string.Empty,
        peoplememo = b.PeopleMemo ?? string.Empty,
        peoplephoto = b.PeoplePhoto ?? string.Empty,
        keyword = b.Keyword ?? string.Empty,
        description = b.Description ?? string.Empty,
        sort = b.Sort,
        isdisplay = b.IsDisplay ? 1 : 2,
    };

    // ── Row / DTO types ───────────────────────────────────────────────────────────

    private sealed record ProductListRow(
        Guid productid, string? productnum, string title, int price, int? fixprice,
        string photo, bool ishot, bool isnew, bool isdisabled, bool isset, bool isgroupbuy, int sort,
        string brandTitle, string typeTitle);

    private sealed record SortItem(Guid Id, int Sort);

    private sealed record UpsertPhotoRequest(string? Photo, int Sort);

    private sealed record SetProductItem(Guid ProductId, int Qty);

    private sealed record UpsertProductRequest(
        Guid ProductTypeId, Guid BrandId,
        string? ProductNum, string Title, string? EnTitle,
        string? Intro, string? Memo,
        int? FixPrice, int Price,
        string? Capacity, string? Photo, int Added,
        bool IsHot, bool IsNew, bool IsDisabled,
        string? Keyword, string? Description,
        string? Unit, int? Conversion, decimal? Weight,
        bool IsSet, bool IsGroupBuy,
        int Sort, string? Shortener,
        List<Guid>? TagIds, List<SetProductItem>? SetProducts);

    private sealed record UpsertBrandRequest(
        string Title, string? Subtitle, string? Logo, string? Banner,
        string? PatternEnTitle, string? PatternChTitle, string? PartnerVideo, string? PatternMemo, string? PatternClass,
        string? ILogo, string? Slogan, string? Intro,
        string? StoryBgClass, string? StoryEnTitle, string? StoryChTitle, string? StoryMemo,
        string? PeopleTitle, string? PeopleSlogan, string? PeopleMemo, string? PeoplePhoto,
        string? Keyword, string? Description,
        int Sort, bool IsDisplay);

    private sealed record UpsertProductTypeRequest(
        string Title, string? Memo, string? Keyword, string? Description, int Sort, bool IsEnable);

    private sealed record UpsertTagRequest(string Title);
}
