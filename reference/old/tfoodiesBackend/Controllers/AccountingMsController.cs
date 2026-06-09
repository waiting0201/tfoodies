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
    public class AccountingMsController : BaseController
    {
        private ISuppliersService suppliersService;
        private IAccountingsService accountingsService;
        private IExpendituresService expendituresService;
        private IExpenditureDetailsService expendituredetailsService;
        private IOutcomesService outcomesService;
        private IExchangesService exchangesService;
        private IPurchasesService purchasesService;
        private IReturnsService returnsService;
        private IReturnDetailsService returndetailsService;
        private IRefoundsService refoundsService;
        private IOrdersService ordersService;
        private IInvoicesService invoicesService;
        private IInvoiceDetailsService invoicedetailsService;
        private IIncomesService incomesService;

        public AccountingMsController()
        {
            tfoodiesEntities db = new tfoodiesEntities();
            suppliersService = new SuppliersService();
            accountingsService = new AccountingsService();
            expendituresService = new ExpendituresService(db);
            expendituredetailsService = new ExpenditureDetailsService(db);
            outcomesService = new OutcomesService();
            exchangesService = new ExchangesService();
            purchasesService = new PurchasesService();
            returnsService = new ReturnsService();
            returndetailsService = new ReturnDetailsService();
            refoundsService = new RefoundsService();
            ordersService = new OrdersService(db);
            invoicesService = new InvoicesService(db);
            invoicedetailsService = new InvoiceDetailsService();
            incomesService = new IncomesService(db);
        }

        [CheckSession(IsAuth = true)]
        public ActionResult Exchanges(int p = 1)
        {
            var data = exchangesService.Get().OrderBy(o => o.exchangeid);

            ViewBag.pageNumber = p;
            ViewBag.Exchanges = data.ToPagedList(pageNumber: p, pageSize: 20);
            return View();
        }

        [CheckSession(IsAuth = true)]
        public ActionResult AddExchanges(int p)
        {
            ViewBag.pageNumber = p;
            return View();
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult AddExchanges(Exchanges entity, int p)
        {
            if (TryUpdateModel(entity, new string[] { "title", "rate" }) && ModelState.IsValid)
            {
                entity.exchangeid = Guid.NewGuid();

                exchangesService.Create(entity);
                exchangesService.SaveChanges();

                return RedirectToAction("Exchanges", new { p = p });
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
        public ActionResult EditExchanges(Guid entityid, int p)
        {
            ViewBag.pageNumber = p;
            Exchanges exchange = exchangesService.GetByID(entityid);

            return View(exchange);
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult EditExchanges(Guid exchangeid, int p, FormCollection form)
        {
            Exchanges exchange = exchangesService.GetByID(exchangeid);

            if (TryUpdateModel(exchange, new string[] { "title", "rate" }) && ModelState.IsValid)
            {
                exchangesService.Update(exchange);
                exchangesService.SaveChanges();

                return RedirectToAction("Exchanges", new { p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;

                return View(exchange);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult DeleteExchanges(Guid exchangeid, int p)
        {
            Purchases purchase = purchasesService.Get().Where(a => a.exchangeid == exchangeid).FirstOrDefault();

            if (purchase == null)
            {
                Exchanges exchange = exchangesService.GetByID(exchangeid);

                exchangesService.Delete(exchange);
                exchangesService.SaveChanges();
            }

            return RedirectToAction("Exchanges", new { p = p });
        }

        [CheckSession(IsAuth = true)]
        public ActionResult Accountings(int p = 1)
        {
            var data = accountingsService.Get().OrderBy(a => a.accountingcode);

            ViewBag.pageNumber = p;
            ViewBag.Accountings = data.ToPagedList(pageNumber: p, pageSize: 20);
            return View();
        }

        [CheckSession(IsAuth = true)]
        public ActionResult AddAccountings(int p)
        {
            ViewBag.pageNumber = p;
            return View();
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult AddAccountings(Accountings entity, int p)
        {
            if (TryUpdateModel(entity, new string[] { "accountingcode", "title" }) && ModelState.IsValid)
            {
                entity.accountingid = Guid.NewGuid();

                accountingsService.Create(entity);
                accountingsService.SaveChanges();

                return RedirectToAction("Accountings", new { p = p });
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
        public ActionResult EditAccountings(Guid entityid, int p)
        {
            ViewBag.pageNumber = p;
            Accountings accounting = accountingsService.GetByID(entityid);
            return View(accounting);
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult EditAccountings(Guid accountingid, int p, FormCollection form)
        {
            Accountings accounting = accountingsService.GetByID(accountingid);

            if (TryUpdateModel(accounting, new string[] { "accountingcode", "title" }) && ModelState.IsValid)
            {
                accountingsService.Update(accounting);
                accountingsService.SaveChanges();

                return RedirectToAction("Accountings", new { p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;
                return View(accounting);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult DeleteAccountings(Guid accountingid, int p)
        {
            Expendituredetails expendituredetail = expendituredetailsService.Get().Where(a => a.accountingid == accountingid).FirstOrDefault();
            Returndetails returndetail = returndetailsService.Get().Where(a => a.accountingid == accountingid).FirstOrDefault();
            Invoicedetails invoicedetail = invoicedetailsService.Get().Where(a => a.accountingid == accountingid).FirstOrDefault();

            if (expendituredetail == null && returndetail == null && invoicedetail == null)
            {
                Accountings accounting = accountingsService.GetByID(accountingid);

                accountingsService.Delete(accounting);
                accountingsService.SaveChanges();

                return RedirectToAction("Accountings", new { p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");

                var data = accountingsService.Get().OrderBy(a => a.accountingcode);

                ViewBag.pageNumber = p;
                ViewBag.Accountings = data.ToPagedList(pageNumber: p, pageSize: 20);
                return View();
            }
        }

        [CheckSession(IsAuth = true)]
        public ActionResult Expenditures(int p = 1)
        {
            var data = expendituresService.GetList();

            ViewBag.pageNumber = p;
            ViewBag.Expenditures = data.ToPagedList(pageNumber: p, pageSize: 20);
            return View();
        }

        [CheckSession(IsAuth = true)]
        public ActionResult AddExpenditures(int p)
        {
            ViewBag.pageNumber = p;
            ViewBag.Accountings = accountingsService.Get().OrderBy(a => a.accountingcode);

            return View();
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult AddExpenditures(Expenditures expenditure, int p)
        {
            if (TryUpdateModel(expenditure, new string[] { "expendituredate", "note" }) && ModelState.IsValid)
            {
                try
                {
                    expenditure.expenditureid = Guid.NewGuid();
                    expenditure.expenditurecode = Librarys.NewExpenditureCode(DateTime.Now);
                    expenditure.createdate = DateTime.Now;
                    expenditure.sourcetype = 0;

                    foreach (Expendituredetails expendituredetail in expenditure.Expendituredetails)
                    {
                        expendituredetail.expendituredetailid = Guid.NewGuid();
                    }

                    expendituresService.Create(expenditure);
                    expendituresService.SaveChanges();

                    return RedirectToAction("Expenditures", new { p = p });
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

                return View(expenditure);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult EditExpenditures(Guid entityid, int p)
        {
            ViewBag.pageNumber = p;

            Expenditures expenditure = expendituresService.GetByID(entityid);

            return View(expenditure);
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult EditExpenditures(Guid expenditureid, int p, ICollection<Expendituredetails> expendituredetails)
        {
            Expenditures expenditure = expendituresService.GetByID(expenditureid);

            if (TryUpdateModel(expenditure, new string[] { "expendituredate", "note", "createdate" }) && ModelState.IsValid)
            {
                try
                {
                    foreach (Expendituredetails rg in expenditure.Expendituredetails.ToArray())
                    {
                        if (!expendituredetails.ToList().Exists(x => x.expendituredetailid == rg.expendituredetailid))
                        {
                            expenditure.Expendituredetails.Remove(rg);
                            expendituredetailsService.Delete(rg.expendituredetailid);
                        }
                    }

                    foreach (Expendituredetails expendituredetail in expendituredetails)
                    {
                        if (expenditure.Expendituredetails.ToList().Exists(x => x.expendituredetailid == expendituredetail.expendituredetailid))
                        {
                            Expendituredetails od = expenditure.Expendituredetails.Where(a => a.expendituredetailid == expendituredetail.expendituredetailid).FirstOrDefault();
                            od.accountingid = expendituredetail.accountingid;
                            od.price = expendituredetail.price;
                            od.summary = expendituredetail.summary;
                        }
                        else
                        {
                            expendituredetail.expendituredetailid = Guid.NewGuid();
                            expendituredetail.expenditureid = expenditure.expenditureid;
                            expenditure.Expendituredetails.Add(expendituredetail);
                        }
                    }

                    expendituresService.Update(expenditure);
                    expendituresService.SaveChanges();

                    return RedirectToAction("Expenditures", new { p = p });
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

                return View(expenditure);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult DeleteExpenditures(Guid expenditureid, int p)
        {
            Expenditures expenditure = expendituresService.GetByID(expenditureid);

            if(expenditure.status == (int)EnumExpenditureStatus.未付款)
            {
                Purchases purchase = purchasesService.GetByID(expenditure.purchaseid);

                expendituresService.Delete(expenditure);
                expendituresService.SaveChanges();

                purchase.isexpenditure = false;
                purchasesService.Update(purchase);
                purchasesService.SaveChanges();

                return RedirectToAction("Expenditures", new { p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");

                var data = expendituresService.GetList();
                ViewBag.pageNumber = p;
                ViewBag.Expenditures = data.ToPagedList(pageNumber: p, pageSize: 20);
                return View();
            }
        }

        [CheckSession(IsAuth = true)]
        public ActionResult Outcomes(int p = 1)
        {
            var data = outcomesService.Get().OrderByDescending(a => a.outcomedate).ThenByDescending(a => a.outcomecode);

            ViewBag.pageNumber = p;
            ViewBag.Outcomes = data.ToPagedList(pageNumber: p, pageSize: 20);
            return View();
        }

        [CheckSession(IsAuth = true)]
        public ActionResult AddOutcomes(int p)
        {
            ViewBag.pageNumber = p;
            ViewBag.OutcomeDetail = expendituresService.GetUnpayList();

            return View();
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult AddOutcomes(Outcomes outcome, int p)
        {
            if (TryUpdateModel(outcome, new string[] { "outcomedate", "expenditureid", "amount", "note" }) && ModelState.IsValid)
            {
                try
                {
                    outcome.outcomeid = Guid.NewGuid();
                    outcome.outcomecode = Librarys.NewOutcomeCode(DateTime.Now);
                    outcome.createdate = DateTime.Now;

                    outcomesService.Create(outcome);
                    outcomesService.SaveChanges();

                    General.CheckExpenditureStatus(outcome.expenditureid);

                    return RedirectToAction("Outcomes", new { p = p });
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
                ViewBag.OutcomeDetail = expendituresService.GetUnpayList();

                return View(outcome);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult EditOutcomes(Guid entityid, int p)
        {
            ViewBag.pageNumber = p;
            Outcomes outcome = outcomesService.GetByID(entityid);

            ViewBag.OutcomeDetail = expendituresService.GetUnpayList().Where(a => a.expenditureid == outcome.expenditureid);

            return View(outcome);
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult EditOutcomes(Guid outcomeid, int p, FormCollection form)
        {
            Outcomes outcome = outcomesService.GetByID(outcomeid);

            if (TryUpdateModel(outcome, new string[] { "outcomedate", "expenditureid", "amount", "note" }) && ModelState.IsValid)
            {
                try
                {
                    outcomesService.Update(outcome);
                    outcomesService.SaveChanges();

                    General.CheckExpenditureStatus(outcome.expenditureid);

                    return RedirectToAction("Outcomes", new { p = p });
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

                ViewBag.OutcomeDetail = expendituresService.GetUnpayList().Where(a => a.expenditureid == outcome.expenditureid);

                return View(outcome);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult DeleteOutcomes(Guid outcomeid, int p)
        {
            Outcomes outcome = outcomesService.GetByID(outcomeid);

            outcomesService.Delete(outcome);
            outcomesService.SaveChanges();

            General.CheckExpenditureStatus(outcome.expenditureid);

            return RedirectToAction("Outcomes", new { p = p });
        }

        [CheckSession(IsAuth = true)]
        public ActionResult Refounds(int p = 1)
        {
            var data = refoundsService.Get().OrderByDescending(a => a.refounddate).ThenByDescending(a => a.refoundcode);

            ViewBag.pageNumber = p;
            ViewBag.Refounds = data.ToPagedList(pageNumber: p, pageSize: 20);
            return View();
        }

        [CheckSession(IsAuth = true)]
        public ActionResult AddRefounds(int p)
        {
            ViewBag.pageNumber = p;
            PayMemberDropDownList();

            return View();
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult AddRefounds(Refounds refound, int p)
        {
            if (TryUpdateModel(refound, new string[] { "refounddate", "memberid", "returnid", "amount", "note" }) && ModelState.IsValid)
            {
                try
                {
                    refound.refoundid = Guid.NewGuid();
                    refound.refoundcode = Librarys.NewRefoundCode(DateTime.Now);
                    refound.createdate = DateTime.Now;

                    Returns rt = returnsService.GetByID(refound.returnid);
                    rt.refunddate = refound.refounddate;
                    rt.refundstatus = (int)EnumRefundStatus.已退款;

                    returnsService.Update(rt);
                    returnsService.SaveChanges();

                    Orders order = rt.Orders;
                    order.paystatus = (int)EnumPayStatus.退款;

                    ordersService.Update(order);
                    ordersService.SaveChanges();

                    refoundsService.Create(refound);
                    refoundsService.SaveChanges();

                    return RedirectToAction("Refounds", new { p = p });
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
                PayMemberDropDownList();

                return View(refound);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult EditRefounds(Guid entityid, int p)
        {
            ViewBag.pageNumber = p;
            Refounds refound = refoundsService.GetByID(entityid);

            ViewBag.RefoundDetail = refound.Returns;
            PayMemberDropDownList(refound.memberid);

            return View(refound);
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult EditRefounds(Guid refoundid, int p, FormCollection form)
        {
            Refounds refound = refoundsService.GetByID(refoundid);

            if (TryUpdateModel(refound, new string[] { "refounddate", "memberid", "returnid", "amount", "note" }) && ModelState.IsValid)
            {
                try
                {
                    Returns rt = refound.Returns;
                    rt.refunddate = refound.refounddate;

                    returnsService.Update(rt);
                    returnsService.SaveChanges();

                    refoundsService.Update(refound);
                    refoundsService.SaveChanges();

                    return RedirectToAction("Refounds", new { p = p });
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

                ViewBag.RefoundDetail = returnsService.GetUnpayListByMemberID(refound.memberid);
                PayMemberDropDownList(refound.memberid);

                return View(refound);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult DeleteRefounds(Guid refoundid, int p)
        {
            Refounds refound = refoundsService.GetByID(refoundid);

            Returns rt = refound.Returns;
            rt.refunddate = null; ;
            rt.refundstatus = (int)EnumRefundStatus.未退款;

            returnsService.Update(rt);
            returnsService.SaveChanges();

            Orders order = rt.Orders;
            order.paystatus = (int)EnumPayStatus.已付款;

            ordersService.Update(order);
            ordersService.SaveChanges();

            refoundsService.Delete(refound);
            refoundsService.SaveChanges();

            return RedirectToAction("Refounds", new { p = p });
        }

        [CheckSession(IsAuth = true)]
        public ActionResult Invoices(int p = 1)
        {
            var data = invoicesService.Get().OrderByDescending(a => a.requestdate).ThenByDescending(a => a.invoicecode);

            ViewBag.pageNumber = p;
            ViewBag.Invoices = data.ToPagedList(pageNumber: p, pageSize: 20);
            return View();
        }

        [CheckSession(IsAuth = true)]
        public ActionResult AddInvoices(int p)
        {
            ViewBag.pageNumber = p;
            UnpayMemberDropDownList();

            return View();
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult AddInvoices(Invoices invoice, int p, Guid[] orderid)
        {
            if (TryUpdateModel(invoice, new string[] { "requestdate", "memberid", "note" }) && ModelState.IsValid)
            {
                try
                {
                    invoice.invoiceid = Guid.NewGuid();
                    invoice.invoicecode = Librarys.NewInvoiceCode(DateTime.Now);
                    invoice.createdate = DateTime.Now;

                    IEnumerable<Orders> orders = ordersService.GetListByOrderIDs(orderid);
                    foreach (Orders order in orders)
                    {
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
                    }

                    invoicesService.Create(invoice);
                    invoicesService.SaveChanges();

                    return RedirectToAction("Invoices", new { p = p });
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
                UnpayMemberDropDownList();

                return View(invoice);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult EditInvoices(Guid entityid, int p)
        {
            ViewBag.pageNumber = p;
            Invoices invoice = invoicesService.GetByID(entityid);

            if(invoice.incomeid == null)
            {
                ViewBag.InvoiceDetail = ordersService.GetInvoicedetailsByMemberID(invoice.memberid);
            }
            else
            {
                ViewBag.InvoiceDetail = invoice.Invoicedetails;
            }

            return View(invoice);
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult EditInvoices(Guid invoiceid, int p, Guid[] orderid)
        {
            Invoices invoice = invoicesService.GetByID(invoiceid);

            if (TryUpdateModel(invoice, new string[] { "requestdate", "memberid", "note" }) && ModelState.IsValid)
            {
                try
                {
                    invoicesService.Update(invoice);
                    invoicesService.SaveChanges();

                    return RedirectToAction("Invoices", new { p = p });
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

                if (invoice.incomeid == null)
                {
                    ViewBag.InvoiceDetail = ordersService.GetInvoicedetailsByMemberID(invoice.memberid);
                }
                else
                {
                    ViewBag.InvoiceDetail = invoice.Invoicedetails;
                }

                return View(invoice);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult DeleteInvoices(Guid invoiceid, int p)
        {
            Invoices invoice = invoicesService.GetByID(invoiceid);

            if(invoice.incomeid == null)
            {
                invoicesService.Delete(invoice);
                invoicesService.SaveChanges();
            }

            return RedirectToAction("Invoices", new { p = p });
        }

        [CheckSession(IsAuth = true)]
        public ActionResult Incomes(int p = 1)
        {
            var data = incomesService.Get().OrderByDescending(a => a.incomedate).ThenByDescending(a => a.incomecode);

            ViewBag.pageNumber = p;
            ViewBag.Incomes = data.ToPagedList(pageNumber: p, pageSize: 20);
            return View();
        }

        [CheckSession(IsAuth = true)]
        public ActionResult AddIncomes(int p)
        {
            ViewBag.pageNumber = p;
            UnpayInvoiceDropDownList();

            return View();
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult AddIncomes(Incomes income, int p, Guid[] invoiceid)
        {
            if (TryUpdateModel(income, new string[] { "incomedate", "memberid", "amount", "fee", "note" }) && ModelState.IsValid)
            {
                try
                {
                    income.incomeid = Guid.NewGuid();
                    income.incomecode = Librarys.NewIncomeCode(DateTime.Now);
                    income.createdate = DateTime.Now;

                    incomesService.Create(income);
                    
                    IEnumerable<Invoices> invoices = invoicesService.GetListByInvoiceIDs(invoiceid);
                    foreach (Invoices invoice in invoices)
                    {
                        invoice.incomeid = income.incomeid;

                        foreach (Invoicedetails invoicedetail in invoice.Invoicedetails)
                        {
                            Orders order = invoicedetail.Orders;
                            order.paydate = income.incomedate;
                            order.paystatus = (int)EnumPayStatus.已付款;
                        }

                        invoicesService.Update(invoice);
                    }

                    invoicesService.SaveChanges();
                    incomesService.SaveChanges();

                    return RedirectToAction("Incomes", new { p = p });
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
                UnpayInvoiceDropDownList();

                return View(income);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult EditIncomes(Guid entityid, int p)
        {
            ViewBag.pageNumber = p;
            Incomes income = incomesService.GetByID(entityid);

            ViewBag.IncomeDetail = invoicesService.GetListByIncomeID(income.incomeid);

            return View(income);
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult EditIncomes(Guid incomeid, int p, Guid[] invoiceid)
        {
            Incomes income = incomesService.GetByID(incomeid);

            if (TryUpdateModel(income, new string[] { "incomedate", "memberid", "amount", "fee", "note" }) && ModelState.IsValid)
            {
                try
                {
                    IEnumerable<Invoices> ods = invoicesService.GetListByIncomeID(incomeid);
                    foreach (Invoices invoice in ods)
                    {
                        foreach (Invoicedetails invoicedetail in invoice.Invoicedetails)
                        {
                            Orders order = invoicedetail.Orders;
                            order.paydate = income.incomedate;

                            ordersService.Update(order);
                            ordersService.SaveChanges();
                        }
                    }

                    incomesService.Update(income);
                    incomesService.SaveChanges();

                    return RedirectToAction("Incomes", new { p = p });
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

                ViewBag.IncomeDetail = invoicesService.GetListByIncomeID(income.incomeid);

                return View(income);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult DeleteIncomes(Guid incomeid, int p)
        {
            Incomes income = incomesService.GetByID(incomeid);

            IEnumerable<Invoices> ods = invoicesService.GetListByIncomeID(incomeid);
            foreach (Invoices invoice in ods)
            {
                invoice.incomeid = Guid.Empty;

                invoicesService.Update(invoice);
                invoicesService.SaveChanges();

                foreach(Invoicedetails invoicedetail in invoice.Invoicedetails)
                {
                    Orders order = invoicedetail.Orders;
                    order.paydate = null;
                    order.paystatus = (int)EnumPayStatus.未付款;

                    ordersService.Update(order);
                    ordersService.SaveChanges();
                }
            }

            incomesService.Delete(income);
            incomesService.SaveChanges();

            return RedirectToAction("Incomes", new { p = p });
        }

        #region -- DropDownList ViewBag --
        private void PayMemberDropDownList(Object selectMembers = null)
        {
            var querys = returnsService.Get().Where(a => a.refundstatus == (int)EnumRefundStatus.未退款).AsEnumerable().Select(a => new Members
            {
                memberid = a.memberid,
                name = a.Members.name
            }).Distinct(new MemberCompare());

            ViewBag.memberid = new SelectList(querys, "memberid", "name", selectMembers);
        }
        private void UnpayMemberDropDownList(Object selectMembers = null)
        {
            var querys = ordersService.Get().Where(a => a.paystatus == (int)EnumPayStatus.未付款).AsEnumerable().Select(a => new Members {
                memberid = a.memberid,
                name = a.Members.name
            }).Distinct(new MemberCompare());

            ViewBag.memberid = new SelectList(querys, "memberid", "name", selectMembers);
        }
        private void UnpayInvoiceDropDownList(Object selectMembers = null)
        {
            var querys = invoicesService.Get().Where(a => a.incomeid == null).AsEnumerable().Select(a => new Members
            {
                memberid = a.memberid,
                name = a.Members.name
            }).Distinct(new MemberCompare());

            ViewBag.memberid = new SelectList(querys, "memberid", "name", selectMembers);
        }
        #endregion
    }

    class SupplierCompare : IEqualityComparer<Suppliers>
    {
        #region IEqualityComparer<Suppliers> Suppliers

        public bool Equals(Suppliers x, Suppliers y)
        {
            return x.supplierid.Equals(y.supplierid);
        }

        public int GetHashCode(Suppliers obj)
        {
            return obj.supplierid.GetHashCode();
        }
        #endregion
    }

    class MemberCompare : IEqualityComparer<Members>
    {
        #region IEqualityComparer<Members> Members

        public bool Equals(Members x, Members y)
        {
            return x.memberid.Equals(y.memberid);
        }

        public int GetHashCode(Members obj)
        {
            return obj.memberid.GetHashCode();
        }

        #endregion
    }
}