using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Script.Serialization;
using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.IO;
using System.Net.Http;

using IniParser;
using IniParser.Model;

using tfoodies.Libs;
using tfoodiesBackend.Filters;
using tfoodiesBackend.Commons;
using tfoodies.Models;
using tfoodies.Service;

namespace tfoodiesBackend.Controllers
{
    public class AjaxController : BaseController
    {
        private static string MerchantID = ConfigurationManager.AppSettings["MerchantID"];
        private static string InvCreateUrl = ConfigurationManager.AppSettings["InvCreateUrl"];
        private static string InvCancelUrl = ConfigurationManager.AppSettings["InvCancelUrl"];

        private IProductsService productsService;
        private IProductTypesService producttypesService;
        private IZipcodesService zipcodesService;
        private IMembersService membersService;
        private IOrdersService ordersService;
        private IPurchasesService purchasesService;
        private IWarehousesService warehousesService;
        private IWarehouseStocksService warehousestocksService;
        private IOrderDetailStocksService orderdetailstocksService;
        private IDiscountsService discountsService;
        private IStocksService stocksService;
        private ISmsService smsService;
        private ISmsdetailsService smsdetailsService;
        private IReturnsService returnsService;
        private IAccountingsService accountingsService;
        private IExpendituresService expendituresService;
        private IRefoundsService refoundsService;
        private IInvoicesService invoicesService;
        private IInvoiceDetailsService invoicedetailService;

        public AjaxController()
        {
            productsService = new ProductsService();
            producttypesService = new ProductTypesService();
            zipcodesService = new ZipcodesService();
            membersService = new MembersService();
            ordersService = new OrdersService();
            purchasesService = new PurchasesService();
            warehousesService = new WarehousesService();
            warehousestocksService = new WarehouseStocksService();
            orderdetailstocksService = new OrderDetailStocksService();
            discountsService = new DiscountsService();
            stocksService = new StocksService();
            smsService = new SmsService();
            smsdetailsService = new SmsdetailsService();
            returnsService = new ReturnsService();
            accountingsService = new AccountingsService();
            expendituresService = new ExpendituresService();
            refoundsService = new RefoundsService();
            invoicesService = new InvoicesService();
            invoicedetailService = new InvoiceDetailsService();
        }

        [CheckSession]
        [HttpGet]
        public ActionResult Checknoticenumber(string noticenumber, Guid? stockid = null)
        {
            bool isnew = true;
            Stocks stock;

            if (stockid != null)
            {
                stock = stocksService.Get().Where(a => a.stockid != stockid && a.noticenumber == noticenumber).ToList().FirstOrDefault();
            }
            else
            {
                stock = stocksService.Get().Where(a => a.noticenumber == noticenumber).FirstOrDefault();
            }

            if (stock != null)
            {
                isnew = false;
            }

            return Json(new { valid = isnew }, JsonRequestBehavior.AllowGet);
        }

        [CheckSession]
        [HttpGet]
        public ActionResult Checkproductnum(string productnum, Guid? productid = null)
        {
            bool isnew = true;
            Products product;

            if (productid != null)
            {
                product = productsService.Get().Where(a => a.productid != productid && a.productnum == productnum).ToList().FirstOrDefault();
            }
            else
            {
                product = productsService.Get().Where(a => a.productnum == productnum).FirstOrDefault();
            }

            if (product != null)
            {
                isnew = false;
            }

            return Json(new { valid = isnew }, JsonRequestBehavior.AllowGet);
        }

        [CheckSession]
        [HttpGet]
        public ActionResult Checkproductname(string title, Guid? productid = null)
        {
            bool isnew = true;
            Products product;

            if (productid != null)
            {
                product = productsService.Get().Where(a => a.productid != productid && a.title == title).ToList().FirstOrDefault();
            }
            else
            {
                product = productsService.Get().Where(a => a.title == title).FirstOrDefault();
            }

            if (product != null)
            {
                isnew = false;
            }

            return Json(new { valid = isnew }, JsonRequestBehavior.AllowGet);
        }

        [CheckSession]
        [HttpGet]
        public ActionResult Checkproducttypename(string title, Guid? producttypeid = null)
        {
            bool isnew = true;
            Producttypes producttype;

            if (producttypeid != null)
            {
                producttype = producttypesService.Get().Where(a => a.producttypeid != producttypeid && a.title == title).ToList().FirstOrDefault();
            }
            else
            {
                producttype = producttypesService.Get().Where(a => a.title == title).FirstOrDefault();
            }

            if (producttype != null)
            {
                isnew = false;
            }

            return Json(new { valid = isnew }, JsonRequestBehavior.AllowGet);
        }

        [CheckSession]
        [HttpGet]
        public ActionResult Checkmobile(string mobile, Guid? memberid = null)
        {
            bool isnew = true;
            Members member;

            if (memberid != null)
            {
                member = membersService.Get().Where(a => a.memberid != memberid && a.mobile == mobile).ToList().FirstOrDefault();
            }
            else
            {
                member = membersService.Get().Where(a => a.mobile == mobile).FirstOrDefault();
            }

            if (member != null)
            {
                isnew = false;
            }

            return Json(new { valid = isnew }, JsonRequestBehavior.AllowGet);
        }

        [CheckSession]
        [HttpGet]
        public ActionResult Checkdiscountcode(string discountcode, Guid? discountid = null)
        {
            bool isnew = true;
            Discounts discount;

            if (discountid != null)
            {
                discount = discountsService.Get().Where(a => a.discountid != discountid && a.discountcode == discountcode).ToList().FirstOrDefault();
            }
            else
            {
                discount = discountsService.Get().Where(a => a.discountcode == discountcode).FirstOrDefault();
            }

            if (discount != null)
            {
                isnew = false;
            }

            return Json(new { valid = isnew }, JsonRequestBehavior.AllowGet);
        }

        [CheckSession]
        [HttpGet]
        public ActionResult Checkaccountingcode(string accountingcode, Guid? accountingid = null)
        {
            bool isnew = true;
            Accountings accounting;

            if (accountingid != null)
            {
                accounting = accountingsService.Get().Where(a => a.accountingid != accountingid && a.accountingcode == accountingcode.Trim()).ToList().FirstOrDefault();
            }
            else
            {
                accounting = accountingsService.Get().Where(a => a.accountingcode == accountingcode.Trim()).FirstOrDefault();
            }

            if (accounting != null)
            {
                isnew = false;
            }

            return Json(new { valid = isnew }, JsonRequestBehavior.AllowGet);
        }

        [CheckSession]
        [HttpPost]
        public ActionResult GetProductbyProductnum(string productnum)
        {
            string recode = string.Empty;
            Guid productid = Guid.Empty;
            string title = string.Empty;
            string capacity = string.Empty;
            int price = 0;
            string setproduct = string.Empty;

            Products product = productsService.Get().Where(a => a.productnum == productnum).FirstOrDefault();

            if (product != null)
            {
                recode = "200";
                productid = product.productid;
                title = product.title;
                capacity = product.capacity;
                price = product.price;

                if (product.isset)
                {
                    string s = string.Empty;
                    foreach(Setproducts sp in product.Setproducts)
                    {
                        s = s + "<br><small class=\"text-muted\">" + sp.Products1.title + " " + sp.Products1.capacity + " " + sp.qty + "</small>";
                    }
                    setproduct = s;
                }
            }
            else
            {
                recode = "201";
            }

            return Json(new { code = recode, productid = productid, title = title, capacity = capacity, price = price, setproduct = setproduct });
        }

        [CheckSession]
        [HttpPost]
        public ActionResult GetAccountingbyAccountingcode(string accountingcode)
        {
            string recode = string.Empty;
            Guid accountingid = Guid.Empty;
            string title = string.Empty;

            Accountings accounting = accountingsService.Get().Where(a => a.accountingcode == accountingcode).FirstOrDefault();

            if (accounting != null)
            {
                recode = "200";
                accountingid = accounting.accountingid;
                title = accounting.title;
            }
            else
            {
                recode = "201";
            }

            return Json(new { code = recode, accountingid = accountingid, title = title });
        }

        [CheckSession]
        [HttpPost]
        public ActionResult GetZipcodeByCity(string city)
        {
            IEnumerable<Zipcodes> zipcodes = zipcodesService.Get().Where(a => a.city == city).OrderBy(a => a.zipcode);
            string option = "<option value=\"\">鄉鎮市區</option>";

            foreach (Zipcodes zipcode in zipcodes)
            {
                option += "<option value=\"" + zipcode.zipcodeid + "\">" + zipcode.area + "</option>";
            }

            return Content(option);
        }

        [CheckSession]
        [HttpPost]
        public ActionResult GetPurchaseDetailByPurchaseid(Guid purchaseid)
        {
            IEnumerable<Purchasedetails> purchasedetails = purchasesService.GetByID(purchaseid).Purchasedetails.OrderBy(o => o.Products.productnum);
            string option = "<option value=\"\">採購單細項</option>";

            foreach (Purchasedetails entity in purchasedetails)
            {
                option += "<option value=\"" + entity.purchasedetailid + "\">" + entity.Products.title + " " + entity.Products.capacity + "</option>";
            }

            return Content(option);
        }

        [CheckSession]
        [HttpPost]
        public ActionResult GetWarehousesWithoutSelf(Guid warehouseid)
        {
            IEnumerable<Warehouses> warehouses = warehousesService.Get().Where(a => a.warehouseid != warehouseid);
            string option = "<option value=\"\">請選擇入庫倉</option>";

            foreach (Warehouses entity in warehouses)
            {
                option += "<option value=\"" + entity.warehouseid + "\">" + entity.title + "</option>";
            }

            IEnumerable<Warehousestocks> warehousestocks = warehousestocksService.Get().Where(a => a.warehouseid == warehouseid && a.quantity_left != 0).OrderBy(o => o.Stocks.Purchasedetails.Products.productnum);
            string checkstockid = string.Empty;
            string stockoption = "<option value=\"\">請選擇產品</option>";

            foreach (Warehousestocks entity in warehousestocks)
            {
                if(checkstockid != entity.stockid.ToString())
                {
                    checkstockid = entity.stockid.ToString();
                    stockoption += "<option value=\"" + entity.stockid + "\">" + entity.Stocks.Purchasedetails.Products.title + " " + entity.Stocks.Purchasedetails.Products.capacity + " " + entity.Stocks.noticenumber + " " + entity.Stocks.expiredate + "</option>";
                }
            }

            return Json(new { warehouseoptions = option, stockoptions = stockoption });
        }

        [CheckSession]
        [HttpPost]
        public ActionResult GetWarehouseStock(Guid warehouseid, Guid stockid)
        {
            int stockamounts = warehousestocksService.Get().Where(a => a.warehouseid == warehouseid && a.stockid == stockid).Sum(s => s.quantity_left);

            return Json(new { stockamounts = stockamounts });
        }

        [CheckSession]
        [HttpPost]
        public ActionResult GetCompanyTitle(string companynumber)
        {
            string jsonString = Librarys.GetCompanytitle(companynumber);

            if (jsonString == null || jsonString == "")
            {
                return Json(false);
            }
            else
            {
                JavaScriptSerializer j = new JavaScriptSerializer();
                IList<object> a = (List<object>)j.Deserialize(jsonString, typeof(List<object>));

                return Json(a[0]);
            }
        }

        [CheckSession]
        [HttpPost]
        public ActionResult GetOutcomeDetail(Guid supplierid)
        {
            return PartialView("_OutcomeDetail", expendituresService.GetUnpayListBySupplierID(supplierid));
        }

        [CheckSession]
        [HttpPost]
        public ActionResult GetRefoundDetail(Guid memberid)
        {
            return PartialView("_RefoundDetail", returnsService.GetUnpayListByMemberID(memberid));
        }

        [CheckSession]
        [HttpPost]
        public ActionResult GetInvoiceDetail(Guid memberid)
        {
            return PartialView("_InvoiceDetail", ordersService.GetInvoicedetailsByMemberID(memberid));
        }

        [CheckSession]
        [HttpPost]
        public ActionResult GetIncomeDetail(Guid memberid)
        {
            return PartialView("_IncomeDetail", invoicesService.GetUnpayListByMemberID(memberid));
        }

        [CheckSession]
        [HttpPost]
        public ActionResult CreateInv(Guid orderid)
        {
            string recode = string.Empty;
            string remessage = string.Empty;

            Orders order = ordersService.GetByID(orderid);

            if (order.invoicestatus == (int)EnumInvoiceStatus.未開)
            {
                int timeStamp = Convert.ToInt32(DateTime.UtcNow.AddHours(8).Subtract(new DateTime(1970, 1, 1)).TotalSeconds);
                NameValueCollection inv = HttpUtility.ParseQueryString(string.Empty, Encoding.UTF8);

                inv.Add("RespondType", "String");
                inv.Add("Version", "1.4");
                inv.Add("TimeStamp", timeStamp.ToString());
                inv.Add("MerchantOrderNo", order.ordercode);
                inv.Add("Status", "1"); //1=立即開立; 0=待開立; 3=延遲開立
                inv.Add("BuyerEmail", order.Members.email);
                inv.Add("PrintFlag", "Y");
                inv.Add("TaxType", "1");
                inv.Add("TaxRate", "5");

                bool isfirst = true;

                string ItemName = string.Empty;
                string ItemCount = string.Empty;
                string ItemUnit = string.Empty;
                string ItemPrice = string.Empty;
                string ItemAmt = string.Empty;

                foreach (Orderdetails od in order.Orderdetails)
                {
                    int subtotal = od.qty * od.price;
                    ItemName = (isfirst) ? od.Products.title : ItemName + "|" + od.Products.title;
                    ItemCount = (isfirst) ? od.qty.ToString() : ItemCount + "|" + od.qty.ToString();
                    ItemUnit = (isfirst) ? "件" : ItemUnit + "|件";

                    ItemPrice = (isfirst) ? od.price.ToString() : ItemPrice + "|" + od.price.ToString();
                    ItemAmt = (isfirst) ? subtotal.ToString() : ItemAmt + "|" + subtotal.ToString();

                    if(od.discount != null)
                    {
                        ItemName = ItemName + "|" + od.Products.title + od.discount + "折";
                        ItemCount = ItemCount + "|" + 1;
                        ItemUnit = ItemUnit + "|折";
                        ItemPrice = ItemPrice + "|" + (od.subtotal - subtotal);
                        ItemAmt = ItemAmt + "|" + (od.subtotal - subtotal);
                    }

                    isfirst = false;
                }

                inv.Add("ItemName", ItemName);
                inv.Add("ItemCount", ItemCount);
                inv.Add("ItemUnit", ItemUnit);
                inv.Add("ItemPrice", ItemPrice);
                inv.Add("ItemAmt", ItemAmt);

                int TotalAmt = order.total + order.freight - Convert.ToInt32(order.discount);
                int Amt = Convert.ToInt32(Math.Round(TotalAmt / 1.05, MidpointRounding.AwayFromZero));

                if (order.invoicetype == (int)EnumInvoiceType.三聯式)
                {
                    inv.Add("Category", "B2B");
                    inv.Add("BuyerName", order.companytitle);
                    inv.Add("BuyerUBN", order.companynumber);
                }
                else
                {
                    inv.Add("Category", "B2C");
                    inv.Add("BuyerName", order.Members.name);

                    if (order.invoicetype == (int)EnumInvoiceType.捐贈)
                    {
                        inv.Add("LoveCode", order.lovecode);
                        inv.Add("PrintFlag", "N");
                    }
                }

                int TaxAmt = TotalAmt - Amt;

                inv.Add("Amt", Amt.ToString());
                inv.Add("TaxAmt", TaxAmt.ToString());
                inv.Add("TotalAmt", TotalAmt.ToString());
                if(order.paytype == (int)EnumPayType.信用卡線上刷卡) inv.Add("Comment", "信用卡末四碼:" + order.lastpan4);

                string post_data = General.EncryptAES256(HttpUtility.UrlDecode(inv.ToString()));

                NameValueCollection pdata = HttpUtility.ParseQueryString(string.Empty);
                pdata.Add("MerchantID_", MerchantID);
                pdata.Add("PostData_", post_data);

                byte[] data = Encoding.UTF8.GetBytes(pdata.ToString());

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(InvCreateUrl);
                request.Method = "POST";
                request.ContentType = "application/x-www-form-urlencoded;charset=utf-8";
                request.ContentLength = data.Length;

                using (Stream stream = request.GetRequestStream())
                {
                    stream.Write(data, 0, data.Length);
                }

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    StreamReader reader = new StreamReader(response.GetResponseStream());
                    string strData = reader.ReadToEnd();
                    strData = HttpUtility.UrlDecode(strData);

                    NameValueCollection res = HttpUtility.ParseQueryString(strData);
                    if (res["Status"].ToString() == "SUCCESS")
                    {
                        string ordercode = Convert.ToString(res["MerchantOrderNo"]);
                        string invoicecode = Convert.ToString(res["InvoiceNumber"]);

                        Orders od = ordersService.Get().Where(a => a.ordercode == ordercode).FirstOrDefault();
                        od.invoicestatus = 1;
                        od.invoicecode = invoicecode;
                        ordersService.Update(od);
                        ordersService.SaveChanges();

                        recode = "200";
                        remessage = res["Message"];
                    }
                    else
                    {
                        recode = "201";
                        remessage = res["Message"];
                    }
                }
            }

            return Json(new { code = recode, message = remessage });
        }

        [HttpPost]
        public ActionResult CancelInv(Guid orderid)
        {
            string recode = string.Empty;
            string remessage = string.Empty;

            Orders order = ordersService.GetByID(orderid);

            if (order.invoicestatus == (int)EnumInvoiceStatus.已開)
            {
                int timeStamp = Convert.ToInt32(DateTime.UtcNow.AddHours(8).Subtract(new DateTime(1970, 1, 1)).TotalSeconds);
                NameValueCollection inv = HttpUtility.ParseQueryString(string.Empty, Encoding.UTF8);

                inv.Add("RespondType", "String");
                inv.Add("Version", "1.0");
                inv.Add("TimeStamp", timeStamp.ToString());
                inv.Add("InvoiceNumber", order.invoicecode);
                inv.Add("InvalidReason", "退貨");

                string post_data = General.EncryptAES256(HttpUtility.UrlDecode(inv.ToString()));

                NameValueCollection pdata = HttpUtility.ParseQueryString(string.Empty);
                pdata.Add("MerchantID_", MerchantID);
                pdata.Add("PostData_", post_data);

                byte[] data = Encoding.UTF8.GetBytes(pdata.ToString());

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(InvCancelUrl);
                request.Method = "POST";
                request.ContentType = "application/x-www-form-urlencoded;charset=utf-8";
                request.ContentLength = data.Length;

                using (Stream stream = request.GetRequestStream())
                {
                    stream.Write(data, 0, data.Length);
                }

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    StreamReader reader = new StreamReader(response.GetResponseStream());
                    string strData = reader.ReadToEnd();
                    strData = HttpUtility.UrlDecode(strData);

                    NameValueCollection res = HttpUtility.ParseQueryString(strData);
                    if (res["Status"] == "SUCCESS")
                    {
                        Orders od = ordersService.Get().Where(a => a.invoicecode == res["InvoiceNumber"]).FirstOrDefault();
                        od.invoicestatus = 0;
                        od.invoicecode = null;
                        ordersService.Update(od);
                        ordersService.SaveChanges();

                        recode = "200";
                        remessage = res["Message"];
                    }
                    else
                    {
                        recode = "201";
                        remessage = res["Message"];
                    }
                }
            }

            order.ordercode = Librarys.NewOrderCode(DateTime.Now);
            order.invoicestatus = 0;
            order.invoicecode = null;

            ordersService.Update(order);
            ordersService.SaveChanges();

            return Json(new { code = recode, message = remessage });
        }

        /// <summary>
        /// 訂單出貨狀態改為待出貨
        /// </summary>
        /// <param name="orderid"></param>
        /// <returns></returns>
        [CheckSession]
        [HttpPost]
        public ActionResult ChangeToBeShipped(Guid orderid)
        {
            string recode = string.Empty;
            string remessage = string.Empty;

            Orders order = ordersService.GetByID(orderid);

            if (order.logisticid != null && order.warehouseid != null && order.memberid != null && order.Orderdetails != null)
            {
                //if (order.invoicetype == (int)EnumInvoiceType.二聯式發票 && order.invoicestatus == (int)EnumInvoiceStatus.未開)
                //{
                //    recode = "201";
                //    remessage = "是否尚未開立二聯式發票，請重新確認後再提交待出貨！";
                //    return Json(new { code = recode, message = remessage });
                //}

                //if (order.invoicetype == (int)EnumInvoiceType.三聯式發票 && order.invoicecode == "" && order.companynumber == "" && order.companytitle == "")
                //{
                //    recode = "201";
                //    remessage = "是否尚未開立三聯式發票，請重新確認後再提交待出貨！";
                //    return Json(new { code = recode, message = remessage });
                //}

                order.deliverstatus = (int)EnumDeliverStatus.待出貨;
                ordersService.Update(order);
                ordersService.SaveChanges();

                recode = "200";
                remessage = "提交待出貨完成！";
            }
            else
            {
                recode = "201";
                remessage = "訂單資料尚未完成，請重新確認後再提交待出貨！";
            }

            return Json(new { code = recode, message = remessage });
        }

        /// <summary>
        /// 訂單出貨狀態改已出貨
        /// </summary>
        /// <param name="orderid"></param>
        /// <param name="warehouseid"></param>
        /// <returns></returns>
        [CheckSession]
        [HttpPost]
        public ActionResult ChangeToShipped(Guid orderid, Guid warehouseid, DateTime? deliverdate = null)
        {
            string recode = string.Empty;
            string remessage = string.Empty;

            Orders order = ordersService.GetByID(orderid);

            // 檢查庫存
            CheckInventoryResponse check = Librarys.CheckInventory(order, warehouseid);
            if(check.code == "201")
            {
                recode = check.code;
                remessage = check.message;

                return Json(new { code = recode, message = remessage });
            }

            // 扣上架數
            //Librarys.SetAdded(order);
            // 扣庫存
            Librarys.SetInventory(order, warehouseid);

            order.deliverstatus = (int)EnumDeliverStatus.已出貨;
            order.deliverdate = (deliverdate != null) ? deliverdate : Convert.ToDateTime(DateTime.Now.ToString("yyyy-MM-dd"));

            ordersService.Update(order);
            ordersService.SaveChanges();

            Invoicedetails ivds = invoicedetailService.Get().FirstOrDefault(a => a.orderid == order.orderid);
            if(ivds == null)
            {
                Invoices invoice = new Invoices();
                invoice.invoiceid = Guid.NewGuid();
                invoice.invoicecode = Librarys.NewInvoiceCode(DateTime.Now);
                invoice.memberid = order.memberid;
                invoice.requestdate = order.orderdate;
                invoice.createdate = DateTime.Now;

                int TotalAmt = order.total + order.freight - Convert.ToInt32(order.discount);
                int Amt = Convert.ToInt32(Math.Round(TotalAmt / 1.05, MidpointRounding.AwayFromZero));
                int TaxAmt = TotalAmt - Amt;

                Invoicedetails invoicedetail = new Invoicedetails();
                invoicedetail.invoicedetailid = Guid.NewGuid();
                invoicedetail.orderid = order.orderid;
                invoicedetail.accountingid = new Guid("f6cfd53f-13ca-4843-881f-141b579b4a5b");
                invoicedetail.price = TotalAmt;
                invoicedetail.tax = TaxAmt;
                invoicedetail.note = order.note;

                invoice.Invoicedetails.Add(invoicedetail);

                invoicesService.Create(invoice);
                invoicesService.SaveChanges();
            }

            recode = "200";
            remessage = "提交已出貨完成！";

            return Json(new { code = recode, message = remessage });
        }

        [CheckSession]
        [HttpPost]
        public ActionResult AddSmsMembers(Guid smsid, Guid[] memberids)
        {
            string recode = string.Empty;
            string remessage = string.Empty;

            if (memberids != null)
            {
                foreach (Guid memberid in memberids)
                {
                    Smsdetails lastsd = smsdetailsService.Get().Where(a => a.smsid == smsid).OrderBy(o => o.section).LastOrDefault();
                    Smsdetails smsdetail = smsdetailsService.Get().Where(a => a.smsid == smsid && a.memberid == memberid).FirstOrDefault();
                    if (smsdetail == null)
                    {
                        Smsdetails sd = new Smsdetails();
                        sd.smsdetailid = Guid.NewGuid();
                        sd.smsid = smsid;
                        sd.memberid = memberid;
                        sd.issend = 0;
                        sd.section = (lastsd != null) ? lastsd.section + 1 : 1;

                        smsdetailsService.Create(sd);
                        smsdetailsService.SaveChanges();
                    }
                }

                recode = "200";
                remessage = "會員成功加入！";
            }
            else
            {
                recode = "201";
                remessage = "尚未選擇會員！";
            }

            return Json(new { code = recode, message = remessage });
        }

        [CheckSession]
        [HttpPost]
        public ActionResult SendSms(Guid smsid)
        {
            string recode = string.Empty;
            string remessage = string.Empty;

            Sms sms = smsService.GetByID(smsid);
            IEnumerable<Smsdetails> smsdetails = sms.Smsdetails.Where(a => a.issend == 0).OrderBy(o => o.section);

            if (smsdetails == null)
            {
                recode = "201";
                remessage = "沒有寄送人";
            }
            else
            {
                //byte[] b = UTF8Encoding.ASCII.GetBytes("[1]\r\nstatuscode=u\r\nError=²�T���e���o���ť�\r\n[2]\r\nmsgid=1091821823\r\nstatuscode=1\r\nAccountPoint=88\r\n");
                //string s = UTF8Encoding.ASCII.GetString(b);

                Smsdetails lastsd = smsdetails.LastOrDefault();
                int length = lastsd.section.ToString().Length;
                bool isfirst = true;

                StringBuilder sb = new StringBuilder();
                foreach (Smsdetails sd in smsdetails)
                {
                    sb.AppendLine(string.Format("[{0}]", sd.section.ToString().PadLeft(length, '0')));
                    sb.AppendLine(string.Format("DestName={0}", sd.smsdetailid.ToString()));
                    sb.AppendLine(string.Format("dstaddr={0}", sd.Members.mobile));
                    if (isfirst) sb.AppendLine(string.Format("dlvtime={0}", (sms.dlvtime != null) ? Convert.ToDateTime(sms.dlvtime).ToString("yyyyMMddHHmmss") : null));
                    if (isfirst) sb.AppendLine(string.Format("response={0}", "http://192.168.1.200/smreply.asp"));
                    sb.AppendLine(string.Format("smbody={0}", sms.smbody));
                    isfirst = false;
                }

                HttpClient client = new HttpClient();
                StringBuilder url = new StringBuilder("http://smexpress.mitake.com.tw:9600/SmSendGet.asp?");
                url.Append("username=").Append("45889852");
                url.Append("&password=").Append("!QW@2we3");
                //url.Append("&encoding=UTF8");

                byte[] buffer = Encoding.GetEncoding("Big5").GetBytes(sb.ToString());

                var response = client.PostAsync(url.ToString(), new ByteArrayContent(buffer)).Result;
                string responseBody = response.Content.ReadAsStringAsync().Result; //"[1]\r\nstatuscode=u\r\nError=²�T���e���o���ť�\r\n[2]\r\nmsgid=1091821823\r\nstatuscode=1\r\nAccountPoint=88\r\n"

                byte[] byteArray = Encoding.GetEncoding("Big5").GetBytes(responseBody);
                //byte[] byteArray = Encoding.ASCII.GetBytes("[1]\r\nstatuscode=u\r\nError=²�T���e���o���ť�\r\n[2]\r\nmsgid=1091821823\r\nstatuscode=1\r\nAccountPoint=88\r\n");
                MemoryStream stream = new MemoryStream(byteArray);
                StreamReader reader = new StreamReader(stream);

                FileIniDataParser parser = new FileIniDataParser();
                IniData data = parser.ReadData(reader);

                foreach (Smsdetails sd in smsdetails)
                {
                    string statuscode = data[sd.section.ToString()]["statuscode"];
                    sd.statuscode = statuscode;

                    if (statuscode == "1")
                    {
                        sd.issend = 1;
                        sd.msgid = data[sd.section.ToString()]["msgid"];
                    }
                }
                smsService.Update(sms);
                smsService.SaveChanges();

                recode = "200";
                remessage = "寄發完成";
            }

            return Json(new { code = recode, message = remessage });
        }

        [CheckSession]
        [HttpPost]
        public ActionResult GetMember(Guid memberid)
        {
            Members member = membersService.GetByID(memberid);

            return Json(new { recivername = member.name, recivermobile = member.mobile, recivercity = member.Zipcodes.city, reciverzipcodeid = member.zipcodeid, reciveraddress = member.address });
        }

        [CheckSession]
        [HttpPost]
        public ActionResult GetOrderdetailByOrdercode(string ordercode)
        {
            string recode = string.Empty;
            string remessage = string.Empty;
            string reorderdetails = string.Empty;
            Guid reorderid = Guid.Empty;

            Orders order = ordersService.Get().Where(a => a.deliverstatus == (int)EnumDeliverStatus.已出貨 && a.ordercode == ordercode).FirstOrDefault();

            if (order == null)
            {
                recode = "201";
                remessage = "查無此訂單";
            }
            else
            {
                int idx = 0;
                StringBuilder sb = new StringBuilder();

                foreach (Orderdetails od in order.Orderdetails)
                {
                    string s = string.Empty;
                    if (od.Products.isset)
                    {
                        foreach (Setproducts sp in od.Products.Setproducts)
                        {
                            s = s + "<br><small class=\"text-muted\">" + sp.Products1.title + " " + sp.Products1.capacity + " " + sp.qty + "</small>";
                        }
                    }

                    sb.Append("<tr>");
                    sb.Append("<td>" + od.Products.productnum + "<input type=\"hidden\" name=\"returndetails[" + idx + "].orderdetailid\" value=\"" + od.orderdetailid + "\" /><input type=\"hidden\" name=\"returndetails[" + idx + "].returndetailid\" value=\"00000000-0000-0000-0000-000000000000\" /><input type=\"hidden\" name=\"returndetails[" + idx + "].price\" class=\"tp\" value=\"0\" /><input type=\"hidden\" name=\"price\" value=\"" + od.price + "\"></td>");
                    sb.Append("<td>" + od.Products.title + " " + od.Products.capacity + s + "</td>");
                    sb.Append("<td class=\"text-center\">" + od.qty + "</td>");
                    sb.Append("<td class=\"text-center\">");
                    sb.Append("<select name=\"returndetails[" + idx + "].qty\" class=\"form-control qty\" >");
                    for (int q = 0; q <= od.qty; q++)
                    {
                        sb.Append("<option value=\"" + q + "\">" + q + "</option>");
                    }
                    sb.Append("</select>");
                    sb.Append("</td>");
                    sb.Append("<td class=\"text-right\">0</td>");
                    sb.Append("</tr>");

                    idx++;
                }
                recode = "200";
                remessage = "";
                reorderdetails = sb.ToString();
                reorderid = order.orderid;
            }

            return Json(new { code = recode, message = remessage, orderdetails = reorderdetails, orderid = reorderid });
        }

        public ActionResult ChangeWarehousing(Guid returnid)
        {
            string recode = string.Empty;
            string remessage = string.Empty;

            Returns rt = returnsService.GetByID(returnid);

            // 入庫存
            Librarys.ReturnInventory(rt);

            rt.warehousestatus = (int)EnumWarehouseStatus.已入庫;
            rt.warehousesdate = DateTime.Now;

            returnsService.Update(rt);
            returnsService.SaveChanges();

            recode = "200";
            remessage = "提交入庫完成！";

            return Json(new { code = recode, message = remessage });
        }

        /// <summary>
        /// 採購單轉拋營業支出
        /// </summary>
        /// <param name="purchaseid"></param>
        /// <returns></returns>
        [CheckSession]
        [HttpPost]
        public ActionResult ChangeToExpenditure(Guid purchaseid)
        {
            string recode = string.Empty;
            string remessage = string.Empty;

            Purchases purchase = purchasesService.GetByID(purchaseid);
            Expenditures ed = expendituresService.Get().Where(a => a.purchaseid == purchaseid).FirstOrDefault();

            if (ed == null)
            {
                Expenditures expenditure = new Expenditures();
                expenditure.expenditureid = Guid.NewGuid();
                expenditure.supplierid = purchase.supplierid;
                expenditure.expenditurecode = Librarys.NewExpenditureCode(DateTime.Now);
                expenditure.expendituredate = purchase.purchasedate;
                expenditure.sourcetype = (int)EnumSourceType.採購帶入;
                expenditure.purchaseid = purchase.purchaseid;
                expenditure.status = (int)EnumExpenditureStatus.未付款;
                expenditure.createdate = DateTime.Now;

                foreach(Purchasedetails pd in purchase.Purchasedetails)
                {
                    Expendituredetails expendituredetail = new Expendituredetails();
                    expendituredetail.expendituredetailid = Guid.NewGuid();
                    expendituredetail.purchasedetailid = pd.purchasedetailid;
                    expendituredetail.price = Convert.ToInt32(Math.Round(pd.subtotal * purchase.Exchanges.rate, MidpointRounding.AwayFromZero));
                    expendituredetail.accountingid = new Guid("08320744-81df-4fa9-8070-f1b3dd7ba8c8");

                    expenditure.Expendituredetails.Add(expendituredetail);
                }

                expendituresService.Create(expenditure);
                expendituresService.SaveChanges();

                purchase.isexpenditure = true;
                purchasesService.Update(purchase);
                purchasesService.SaveChanges();

                recode = "200";
                remessage = "提交營業支出完成！";
            }
            else
            {
                recode = "201";
                remessage = "營業支出重複提交，請重新確認！";
            }

            return Json(new { code = recode, message = remessage });
        }
    }
}