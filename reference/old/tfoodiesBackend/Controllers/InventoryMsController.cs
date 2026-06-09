using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using PagedList;
using tfoodiesBackend.Filters;
using tfoodiesBackend.Commons;
using tfoodies.Libs;
using tfoodies.Models;
using tfoodies.Service;

namespace tfoodiesBackend.Controllers
{
    public class InventoryMsController : BaseController
    {
        IWarehousesService warehousesService;
        IWarehouseStocksService warehousestocksService;
        IProductsService productsService;
        IStocksService stocksService;
        IPurchasesService purchasesService;
        IPurchaseDetailsService purchasedetailsService;

        public InventoryMsController()
        {
            tfoodiesEntities db = new tfoodiesEntities();
            warehousesService = new WarehousesService(db);
            warehousestocksService = new WarehouseStocksService(db);
            productsService = new ProductsService();
            stocksService = new StocksService(db);
            purchasesService = new PurchasesService(db);
            purchasedetailsService = new PurchaseDetailsService(db);
        }

        [CheckSession(IsAuth = true)]
        public ActionResult Warehouses(int p = 1)
        {
            var data = warehousesService.Get().OrderBy(a => a.warehousetype);

            ViewBag.pageNumber = p;
            ViewBag.Warehouses = data.ToPagedList(pageNumber: p, pageSize: 20);

            return View();
        }

        [CheckSession(IsAuth = true)]
        public ActionResult AddWarehouses(int p)
        {
            ViewBag.pageNumber = p;

            return View();
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult AddWarehouses(Warehouses warehouse, int p)
        {
            if (TryUpdateModel(warehouse, new string[] { "warehousetype", "title" }) && ModelState.IsValid)
            {
                warehouse.warehouseid = Guid.NewGuid();

                warehousesService.Create(warehouse);
                warehousesService.SaveChanges();

                return RedirectToAction("Warehouses", new { p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;
                return View(warehouse);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult EditWarehouses(Guid entityid, int p)
        {
            ViewBag.pageNumber = p;
            Warehouses warehouse = warehousesService.GetByID(entityid);
            return View(warehouse);
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult EditWarehouses(Guid warehouseid, int p, FormCollection form)
        {
            Warehouses warehouse = warehousesService.GetByID(warehouseid);

            if (TryUpdateModel(warehouse, new string[] { "warehousetype", "title" }) && ModelState.IsValid)
            {
                warehousesService.Update(warehouse);
                warehousesService.SaveChanges();

                return RedirectToAction("Warehouses", new { p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;
                return View(warehouse);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult ResultWarehouses(Guid entityid, int p)
        {
            ViewBag.pageNumber = p;
            Warehouses warehouse = warehousesService.GetByID(entityid);
            return View(warehouse);
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult DeleteWarehouses(Guid entityid, int p)
        {
            Warehouses warehouse = warehousesService.GetByID(entityid);

            if(warehouse.Warehousestocks.Count() > 0)
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                return RedirectToAction("Warehouses", new { p = p });
            }

            warehousesService.Delete(warehouse);
            warehousesService.SaveChanges();

            return RedirectToAction("Warehouses", new { p = p });
        }

        [CheckSession(IsAuth = true)]
        public ActionResult Stocks(int p = 1)
        {
            var data = stocksService.Get().OrderByDescending(a => a.createdate);

            ViewBag.pageNumber = p;
            ViewBag.Stocks = data.ToPagedList(pageNumber: p, pageSize: 20);

            return View();
        }

        [CheckSession(IsAuth = true)]
        public ActionResult AddStocks(int p)
        {
            ViewBag.Purchases = purchasesService.Get().Where(a => a.status == 1 || a.status == 3).OrderBy(a => a.createdate);
            ViewBag.pageNumber = p;
            ViewBag.Warehouses = warehousesService.Get();

            return View();
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult AddStocks(Stocks stock, Guid warehouseid, int detailstatus, int p)
        {
            if (TryUpdateModel(stock, new string[] { "purchaseid", "purchasedetailid", "createdate", "barcode", "noticenumber", "declarationnumber", "item", "manufacturedate", "expiredate", "quantity", "weight", "status" }) && ModelState.IsValid)
            {
                stock.stockid = Guid.NewGuid();
                stock.stocktype = 1;

                Warehousestocks warehousestock = new Warehousestocks();
                warehousestock.warehousestockid = Guid.NewGuid();
                warehousestock.warehouseid = warehouseid;
                warehousestock.quantity = stock.quantity;
                warehousestock.quantity_left = stock.quantity;
                warehousestock.createdate = DateTime.Now;
                warehousestock.transdate = stock.createdate;

                stock.Warehousestocks.Add(warehousestock);

                stocksService.Create(stock);
                stocksService.SaveChanges();

                General.CheckPurchaseStatus(stock.purchasedetailid, detailstatus);

                return RedirectToAction("Stocks", new { p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.Purchases = purchasesService.Get().Where(a => a.status < 3).OrderBy(a => a.createdate);
                ViewBag.pageNumber = p;
                return View(stock);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult EditStocks(Guid entityid, int p)
        {
            Stocks stock = stocksService.GetByID(entityid);

            ViewBag.pageNumber = p;

            ViewBag.Purchases = purchasesService.Get().Where(a => a.status < 3).OrderBy(a => a.createdate);

            return View(stock);
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult EditStocks(Guid stockid, int detailstatus, int p)
        {
            Stocks stock = stocksService.GetByID(stockid);

            if (TryUpdateModel(stock, new string[] { "barcode", "noticenumber", "declarationnumber", "item", "manufacturedate", "expiredate", "quantity", "weight", "status" }) && ModelState.IsValid)
            {
                if(stock.Warehousestocks.Count() == 1 && stock.Warehousestocks.FirstOrDefault().quantity == stock.Warehousestocks.FirstOrDefault().quantity_left)
                {
                    Warehousestocks warehousestock = stock.Warehousestocks.FirstOrDefault();
                    warehousestock.quantity = stock.quantity;
                    warehousestock.quantity_left = stock.quantity;
                }
                
                stocksService.Update(stock);
                stocksService.SaveChanges();

                General.CheckPurchaseStatus(stock.purchasedetailid, detailstatus);

                return RedirectToAction("Stocks", new { p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;
                ViewBag.Purchases = purchasesService.Get().Where(a => a.status < 3).OrderBy(a => a.createdate);

                return View(stock);
            }
        }

        [CheckSession(IsAuth = true)]
        public ActionResult AddNoStocks(int p)
        {
            ViewBag.Purchases = purchasesService.Get().Where(a => a.status == 1 || a.status == 3).OrderBy(a => a.createdate);
            ViewBag.pageNumber = p;
            ViewBag.Warehouses = warehousesService.Get();

            return View();
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult AddNoStocks(Stocks stock, Guid warehouseid, int detailstatus, int p)
        {
            if (TryUpdateModel(stock, new string[] { "purchaseid", "purchasedetailid", "createdate", "quantity" }) && ModelState.IsValid)
            {
                stock.stockid = Guid.NewGuid();
                stock.stocktype = 2;

                Warehousestocks warehousestock = new Warehousestocks();
                warehousestock.warehousestockid = Guid.NewGuid();
                warehousestock.warehouseid = warehouseid;
                warehousestock.quantity = stock.quantity;
                warehousestock.quantity_left = stock.quantity;
                warehousestock.createdate = DateTime.Now;
                warehousestock.transdate = stock.createdate;

                stock.Warehousestocks.Add(warehousestock);

                stocksService.Create(stock);
                stocksService.SaveChanges();

                General.CheckPurchaseStatus(stock.purchasedetailid, detailstatus);

                return RedirectToAction("Stocks", new { p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.Purchases = purchasesService.Get().Where(a => a.status < 3).OrderBy(a => a.createdate);
                ViewBag.pageNumber = p;
                return View(stock);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult EditNoStocks(Guid entityid, int p)
        {
            Stocks stock = stocksService.GetByID(entityid);

            ViewBag.pageNumber = p;

            ViewBag.Purchases = purchasesService.Get().Where(a => a.status < 3).OrderBy(a => a.createdate);

            return View(stock);
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult EditNoStocks(Guid stockid, int detailstatus, int p)
        {
            Stocks stock = stocksService.GetByID(stockid);

            if (TryUpdateModel(stock, new string[] { "quantity" }) && ModelState.IsValid)
            {
                if(stock.Warehousestocks.Count() == 1 && stock.Warehousestocks.FirstOrDefault().quantity == stock.Warehousestocks.FirstOrDefault().quantity_left)
                {
                    Warehousestocks warehousestock = stock.Warehousestocks.FirstOrDefault();
                    warehousestock.quantity = stock.quantity;
                    warehousestock.quantity_left = stock.quantity;
                }

                stocksService.Update(stock);
                stocksService.SaveChanges();

                General.CheckPurchaseStatus(stock.purchasedetailid, detailstatus);

                return RedirectToAction("Stocks", new { p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;
                ViewBag.Purchases = purchasesService.Get().Where(a => a.status < 3).OrderBy(a => a.createdate);

                return View(stock);
            }
        }

        [CheckSession(IsAuth = true)]
        public ActionResult Warehousestocks(int p = 1, Guid? warehouseid = null, Guid? stockid = null)
        {
            var data = warehousestocksService.Get();

            if(warehouseid != null)
            {
                data = data.Where(a => a.warehouseid == warehouseid);
            }

            if(stockid != null)
            {
                data = data.Where(a => a.stockid == stockid);
            }

            data = data.OrderByDescending(a => a.transdate);

            ViewBag.pageNumber = p;
            ViewBag.warehouseid = warehouseid;
            ViewBag.stockid = stockid;
            ViewBag.Warehouses = warehousesService.Get();
            ViewBag.Stocks = stocksService.Get().OrderBy(a => a.expiredate);
            ViewBag.WarehouseStocks = data.ToPagedList(pageNumber: p, pageSize: 20);

            return View();
        }

        [CheckSession(IsAuth = true)]
        public ActionResult AddWarehousestocks(int p)
        {
            ViewBag.pageNumber = p;
            ViewBag.Warehouses = warehousesService.Get();

            return View();
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult AddWarehousestocks(Warehousestocks warehousestock, Guid whid, int p)
        {
            if (TryUpdateModel(warehousestock, new string[] { "transdate", "stockid", "warehouseid", "quantity", "memo" }) && ModelState.IsValid)
            {
                int needqty = warehousestock.quantity;
                // 移出倉
                IEnumerable<Warehousestocks> whss = warehousestocksService.Get().Where(a => a.warehouseid == whid && a.stockid == warehousestock.stockid);
                foreach(Warehousestocks whs in whss)
                {
                    if (needqty <= whs.quantity_left)
                    {
                        whs.quantity_left = whs.quantity_left - needqty;
                        warehousestocksService.Update(whs);
                        break;                        
                    }
                    else
                    {
                        needqty = needqty - whs.quantity_left;
                        whs.quantity_left = 0;
                        warehousestocksService.Update(whs);
                    }
                }

                warehousestock.warehousestockid = Guid.NewGuid();
                warehousestock.quantity_left = warehousestock.quantity;
                warehousestock.createdate = DateTime.Now;

                warehousestocksService.Create(warehousestock);
                warehousestocksService.SaveChanges();

                return RedirectToAction("Warehousestocks", new { p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;
                ViewBag.Warehouses = warehousesService.Get();

                return View(warehousestock);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult EditWarehousestocks(Guid entityid, int p)
        {
            Warehousestocks warehousestock = warehousestocksService.GetByID(entityid);

            ViewBag.pageNumber = p;

            return View(warehousestock);
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult EditWarehousestocks(Guid warehousestockid, int p, FormCollection form)
        {
            Warehousestocks warehousestock = warehousestocksService.GetByID(warehousestockid);

            if (TryUpdateModel(warehousestock, new string[] { "quantity", "quantity_left", "memo" }) && ModelState.IsValid)
            {
                warehousestocksService.Update(warehousestock);
                warehousestocksService.SaveChanges();

                return RedirectToAction("Warehousestocks", new { p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;

                return View(warehousestock);
            }
        }

        #region -- DropDownList ViewBag --
        private void WarehouseDropDownList(Object selectWarehouses = null)
        {
            var querys = warehousesService.Get();

            ViewBag.warehouseid = new SelectList(querys, "warehouseid", "title", selectWarehouses);
        }
        #endregion
    }
}