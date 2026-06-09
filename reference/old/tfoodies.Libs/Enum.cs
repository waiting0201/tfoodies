using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tfoodies.Libs
{
    public enum EnumInvoiceType
    {
        二聯式 = 1,
        捐贈 = 2,
        三聯式 = 3,
        免開 = 4,
        POS機 = 5
    }

    public enum EnumPayType
    {
        信用卡線上刷卡 = 1,
        宅配貨到付款 = 2,
        ATM轉帳付款 = 3,
        免付款 = 4,
        現金支付 = 5,
        電匯 = 6,
        支票 = 7
    }

    public enum EnumPayStatus
    {
        未付款 = 0,
        已付款 = 1,
        退款 = 2,
        免付款 = 3,
        取消 = 4
    }

    public enum EnumDeliverStatus
    {
        未出貨 = 0,
        已出貨 = 1,
        退貨 = 2,
        取消 = 3,
        待出貨 = 4
    }

    public enum EnumWarehouseType
    {
        線上倉 = 1,
        線下倉 = 2,
        瑕疵品倉 = 3
    }

    public enum EnumOrderType
    {
        線上單 = 1,
        線下單 = 2,
        自用 = 3,
        預購 = 4,
        公關 = 5
    }

    public enum EnumReciverTime
    {
        上午 = 1,
        下午 = 2,
        晚上 = 3,
        不指定 = 4
    }

    public enum EnumInvoiceStatus
    {
        未開 = 0,
        已開 = 1,
        作廢 = 2
    }

    public enum EnumReceiveStatus
    {
        退貨中 = 0,
        已到達 = 1,
        取消 = 2,
        免退回 = 3
    }

    public enum EnumRefundStatus
    {
        未退款 = 0,
        已退款 = 1,
        折讓 = 2,
        免退款 = 3,
        取消 = 4
    }

    public enum EnumWarehouseStatus
    {
        未入庫 = 0,
        已入庫 = 1
    }

    public enum EnumDiscountType
    {
        折扣 = 0,
        折價 = 1
    }

    public enum EnumBannerType
    {
        僅連結 = 1,
        內容置中 = 2,
        內容靠左 = 3,
        影片 = 4
    }

    public enum EnumSourceType
    {
        手動輸入 = 0,
        採購帶入 = 1,
        退貨帶入 = 2
    }

    public enum EnumExpenditureStatus
    {
        未付款 = 0,
        部分付款 = 1,
        已付款 = 2
    }
}
