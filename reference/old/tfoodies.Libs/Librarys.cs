using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using tfoodies.Models;
using tfoodies.Service;

namespace tfoodies.Libs
{
    public static class Librarys
    {
        public static string GetRemoteUrl(string url)
        {
            string jsonData;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.KeepAlive = false;
            request.Method = "GET";
            request.Timeout = 10000;

            request.UserAgent = "Mozilla/4.0 (compatible; MSIE 6.0b; Windows NT 5.1)";

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    jsonData = reader.ReadToEnd();
                }
            }
            return jsonData;
        }

        public static string GetRecaptchaV3ClientKey()
        {
            return "6LcEju0UAAAAACEv784qfT8AWaHQRw6Ul2O1b8cC";
        }

        public static string GetRecaptchaV3Secret()
        {
            return "6LcEju0UAAAAAD-mfrlGZgkKFa8CMPngwKmSWlAu";
        }

        public static string GetCompanynumber(string companynumber)
        {
            string nameurl = string.Format("http://data.gcis.nat.gov.tw/od/data/api/9D17AE0D-09B5-4732-A8F4-81ADED04B679?$format={0}&$filter=Business_Accounting_NO%20eq%20{1}", "json", companynumber);
            string jsonData = GetRemoteUrl(nameurl);
            return jsonData;
        }

        public static string GetCompanytitle(string companynumber)
        {
            string detailurl = string.Format("http://data.gcis.nat.gov.tw/od/data/api/5F64D864-61CB-4D0D-8AD9-492047CC1EA6?$format={0}&$filter=Business_Accounting_NO%20eq%20{1}", "json", companynumber);
            string jsonString = GetRemoteUrl(detailurl);
            return jsonString;
        }

        public static string GetSubCompanytitle(string companynumber)
        {
            string detailurl = string.Format("http://data.gcis.nat.gov.tw/od/data/api/FDB8D2C8-573D-4276-BFA4-8D3925ABE1CB?$format={0}&$filter=Business_Accounting_NO%20eq%20{1}", "json", companynumber);
            string jsonString = GetRemoteUrl(detailurl);
            return jsonString;
        }

        public static string NewOrderCode(DateTime today)
        {
            IOrderCodesService ordercodesService = new OrderCodesService();

            string year = today.Year.ToString();
            string month = today.Month.ToString().PadLeft(2, '0');
            string day = today.Day.ToString().PadLeft(2, '0');
            string result = string.Empty;

            Ordercodes ordercode = ordercodesService.Get().Where(a => a.year == year && a.month == month && a.day == day).FirstOrDefault();
            if (ordercode == null)
            {
                ordercode = new Ordercodes();
                ordercode.ordercodeid = Guid.NewGuid();
                ordercode.year = year;
                ordercode.month = month;
                ordercode.day = day;
                ordercode.code = 1;

                ordercodesService.Create(ordercode);
            }
            else
            {
                ordercode.code = ordercode.code + 1;

                ordercodesService.Update(ordercode);
            }

            ordercodesService.SaveChanges();

            result = year + month + day + ordercode.code.ToString().PadLeft(3, '0');

            HttpContext.Current.Session.Add("orderCode", result);

            return result;
        }

        public static string NewPurchaseCode(DateTime today)
        {
            IPurchaseCodesService purchasecodesService = new PurchaseCodesService();

            string year = today.Year.ToString();
            string month = today.Month.ToString().PadLeft(2, '0');
            string day = today.Day.ToString().PadLeft(2, '0');
            string result = string.Empty;

            Purchasecodes purchasecode = purchasecodesService.Get().Where(a => a.year == year && a.month == month && a.day == day).FirstOrDefault();
            if (purchasecode == null)
            {
                purchasecode = new Purchasecodes();
                purchasecode.purchasecodeid = Guid.NewGuid();
                purchasecode.year = year;
                purchasecode.month = month;
                purchasecode.day = day;
                purchasecode.code = 1;

                purchasecodesService.Create(purchasecode);
            }
            else
            {
                purchasecode.code = purchasecode.code + 1;

                purchasecodesService.Update(purchasecode);
            }

            purchasecodesService.SaveChanges();

            result = year + month + day + purchasecode.code.ToString().PadLeft(3, '0');

            HttpContext.Current.Session.Add("purchaseCode", result);

            return result;
        }

        public static string NewReturnCode(DateTime today)
        {
            IReturnCodesService returncodesService = new ReturnCodesService();

            string year = today.Year.ToString();
            string month = today.Month.ToString().PadLeft(2, '0');
            string day = today.Day.ToString().PadLeft(2, '0');
            string result = string.Empty;

            Returncodes returncode = returncodesService.Get().Where(a => a.year == year && a.month == month && a.day == day).FirstOrDefault();
            if (returncode == null)
            {
                returncode = new Returncodes();
                returncode.returncodeid = Guid.NewGuid();
                returncode.year = year;
                returncode.month = month;
                returncode.day = day;
                returncode.code = 1;

                returncodesService.Create(returncode);
            }
            else
            {
                returncode.code = returncode.code + 1;

                returncodesService.Update(returncode);
            }

            returncodesService.SaveChanges();

            result = year + month + day + returncode.code.ToString().PadLeft(3, '0');

            HttpContext.Current.Session.Add("returnCode", result);

            return result;
        }

        public static string NewExpenditureCode(DateTime today)
        {
            IExpenditureCodesService expenditurecodesService = new ExpenditureCodesService();

            string year = today.Year.ToString();
            string month = today.Month.ToString().PadLeft(2, '0');
            string day = today.Day.ToString().PadLeft(2, '0');
            string result = string.Empty;

            Expenditurecodes expenditurecode = expenditurecodesService.Get().Where(a => a.year == year && a.month == month && a.day == day).FirstOrDefault();
            if (expenditurecode == null)
            {
                expenditurecode = new Expenditurecodes();
                expenditurecode.expenditurecodeid = Guid.NewGuid();
                expenditurecode.year = year;
                expenditurecode.month = month;
                expenditurecode.day = day;
                expenditurecode.code = 1;

                expenditurecodesService.Create(expenditurecode);
            }
            else
            {
                expenditurecode.code = expenditurecode.code + 1;

                expenditurecodesService.Update(expenditurecode);
            }

            expenditurecodesService.SaveChanges();

            result = year + month + day + expenditurecode.code.ToString().PadLeft(3, '0');

            HttpContext.Current.Session.Add("expenditureCode", result);

            return result;
        }

        public static string NewOutcomeCode(DateTime today)
        {
            IOutcomeCodesService outcomecodesService = new OutcomeCodesService();

            string year = today.Year.ToString();
            string month = today.Month.ToString().PadLeft(2, '0');
            string day = today.Day.ToString().PadLeft(2, '0');
            string result = string.Empty;

            Outcomecodes outcomecode = outcomecodesService.Get().Where(a => a.year == year && a.month == month && a.day == day).FirstOrDefault();
            if (outcomecode == null)
            {
                outcomecode = new Outcomecodes();
                outcomecode.outcomecodeid = Guid.NewGuid();
                outcomecode.year = year;
                outcomecode.month = month;
                outcomecode.day = day;
                outcomecode.code = 1;

                outcomecodesService.Create(outcomecode);
            }
            else
            {
                outcomecode.code = outcomecode.code + 1;

                outcomecodesService.Update(outcomecode);
            }

            outcomecodesService.SaveChanges();

            result = year + month + day + outcomecode.code.ToString().PadLeft(3, '0');

            HttpContext.Current.Session.Add("outcomeCode", result);

            return result;
        }

        public static string NewIncomeCode(DateTime today)
        {
            IIncomeCodesService incomecodesService = new IncomeCodesService();

            string year = today.Year.ToString();
            string month = today.Month.ToString().PadLeft(2, '0');
            string day = today.Day.ToString().PadLeft(2, '0');
            string result = string.Empty;

            Incomecodes incomecode = incomecodesService.Get().Where(a => a.year == year && a.month == month && a.day == day).FirstOrDefault();
            if (incomecode == null)
            {
                incomecode = new Incomecodes();
                incomecode.incomecodeid = Guid.NewGuid();
                incomecode.year = year;
                incomecode.month = month;
                incomecode.day = day;
                incomecode.code = 1;

                incomecodesService.Create(incomecode);
            }
            else
            {
                incomecode.code = incomecode.code + 1;

                incomecodesService.Update(incomecode);
            }

            incomecodesService.SaveChanges();

            result = year + month + day + incomecode.code.ToString().PadLeft(3, '0');

            HttpContext.Current.Session.Add("incomeCode", result);

            return result;
        }

        public static string NewRefoundCode(DateTime today)
        {
            IRefoundCodesService refoundcodesService = new RefoundCodesService();

            string year = today.Year.ToString();
            string month = today.Month.ToString().PadLeft(2, '0');
            string day = today.Day.ToString().PadLeft(2, '0');
            string result = string.Empty;

            Refoundcodes refoundcode = refoundcodesService.Get().Where(a => a.year == year && a.month == month && a.day == day).FirstOrDefault();
            if (refoundcode == null)
            {
                refoundcode = new Refoundcodes();
                refoundcode.refoundcodeid = Guid.NewGuid();
                refoundcode.year = year;
                refoundcode.month = month;
                refoundcode.day = day;
                refoundcode.code = 1;

                refoundcodesService.Create(refoundcode);
            }
            else
            {
                refoundcode.code = refoundcode.code + 1;

                refoundcodesService.Update(refoundcode);
            }

            refoundcodesService.SaveChanges();

            result = year + month + day + refoundcode.code.ToString().PadLeft(3, '0');

            HttpContext.Current.Session.Add("refoundcode", result);

            return result;
        }

        public static string NewInvoiceCode(DateTime today)
        {
            IInvoiceCodesService invoicecodesService = new InvoiceCodesService();

            string year = today.Year.ToString();
            string month = today.Month.ToString().PadLeft(2, '0');
            string day = today.Day.ToString().PadLeft(2, '0');
            string result = string.Empty;

            Invoicecodes invoicecode = invoicecodesService.Get().Where(a => a.year == year && a.month == month && a.day == day).FirstOrDefault();
            if (invoicecode == null)
            {
                invoicecode = new Invoicecodes();
                invoicecode.invoicecodeid = Guid.NewGuid();
                invoicecode.year = year;
                invoicecode.month = month;
                invoicecode.day = day;
                invoicecode.code = 1;

                invoicecodesService.Create(invoicecode);
            }
            else
            {
                invoicecode.code = invoicecode.code + 1;

                invoicecodesService.Update(invoicecode);
            }

            invoicecodesService.SaveChanges();

            result = year + month + day + invoicecode.code.ToString().PadLeft(3, '0');

            HttpContext.Current.Session.Add("invoicecode", result);

            return result;
        }

        // 檢查庫存
        public static CheckInventoryResponse CheckInventory(Orders order, Guid warehouseid)
        {
            IList<CheckInventoryResponse> checkInventoryResponses = new List<CheckInventoryResponse>();

            IWarehouseStocksService warehousestocksService = new WarehouseStocksService();
            IProductsService productsService = new ProductsService();

            foreach (Orderdetails od in order.Orderdetails)
            {
                CheckInventoryResponse checkInventoryResponse = new CheckInventoryResponse();

                int needqty;

                Products product = productsService.GetByID(od.productid);
                checkInventoryResponse.productid = od.productid;

                if (product.isset)
                {
                    foreach (Setproducts sp in product.Setproducts)
                    {
                        needqty = sp.qty * od.qty;

                        IEnumerable<Warehousestocks> whss = warehousestocksService.GetStockWarehouses(warehouseid, sp.productid);
                        foreach (Warehousestocks whs in whss)
                        {
                            if (Convert.ToInt32(needqty) <= Convert.ToInt32(whs.quantity_left))
                            {
                                needqty = 0;
                                break;
                            }
                            else if (Convert.ToInt32(needqty) > Convert.ToInt32(whs.quantity_left))
                            {
                                needqty = needqty - whs.quantity_left;
                            }
                        }

                        if (needqty == 0)
                        {
                            checkInventoryResponse.code = "200";
                        }
                        else
                        {
                            checkInventoryResponse.code = "201";
                            checkInventoryResponse.message = product.title + " - " + sp.productid + " 庫存不足";
                        }

                        checkInventoryResponses.Add(checkInventoryResponse);
                    }
                }
                else
                {
                    needqty = od.qty;

                    IEnumerable<Warehousestocks> whss = warehousestocksService.GetStockWarehouses(warehouseid, od.productid);
                    foreach (Warehousestocks whs in whss)
                    {
                        if (Convert.ToInt32(needqty) <= Convert.ToInt32(whs.quantity_left))
                        {
                            needqty = 0;
                            break;
                        }
                        else if (Convert.ToInt32(needqty) > Convert.ToInt32(whs.quantity_left))
                        {
                            needqty = needqty - whs.quantity_left;
                        }
                    }

                    if(needqty == 0)
                    {
                        checkInventoryResponse.code = "200";
                    }
                    else
                    {
                        checkInventoryResponse.code = "201";
                        checkInventoryResponse.message = product.title + " 庫存不足 " + needqty;
                    }

                    checkInventoryResponses.Add(checkInventoryResponse);
                }
            }

            CheckInventoryResponse check = new CheckInventoryResponse();
            IEnumerable<CheckInventoryResponse> res = checkInventoryResponses.Where(a => a.code == "201");
            if(res.Count() == 0)
            {
                check.code = "200";
            }
            else
            {
                StringBuilder sb = new StringBuilder();
                foreach(CheckInventoryResponse c in res)
                {
                    sb.AppendLine(c.message);
                }

                check.code = "201";
                check.message = sb.ToString();
            }

            return check;
        }

        // 扣上架數
        public static void SetAdded(Orders order)
        {
            IProductsService productsService = new ProductsService();

            foreach (Orderdetails orderdetail in order.Orderdetails)
            {
                int qty = orderdetail.qty;

                Products product = productsService.GetByID(orderdetail.productid);

                if (product.isset)
                {
                    foreach (Setproducts sp in product.Setproducts)
                    {
                        Products pd = productsService.GetByID(sp.productid);
                        if (pd.added >= qty)
                        {
                            pd.added = pd.added - qty;
                        }
                        else
                        {
                            pd.added = 0;
                        }

                        productsService.Update(pd);
                        productsService.SaveChanges();
                    }
                }
                else
                {
                    if (product.added >= qty)
                    {
                        product.added = product.added - qty;
                    }
                    else
                    {
                        product.added = 0;
                    }

                    productsService.Update(product);
                    productsService.SaveChanges();
                }
            }
        }

        // 扣庫存
        public static void SetInventory(Orders order, Guid warehouseid)
        {
            IOrdersService ordersService = new OrdersService();
            IWarehouseStocksService warehousestocksService = new WarehouseStocksService();
            IOrderDetailStocksService orderdetailstocksService = new OrderDetailStocksService();
            IProductsService productsService = new ProductsService();

            foreach (Orderdetails od in order.Orderdetails)
            {
                int needqty;

                Products product = productsService.GetByID(od.productid);

                if (product.isset)
                {
                    foreach (Setproducts sp in product.Setproducts)
                    {
                        needqty = sp.qty * od.qty;

                        IEnumerable<Warehousestocks> whss = warehousestocksService.GetStockWarehouses(warehouseid, sp.productid);
                        //列舉工作中的集合資料，不能直接修改，需加上ToArray()方式將 value 集合複製一份出來
                        foreach (Warehousestocks whs in whss.ToArray())
                        {
                            Orderdetailstocks orderdetailstock = whs.Orderdetailstocks.FirstOrDefault(a => a.orderdetailid == od.orderdetailid);

                            if (Convert.ToInt32(needqty) <= Convert.ToInt32(whs.quantity_left))
                            {
                                if (orderdetailstock == null)
                                {
                                    Orderdetailstocks ods = new Orderdetailstocks();
                                    ods.orderdetailstockid = Guid.NewGuid();
                                    ods.orderdetailid = od.orderdetailid;
                                    ods.qty = needqty;
                                    ods.createdate = DateTime.Now;

                                    whs.Orderdetailstocks.Add(ods);
                                }

                                whs.quantity_left = whs.quantity_left - needqty;
                                warehousestocksService.Update(whs);
                                warehousestocksService.SaveChanges();

                                break;
                            }
                            else if(Convert.ToInt32(needqty) > Convert.ToInt32(whs.quantity_left))
                            {
                                if (orderdetailstock == null)
                                {
                                    Orderdetailstocks ods = new Orderdetailstocks();
                                    ods.orderdetailstockid = Guid.NewGuid();
                                    ods.orderdetailid = od.orderdetailid;
                                    ods.qty = whs.quantity_left;
                                    ods.createdate = DateTime.Now;

                                    whs.Orderdetailstocks.Add(ods);
                                }

                                needqty = needqty - whs.quantity_left;
                                whs.quantity_left = 0;
                                warehousestocksService.Update(whs);
                                warehousestocksService.SaveChanges();
                            }
                        }
                    }
                }
                else
                {
                    needqty = od.qty;

                    IEnumerable<Warehousestocks> whss = warehousestocksService.GetStockWarehouses(warehouseid, product.productid);
                    //列舉工作中的集合資料，不能直接修改，需加上ToArray()方式將 value 集合複製一份出來
                    foreach (Warehousestocks whs in whss.ToArray())
                    {
                        Orderdetailstocks orderdetailstock = whs.Orderdetailstocks.FirstOrDefault(a => a.orderdetailid == od.orderdetailid);

                        if (Convert.ToInt32(needqty) <= Convert.ToInt32(whs.quantity_left))
                        {
                            if(orderdetailstock == null)
                            {
                                Orderdetailstocks ods = new Orderdetailstocks();
                                ods.orderdetailstockid = Guid.NewGuid();
                                ods.orderdetailid = od.orderdetailid;
                                ods.qty = needqty;
                                ods.createdate = DateTime.Now;

                                whs.Orderdetailstocks.Add(ods);
                            }

                            whs.quantity_left = whs.quantity_left - needqty;
                            warehousestocksService.Update(whs);
                            warehousestocksService.SaveChanges();

                            break;
                        }
                        else if(Convert.ToInt32(needqty) > Convert.ToInt32(whs.quantity_left))
                        {
                            if (orderdetailstock == null)
                            {
                                Orderdetailstocks ods = new Orderdetailstocks();
                                ods.orderdetailstockid = Guid.NewGuid();
                                ods.orderdetailid = od.orderdetailid;
                                ods.qty = whs.quantity_left;
                                ods.createdate = DateTime.Now;

                                whs.Orderdetailstocks.Add(ods);
                            }

                            needqty = needqty - whs.quantity_left;
                            whs.quantity_left = 0;
                            warehousestocksService.Update(whs);
                            warehousestocksService.SaveChanges();
                        }
                    }
                }
            }
        }

        // 入庫存
        public static void ReturnInventory(Returns rt)
        {
            IWarehouseStocksService warehousestocksService = new WarehouseStocksService();
            IOrderDetailsService orderdetailsService = new OrderDetailsService();

            foreach (Returndetails returndetail in rt.Returndetails)
            {
                if(returndetail.qty > 0)
                {
                    string productid = string.Empty;

                    foreach (Orderdetailstocks ods in returndetail.Orderdetails.Orderdetailstocks.OrderBy(o => o.Warehousestocks.Stocks.Purchasedetails.productid))
                    {
                        if (returndetail.Orderdetails.Products.isset)
                        {
                            if(productid != ods.Warehousestocks.Stocks.Purchasedetails.productid.ToString())
                            {
                                productid = ods.Warehousestocks.Stocks.Purchasedetails.productid.ToString();

                                int tqty = returndetail.Orderdetails.Orderdetailstocks.Where(a => a.Warehousestocks.Stocks.Purchasedetails.productid == ods.Warehousestocks.Stocks.Purchasedetails.productid).Sum(s => s.qty);
                                int spqty = tqty / returndetail.Orderdetails.qty;

                                Warehousestocks warehousestock = new Warehousestocks();
                                warehousestock.warehousestockid = Guid.NewGuid();
                                warehousestock.warehouseid = new Guid("06CDFDD5-1787-49E3-BCE2-FA5AA026E17A");
                                warehousestock.stockid = ods.Warehousestocks.stockid;
                                warehousestock.transdate = DateTime.Now;
                                warehousestock.memo = rt.returncode + " 退貨單";
                                warehousestock.createdate = DateTime.Now;
                                warehousestock.quantity = spqty * returndetail.qty;
                                warehousestock.quantity_left = spqty * returndetail.qty;

                                warehousestocksService.Create(warehousestock);
                            }
                        }
                        else
                        {
                            Warehousestocks warehousestock = new Warehousestocks();
                            warehousestock.warehousestockid = Guid.NewGuid();
                            warehousestock.warehouseid = new Guid("06CDFDD5-1787-49E3-BCE2-FA5AA026E17A");
                            warehousestock.stockid = ods.Warehousestocks.stockid;
                            warehousestock.transdate = DateTime.Now;
                            warehousestock.memo = rt.returncode + " 退貨單";
                            warehousestock.createdate = DateTime.Now;
                            warehousestock.quantity = returndetail.qty;
                            warehousestock.quantity_left = returndetail.qty;

                            warehousestocksService.Create(warehousestock);
                        }
                        
                    }

                    warehousestocksService.SaveChanges();

                    Orderdetails od = returndetail.Orderdetails;
                    od.status = 1;

                    orderdetailsService.Update(od);
                    orderdetailsService.SaveChanges();
                }
            }
        }

        public static string GetPayType(int paytype)
        {
            string rspaytype = string.Empty;

            switch (paytype)
            {
                case (int)EnumPayType.信用卡線上刷卡:
                    rspaytype = "credit";
                    break;
                case (int)EnumPayType.ATM轉帳付款:
                    rspaytype = "atmcode";
                    break;
                case (int)EnumPayType.宅配貨到付款:
                    rspaytype = "delivery";
                    break;
                case (int)EnumPayType.免付款:
                    rspaytype = "nopay";
                    break;
            }

            return rspaytype;
        }

        public static string GetAtmCode(int total, int limit)
        {
            IAtmCodesService atmcodesService = new AtmCodesService();

            //一銀
            //string code = "0040739";
            //國泰
            string code = "1943";
            string year = DateTime.Now.AddDays(limit).Year.ToString();
            string month = DateTime.Now.AddDays(limit).Month.ToString().PadLeft(2, '0');
            string day = DateTime.Now.AddDays(limit).Day.ToString().PadLeft(2, '0');

            Atmcodes atmcode = atmcodesService.Get().Where(a => a.year == year && a.month == month && a.day == day).FirstOrDefault();
            if (atmcode == null)
            {
                atmcode = new Atmcodes();
                atmcode.atmcodeid = Guid.NewGuid();
                atmcode.year = year;
                atmcode.month = month;
                atmcode.day = day;
                atmcode.code = 1;

                atmcodesService.Create(atmcode);
            }
            else
            {
                atmcode.code = atmcode.code + 1;

                atmcodesService.Update(atmcode);
            }

            atmcodesService.SaveChanges();
            
            //一銀
            //code = code + atmcode.code.ToString().PadLeft(3, '0') + month + day;
            //國泰
            code = code + year.Substring(2) + month + day + atmcode.code.ToString().PadLeft(5, '0');

            //一銀
            //return code + GetCheckCode(code, total.ToString().PadLeft(8, '0'));
            //國泰
            return code + GetCheckNumber(code, total.ToString().PadLeft(8, '0'));
        }

        //一銀驗證碼
        private static string GetCheckCode(string code, string pricecode)
        {
            string checkcode = string.Empty;
            string check = code + pricecode;
            
            int total1 = 0;
            int total2 = 0;

            int startindex1 = 1;
            int startindex2 = 0;

            int[] check1 = { 1, 3, 7 };
            int[] check2 = { 8, 7, 6, 5, 4, 3, 2, 1 };

            string[] arr = check.ToStrSplit();

            for (int i = 0; i < arr.Length; i++ )
            {
                int A = Convert.ToInt32(arr[i]);
                int B = Convert.ToInt32(check1[startindex1]);
                int C = Convert.ToInt32(check2[startindex2]);

                total1 = total1 + (A * B);
                total2 = total2 + (A * C);

                if (startindex1 == 2)
                {
                    startindex1 = 0;
                }
                else
                {
                    startindex1++;
                }
                
                if(startindex2 == 7)
                {
                    startindex2 = 0;
                }
                else
                {
                    startindex2++;
                }
            }

            int digit1 = total1 % 10;
            int digit2 = total2 % 10;

            int O = (10 - digit1) % 10;
            int P = (10 - digit2) % 10;

            checkcode = O.ToString() + P.ToString();

            return checkcode;
        }

        //國泰驗證碼
        private static string GetCheckNumber(string atmnumber, string pricetotal)
        {
            string checknumber = string.Empty;

            int cc = 4;
            int xx = 1;
            int AA = 0;

            var ds = atmnumber.ToCharArray();
            for(int k = 0; k < ds.Length; k++)
            {
                int h = Convert.ToInt32(ds[k].ToString());
                if (k < 6)
                {
                    AA = AA + GetDigit(h * cc);
                    cc++;
                }
                else
                {
                    AA = AA + GetDigit(h * xx);
                    xx++;
                }
            }
            int DD = GetDigit(10 - GetDigit(AA));

            int pp = 8;
            int A = 0;

            var ts = pricetotal.ToCharArray();
            for(int k = 0; k < ts.Length; k++)
            {
                int h = Convert.ToInt32(ts[k].ToString());
                A = A + GetDigit(h * pp);
                pp--;
            }
            int BD = GetDigit(10 - GetDigit(A));

            checknumber = GetDigit(DD + BD).ToString();

            return checknumber;
        }

        private static int GetDigit(int n)
        {
            return n % 10;
        }

        public static void SendMail(string sAdd, string sFrom, string sSubject, string sBody, string[] img = null)
        {
            try
            {
                string myMailEncoding = "utf-8";
                MailMessage mail = new MailMessage();
                //前面是發信email後面是顯示的名稱
                mail.From = new MailAddress("noreply@tfoodies.com", sFrom);
                //收信者email

                //mail.To.Add(sAdd);

                string[] emaillist = sAdd.Split(',');
                for (int i = 0; i < emaillist.Length; i++)
                {
                    mail.Bcc.Add(emaillist[i]);
                }

                MailAddress bcc = new MailAddress("hi@tfoodies.com");
                mail.Bcc.Add(bcc);
                MailAddress bcc1 = new MailAddress("angela@tfoodies.com");
                mail.Bcc.Add(bcc1);

                //設定優先權
                mail.Priority = MailPriority.Normal;
                //標題
                mail.Subject = sSubject;
                //內容
                mail.Body = sBody;
                //內容使用html
                mail.IsBodyHtml = true;
                //設定gmail的smtp
                SmtpClient MySmtp = new SmtpClient("smtp-relay.sendinblue.com", 587);
                //您在gmail的帳號密碼
                MySmtp.Credentials = new NetworkCredential("tim@weypro.com", "PzLpHKXgI9xm7EJ3");

                if (img != null)
                {
                    foreach (var item in img)
                    {
                        // 設定附件檔案(Attachment)
                        System.Net.Mail.Attachment attachment1 = new System.Net.Mail.Attachment(item);
                        attachment1.Name = System.IO.Path.GetFileName(item);
                        attachment1.NameEncoding = Encoding.GetEncoding(myMailEncoding);
                        attachment1.TransferEncoding = System.Net.Mime.TransferEncoding.Base64;

                        // 設定該附件為一個內嵌附件(Inline Attachment)
                        attachment1.ContentDisposition.Inline = true;
                        attachment1.ContentDisposition.DispositionType = System.Net.Mime.DispositionTypeNames.Inline;

                        mail.Attachments.Add(attachment1);
                    }
                }

                //開啟ssl
                //MySmtp.EnableSsl = true;
                //發送郵件
                MySmtp.Send(mail);
                //放掉宣告出來的MySmtp
                MySmtp = null;
                //放掉宣告出來的mail
                mail.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                SendMail(sAdd, sFrom, sSubject, sBody, img);
            }
        }

        public static string ConvertUTF8toBIG5(string strInput)
        {
            byte[] strut8 = Encoding.Unicode.GetBytes(strInput);
            byte[] strbig5 = Encoding.Convert(Encoding.Unicode, Encoding.Default, strut8);
            return Encoding.Default.GetString(strbig5);
        }
    }
}
