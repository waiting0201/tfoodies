using ClosedXML.Excel;
using TFoodies.Domain.Enums;

namespace TFoodies.Api.Functions.Helpers;

/// <summary>
/// 訂單相關 Excel 報表產生器。移植自舊系統 OrderMsController 的 NPOI 匯出
/// （OrdersExport / ShippedExport(tfoodies/shopcom) / ShipmentsExport 揀貨單 / ExportDeliver 出貨單）。
/// 舊系統用 NPOI 產 .xls；此處改用 ClosedXML 產 .xlsx，內容欄位一致。
///
/// 新系統 enum 為英文，匯出需顯示中文，故在此提供對應標籤。
/// </summary>
public static class OrderExcelReport
{
    public const string ContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    // ── 中文標籤對應（取代舊系統 Enum.GetName(EnumXxx)）─────────────────────────

    public static string PayTypeLabel(int v) => (PayType)v switch
    {
        PayType.CreditCard => "信用卡線上刷卡",
        PayType.CashOnDelivery => "宅配貨到付款",
        PayType.AtmTransfer => "ATM轉帳付款",
        PayType.NoPayment => "免付款",
        PayType.Cash => "現金支付",
        PayType.WireTransfer => "電匯",
        PayType.Check => "支票",
        _ => "-",
    };

    public static string InvoiceTypeLabel(int v) => (InvoiceType)v switch
    {
        InvoiceType.Duplex => "二聯式",
        InvoiceType.Donation => "捐贈",
        InvoiceType.Triplicate => "三聯式",
        InvoiceType.None => "免開",
        InvoiceType.Pos => "POS機",
        _ => "-",
    };

    public static string DeliverStatusLabel(int v) => (DeliverStatus)v switch
    {
        DeliverStatus.NotShipped => "未出貨",
        DeliverStatus.Shipped => "已出貨",
        DeliverStatus.Returned => "退貨",
        DeliverStatus.Cancelled => "取消",
        DeliverStatus.PendingShipment => "待出貨",
        _ => "-",
    };

    public static string OrderTypeLabel(int v) => (OrderType)v switch
    {
        OrderType.Online => "線上單",
        OrderType.Offline => "線下單",
        OrderType.SelfUse => "自用",
        OrderType.Preorder => "預購",
        OrderType.PublicRelations => "公關",
        _ => "-",
    };

    // ── 訂單匯出（未出貨 / 已出貨內部版面）─────────────────────────────────────

    /// <summary>OrdersExport / ShippedExport(category=tfoodies)。一筆訂單可多列明細。</summary>
    public static byte[] BuildOrdersSheet(IReadOnlyList<OrderExportModel> orders)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("orders");

        string[] headers =
        {
            "訂單日期", "訂單編號", "訂單類型", "收件人", "電話", "付款方式",
            "發票類型", "發票號碼", "物流", "購買品項", "數量", "價格", "折扣數",
            "總金額", "出貨倉", "出貨狀態", "備註",
        };
        for (var c = 0; c < headers.Length; c++)
            ws.Cell(1, c + 1).Value = headers[c];
        ws.Row(1).Style.Font.Bold = true;

        var row = 2;
        foreach (var o in orders)
        {
            var lines = o.Lines.Count > 0 ? o.Lines : new List<OrderExportLine> { new("", false, new(), 0, 0, "-") };
            var first = true;
            foreach (var line in lines)
            {
                if (first)
                {
                    ws.Cell(row, 1).Value = o.OrderDate.ToString("yyyy-MM-dd");
                    ws.Cell(row, 2).Value = o.OrderCode;
                    ws.Cell(row, 3).Value = OrderTypeLabel(o.OrderType);
                    ws.Cell(row, 4).Value = o.ReceiverName;
                    ws.Cell(row, 5).Value = o.ReceiverMobile;
                    ws.Cell(row, 6).Value = PayTypeLabel(o.PayType);
                    ws.Cell(row, 7).Value = InvoiceTypeLabel(o.InvoiceType);
                    ws.Cell(row, 8).Value = o.InvoiceCode ?? "";
                    ws.Cell(row, 9).Value = o.LogisticTitle ?? "";
                    ws.Cell(row, 14).Value = o.Total;
                    ws.Cell(row, 15).Value = o.WarehouseTitle ?? "";
                    ws.Cell(row, 16).Value = DeliverStatusLabel(o.DeliverStatus);
                    ws.Cell(row, 17).Value = o.Note ?? "";
                    first = false;
                }

                var name = line.ProductTitle + (line.IsGift ? " (贈品)" : "");
                if (line.SetComponents.Count > 0)
                    name += "\n" + string.Join("\n", line.SetComponents);
                ws.Cell(row, 10).Value = name;
                if (name.Contains('\n')) ws.Cell(row, 10).Style.Alignment.WrapText = true;
                ws.Cell(row, 11).Value = line.Qty;
                ws.Cell(row, 12).Value = line.Price;
                ws.Cell(row, 13).Value = line.DiscountText;
                row++;
            }
        }

        ws.Columns().AdjustToContents();
        return ToBytes(wb);
    }

    // ── 美安佣金報表（ShippedExport category=shopcom）──────────────────────────

    public static byte[] BuildShopcomSheet(IReadOnlyList<ShopcomOrderModel> orders)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("shopcom");

        string[] zh =
        {
            "訂購日期", "訂單編號", "購買人", "Market Taiwan RID number", "Click_ID",
            "商品編號", "購買商品", "數量", "網路銷售價", "總計",
        };
        string[] en =
        {
            "Order Date", "Order Serial Number", "Buyer Name", "", "",
            "Product Serial Number", "Product description", "Product Quantity", "Unit price", "Sale Amount",
        };
        for (var c = 0; c < zh.Length; c++)
        {
            ws.Cell(1, c + 1).Value = zh[c];
            ws.Cell(2, c + 1).Value = en[c];
        }
        ws.Row(1).Style.Font.Bold = true;

        var row = 3;
        var total = 0;
        foreach (var o in orders)
        {
            foreach (var l in o.Lines)
            {
                ws.Cell(row, 1).Value = o.OrderDate.ToString("yyyy/MM/dd");
                ws.Cell(row, 2).Value = o.OrderCode;
                ws.Cell(row, 3).Value = o.MemberName;
                ws.Cell(row, 4).Value = o.RID ?? "";
                ws.Cell(row, 5).Value = o.ClickId ?? "";
                ws.Cell(row, 6).Value = l.ProductNum ?? "";
                ws.Cell(row, 7).Value = l.ProductTitle;
                ws.Cell(row, 8).Value = l.Qty;
                ws.Cell(row, 9).Value = "NT$" + l.Price;
                ws.Cell(row, 10).Value = "NT$" + l.Subtotal;
                row++;
                total += l.Subtotal;
            }
        }

        ws.Cell(row, 10).Value = total; row++;

        var commission = (int)Math.Round(total * 0.2, MidpointRounding.AwayFromZero);
        ws.Cell(row, 8).Value = "右欄填入佣金%";
        ws.Cell(row, 9).Value = "20.0%";
        ws.Cell(row, 10).Value = commission; row++;

        var tax = (int)Math.Round((commission * 1.05) - commission, MidpointRounding.AwayFromZero);
        ws.Cell(row, 8).Value = "營業稅";
        ws.Cell(row, 9).Value = "5%";
        ws.Cell(row, 10).Value = tax; row++;

        ws.Cell(row, 8).Value = "應付佣金總數";
        ws.Cell(row, 10).Value = commission + tax; row += 3;

        string[] notes =
        {
            "注意事項與需知：",
            "1.每月5-10號提供上個月的購買完整交易報表，RID號碼必定為至少（含）三組英數混和的字元，mail至美安帳務處理信箱 psreport@markettaiwan.com.tw。",
            "2.收到發票後，應於當月25 日前匯佣金至下列指定銀行帳號，並照下一步驟，將收據給予美安公司，方完成整個流程。",
            "   銀行帳號：香港商香港匯豐銀行臺北分行(行庫代碼:081)",
            "   戶名：美商美安美台股份有限公司台灣分公司",
            "   銀行帳號: 001- 270008 – 031",
            "3.請將匯款收據或ATM轉帳明細，傳真或mail至02-87128189；或掃描收據並mail至美安帳務處理信箱 psreport@markettaiwan.com.tw。",
            "4.累積2個月份延遲報表或付款單據之廠商，美安公司將中止與其之夥伴合作關係。",
        };
        foreach (var n in notes) { ws.Cell(row, 1).Value = n; row++; }

        ws.Columns().AdjustToContents();
        return ToBytes(wb);
    }

    // ── 揀貨單（ShipmentsExport）────────────────────────────────────────────────

    public static byte[] BuildPickingSheet(IReadOnlyList<PickUpModel> picks)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("shipment");

        string[] headers = { "Check", "通知編號", "條碼", "產品名稱", "數量", "到期日", "倉庫", "撿貨日" };
        for (var c = 0; c < headers.Length; c++) ws.Cell(1, c + 1).Value = headers[c];
        ws.Row(1).Style.Font.Bold = true;

        var row = 2;
        foreach (var p in picks)
        {
            ws.Cell(row, 1).Value = "□";
            ws.Cell(row, 2).Value = p.Noticenumber ?? "";
            ws.Cell(row, 3).Value = p.Barcode ?? "";
            ws.Cell(row, 4).Value = p.Product ?? "";
            ws.Cell(row, 5).Value = p.Quantity;
            ws.Cell(row, 6).Value = p.Expiredate?.ToString("yyyy-MM-dd") ?? "";
            ws.Cell(row, 7).Value = p.Warehouse ?? "";
            ws.Cell(row, 8).Value = p.Pickupdate.ToString("yyyy-MM-dd");
            row++;
        }

        ws.Columns().AdjustToContents();
        return ToBytes(wb);
    }

    // ── 出貨單（ExportDeliver / ExportOrders，使用 deliver.xlsx 範本）───────────
    //
    // NPOI 是 0-based（GetRow(5)/GetCell(7)）；ClosedXML 是 1-based（Row(6)/Cell(8)）。

    public static byte[] BuildDeliverNote(string templatePath, DeliverNoteModel m)
    {
        using var wb = new XLWorkbook(templatePath);
        var ws = wb.Worksheet(1);

        ws.Cell(6, 8).Value = m.DeliverDate?.ToString("yyyy-MM-dd") ?? "";
        ws.Cell(7, 2).Value = m.OrderCode;
        ws.Cell(7, 5).Value = m.MemberName;
        ws.Cell(8, 2).Value = m.OrderDate.ToString("yyyy-MM-dd");
        ws.Cell(8, 5).Value = m.MemberMobile;
        ws.Cell(9, 2).Value = m.PayDate?.ToString("yyyy-MM-dd") ?? "";
        ws.Cell(9, 5).Value = m.AddressFull;
        ws.Cell(10, 2).Value = PayTypeLabel(m.PayType);
        ws.Cell(11, 2).Value = m.InvoiceText;
        ws.Cell(11, 5).Value = m.MemberEmail ?? "";

        var rowindex = 16; // NPOI 15 → ClosedXML 16
        foreach (var l in m.Lines)
        {
            ws.Cell(rowindex, 2).Value = l.ProductTitle;
            ws.Cell(rowindex, 4).Value = l.Qty;
            ws.Cell(rowindex, 5).Value = l.FixPrice.ToString();
            ws.Cell(rowindex, 6).Value = l.DiscountText;
            ws.Cell(rowindex, 7).Value = l.Price;
            ws.Cell(rowindex, 8).Value = l.Subtotal;
            rowindex++;
        }

        ws.Cell(30, 8).Value = m.Total;
        ws.Cell(31, 8).Value = m.Freight;
        ws.Cell(32, 8).Value = m.Discount;
        ws.Cell(33, 4).Value = m.Lines.Sum(x => x.Qty);
        ws.Cell(33, 8).Value = m.Total + m.Freight - m.Discount;
        ws.Cell(34, 1).Value = "備註：" + (m.Note ?? "");

        return ToBytes(wb);
    }

    private static byte[] ToBytes(XLWorkbook wb)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ── DTO ─────────────────────────────────────────────────────────────────────

    public sealed record OrderExportModel(
        DateTime OrderDate, string OrderCode, int OrderType,
        string ReceiverName, string ReceiverMobile, int PayType,
        int InvoiceType, string? InvoiceCode, string? LogisticTitle,
        int Total, string? WarehouseTitle, int DeliverStatus, string? Note,
        List<OrderExportLine> Lines);

    public sealed record OrderExportLine(
        string ProductTitle, bool IsGift, List<string> SetComponents,
        int Qty, int Price, string DiscountText);

    public sealed record ShopcomOrderModel(
        DateTime OrderDate, string OrderCode, string MemberName,
        string? RID, string? ClickId, List<ShopcomLine> Lines);

    public sealed record ShopcomLine(
        string? ProductNum, string ProductTitle, int Qty, int Price, int Subtotal);

    public sealed record PickUpModel(
        string? Noticenumber, string? Barcode, string? Product,
        int Quantity, DateTime? Expiredate, string? Warehouse, DateTime Pickupdate);

    public sealed record DeliverNoteModel(
        DateTime? DeliverDate, string OrderCode, string MemberName,
        DateTime OrderDate, string MemberMobile, DateTime? PayDate,
        string AddressFull, int PayType, string InvoiceText, string? MemberEmail,
        List<DeliverLine> Lines, int Total, int Freight, int Discount, string? Note);

    public sealed record DeliverLine(
        string ProductTitle, int Qty, int FixPrice, string DiscountText, int Price, int Subtotal);
}
