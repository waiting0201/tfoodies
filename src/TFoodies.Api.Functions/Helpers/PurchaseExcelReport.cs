using ClosedXML.Excel;

namespace TFoodies.Api.Functions.Helpers;

/// <summary>
/// 採購單 Excel 報表產生器（對應舊系統 PurchaseMs 的「匯出」按鈕）。
/// 舊系統 view 雖有匯出按鈕，但 PurchasesExport action 從未實作；
/// 此處比照新系統訂單匯出（OrderExcelReport）以 ClosedXML 產生 .xlsx。
/// 一筆採購單可展開為多列明細，排序與列表一致（採購日期 DESC、採購編號 DESC）。
/// </summary>
public static class PurchaseExcelReport
{
    public const string ContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static string StatusLabel(int v) => v switch
    {
        2 => "已入庫",
        3 => "部分入庫",
        _ => "未入庫",
    };

    public static byte[] BuildPurchasesSheet(IReadOnlyList<PurchaseExportModel> purchases)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("purchases");

        string[] headers =
        {
            "採購編號", "採購日期", "供應商", "幣別", "付款條件", "ETD", "交貨期限", "狀態",
            "產品編號", "產品名稱", "單價", "數量", "小計", "備註",
        };
        for (var c = 0; c < headers.Length; c++)
            ws.Cell(1, c + 1).Value = headers[c];
        ws.Row(1).Style.Font.Bold = true;

        var row = 2;
        foreach (var p in purchases)
        {
            var lines = p.Lines.Count > 0
                ? p.Lines
                : new List<PurchaseExportLine> { new("", "", 0, 0, 0) };
            var first = true;
            foreach (var line in lines)
            {
                if (first)
                {
                    ws.Cell(row, 1).Value = p.PurchaseCode;
                    ws.Cell(row, 2).Value = p.PurchaseDate.ToString("yyyy-MM-dd");
                    ws.Cell(row, 3).Value = p.SupplierName;
                    ws.Cell(row, 4).Value = p.ExchangeName ?? "";
                    ws.Cell(row, 5).Value = p.Payment ?? "";
                    ws.Cell(row, 6).Value = p.Etd?.ToString("yyyy-MM-dd") ?? "";
                    ws.Cell(row, 7).Value = p.DeliverTerm ?? "";
                    ws.Cell(row, 8).Value = StatusLabel(p.Status);
                    ws.Cell(row, 14).Value = p.Note ?? "";
                    first = false;
                }

                ws.Cell(row, 9).Value = line.ProductNum;
                ws.Cell(row, 10).Value = line.ProductTitle;
                ws.Cell(row, 11).Value = line.UnitPrice;
                ws.Cell(row, 12).Value = line.Qty;
                ws.Cell(row, 13).Value = line.Subtotal;
                row++;
            }
        }

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public sealed record PurchaseExportModel(
        string PurchaseCode, DateOnly PurchaseDate, string SupplierName, string? ExchangeName,
        string? Payment, DateOnly? Etd, string? DeliverTerm, int Status, string? Note,
        IReadOnlyList<PurchaseExportLine> Lines);

    public sealed record PurchaseExportLine(
        string ProductNum, string ProductTitle, decimal UnitPrice, int Qty, decimal Subtotal);
}
