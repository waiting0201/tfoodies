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
using System.Data.Entity.Validation;

namespace tfoodiesBackend.Controllers
{
    public class PurchaseMsController : BaseController
    {
        private ISuppliersService suppliersService;
        private IPurchasesService purchasesService;
        private IPurchaseDetailsService purchasedetailsService;
        private IAccountingsService accountingsService;
        private IExpendituresService expendituresService;
        private IExpenditureDetailsService expendituredetailsService;
        private IOutcomesService outcomesService;
        private IExchangesService exchangesService;

        public PurchaseMsController()
        {
            tfoodiesEntities db = new tfoodiesEntities();
            suppliersService = new SuppliersService();
            purchasesService = new PurchasesService(db);
            purchasedetailsService = new PurchaseDetailsService(db);
            accountingsService = new AccountingsService();
            expendituresService = new ExpendituresService(db);
            expendituredetailsService = new ExpenditureDetailsService(db);
            outcomesService = new OutcomesService();
            exchangesService = new ExchangesService();
        }

        [CheckSession(IsAuth = true)]
        public ActionResult Suppliers(int p = 1)
        {
            var data = suppliersService.Get().OrderBy(a => a.supplierid);

            ViewBag.pageNumber = p;
            ViewBag.Suppliers = data.ToPagedList(pageNumber: p, pageSize: 20);
            return View();
        }

        [CheckSession(IsAuth = true)]
        public ActionResult AddSuppliers(int p)
        {
            ViewBag.pageNumber = p;
            return View();
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult AddSuppliers(Suppliers entity, int p)
        {
            if (TryUpdateModel(entity, new string[] { "title", "contactor", "address", "phone" }) && ModelState.IsValid)
            {
                entity.supplierid = Guid.NewGuid();

                suppliersService.Create(entity);
                suppliersService.SaveChanges();

                return RedirectToAction("Suppliers", new { p = p });
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
        public ActionResult EditSuppliers(Guid entityid, int p)
        {
            ViewBag.pageNumber = p;
            Suppliers supplier = suppliersService.GetByID(entityid);
            return View(supplier);
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult EditSuppliers(Guid supplierid, int p, FormCollection form)
        {
            Suppliers supplier = suppliersService.GetByID(supplierid);

            if (TryUpdateModel(supplier, new string[] { "title", "contactor", "address", "phone" }) && ModelState.IsValid)
            {
                suppliersService.Update(supplier);
                suppliersService.SaveChanges();

                return RedirectToAction("Suppliers", new { p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;
                return View(supplier);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult DeleteSuppliers(Guid supplierid, int p)
        {
            Suppliers supplier = suppliersService.GetByID(supplierid);

            suppliersService.Delete(supplier);
            suppliersService.SaveChanges();

            return RedirectToAction("Suppliers", new { p = p });
        }

        [CheckSession(IsAuth = true)]
        public ActionResult Purchases(int p = 1)
        {
            var data = purchasesService.Get().OrderByDescending(a => a.purchasedate).ThenByDescending(a => a.purchasecode);

            ViewBag.pageNumber = p;
            ViewBag.Purchases = data.ToPagedList(pageNumber: p, pageSize: 20);
            return View();
        }

        [CheckSession(IsAuth = true)]
        public ActionResult AddPurchases(int p)
        {
            ViewBag.pageNumber = p;
            SupplierDropDownList();
            ExchangesDropDownList();

            return View();
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult AddPurchases(Purchases purchase, int p)
        {
            if (TryUpdateModel(purchase, new string[] { "supplierid", "purchasedate", "exchangeid", "etd", "payment", "deliverterm", "note", "createdate" }) && ModelState.IsValid)
            {
                try
                {
                    purchase.purchaseid = Guid.NewGuid();
                    purchase.purchasecode = Librarys.NewPurchaseCode(DateTime.Now);
                    purchase.createdate = DateTime.Now;
                    purchase.status = 1;

                    foreach (Purchasedetails purchasedetail in purchase.Purchasedetails)
                    {
                        purchasedetail.purchasedetailid = Guid.NewGuid();
                    }

                    purchasesService.Create(purchase);
                    purchasesService.SaveChanges();

                    return RedirectToAction("Purchases", new { p = p });
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
                SupplierDropDownList();
                ExchangesDropDownList();

                return View(purchase);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult EditPurchases(Guid entityid, int p)
        {
            ViewBag.pageNumber = p;
            
            Purchases purchase = purchasesService.GetByID(entityid);
            SupplierDropDownList(purchase.supplierid);
            ExchangesDropDownList(purchase.exchangeid);

            return View(purchase);
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult EditPurchases(Guid purchaseid, int p, ICollection<Purchasedetails> purchasedetails)
        {
            Purchases purchase = purchasesService.GetByID(purchaseid);

            if (TryUpdateModel(purchase, new string[] { "supplierid", "purchasedate", "exchangeid", "etd", "payment", "deliverterm", "note", "createdate" }) && ModelState.IsValid)
            {
                try
                {
                    foreach (Purchasedetails rg in purchase.Purchasedetails.ToArray())
                    {
                        if (!purchasedetails.ToList().Exists(x => x.purchasedetailid == rg.purchasedetailid))
                        {
                            purchase.Purchasedetails.Remove(rg);
                            purchasedetailsService.Delete(rg.purchasedetailid);
                        }
                    }

                    foreach (Purchasedetails purchasedetail in purchasedetails)
                    {
                        if (purchase.Purchasedetails.ToList().Exists(x => x.purchasedetailid == purchasedetail.purchasedetailid))
                        {
                            Purchasedetails od = purchase.Purchasedetails.Where(a => a.purchasedetailid == purchasedetail.purchasedetailid).FirstOrDefault();
                            od.productid = purchasedetail.productid;
                            od.qty = purchasedetail.qty;
                            od.unitprice = purchasedetail.unitprice;
                            od.subtotal = purchasedetail.subtotal;
                            od.status = purchasedetail.status;
                        }
                        else
                        {
                            purchasedetail.purchasedetailid = Guid.NewGuid();
                            purchasedetail.purchaseid = purchase.purchaseid;
                            purchase.Purchasedetails.Add(purchasedetail);
                        }
                    }

                    purchasesService.Update(purchase);
                    purchasesService.SaveChanges();

                    return RedirectToAction("Purchases", new { p = p });
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
                SupplierDropDownList(purchase.supplierid);
                ExchangesDropDownList(purchase.exchangeid);

                return View(purchase);
            }
        }

        #region -- DropDownList ViewBag --
        private void SupplierDropDownList(Object selectSuppliers = null)
        {
            var querys = suppliersService.Get();

            ViewBag.supplierid = new SelectList(querys, "supplierid", "title", selectSuppliers);
        }
        private void ExchangesDropDownList(Object selectExchanges = null)
        {
            var querys = exchangesService.Get();

            ViewBag.exchangeid = new SelectList(querys, "exchangeid", "title", selectExchanges);
        }
        #endregion
    }
}