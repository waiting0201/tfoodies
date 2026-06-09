using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using TFoodies.Contracts.Common;

namespace TFoodies.Api.Functions.Router;

/// <summary>
/// 解析請求路由、比對路由表、提取路徑參數、呼叫 controller handler（Singleton）。
///
/// ╔══════════════════════════════════════════════════════════════════════╗
/// ║  ⚠ 設計決策：此類別必須註冊為 Singleton                              ║
/// ║                                                                    ║
/// ║  RouteHandler 在建構時預編譯所有路由 Regex（PrecompilePatterns），     ║
/// ║  這些 Regex 不會改變，應該只編譯一次。                                ║
/// ║                                                                    ║
/// ║  請求時透過 IServiceProvider 延遲解析 Controller，                   ║
/// ║  IServiceProvider 由 ApiFunction 傳入（每次請求的 scoped provider）。 ║
/// ║  RouteHandler 本身不持有任何 Scoped 依賴，可安全作為 Singleton。       ║
/// ╚══════════════════════════════════════════════════════════════════════╝
/// </summary>
public class RouteHandler
{
    private readonly RouteTable _routeTable;
    private readonly Dictionary<string, (Regex Regex, List<string> ParamNames)> _compiledPatterns = new();

    public RouteHandler(RouteTable routeTable)
    {
        _routeTable = routeTable;
        PrecompilePatterns();
    }

    /// <summary>
    /// 依 HTTP method + route 找到對應 handler 並執行；
    /// 找不到時回傳 404，方法不符時回傳 405。
    /// </summary>
    /// <param name="context">請求上下文（per-request）</param>
    /// <param name="serviceProvider">
    /// 當前請求的 scoped IServiceProvider，用於延遲解析 Controller。
    /// 必須來自當前 HTTP 請求的 scope（req.HttpContext.RequestServices），不可使用 root provider。
    /// </param>
    public async Task<IActionResult> HandleAsync(RouteContext context, IServiceProvider serviceProvider)
    {
        var method = context.Request.Method.ToUpperInvariant();
        var route = context.Route.Trim('/');

        var matchedByPattern = new List<(RouteDefinition Def, Match Match, List<string> ParamNames)>();

        foreach (var def in _routeTable.Routes)
        {
            if (!_compiledPatterns.TryGetValue(def.Pattern, out var compiled)) continue;

            var match = compiled.Regex.Match(route);
            if (match.Success)
                matchedByPattern.Add((def, match, compiled.ParamNames));
        }

        if (matchedByPattern.Count == 0)
        {
            return new NotFoundObjectResult(
                ApiErrorResponse.Create("NOT_FOUND", $"找不到路由 '{route}'。"));
        }

        var matched = matchedByPattern.FirstOrDefault(x => x.Def.Method == method);
        if (matched == default)
        {
            var allowed = string.Join(", ", matchedByPattern.Select(x => x.Def.Method).Distinct());
            return new ObjectResult(
                ApiErrorResponse.Create("METHOD_NOT_ALLOWED", $"方法 {method} 不允許。允許：{allowed}"))
            { StatusCode = 405 };
        }

        for (int i = 0; i < matched.ParamNames.Count; i++)
        {
            var name = matched.ParamNames[i];
            context.PathParams[name] = matched.Match.Groups[name].Value;
        }

        return await matched.Def.HandlerFactory(serviceProvider, context);
    }

    // ── 私有：預編譯所有路由樣式 ─────────────────────────────────────────────

    private void PrecompilePatterns()
    {
        foreach (var def in _routeTable.Routes)
        {
            if (!_compiledPatterns.ContainsKey(def.Pattern))
                _compiledPatterns[def.Pattern] = CompilePattern(def.Pattern);
        }
    }

    /// <summary>
    /// 將路由樣式轉換為 Regex 並提取參數名稱列表。
    /// 支援兩種語法：
    ///   {param}        → 轉換為 (?&lt;param&gt;[^/]+)，原始字串先 Regex.Escape 再還原
    ///   (?&lt;param&gt;...) → 已是 raw regex，直接加 ^ $ 錨定後編譯（不做 Escape）
    /// </summary>
    private static (Regex Regex, List<string> ParamNames) CompilePattern(string pattern)
    {
        // Raw regex 語法（路由表中以 (?<name>...) 定義的路由）
        // Regex.Escape 會把 (、?、< 等字元轉義成字面值，導致路由永遠不匹配。
        // 偵測到 (?< 後直接提取參數名並加錨定，略過 Escape。
        if (pattern.Contains("(?<"))
        {
            var names = Regex.Matches(pattern, @"\(\?<(\w+)>")
                .Select(m => m.Groups[1].Value)
                .ToList();
            var anchored = (pattern.StartsWith("^") ? "" : "^") + pattern;
            if (!anchored.EndsWith("$")) anchored += "$";
            return (new Regex(anchored, RegexOptions.IgnoreCase | RegexOptions.Compiled), names);
        }

        // {param} 語法：用 placeholder token 保護參數段，其餘部分安全 Escape
        var paramNames = new List<string>();
        var placeholders = new List<string>();

        var withPlaceholders = Regex.Replace(pattern, @"\{(\w+)\}", m =>
        {
            var name = m.Groups[1].Value;
            paramNames.Add(name);
            var token = $"__PARAM_{placeholders.Count}__";
            placeholders.Add(name);
            return token;
        });

        var escaped = Regex.Escape(withPlaceholders);

        for (int i = 0; i < placeholders.Count; i++)
            escaped = escaped.Replace($"__PARAM_{i}__", $"(?<{placeholders[i]}>[^/]+)");

        var regex = new Regex($"^{escaped}$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        return (regex, paramNames);
    }
}
