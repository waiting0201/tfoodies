using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using PagedList;
using System.IO;
using tfoodiesBackend.Filters;
using tfoodiesBackend.Commons;
using tfoodies.Libs;
using tfoodies.Models;
using tfoodies.Service;
using System.Data.Entity.Validation;
using NPOI;
using NPOI.HSSF.UserModel;
using NPOI.XSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.SS.Util;

namespace tfoodiesBackend.Controllers
{
    public class OrderMsController : BaseController
    {
        private IOrdersService ordersService;
        private IOrderDetailsService orderdetailsService;
        private IMembersService membersService;
        private IZipcodesService zipcodesService;
        private ILogisticsService logisticsService;
        private IDeclarationsService declarationsService;
        private IWarehousesService warehousesService;
        private IOutofnoticesService outofnoticesService;
        private IWarehouseStocksService warehousestocksService;
        private IReturnsService returnsService;
        private IReturnDetailsService returndetailsService;
        private IInvoiceDetailsService invoicedetailService;

        private string lovecode = "01170";

        public OrderMsController()
        {
            tfoodiesEntities db = new tfoodiesEntities();
            ordersService = new OrdersService(db);
            orderdetailsService = new OrderDetailsService(db);
            membersService = new MembersService();
            zipcodesService = new ZipcodesService();
            logisticsService = new LogisticsService();
            declarationsService = new DeclarationsService();
            warehousesService = new WarehousesService();
            outofnoticesService = new OutofnoticesService();
            warehousestocksService = new WarehouseStocksService();
            returnsService = new ReturnsService(db);
            returndetailsService = new ReturnDetailsService(db);
            invoicedetailService = new InvoiceDetailsService();
        }

        [CheckSession(IsAuth = true)]
        public ActionResult Logistics()
        {
            ViewBag.Logistics = logisticsService.Get();
            return View();
        }

        [CheckSession(IsAuth = true)]
        public ActionResult AddLogistics()
        {
            return View();
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult AddLogistics(Logistics logistic)
        {
            if (TryUpdateModel(logistic, new string[] { "logisticcode", "title", "address", "phone", "isenable" }) && ModelState.IsValid)
            {
                logistic.logisticid = Guid.NewGuid();

                logisticsService.Create(logistic);
                logisticsService.SaveChanges();

                return RedirectToAction("Logistics");
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                return View(logistic);
            }
        }

        [CheckSession(IsAuth = true)]
        public ActionResult EditLogistics(Guid logisticid)
        {
            Logistics logistic = logisticsService.GetByID(logisticid);
            return View(logistic);
        }

        [CheckSession(IsAuth = true)]
        public ActionResult EditLogistics(Guid logisticid, FormCollection form)
        {
            Logistics logistic = logisticsService.GetByID(logisticid);

            if (TryUpdateModel(logistic, new string[] { "logisticcode", "title", "address", "phone", "isenable" }) && ModelState.IsValid)
            {
                logisticsService.Update(logistic);
                logisticsService.SaveChanges();

                return RedirectToAction("Logistics");
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                return View(logistic);
            }
        }

        [CheckSession(IsAuth = true)]
        public ActionResult Orders(int p = 1, DateTime? orderdate = null, DateTime? ordermonth = null)
        {
            IEnumerable<Orders> data;

            if (orderdate == null && ordermonth == null)
            {
                data = ordersService.Get().Where(a => a.deliverstatus == (int)EnumDeliverStatus.未出貨).OrderByDescending(a => a.orderdate).ThenByDescending(a => a.ordercode);
            }
            else if (orderdate != null && ordermonth == null)
            {
                data = ordersService.Get().Where(a => a.orderdate == orderdate && a.deliverstatus == (int)EnumDeliverStatus.未出貨).OrderByDescending(a => a.orderdate).ThenByDescending(a => a.ordercode);
            }
            else if (ordermonth != null && orderdate == null)
            {
                data = ordersService.Get().Where(a => a.orderdate.Year == ordermonth.Value.Year && a.orderdate.Month == ordermonth.Value.Month && a.deliverstatus == (int)EnumDeliverStatus.未出貨).OrderByDescending(a => a.orderdate).ThenByDescending(a => a.ordercode);
            }
            else
            {
                data = ordersService.Get().Where(a => a.orderdate.Year == ordermonth.Value.Year && a.orderdate.Month == ordermonth.Value.Month && a.deliverstatus == (int)EnumDeliverStatus.未出貨).OrderByDescending(a => a.orderdate).ThenByDescending(a => a.ordercode);
            }

            ViewBag.pageNumber = p;
            ViewBag.orderdate = orderdate;
            ViewBag.ordermonth = ordermonth;
            ViewBag.Orders = data.ToPagedList(pageNumber: p, pageSize: 20);
            return View();
        }

        [CheckSession]
        public ActionResult OrdersExport(DateTime? orderdate = null, DateTime? ordermonth = null)
        {
            IEnumerable<Orders> datas;

            if (orderdate == null && ordermonth == null)
            {
                datas = ordersService.Get().Where(a => a.deliverstatus == (int)EnumDeliverStatus.未出貨).OrderByDescending(a => a.orderdate).ThenByDescending(a => a.ordercode);
            }
            else if (orderdate != null && ordermonth == null)
            {
                datas = ordersService.Get().Where(a => a.orderdate == orderdate && a.deliverstatus == (int)EnumDeliverStatus.未出貨).OrderByDescending(a => a.orderdate).ThenByDescending(a => a.ordercode);
            }
            else if (ordermonth != null && orderdate == null)
            {
                datas = ordersService.Get().Where(a => a.orderdate.Year == ordermonth.Value.Year && a.orderdate.Month == ordermonth.Value.Month && a.deliverstatus == (int)EnumDeliverStatus.未出貨).OrderByDescending(a => a.orderdate).ThenByDescending(a => a.ordercode);
            }
            else
            {
                datas = ordersService.Get().Where(a => a.orderdate.Year == ordermonth.Value.Year && a.orderdate.Month == ordermonth.Value.Month && a.deliverstatus == (int)EnumDeliverStatus.未出貨).OrderByDescending(a => a.orderdate).ThenByDescending(a => a.ordercode);
            }

            IWorkbook wBook = new HSSFWorkbook();
            ISheet wSheet = wBook.CreateSheet();

            wSheet.AddMergedRegion(new CellRangeAddress(0, 0, 9, 12));

            wSheet.CreateRow(0);
            wSheet.GetRow(0).CreateCell(0).SetCellValue("訂單日期");
            wSheet.GetRow(0).CreateCell(1).SetCellValue("訂單編號");
            wSheet.GetRow(0).CreateCell(2).SetCellValue("訂單類型");
            wSheet.GetRow(0).CreateCell(3).SetCellValue("收件人");
            wSheet.GetRow(0).CreateCell(4).SetCellValue("電話");
            wSheet.GetRow(0).CreateCell(5).SetCellValue("付款方式");
            wSheet.GetRow(0).CreateCell(6).SetCellValue("發票類型");
            wSheet.GetRow(0).CreateCell(7).SetCellValue("發票號碼");
            wSheet.GetRow(0).CreateCell(8).SetCellValue("物流");

            wSheet.GetRow(0).CreateCell(9).SetCellValue("購買品項");

            wSheet.GetRow(0).CreateCell(13).SetCellValue("總金額");
            wSheet.GetRow(0).CreateCell(14).SetCellValue("出貨倉");
            wSheet.GetRow(0).CreateCell(15).SetCellValue("出貨狀態");
            wSheet.GetRow(0).CreateCell(16).SetCellValue("備註");

            int rowIdx = 2;
            int orderIdx = 1;
            foreach (Orders order in datas)
            {
                IRow urow = wSheet.CreateRow(orderIdx);

                urow.CreateCell(0).SetCellValue(order.orderdate.ToString("yyyy-MM-dd"));
                urow.CreateCell(1).SetCellValue(order.ordercode);
                urow.CreateCell(2).SetCellValue(Enum.GetName(typeof(EnumOrderType), order.ordertype));
                urow.CreateCell(3).SetCellValue(order.recivername);
                urow.CreateCell(4).SetCellValue(order.recivermobile);
                urow.CreateCell(5).SetCellValue(Enum.GetName(typeof(EnumPayType), order.paytype));
                urow.CreateCell(6).SetCellValue(Enum.GetName(typeof(EnumInvoiceType), order.invoicetype));
                urow.CreateCell(7).SetCellValue(order.invoicecode);
                urow.CreateCell(8).SetCellValue(order.Logistics.title);
                urow.CreateCell(9).SetCellValue("產品");
                urow.CreateCell(10).SetCellValue("數量");
                urow.CreateCell(11).SetCellValue("價格");
                urow.CreateCell(12).SetCellValue("折扣數");
                urow.CreateCell(13).SetCellValue(order.total);
                urow.CreateCell(14).SetCellValue(order.Warehouses.title);
                urow.CreateCell(15).SetCellValue(Enum.GetName(typeof(EnumDeliverStatus), order.deliverstatus));
                urow.CreateCell(16).SetCellValue(order.note);

                foreach (Orderdetails orderdetail in order.Orderdetails)
                {
                    wSheet.AddMergedRegion(new CellRangeAddress(rowIdx, rowIdx, 0, 8));
                    wSheet.AddMergedRegion(new CellRangeAddress(rowIdx, rowIdx, 13, 16));

                    IRow drow = wSheet.CreateRow(rowIdx);

                    int r = 1;
                    string s = orderdetail.Products.title + (orderdetail.isgift == 1 ? " (贈品)" : "");
                    if (orderdetail.Products.isset)
                    {
                        foreach(Setproducts setproduct in orderdetail.Products.Setproducts)
                        {
                            s = s + "\n" + setproduct.Products1.title + " " + setproduct.Products1.capacity + " " + setproduct.qty;
                            r++;
                        }
                    }
                    drow.CreateCell(9).SetCellValue(s);
                    if (s.Contains("\n"))
                    {
                        ICellStyle cs = wBook.CreateCellStyle();
                        cs.WrapText = true;
                        drow.GetCell(9).CellStyle = cs;

                        drow.HeightInPoints = r * wSheet.DefaultRowHeight / 20;
                    }

                    drow.CreateCell(10).SetCellValue(orderdetail.qty);
                    drow.CreateCell(11).SetCellValue(orderdetail.price);
                    drow.CreateCell(12).SetCellValue((orderdetail.discount != null) ? orderdetail.discount + " 折" : "-");

                    rowIdx++;
                    orderIdx++;
                }

                rowIdx++;
                orderIdx++;
            }

            MemoryStream ms = new MemoryStream();
            wBook.Write(ms);

            return File(ms.ToArray(), "application/vnd.ms-excel", DateTime.Now.ToString("yyyyMMdd") + "export.xls");
        }

        [CheckSession(IsAuth = true)]
        public ActionResult AddOrders(int p)
        {
            ViewBag.Citys = zipcodesService.Get().Select(g => g.city).Distinct();
            ViewBag.pageNumber = p;
            ViewBag.Members = membersService.Get().OrderBy(a => a.memberid);

            LogisticDropDownList();
            WarehousesDropDownList();

            return View();
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult AddOrders(Orders order, int p)
        {
            if (TryUpdateModel(order, new string[] { "ordertype", "warehouseid", "paystatus", "paydate", "deliverstatus", "deliverdate", "memberid", "orderdate", "recivername", "recivermobile", "reciverzipcodeid", "reciveraddress", "recivertime", "invoicetype", "invoicecode", "companytitle", "companynumber", "paytype", "freight", "discount", "total", "note", "logisticid", "trackingnumber" }) && ModelState.IsValid)
            {
                try
                {
                    order.orderid = Guid.NewGuid();
                    order.ordercode = Librarys.NewOrderCode(DateTime.Now);
                    order.createdate = DateTime.Now;
                    //order.discount = 0;

                    if(order.invoicetype == (int)EnumInvoiceType.捐贈)
                    {
                        order.lovecode = lovecode;
                    }

                    foreach (Orderdetails orderdetail in order.Orderdetails)
                    {
                        orderdetail.orderdetailid = Guid.NewGuid();
                    }

                    ordersService.Create(order);
                    ordersService.SaveChanges();

                    return RedirectToAction("Orders", new { p = p });
                }
                catch (DbEntityValidationException ex)
                {
                    foreach (var eve in ex.EntityValidationErrors)
                    {
                        Console.WriteLine("Entity of type \"{0}\" in state \"{1}\" has the following validation errors:", eve.Entry.Entity.GetType().Name, eve.Entry.State);
                        foreach (var ve in eve.ValidationErrors)
                        {
                            Console.WriteLine("- Property: \"{0}\", Error: \"{1}\"", ve.PropertyName, ve.ErrorMessage);
                        }
                    }
                    throw;
                }
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.Citys = zipcodesService.Get().Select(g => g.city).Distinct();
                ViewBag.pageNumber = p;
                ViewBag.Members = membersService.Get().OrderBy(a => a.memberid);

                LogisticDropDownList();
                WarehousesDropDownList();

                return View(order);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult EditOrders(Guid entityid, int p)
        {
            ViewBag.pageNumber = p;
            Orders order = ordersService.GetByID(entityid);

            LogisticDropDownList(order.logisticid);
            WarehousesDropDownList(order.warehouseid);

            return View(order);
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult EditOrders(Guid orderid, int p, ICollection<Orderdetails> orderdetails)
        {
            Orders order = ordersService.GetByID(orderid);

            if (TryUpdateModel(order, new string[] { "ordertype", "paystatus", "warehouseid", "paydate", "paytype", "deliverstatus", "deliverdate", "invoicetype", "invoicecode", "companytitle", "companynumber", "logisticid", "freight", "discount", "total", "note", "trackingnumber", "lastpan4" }) && ModelState.IsValid)
            {
                try
                {
                    //order.discount = 0;

                    if(order.invoicetype == (int)EnumInvoiceType.捐贈)
                    {
                        order.lovecode = lovecode;
                    }
                    else
                    {
                        order.lovecode = null;
                    }

                    foreach (Orderdetails rg in order.Orderdetails.ToArray())
                    {
                        if (!orderdetails.ToList().Exists(x => x.orderdetailid == rg.orderdetailid))
                        {
                            order.Orderdetails.Remove(rg);
                            orderdetailsService.Delete(rg.orderdetailid);
                        }
                    }

                    foreach (Orderdetails orderdetail in orderdetails)
                    {
                        if (order.Orderdetails.ToList().Exists(x => x.orderdetailid == orderdetail.orderdetailid))
                        {
                            Orderdetails od = order.Orderdetails.Where(a => a.orderdetailid == orderdetail.orderdetailid).FirstOrDefault();
                            od.productid = orderdetail.productid;
                            od.qty = orderdetail.qty;
                            od.discount = orderdetail.discount;
                            od.price = orderdetail.price;
                            od.subtotal = orderdetail.subtotal;
                            od.status = orderdetail.status;
                            od.isgift = orderdetail.isgift;
                        }
                        else
                        {
                            orderdetail.orderdetailid = Guid.NewGuid();
                            orderdetail.orderid = order.orderid;
                            order.Orderdetails.Add(orderdetail);
                        }
                    }

                    ordersService.Update(order);
                    ordersService.SaveChanges();

                    Invoicedetails ivds = invoicedetailService.Get().FirstOrDefault(a => a.orderid == order.orderid);
                    if(ivds != null)
                    {
                        int TotalAmt = order.total + order.freight - Convert.ToInt32(order.discount);
                        int Amt = Convert.ToInt32(Math.Round(TotalAmt / 1.05, MidpointRounding.AwayFromZero));
                        int TaxAmt = TotalAmt - Amt;

                        ivds.price = TotalAmt;
                        ivds.tax = TaxAmt;

                        invoicedetailService.Update(ivds);
                        invoicedetailService.SaveChanges();
                    }

                    return RedirectToAction("Orders", new { p = p });
                }
                catch (DbEntityValidationException ex)
                {
                    foreach (var eve in ex.EntityValidationErrors)
                    {
                        Console.WriteLine("Entity of type \"{0}\" in state \"{1}\" has the following validation errors:", eve.Entry.Entity.GetType().Name, eve.Entry.State);
                        foreach (var ve in eve.ValidationErrors)
                        {
                            Console.WriteLine("- Property: \"{0}\", Error: \"{1}\"", ve.PropertyName, ve.ErrorMessage);
                        }
                    }
                    throw;
                }
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;

                LogisticDropDownList(order.logisticid);
                WarehousesDropDownList(order.warehouseid);

                return View(order);
            }
        }

        [HttpGet]
        public ActionResult ExportOrders(Guid orderid)
        {
            Orders order = ordersService.GetByID(orderid);
            string filepath = Server.MapPath("~/Template/deliver.xlsx");

            FileStream excel = new FileStream(filepath, FileMode.Open, FileAccess.ReadWrite);
            IWorkbook wBook = new XSSFWorkbook(excel);
            ISheet wSheet = wBook.GetSheetAt(0);

            IRow row5 = wSheet.GetRow(5);
            row5.GetCell(7).SetCellValue(Convert.ToDateTime(order.deliverdate).ToString("yyyy-MM-dd"));

            IRow row6 = wSheet.GetRow(6);
            row6.GetCell(1).SetCellValue(order.ordercode);
            row6.GetCell(4).SetCellValue(order.Members.name);

            IRow row7 = wSheet.GetRow(7);
            row7.GetCell(1).SetCellValue(order.orderdate.ToString("yyyy-MM-dd"));
            row7.GetCell(4).SetCellType(CellType.String);
            row7.GetCell(4).SetCellValue(order.Members.mobile);

            IRow row8 = wSheet.GetRow(8);
            row8.GetCell(1).SetCellValue((order.paydate != null) ? Convert.ToDateTime(order.paydate).ToString("yyyy-MM-dd") : "");
            row8.GetCell(4).SetCellValue(order.Zipcodes.city + order.Zipcodes.area + order.reciveraddress);

            IRow row9 = wSheet.GetRow(9);
            row9.GetCell(1).SetCellValue(Enum.GetName(typeof(EnumPayType), order.paytype));

            IRow row10 = wSheet.GetRow(10);
            row10.GetCell(1).SetCellValue((order.invoicetype == (int)EnumInvoiceType.捐贈) ? "捐贈" : order.invoicecode);
            row10.GetCell(4).SetCellValue(order.Members.email);

            int rowindex = 15;
            foreach (Orderdetails od in order.Orderdetails.OrderBy(o => o.isgift))
            {
                IRow row = wSheet.GetRow(rowindex);
                row.GetCell(1).SetCellValue(od.Products.title);
                row.GetCell(3).SetCellValue(od.qty);
                row.GetCell(4).SetCellValue(od.Products.fixprice.ToString());
                row.GetCell(5).SetCellValue(od.discount + "折");
                row.GetCell(6).SetCellValue(od.price);
                row.GetCell(7).SetCellValue(od.subtotal);

                rowindex++;
            }

            IRow row29 = wSheet.GetRow(29);
            row29.GetCell(7).SetCellValue(order.total);

            IRow row30 = wSheet.GetRow(30);
            row30.GetCell(7).SetCellValue(order.freight);

            IRow row31 = wSheet.GetRow(31);
            row31.GetCell(7).SetCellValue(Convert.ToInt32(order.discount));

            IRow row32 = wSheet.GetRow(32);
            row32.GetCell(3).SetCellValue(order.Orderdetails.Sum(s => s.qty));
            row32.GetCell(7).SetCellValue(order.total + order.freight - Convert.ToInt32(order.discount));

            IRow row33 = wSheet.GetRow(33);
            row33.GetCell(0).SetCellValue("備註：" + order.note);

            MemoryStream ms = new MemoryStream();
            wBook.Write(ms);

            return File(ms.ToArray(), "application/vnd.ms-excel", order.ordercode + "oddeliver.xls");
        }

        [CheckSession(IsAuth = true)]
        public ActionResult Outofnotices(int p = 1)
        {
            IEnumerable<Outofnotices> data = outofnoticesService.Get().OrderByDescending(o => o.createdate);

            ViewBag.pageNumber = p;

            ViewBag.Outofnotices = data.ToPagedList(pageNumber: p, pageSize: 20);

            return View();
        }

        [CheckSession(IsAuth = true)]
        public ActionResult Shipments()
        {
            ViewBag.Orders = ordersService.Get().Where(a => a.deliverstatus == (int)EnumDeliverStatus.待出貨).OrderByDescending(o => o.ordercode).ThenByDescending(o => o.orderdate);

            return View();
        }

        [CheckSession]
        public ActionResult ShipmentsExport(Guid[] orderids)
        {
            if(orderids != null)
            {
                IList<PickUp> pickups = new List<PickUp>();

                IEnumerable<Orders> datas = ordersService.Get().Where(a => orderids.Contains(a.orderid)).OrderBy(o => o.ordercode).ThenBy(o => o.orderdate);

                foreach (Orders order in datas)
                {
                    foreach (Orderdetails od in order.Orderdetails)
                    {
                        IEnumerable<Warehousestocks> whss = warehousestocksService.GetStockWarehouses(order.warehouseid, od.productid);
                        foreach (Warehousestocks whs in whss)
                        {
                            PickUp pickup = pickups.Where(a => a.Noticenumber == whs.Stocks.noticenumber).FirstOrDefault();

                            if (pickup == null)
                            {
                                PickUp pu = new PickUp
                                {
                                    RowId = Guid.NewGuid(),
                                    Barcode = whs.Stocks.barcode,
                                    Noticenumber = whs.Stocks.noticenumber,
                                    Expiredate = Convert.ToDateTime(whs.Stocks.expiredate),
                                    Product = whs.Stocks.Purchasedetails.Products.title,
                                    Warehouse = whs.Warehouses.title,
                                    Quantity = (od.qty <= whs.quantity_left) ? od.qty : whs.quantity_left,
                                    Pickupdate = DateTime.Now
                                };

                                pickups.Add(pu);
                            }
                            else
                            {
                                pickup.Quantity = pickup.Quantity + ((od.qty <= whs.quantity_left) ? od.qty : whs.quantity_left);
                            }

                            //庫存足夠就跳出，找下一筆產品
                            if (od.qty <= whs.quantity_left) break;
                        }
                    }
                }

                IWorkbook wBook = new HSSFWorkbook();
                ISheet wSheet = wBook.CreateSheet();

                IRow hrow = wSheet.CreateRow(0);

                hrow.CreateCell(0).SetCellValue("Check");
                hrow.CreateCell(1).SetCellValue("通知編號");
                hrow.CreateCell(2).SetCellValue("條碼");
                hrow.CreateCell(3).SetCellValue("產品名稱");
                hrow.CreateCell(4).SetCellValue("數量");
                hrow.CreateCell(5).SetCellValue("到期日");
                hrow.CreateCell(6).SetCellValue("倉庫");
                hrow.CreateCell(7).SetCellValue("撿貨日");

                int rowIdx = 1;

                foreach(PickUp pu in pickups)
                {
                    IRow urow = wSheet.CreateRow(rowIdx);

                    urow.CreateCell(0).SetCellValue("□");
                    urow.CreateCell(1).SetCellValue(pu.Noticenumber);
                    urow.CreateCell(2).SetCellValue(pu.Barcode);
                    urow.CreateCell(3).SetCellValue(pu.Product);
                    urow.CreateCell(4).SetCellValue(pu.Quantity);
                    urow.CreateCell(5).SetCellValue(pu.Expiredate);
                    urow.CreateCell(6).SetCellValue(pu.Warehouse);
                    urow.CreateCell(7).SetCellValue(pu.Pickupdate.ToShortDateString());

                    rowIdx++;
                }

                MemoryStream ms = new MemoryStream();
                wBook.Write(ms);

                return File(ms.ToArray(), "application/vnd.ms-excel", DateTime.Now.ToString("yyyyMMdd") + "shipment.xls");
            }
            else
            {
                return RedirectToAction("Shipments");
            }
        }

        [CheckSession(IsAuth = true)]
        public ActionResult EditShipments(Guid orderid)
        {
            Orders order = ordersService.GetByID(orderid);

            return View(order);
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult EditShipments(Guid orderid, FormCollection form)
        {
            Orders order = ordersService.GetByID(orderid);

            if (TryUpdateModel(order, new string[] { "deliverstatus", "deliverdate", "paystatus", "paytype", "note", "trackingnumber" }) && ModelState.IsValid)
            {
                try
                {
                    ordersService.Update(order);
                    ordersService.SaveChanges();

                    return RedirectToAction("Shipments");
                }
                catch (DbEntityValidationException ex)
                {
                    foreach (var eve in ex.EntityValidationErrors)
                    {
                        Console.WriteLine("Entity of type \"{0}\" in state \"{1}\" has the following validation errors:", eve.Entry.Entity.GetType().Name, eve.Entry.State);
                        foreach (var ve in eve.ValidationErrors)
                        {
                            Console.WriteLine("- Property: \"{0}\", Error: \"{1}\"", ve.PropertyName, ve.ErrorMessage);
                        }
                    }
                    throw;
                }
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");

                return View(order);
            }
        }

        [HttpGet]
        public ActionResult ExportDeliver(Guid orderid)
        {
            Orders order = ordersService.GetByID(orderid);
            string filepath = Server.MapPath("~/Template/deliver.xlsx");

            FileStream excel = new FileStream(filepath, FileMode.Open, FileAccess.ReadWrite);
            IWorkbook wBook = new XSSFWorkbook(excel);
            ISheet wSheet = wBook.GetSheetAt(0);

            IRow row5 = wSheet.GetRow(5);
            row5.GetCell(7).SetCellValue(Convert.ToDateTime(order.deliverdate).ToString("yyyy-MM-dd"));

            IRow row6 = wSheet.GetRow(6);
            row6.GetCell(1).SetCellValue(order.ordercode);
            row6.GetCell(4).SetCellValue(order.Members.name);

            IRow row7 = wSheet.GetRow(7);
            row7.GetCell(1).SetCellValue(order.orderdate.ToString("yyyy-MM-dd"));
            row7.GetCell(4).SetCellType(CellType.String);
            row7.GetCell(4).SetCellValue(order.Members.mobile);

            IRow row8 = wSheet.GetRow(8);
            row8.GetCell(1).SetCellValue((order.paydate!=null)?Convert.ToDateTime(order.paydate).ToString("yyyy-MM-dd"):"");
            row8.GetCell(4).SetCellValue(order.Zipcodes.city + order.Zipcodes.area + order.reciveraddress);

            IRow row9 = wSheet.GetRow(9);
            row9.GetCell(1).SetCellValue(Enum.GetName(typeof(EnumPayType), order.paytype));

            IRow row10 = wSheet.GetRow(10);
            row10.GetCell(1).SetCellValue(order.invoicecode);
            row10.GetCell(4).SetCellValue(order.Members.email);

            int rowindex = 15;
            foreach(Orderdetails od in order.Orderdetails.OrderBy(o => o.isgift))
            {
                IRow row = wSheet.GetRow(rowindex);
                row.GetCell(1).SetCellValue(od.Products.title);
                row.GetCell(3).SetCellValue(od.qty);
                row.GetCell(4).SetCellValue(od.Products.fixprice.ToString());
                row.GetCell(5).SetCellValue(od.discount + "折");
                row.GetCell(6).SetCellValue(od.price);
                row.GetCell(7).SetCellValue(od.subtotal);

                rowindex++;
            }
            
            IRow row29 = wSheet.GetRow(29);
            row29.GetCell(7).SetCellValue(order.total);

            IRow row30 = wSheet.GetRow(30);
            row30.GetCell(7).SetCellValue(order.freight);

            IRow row31 = wSheet.GetRow(31);
            row31.GetCell(7).SetCellValue(Convert.ToInt32(order.discount));

            IRow row32 = wSheet.GetRow(32);
            row32.GetCell(3).SetCellValue(order.Orderdetails.Sum(s => s.qty));
            row32.GetCell(7).SetCellValue(order.total + order.freight - Convert.ToInt32(order.discount));

            IRow row33 = wSheet.GetRow(33);
            row33.GetCell(0).SetCellValue("備註：" + order.note);

            MemoryStream ms = new MemoryStream();
            wBook.Write(ms);

            return File(ms.ToArray(), "application/vnd.ms-excel", order.ordercode + "deliver.xls");
        }

        [CheckSession(IsAuth = true)]
        public ActionResult Shipped(int p = 1, DateTime? deliverdate = null, DateTime? delivermonth = null, int? paystatus = null, string recivername = null)
        {
            IEnumerable<Orders> data;

            if (deliverdate == null && delivermonth == null)
            {
                data = ordersService.Get().Where(a => a.deliverstatus == (int)EnumDeliverStatus.已出貨).OrderByDescending(a => a.deliverdate).ThenByDescending(a => a.ordercode);
            }
            else if (deliverdate != null && delivermonth == null)
            {
                data = ordersService.Get().Where(a => a.deliverdate == deliverdate && a.deliverstatus == (int)EnumDeliverStatus.已出貨).OrderByDescending(a => a.deliverdate).ThenByDescending(a => a.ordercode);
            }
            else if (delivermonth != null && deliverdate == null)
            {
                data = ordersService.Get().ToList().Where(a => Convert.ToDateTime(a.deliverdate).Year == delivermonth.Value.Year && Convert.ToDateTime(a.deliverdate).Month == delivermonth.Value.Month && a.deliverstatus == (int)EnumDeliverStatus.已出貨).OrderByDescending(a => a.deliverdate).ThenByDescending(a => a.ordercode);
            }
            else
            {
                data = ordersService.Get().ToList().Where(a => Convert.ToDateTime(a.deliverdate).Year == delivermonth.Value.Year && Convert.ToDateTime(a.deliverdate).Month == delivermonth.Value.Month && a.deliverstatus == (int)EnumDeliverStatus.已出貨).OrderByDescending(a => a.deliverdate).ThenByDescending(a => a.ordercode);
            }

            if(paystatus != null)
            {
                data = data.Where(a => a.paystatus == paystatus);
            }

            if(recivername != null)
            {
                data = data.Where(a => a.recivername.Contains(recivername));
            }

            ViewBag.pageNumber = p;
            ViewBag.deliverdate = deliverdate;
            ViewBag.delivermonth = delivermonth;
            ViewBag.paystatus = paystatus;
            ViewBag.recivername = recivername;
            ViewBag.Orders = data.ToPagedList(pageNumber: p, pageSize: 20);

            return View();
        }

        [CheckSession]
        public ActionResult ShippedExport(string exportcategory, DateTime? deliverdate = null, DateTime? delivermonth = null)
        {
            IEnumerable<Orders> datas;

            if (deliverdate == null && delivermonth == null)
            {
                datas = ordersService.Get().Where(a => a.deliverstatus == (int)EnumDeliverStatus.已出貨).OrderByDescending(a => a.deliverdate).ThenByDescending(a => a.ordercode);
            }
            else if (deliverdate != null && delivermonth == null)
            {
                datas = ordersService.Get().Where(a => a.deliverdate == deliverdate && a.deliverstatus == (int)EnumDeliverStatus.已出貨).OrderByDescending(a => a.deliverdate).ThenByDescending(a => a.ordercode);
            }
            else if (delivermonth != null && deliverdate == null)
            {
                datas = ordersService.Get().ToList().Where(a => Convert.ToDateTime(a.deliverdate).Year == delivermonth.Value.Year && Convert.ToDateTime(a.deliverdate).Month == delivermonth.Value.Month && a.deliverstatus == (int)EnumDeliverStatus.已出貨).OrderByDescending(a => a.deliverdate).ThenByDescending(a => a.ordercode);
            }
            else
            {
                datas = ordersService.Get().ToList().Where(a => Convert.ToDateTime(a.deliverdate).Year == delivermonth.Value.Year && Convert.ToDateTime(a.deliverdate).Month == delivermonth.Value.Month && a.deliverstatus == (int)EnumDeliverStatus.已出貨).OrderByDescending(a => a.deliverdate).ThenByDescending(a => a.ordercode);
            }

            if (exportcategory == "shopcom")
            {
                datas = datas.Where(a => a.RID != null && a.Click_ID != null);
            }

            IWorkbook wBook = new HSSFWorkbook();
            ISheet wSheet = wBook.CreateSheet();

            if(exportcategory == "tfoodies")
            {
                wSheet.AddMergedRegion(new CellRangeAddress(0, 0, 9, 12));

                wSheet.CreateRow(0);
                wSheet.GetRow(0).CreateCell(0).SetCellValue("訂單日期");
                wSheet.GetRow(0).CreateCell(1).SetCellValue("訂單編號");
                wSheet.GetRow(0).CreateCell(2).SetCellValue("訂單類型");
                wSheet.GetRow(0).CreateCell(3).SetCellValue("收件人");
                wSheet.GetRow(0).CreateCell(4).SetCellValue("電話");
                wSheet.GetRow(0).CreateCell(5).SetCellValue("付款方式");
                wSheet.GetRow(0).CreateCell(6).SetCellValue("發票類型");
                wSheet.GetRow(0).CreateCell(7).SetCellValue("發票號碼");
                wSheet.GetRow(0).CreateCell(8).SetCellValue("物流");

                wSheet.GetRow(0).CreateCell(9).SetCellValue("購買品項");

                wSheet.GetRow(0).CreateCell(13).SetCellValue("總金額");
                wSheet.GetRow(0).CreateCell(14).SetCellValue("出貨倉");
                wSheet.GetRow(0).CreateCell(15).SetCellValue("出貨狀態");
                wSheet.GetRow(0).CreateCell(16).SetCellValue("備註");

                int rowIdx = 2;
                int orderIdx = 1;
                foreach (Orders order in datas)
                {
                    IRow urow = wSheet.CreateRow(orderIdx);

                    urow.CreateCell(0).SetCellValue(order.orderdate.ToString("yyyy-MM-dd"));
                    urow.CreateCell(1).SetCellValue(order.ordercode);
                    urow.CreateCell(2).SetCellValue(Enum.GetName(typeof(EnumOrderType), order.ordertype));
                    urow.CreateCell(3).SetCellValue(order.recivername);
                    urow.CreateCell(4).SetCellValue(order.recivermobile);
                    urow.CreateCell(5).SetCellValue(Enum.GetName(typeof(EnumPayType), order.paytype));
                    urow.CreateCell(6).SetCellValue(Enum.GetName(typeof(EnumInvoiceType), order.invoicetype));
                    urow.CreateCell(7).SetCellValue(order.invoicecode);
                    urow.CreateCell(8).SetCellValue(order.Logistics.title);
                    urow.CreateCell(9).SetCellValue("產品");
                    urow.CreateCell(10).SetCellValue("數量");
                    urow.CreateCell(11).SetCellValue("價格");
                    urow.CreateCell(12).SetCellValue("折扣數");
                    urow.CreateCell(13).SetCellValue(order.total);
                    urow.CreateCell(14).SetCellValue(order.Warehouses.title);
                    urow.CreateCell(15).SetCellValue(Enum.GetName(typeof(EnumDeliverStatus), order.deliverstatus));
                    urow.CreateCell(16).SetCellValue(order.note);

                    foreach (Orderdetails orderdetail in order.Orderdetails)
                    {
                        wSheet.AddMergedRegion(new CellRangeAddress(rowIdx, rowIdx, 0, 8));
                        wSheet.AddMergedRegion(new CellRangeAddress(rowIdx, rowIdx, 13, 16));

                        IRow drow = wSheet.CreateRow(rowIdx);

                        int r = 1;
                        string s = orderdetail.Products.title + (orderdetail.isgift == 1 ? " (贈品)" : "");
                        if (orderdetail.Products.isset)
                        {
                            foreach (Setproducts setproduct in orderdetail.Products.Setproducts)
                            {
                                s = s + "\n" + setproduct.Products1.title + " " + setproduct.Products1.capacity + " " + setproduct.qty;
                                r++;
                            }
                        }
                        drow.CreateCell(9).SetCellValue(s);
                        if (s.Contains("\n"))
                        {
                            ICellStyle cs = wBook.CreateCellStyle();
                            cs.WrapText = true;
                            drow.GetCell(9).CellStyle = cs;

                            drow.HeightInPoints = r * wSheet.DefaultRowHeight / 20;
                        }

                        drow.CreateCell(10).SetCellValue(orderdetail.qty);
                        drow.CreateCell(11).SetCellValue(orderdetail.price);
                        drow.CreateCell(12).SetCellValue((orderdetail.discount != null) ? orderdetail.discount + " 折" : "-");

                        rowIdx++;
                        orderIdx++;
                    }

                    rowIdx++;
                    orderIdx++;
                }
            }

            if(exportcategory == "shopcom")
            {
                wSheet.CreateRow(0);
                wSheet.GetRow(0).CreateCell(0).SetCellValue("訂購日期");
                wSheet.GetRow(0).CreateCell(1).SetCellValue("訂單編號");
                wSheet.GetRow(0).CreateCell(2).SetCellValue("購買人");
                wSheet.GetRow(0).CreateCell(3).SetCellValue("Market Taiwan RID number");
                wSheet.GetRow(0).CreateCell(4).SetCellValue("Click_ID");
                wSheet.GetRow(0).CreateCell(5).SetCellValue("商品編號");
                wSheet.GetRow(0).CreateCell(6).SetCellValue("購買商品");
                wSheet.GetRow(0).CreateCell(7).SetCellValue("數量");
                wSheet.GetRow(0).CreateCell(8).SetCellValue("網路銷售價");
                wSheet.GetRow(0).CreateCell(9).SetCellValue("總計");

                wSheet.CreateRow(1);
                wSheet.GetRow(1).CreateCell(0).SetCellValue("Order Date");
                wSheet.GetRow(1).CreateCell(1).SetCellValue("Order Serial Number");
                wSheet.GetRow(1).CreateCell(2).SetCellValue("Buyer Name");
                wSheet.GetRow(1).CreateCell(3).SetCellValue("");
                wSheet.GetRow(1).CreateCell(4).SetCellValue("");
                wSheet.GetRow(1).CreateCell(5).SetCellValue("Product Serial Number");
                wSheet.GetRow(1).CreateCell(6).SetCellValue("Product description");
                wSheet.GetRow(1).CreateCell(7).SetCellValue("Product Quantity");
                wSheet.GetRow(1).CreateCell(8).SetCellValue("Unit price");
                wSheet.GetRow(1).CreateCell(9).SetCellValue("Sale Amount");

                int orderIdx = 2;
                int total = 0;

                foreach (Orders order in datas)
                {
                    foreach (Orderdetails orderdetail in order.Orderdetails)
                    {
                        IRow urow = wSheet.CreateRow(orderIdx);

                        urow.CreateCell(0).SetCellValue(order.orderdate.ToString("yyyy/MM/dd"));
                        urow.CreateCell(1).SetCellValue(order.ordercode);
                        urow.CreateCell(2).SetCellValue(order.Members.name);
                        urow.CreateCell(3).SetCellValue(order.RID);
                        urow.CreateCell(4).SetCellValue(order.Click_ID);
                        urow.CreateCell(5).SetCellValue(orderdetail.Products.productnum);
                        urow.CreateCell(6).SetCellValue(orderdetail.Products.title);
                        urow.CreateCell(7).SetCellValue(orderdetail.qty);
                        urow.CreateCell(8).SetCellValue("NT$" + orderdetail.price);
                        urow.CreateCell(9).SetCellValue("NT$" + orderdetail.subtotal);

                        orderIdx++;
                        total += orderdetail.subtotal;
                    }
                }

                IRow totalrow = wSheet.CreateRow(orderIdx);
                totalrow.CreateCell(9).SetCellValue(total);
                orderIdx++;

                int commission = Convert.ToInt32(Math.Round(total * 0.2, MidpointRounding.AwayFromZero));
                IRow commissionrow = wSheet.CreateRow(orderIdx);
                commissionrow.CreateCell(7).SetCellValue("右欄填入佣金%");
                commissionrow.CreateCell(8).SetCellValue("20.0%");
                commissionrow.CreateCell(9).SetCellValue(commission);
                orderIdx++;

                int tax = Convert.ToInt32(Math.Round((commission * 1.05) - commission, MidpointRounding.AwayFromZero));
                IRow taxrow = wSheet.CreateRow(orderIdx);
                taxrow.CreateCell(7).SetCellValue("營業稅");
                taxrow.CreateCell(8).SetCellValue("5%");
                taxrow.CreateCell(9).SetCellValue(tax);
                orderIdx++;

                int pay = commission + tax;
                IRow payrow = wSheet.CreateRow(orderIdx);
                payrow.CreateCell(7).SetCellValue("應付佣金總數");
                payrow.CreateCell(9).SetCellValue(pay);
                orderIdx++;

                orderIdx = orderIdx + 2;

                IRow noterow1 = wSheet.CreateRow(orderIdx);
                noterow1.CreateCell(0).SetCellValue("注意事項與需知：");
                orderIdx++;

                IRow noterow2 = wSheet.CreateRow(orderIdx);
                noterow2.CreateCell(0).SetCellValue("1.每月5-10號提供上個月的購買完整交易報表，RID號碼必定為至少（含）三組英數混和的字元，mail至美安帳務處理信箱 psreport@markettaiwan.com.tw。");
                orderIdx++;

                IRow noterow3 = wSheet.CreateRow(orderIdx);
                noterow3.CreateCell(0).SetCellValue("2.收到發票後，應於當月25 日前匯佣金至下列指定銀行帳號，並照下一步驟，將收據給予美安公司，方完成整個流程。");
                orderIdx++;

                IRow noterow4 = wSheet.CreateRow(orderIdx);
                noterow4.CreateCell(0).SetCellValue("   銀行帳號：香港商香港匯豐銀行臺北分行(行庫代碼:081)");
                orderIdx++;

                IRow noterow5 = wSheet.CreateRow(orderIdx);
                noterow5.CreateCell(0).SetCellValue("   戶名：美商美安美台股份有限公司台灣分公司");
                orderIdx++;

                IRow noterow6 = wSheet.CreateRow(orderIdx);
                noterow6.CreateCell(0).SetCellValue("   銀行帳號: 001- 270008 – 031");
                orderIdx++;

                IRow noterow7 = wSheet.CreateRow(orderIdx);
                noterow7.CreateCell(0).SetCellValue("3.請將匯款收據或ATM轉帳明細，傳真或mail至02-87128189；或掃描收據並mail至美安帳務處理信箱 psreport@markettaiwan.com.tw。");
                orderIdx++;

                IRow noterow8 = wSheet.CreateRow(orderIdx);
                noterow8.CreateCell(0).SetCellValue("4.累積2個月份延遲報表或付款單據之廠商，美安公司將中止與其之夥伴合作關係。");
                orderIdx++;
            }

            MemoryStream ms = new MemoryStream();
            wBook.Write(ms);

            return File(ms.ToArray(), "application/vnd.ms-excel", DateTime.Now.ToString("yyyyMMdd") + "_" + exportcategory + "_export.xls");
        }

        [CheckSession(IsAuth = true)]
        public ActionResult EditShipped(Guid orderid, int p)
        {
            ViewBag.pageNumber = p;
            Orders order = ordersService.GetByID(orderid);

            return View(order);
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult EditShipped(Guid orderid, int p, FormCollection form)
        {
            Orders order = ordersService.GetByID(orderid);

            if (TryUpdateModel(order, new string[] { "paytype", "paystatus", "paydate", "deliverstatus", "invoicecode", "companynumber", "companytitle", "note", "lastpan4" }) && ModelState.IsValid)
            {
                try
                {
                    ordersService.Update(order);
                    ordersService.SaveChanges();

                    return RedirectToAction("Shipped", new { p = p});
                }
                catch (DbEntityValidationException ex)
                {
                    foreach (var eve in ex.EntityValidationErrors)
                    {
                        Console.WriteLine("Entity of type \"{0}\" in state \"{1}\" has the following validation errors:", eve.Entry.Entity.GetType().Name, eve.Entry.State);
                        foreach (var ve in eve.ValidationErrors)
                        {
                            Console.WriteLine("- Property: \"{0}\", Error: \"{1}\"", ve.PropertyName, ve.ErrorMessage);
                        }
                    }
                    throw;
                }
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;

                return View(order);
            }
        }

        [CheckSession(IsAuth = true)]
        public ActionResult Canceled(int p = 1)
        {
            IEnumerable<Orders> datas = ordersService.Get().Where(a => a.deliverstatus == (int)EnumDeliverStatus.取消 || a.paystatus == (int)EnumPayStatus.取消).OrderByDescending(o => o.ordercode).ThenByDescending(o => o.orderdate);

            ViewBag.pageNumber = p;
            ViewBag.Orders = datas.ToPagedList(pageNumber: p, pageSize: 20);
            return View();
        }

        [CheckSession(IsAuth = true)]
        public ActionResult EditCanceled(Guid entityid, int p)
        {
            Orders order = ordersService.GetByID(entityid);

            ViewBag.pageNumber = p;

            return View(order);
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult EditCanceled(Guid orderid, int p, FormCollection form)
        {
            Orders order = ordersService.GetByID(orderid);

            if (TryUpdateModel(order, new string[] { "paystatus", "deliverstatus" }) && ModelState.IsValid)
            {
                try
                {
                    ordersService.Update(order);
                    ordersService.SaveChanges();

                    return RedirectToAction("Canceled", new { p = p });
                }
                catch (DbEntityValidationException ex)
                {
                    foreach (var eve in ex.EntityValidationErrors)
                    {
                        Console.WriteLine("Entity of type \"{0}\" in state \"{1}\" has the following validation errors:", eve.Entry.Entity.GetType().Name, eve.Entry.State);
                        foreach (var ve in eve.ValidationErrors)
                        {
                            Console.WriteLine("- Property: \"{0}\", Error: \"{1}\"", ve.PropertyName, ve.ErrorMessage);
                        }
                    }
                    throw;
                }
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;

                return View(order);
            }
        }

        [CheckSession(IsAuth = true)]
        public ActionResult Returns(int p = 1, DateTime? returndate = null, DateTime? returnmonth = null)
        {
            IEnumerable<Returns> data;

            if (returndate == null && returnmonth == null)
            {
                data = returnsService.Get().OrderByDescending(a => a.receivedate).ThenByDescending(a => a.returncode);
            }
            else if (returndate != null && returnmonth == null)
            {
                data = returnsService.Get().Where(a => a.returndate == returndate).OrderByDescending(a => a.returndate).ThenByDescending(a => a.returncode);
            }
            else if (returnmonth != null && returndate == null)
            {
                data = returnsService.Get().Where(a => a.returndate.Year == returnmonth.Value.Year && a.returndate.Month == returnmonth.Value.Month).OrderByDescending(a => a.returndate).ThenByDescending(a => a.returncode);
            }
            else
            {
                data = returnsService.Get().Where(a => a.returndate.Year == returnmonth.Value.Year && a.returndate.Month == returnmonth.Value.Month).OrderByDescending(a => a.returndate).ThenByDescending(a => a.returncode);
            }

            ViewBag.pageNumber = p;
            ViewBag.returndate = returndate;
            ViewBag.returnmonth = returnmonth;
            ViewBag.Returns = data.ToPagedList(pageNumber: p, pageSize: 20);
            return View();
        }

        [CheckSession(IsAuth = true)]
        public ActionResult AddReturns(int p)
        {
            ViewBag.pageNumber = p;

            return View();
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult AddReturns(Returns entity, int p)
        {
            if (TryUpdateModel(entity, new string[] { "orderid", "returndate", "receivestatus", "receivedate", "refundstatus", "refunddate", "note" }) && ModelState.IsValid)
            {
                try
                {
                    IEnumerable<Returndetails> rds = entity.Returndetails.Where(a => a.qty > 0);

                    if (entity.orderid == null || entity.orderid.ToString() == "")
                    {
                        ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                        ViewBag.pageNumber = p;

                        return View(entity);
                    }

                    Orders order = ordersService.GetByID(entity.orderid);

                    entity.returnid = Guid.NewGuid();
                    entity.memberid = order.memberid;
                    entity.returncode = Librarys.NewReturnCode(DateTime.Now);
                    entity.createdate = DateTime.Now;
                    entity.warehousestatus = (int)EnumWarehouseStatus.未入庫;

                    foreach (Returndetails returndetail in entity.Returndetails)
                    {
                        returndetail.returndetailid = Guid.NewGuid();
                        returndetail.accountingid = new Guid("469AF577-AC6F-4026-AD48-8918525D1ACF");
                    }

                    returnsService.Create(entity);
                    returnsService.SaveChanges();

                    return RedirectToAction("Returns", new { p = p });
                }
                catch (DbEntityValidationException ex)
                {
                    foreach (var eve in ex.EntityValidationErrors)
                    {
                        Console.WriteLine("Entity of type \"{0}\" in state \"{1}\" has the following validation errors:", eve.Entry.Entity.GetType().Name, eve.Entry.State);
                        foreach (var ve in eve.ValidationErrors)
                        {
                            Console.WriteLine("- Property: \"{0}\", Error: \"{1}\"", ve.PropertyName, ve.ErrorMessage);
                        }
                    }
                    throw;
                }
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;

                return View(entity);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult EditReturns(Guid entityid, int p)
        {
            ViewBag.pageNumber = p;

            Returns entity = returnsService.GetByID(entityid);

            return View(entity);
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult EditReturns(Guid returnid, int p, ICollection<Returndetails> returndetails)
        {
            Returns entity = returnsService.GetByID(returnid);

            if (TryUpdateModel(entity, new string[] { "orderid", "returndate", "receivestatus", "receivedate", "refundstatus", "refunddate", "note" }) && ModelState.IsValid)
            {
                try
                {
                    foreach (Returndetails rg in entity.Returndetails.ToArray())
                    {
                        if (!returndetails.ToList().Exists(x => x.returndetailid == rg.returndetailid))
                        {
                            entity.Returndetails.Remove(rg);
                            returndetailsService.Delete(rg.returndetailid);
                        }
                    }

                    foreach (Returndetails returndetail in returndetails)
                    {
                        if (entity.Returndetails.ToList().Exists(x => x.returndetailid == returndetail.returndetailid))
                        {
                            Returndetails od = entity.Returndetails.Where(a => a.returndetailid == returndetail.returndetailid).FirstOrDefault();
                            od.orderdetailid = returndetail.orderdetailid;
                            od.qty = returndetail.qty;
                        }
                        else
                        {
                            returndetail.returndetailid = Guid.NewGuid();
                            returndetail.returnid = entity.returnid;
                            entity.Returndetails.Add(returndetail);
                        }
                    }

                    returnsService.Update(entity);
                    returnsService.SaveChanges();

                    return RedirectToAction("Returns", new { p = p });
                }
                catch (DbEntityValidationException ex)
                {
                    foreach (var eve in ex.EntityValidationErrors)
                    {
                        Console.WriteLine("Entity of type \"{0}\" in state \"{1}\" has the following validation errors:", eve.Entry.Entity.GetType().Name, eve.Entry.State);
                        foreach (var ve in eve.ValidationErrors)
                        {
                            Console.WriteLine("- Property: \"{0}\", Error: \"{1}\"", ve.PropertyName, ve.ErrorMessage);
                        }
                    }
                    throw;
                }
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;

                return View(entity);
            }
        }

        [CheckSession(IsAuth = true)]
        public ActionResult Declarations()
        {
            // TODO: 需要判斷stocks的stocktype(是否需要申報)

            return View();
        }

        #region -- DropDownList ViewBag --
        private void LogisticDropDownList(Object selectLogistics = null)
        {
            var querys = logisticsService.Get();

            ViewBag.logisticid = new SelectList(querys, "logisticid", "title", selectLogistics);
        }

        private void WarehousesDropDownList(Object selectWarehouses = null)
        {
            var querys = warehousesService.Get();

            ViewBag.warehouseid = new SelectList(querys, "warehouseid", "title", selectWarehouses);
        }
        #endregion
    }
}