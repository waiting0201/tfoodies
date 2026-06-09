namespace TFoodies.Domain.Enums;

// Ported 1:1 from reference/old/tfoodies.Libs/Enum.cs.
// English names for maintainability; numeric values MUST match the frozen DB columns.
// The Chinese term from the legacy enum is kept in the doc comment for traceability.

/// <summary>發票類型 (Orders.invoicetype). Legacy EnumInvoiceType.</summary>
public enum InvoiceType
{
    /// <summary>二聯式</summary>
    Duplex = 1,
    /// <summary>捐贈</summary>
    Donation = 2,
    /// <summary>三聯式</summary>
    Triplicate = 3,
    /// <summary>免開</summary>
    None = 4,
    /// <summary>POS機</summary>
    Pos = 5,
}

/// <summary>付款方式 (Orders.paytype). Legacy EnumPayType.</summary>
public enum PayType
{
    /// <summary>信用卡線上刷卡</summary>
    CreditCard = 1,
    /// <summary>宅配貨到付款</summary>
    CashOnDelivery = 2,
    /// <summary>ATM轉帳付款</summary>
    AtmTransfer = 3,
    /// <summary>免付款</summary>
    NoPayment = 4,
    /// <summary>現金支付</summary>
    Cash = 5,
    /// <summary>電匯</summary>
    WireTransfer = 6,
    /// <summary>支票</summary>
    Check = 7,
}

/// <summary>付款狀態 (Orders.paystatus). Legacy EnumPayStatus.</summary>
public enum PayStatus
{
    /// <summary>未付款</summary>
    Unpaid = 0,
    /// <summary>已付款</summary>
    Paid = 1,
    /// <summary>退款</summary>
    Refunded = 2,
    /// <summary>免付款</summary>
    NoPayment = 3,
    /// <summary>取消</summary>
    Cancelled = 4,
}

/// <summary>出貨狀態 (Orders.deliverstatus). Legacy EnumDeliverStatus.</summary>
public enum DeliverStatus
{
    /// <summary>未出貨</summary>
    NotShipped = 0,
    /// <summary>已出貨</summary>
    Shipped = 1,
    /// <summary>退貨</summary>
    Returned = 2,
    /// <summary>取消</summary>
    Cancelled = 3,
    /// <summary>待出貨</summary>
    PendingShipment = 4,
}

/// <summary>倉別 (Warehouses.warehousetype). Legacy EnumWarehouseType.</summary>
public enum WarehouseType
{
    /// <summary>線上倉</summary>
    Online = 1,
    /// <summary>線下倉</summary>
    Offline = 2,
    /// <summary>瑕疵品倉</summary>
    Defective = 3,
}

/// <summary>訂單類型 (Orders.ordertype). Legacy EnumOrderType.</summary>
public enum OrderType
{
    /// <summary>線上單</summary>
    Online = 1,
    /// <summary>線下單</summary>
    Offline = 2,
    /// <summary>自用</summary>
    SelfUse = 3,
    /// <summary>預購</summary>
    Preorder = 4,
    /// <summary>公關</summary>
    PublicRelations = 5,
}

/// <summary>收貨時段. Legacy EnumReciverTime.</summary>
public enum ReceiveTime
{
    /// <summary>上午</summary>
    Morning = 1,
    /// <summary>下午</summary>
    Afternoon = 2,
    /// <summary>晚上</summary>
    Evening = 3,
    /// <summary>不指定</summary>
    Unspecified = 4,
}

/// <summary>發票狀態 (Orders.invoicestatus). Legacy EnumInvoiceStatus.</summary>
public enum InvoiceStatus
{
    /// <summary>未開</summary>
    NotIssued = 0,
    /// <summary>已開</summary>
    Issued = 1,
    /// <summary>作廢</summary>
    Void = 2,
}

/// <summary>退貨收貨狀態 (Returns.receivestatus). Legacy EnumReceiveStatus.</summary>
public enum ReceiveStatus
{
    /// <summary>退貨中</summary>
    Returning = 0,
    /// <summary>已到達</summary>
    Arrived = 1,
    /// <summary>取消</summary>
    Cancelled = 2,
    /// <summary>免退回</summary>
    NoReturnNeeded = 3,
}

/// <summary>退款狀態 (Returns.refundstatus). Legacy EnumRefundStatus.</summary>
public enum RefundStatus
{
    /// <summary>未退款</summary>
    NotRefunded = 0,
    /// <summary>已退款</summary>
    Refunded = 1,
    /// <summary>折讓</summary>
    Allowance = 2,
    /// <summary>免退款</summary>
    NoRefundNeeded = 3,
    /// <summary>取消</summary>
    Cancelled = 4,
}

/// <summary>入庫狀態 (Returns.warehousestatus). Legacy EnumWarehouseStatus.</summary>
public enum WarehouseStatus
{
    /// <summary>未入庫</summary>
    NotStockedIn = 0,
    /// <summary>已入庫</summary>
    StockedIn = 1,
}

/// <summary>折扣類型 (Discounts.istype). Legacy EnumDiscountType.</summary>
public enum DiscountType
{
    /// <summary>折扣 (percentage)</summary>
    Percentage = 0,
    /// <summary>折價 (fixed amount)</summary>
    Amount = 1,
}

/// <summary>Banner 樣式 (Banners.style). Legacy EnumBannerType.</summary>
public enum BannerType
{
    /// <summary>僅連結</summary>
    LinkOnly = 1,
    /// <summary>內容置中</summary>
    ContentCenter = 2,
    /// <summary>內容靠左</summary>
    ContentLeft = 3,
    /// <summary>影片</summary>
    Video = 4,
}

/// <summary>來源類型 (Expenditures.sourcetype). Legacy EnumSourceType.</summary>
public enum SourceType
{
    /// <summary>手動輸入</summary>
    Manual = 0,
    /// <summary>採購帶入</summary>
    FromPurchase = 1,
    /// <summary>退貨帶入</summary>
    FromReturn = 2,
}

/// <summary>支出付款狀態 (Expenditures.status). Legacy EnumExpenditureStatus.</summary>
public enum ExpenditureStatus
{
    /// <summary>未付款</summary>
    Unpaid = 0,
    /// <summary>部分付款</summary>
    PartiallyPaid = 1,
    /// <summary>已付款</summary>
    Paid = 2,
}
