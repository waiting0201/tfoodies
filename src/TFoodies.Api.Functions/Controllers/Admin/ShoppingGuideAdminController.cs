using Dapper;
using Microsoft.AspNetCore.Mvc;
using TFoodies.Api.Functions.Helpers;
using TFoodies.Api.Functions.Router;
using TFoodies.Application.Abstractions;
using TFoodies.Contracts.Common;

namespace TFoodies.Api.Functions.Controllers.Admin;

/// <summary>
/// 後台「購物說明」管理（隸屬 SettingMs 系統管理模組，對應舊系統 SettingMs 的
/// Questiontypes/Questions，前台呈現為「購物說明 / 會員常見問題」FAQ）。
///
/// 購物說明分類（Questiontypes）：
///   GET    /admin/questiontypes            — 全部分類（依 sort，含問題數，亦供分類下拉）
///   POST   /admin/questiontypes            — 新增分類
///   GET    /admin/questiontypes/{id}       — 分類明細
///   PUT    /admin/questiontypes/{id}       — 更新分類
///   DELETE /admin/questiontypes/{id}       — 刪除分類（FK CASCADE 連帶刪底下所有購物說明）
///
/// 購物說明（Questions）：
///   GET    /admin/questions                — 列表（分頁，可依 questiontypeid 篩選）
///   POST   /admin/questions                — 新增購物說明
///   GET    /admin/questions/{id}           — 明細（answer 為 ntext，需 CAST）
///   PUT    /admin/questions/{id}           — 更新購物說明
///   DELETE /admin/questions/{id}           — 刪除購物說明（FK CASCADE 連帶刪 Questionmedias）
///
/// ⚠ 此兩張表皆無「啟用/停用」欄位（與舊系統一致），刪除為實體硬刪；
///   answer 富文本由前端 HtmlEditor 處理（同 CMS/FAQ 慣例，不另解析 base64 寫 Questionmedias）。
/// </summary>
public sealed class ShoppingGuideAdminController
{
    private readonly IAdminPermissionService _perms;
    private readonly IDbConnectionFactory _db;

    public ShoppingGuideAdminController(IAdminPermissionService perms, IDbConnectionFactory db)
    {
        _perms = perms;
        _db    = db;
    }

    // ══════════════════════════════════════════════════════════════════
    // 購物說明分類 Questiontypes
    // ══════════════════════════════════════════════════════════════════

    // GET /admin/questiontypes — 全部分類（依 sort 升冪；含底下購物說明數量；亦供表單下拉）
    public async Task<IActionResult> TypeList(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "SettingMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.QueryAsync(@"
SELECT qt.questiontypeid AS questiontypeId,
       qt.title,
       qt.sort,
       (SELECT COUNT(1) FROM Questions q WHERE q.questiontypeid = qt.questiontypeid) AS questionCount
FROM Questiontypes qt
ORDER BY qt.sort ASC, qt.title ASC");

        return ctx.Ok(rows);
    }

    // POST /admin/questiontypes
    public async Task<IActionResult> TypeCreate(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "SettingMs", AdminOperation.Add);
        if (guard.Result is not null) return guard.Result;

        var body = await ctx.TryReadBodyAsync<UpsertQuestiontypeRequest>();
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");
        var validation = ValidateQuestiontype(body);
        if (validation is not null) return ctx.BadRequest(validation);

        var newId = Guid.NewGuid();

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        await conn.ExecuteAsync(
            "INSERT INTO Questiontypes (questiontypeid, title, sort) VALUES (@questiontypeid, @title, @sort)",
            new { questiontypeid = newId, title = body.Title.Trim(), sort = body.Sort });

        return ctx.Created(new { questiontypeId = newId });
    }

    // GET /admin/questiontypes/{id}
    public async Task<IActionResult> TypeDetail(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "SettingMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var questiontypeId))
            return ctx.BadRequest("無效的分類 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var row = await conn.QuerySingleOrDefaultAsync(
            "SELECT questiontypeid AS questiontypeId, title, sort FROM Questiontypes WHERE questiontypeid=@questiontypeId",
            new { questiontypeId });

        if (row is null) return ctx.NotFound("找不到購物說明分類。");
        return ctx.Ok(row);
    }

    // PUT /admin/questiontypes/{id}
    public async Task<IActionResult> TypeUpdate(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "SettingMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var questiontypeId))
            return ctx.BadRequest("無效的分類 ID。");

        var body = await ctx.TryReadBodyAsync<UpsertQuestiontypeRequest>();
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");
        var validation = ValidateQuestiontype(body);
        if (validation is not null) return ctx.BadRequest(validation);

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.ExecuteAsync(
            "UPDATE Questiontypes SET title=@title, sort=@sort WHERE questiontypeid=@questiontypeid",
            new { questiontypeid = questiontypeId, title = body.Title.Trim(), sort = body.Sort });

        if (rows == 0) return ctx.NotFound("找不到購物說明分類。");
        return ctx.Ok(new { message = "購物說明分類已更新" });
    }

    // DELETE /admin/questiontypes/{id} — 硬刪，FK CASCADE 連帶刪底下 Questions/Questionmedias
    public async Task<IActionResult> TypeDelete(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "SettingMs", AdminOperation.Delete);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var questiontypeId))
            return ctx.BadRequest("無效的分類 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.ExecuteAsync(
            "DELETE FROM Questiontypes WHERE questiontypeid=@questiontypeId",
            new { questiontypeId });

        if (rows == 0) return ctx.NotFound("找不到購物說明分類。");
        return ctx.Ok(new { message = "購物說明分類已刪除" });
    }

    // ══════════════════════════════════════════════════════════════════
    // 購物說明 Questions
    // ══════════════════════════════════════════════════════════════════

    // GET /admin/questions?page=&pageSize=20&questiontypeid=
    public async Task<IActionResult> List(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "SettingMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        var q        = ctx.Request.Query;
        var page     = Math.Max(1, int.TryParse(q["page"],     out var pg) ? pg : 1);
        var pageSize = Math.Clamp(int.TryParse(q["pageSize"],  out var sz) ? sz : 20, 1, 100);
        var offset   = (page - 1) * pageSize;

        Guid? typeFilter = Guid.TryParse(q["questiontypeid"], out var tf) ? tf : null;
        var whereClause = typeFilter.HasValue ? "WHERE q.questiontypeid = @questiontypeid" : "";

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        var total = await conn.ExecuteScalarAsync<int>(
            $"SELECT COUNT(1) FROM Questions q {whereClause}",
            typeFilter.HasValue ? new { questiontypeid = typeFilter.Value } : null);

        var items = await conn.QueryAsync(
            $@"SELECT q.questionid       AS questionId,
                      q.questiontypeid   AS questiontypeId,
                      qt.title           AS questiontypeTitle,
                      q.title,
                      q.sort
               FROM Questions q
               INNER JOIN Questiontypes qt ON qt.questiontypeid = q.questiontypeid
               {whereClause}
               ORDER BY qt.sort ASC, q.sort ASC, q.title ASC
               OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY",
            typeFilter.HasValue
                ? (object)new { questiontypeid = typeFilter.Value, offset, pageSize }
                : new { offset, pageSize });

        return ctx.OkPaged(PaginatedResponse<object>.Create(items.Cast<object>().ToList(), total, page, pageSize));
    }

    // POST /admin/questions
    public async Task<IActionResult> Create(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "SettingMs", AdminOperation.Add);
        if (guard.Result is not null) return guard.Result;

        var body = await ctx.TryReadBodyAsync<UpsertQuestionRequest>();
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");
        var validation = ValidateQuestion(body);
        if (validation is not null) return ctx.BadRequest(validation);

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        var typeExists = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Questiontypes WHERE questiontypeid=@questiontypeid",
            new { questiontypeid = body.QuestiontypeId });
        if (typeExists == 0) return ctx.BadRequest("指定的購物說明分類不存在。");

        var newId = Guid.NewGuid();
        await conn.ExecuteAsync(@"
INSERT INTO Questions (questionid, questiontypeid, title, answer, sort)
VALUES (@questionid, @questiontypeid, @title, @answer, @sort)",
            new
            {
                questionid     = newId,
                questiontypeid = body.QuestiontypeId,
                title          = body.Title.Trim(),
                answer         = body.Answer,
                sort           = body.Sort,
            });

        return ctx.Created(new { questionId = newId });
    }

    // GET /admin/questions/{id} — answer 為 ntext，需 CAST 為 nvarchar(max)
    public async Task<IActionResult> Detail(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "SettingMs", AdminOperation.Read);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var questionId))
            return ctx.BadRequest("無效的購物說明 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var row = await conn.QuerySingleOrDefaultAsync(@"
SELECT questionid AS questionId, questiontypeid AS questiontypeId, title,
       CAST(answer AS nvarchar(max)) AS answer, sort
FROM Questions WHERE questionid=@questionId",
            new { questionId });

        if (row is null) return ctx.NotFound("找不到購物說明。");
        return ctx.Ok(row);
    }

    // PUT /admin/questions/{id}
    public async Task<IActionResult> Update(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "SettingMs", AdminOperation.Update);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var questionId))
            return ctx.BadRequest("無效的購物說明 ID。");

        var body = await ctx.TryReadBodyAsync<UpsertQuestionRequest>();
        if (body is null) return ctx.BadRequest("Request body 格式不正確。");
        var validation = ValidateQuestion(body);
        if (validation is not null) return ctx.BadRequest(validation);

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);

        var typeExists = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Questiontypes WHERE questiontypeid=@questiontypeid",
            new { questiontypeid = body.QuestiontypeId });
        if (typeExists == 0) return ctx.BadRequest("指定的購物說明分類不存在。");

        var rows = await conn.ExecuteAsync(@"
UPDATE Questions SET questiontypeid=@questiontypeid, title=@title, answer=@answer, sort=@sort
WHERE questionid=@questionid",
            new
            {
                questionid     = questionId,
                questiontypeid = body.QuestiontypeId,
                title          = body.Title.Trim(),
                answer         = body.Answer,
                sort           = body.Sort,
            });

        if (rows == 0) return ctx.NotFound("找不到購物說明。");
        return ctx.Ok(new { message = "購物說明已更新" });
    }

    // DELETE /admin/questions/{id} — 硬刪，FK CASCADE 連帶刪 Questionmedias
    public async Task<IActionResult> Delete(RouteContext ctx)
    {
        var guard = await AdminGuard.AuthorizeAsync(ctx, _perms, "SettingMs", AdminOperation.Delete);
        if (guard.Result is not null) return guard.Result;

        if (!Guid.TryParse(ctx.RequirePathParam("id"), out var questionId))
            return ctx.BadRequest("無效的購物說明 ID。");

        using var conn = await _db.CreateOpenConnectionAsync(ctx.Request.HttpContext.RequestAborted);
        var rows = await conn.ExecuteAsync(
            "DELETE FROM Questions WHERE questionid=@questionId",
            new { questionId });

        if (rows == 0) return ctx.NotFound("找不到購物說明。");
        return ctx.Ok(new { message = "購物說明已刪除" });
    }

    // ══════════════════════════════════════════════════════════════════
    // Validation & DTOs
    // ══════════════════════════════════════════════════════════════════

    private static string? ValidateQuestiontype(UpsertQuestiontypeRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Title)) return "分類標題為必填欄位。";
        if (r.Title.Trim().Length > 50)         return "分類標題長度不可超過 50 個字元。";
        return null;
    }

    private static string? ValidateQuestion(UpsertQuestionRequest r)
    {
        if (r.QuestiontypeId == Guid.Empty)        return "請選擇購物說明分類。";
        if (string.IsNullOrWhiteSpace(r.Title))    return "標題為必填欄位。";
        if (r.Title.Trim().Length > 150)           return "標題長度不可超過 150 個字元。";
        if (string.IsNullOrWhiteSpace(r.Answer))   return "內容為必填欄位。";
        return null;
    }

    private sealed record UpsertQuestiontypeRequest(
        string Title,
        int    Sort);

    private sealed record UpsertQuestionRequest(
        Guid   QuestiontypeId,
        string Title,
        string Answer,
        int    Sort);
}
