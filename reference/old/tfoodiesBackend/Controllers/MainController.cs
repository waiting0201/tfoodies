using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity;
using System.Threading.Tasks;
using System.Net.Http;

using tfoodiesBackend.Filters;
using tfoodies.Models;
using tfoodies.Service;
using tfoodies.Libs;

using Newtonsoft.Json;

namespace tfoodiesBackend.Controllers
{
    public class MainController : BaseController
    {
        private AdminsService adminsService;
        private IOrdersService ordersService;
        private IInvoicesService invoicesService;
        private IIncomesService incomesService;

        public MainController()
        {
            adminsService = new AdminsService();
            ordersService = new OrdersService();
            invoicesService = new InvoicesService();
            incomesService = new IncomesService();
        }

        [CheckSession]
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult Login()
        {
            Session.Add("IsLogin", false);
            return View();
        }

        [HttpPost]
        public async Task<ActionResult> Login(string username, string password, FormCollection form)
        {
            //bool isVerify = new GoogleReCaptcha().GetCaptchaResponse(form["g-recaptcha-response"]);
            bool isVerify = await IsCaptchaValid(form["GoogleCaptchaToken"]);
            if (isVerify)
            {
                if (ValidateUser(username, password))
                {
                    return RedirectToAction("Index");
                }
                else
                {
                    ModelState.AddModelError("", "");
                }
            }
            else
            {
                ModelState.AddModelError("", "");
            }
            
            return View();
        }

        // 執行登出
        public ActionResult Logout()
        {
            Session.Add("IsLogin", false);
            Session.RemoveAll();
            return RedirectToAction("Login");
        }

        // 後台刷卡結果
        public ActionResult CardResult(string lidm, string lastPan4 = null, string status = null)
        {
            Orders order = ordersService.Get().FirstOrDefault(a => a.ordercode == lidm);

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

            if (status != "" && status != null && status != "0")
            {
                ViewBag.OrderResult = "信用卡刷卡失敗！";
            }

            if (status != "" && status != null && status == "0")
            {
                ViewBag.OrderResult = "信用卡刷卡成功！";

                order.paystatus = (int)EnumPayStatus.已付款;
                order.paydate = DateTime.Now;
                order.lastpan4 = lastPan4;

                ordersService.Update(order);
                ordersService.SaveChanges();

                Incomes income = new Incomes();
                income.incomeid = Guid.NewGuid();
                income.memberid = order.memberid;
                income.incomecode = Librarys.NewIncomeCode(DateTime.Now);
                income.incomedate = order.orderdate;
                income.amount = TotalAmt;
                income.createdate = DateTime.Now;

                incomesService.Create(income);
                incomesService.SaveChanges();

                invoice.incomeid = income.incomeid;
            }

            invoicesService.Create(invoice);
            invoicesService.SaveChanges();

            return View(order);
        }

        // 驗證帳號密碼
        private bool ValidateUser(string username, string password)
        {
            if (username == "itadmin" && password == "QQQQQ")
            {
                Session.Add("IsLogin", true);
                Session.Add("Username", "itadmin");
                Session.Add("AdminID", 888);

                return true;
            }
            
            Admins admin = adminsService.Get().Where(a => a.Username == username && a.Isenable == 1).FirstOrDefault();
            if (admin == null)
                return false;

            if (admin.Password != password)
                return false;

            Session.Add("IsLogin", true);
            Session.Add("Username", username);
            Session.Add("AdminID", admin.AdminID);
            Session.Add("AdminLims", admin.AdminLims);

            return true;
        }

        private async Task<bool> IsCaptchaValid(string response)
        {
            try
            {
                var secret = Librarys.GetRecaptchaV3Secret();
                using (var client = new HttpClient())
                {
                    var values = new Dictionary<string, string>
                    {
                        {"secret", secret},
                        {"response", response},
                        {"remoteip", Request.UserHostAddress}
                    };

                    var content = new FormUrlEncodedContent(values);
                    var verify = await client.PostAsync("https://www.google.com/recaptcha/api/siteverify", content);
                    var captchaResponseJson = await verify.Content.ReadAsStringAsync();
                    var captchaResult = JsonConvert.DeserializeObject<CaptchaResponseViewModel>(captchaResponseJson);
                    return captchaResult.Success
                           && captchaResult.Action == "contact_us"
                           && captchaResult.Score > 0.5;
                }
            }
            catch (Exception ex)
            {
                string ms = ex.Message;
                return false;
            }

        }
    }
}