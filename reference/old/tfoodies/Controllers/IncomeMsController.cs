using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Globalization;

using tfoodies.Commons;
using tfoodies.Models;
using tfoodies.Service;
using tfoodies.Libs;

namespace tfoodies.Controllers
{
    public class IncomeMsController : Controller
    {
        private IOrdersService ordersService;
        private IInvoicesService invoicesService;
        private IInvoiceDetailsService invoicedetailsService;
        private IIncomesService incomesService;
        private IGlobalmyb2bService globalmyb2bService;

        public IncomeMsController()
        {
            tfoodiesEntities db = new tfoodiesEntities();
            ordersService = new OrdersService();
            invoicesService = new InvoicesService(db);
            invoicedetailsService = new InvoiceDetailsService();
            incomesService = new IncomesService(db);
            globalmyb2bService = new Globalmyb2bService();
        }

        public async Task<ActionResult> Index()
        {
            //string date = DateTime.Now.ToString("yyyyMMdd");
            string date = "20190325";

            Dictionary<string, string> postData = new Dictionary<string, string>();
            postData.Add("cust_id", "45889852");
            postData.Add("cust_nickname", "avator");
            postData.Add("cust_pwd", "ar12ab");
            postData.Add("acno", "006035013669");
            postData.Add("from_date", date);
            postData.Add("to_date", date);
            postData.Add("xml", "Y");
            postData.Add("txdate8", "Y");

            HttpClient client = new HttpClient();

            HttpContent contentPost = new FormUrlEncodedContent(postData);
            HttpResponseMessage response = await client.PostAsync("https://www.globalmyb2b.com/securities/tx10d0_txt.aspx", contentPost);

            HttpContent content = response.Content;
            string result = await content.ReadAsStringAsync();

            //string result = "<?xml version=\"1.0\" encoding=\"BIG5\" ?><TX10D0 error_id=\"0\" error_msg=\"\"><TXDETAIL><BACCNO>006035013669</BACCNO><TX_DATE>20190307</TX_DATE><TX_SEQNO>67093</TX_SEQNO><TX_IDNO>1743</TX_IDNO><SPACE>  </SPACE><CHNO>        </CHNO><DC>2</DC><SIGN> +</SIGN><AMOUNT>1920</AMOUNT><BSIGN> +</BSIGN><BAMOUNT>12305</BAMOUNT><MEMO1>1943190307000019</MEMO1><TX_MACH>0999</TX_MACH><TX_SPEC>跨行轉入</TX_SPEC><BANKID>017</BANKID><ACCNAME>   </ACCNAME><MEMO2>0170000000608480942</MEMO2><TX_TIME>182849</TX_TIME></TXDETAIL></TX10D0>";

            XDocument doc = XDocument.Parse(result);
            XElement eleTX10D0 = doc.Element("TX10D0");
            string error_id = eleTX10D0.Attribute("error_id").Value;
            string error_msg = eleTX10D0.Attribute("error_msg").Value;

            GlobalMyB2B globalMyB2B = new GlobalMyB2B();
            globalMyB2B.globalmyb2bid = Guid.NewGuid();
            globalMyB2B.error_id = error_id;
            globalMyB2B.error_msg = error_msg;
            globalMyB2B.remark = result;
            globalMyB2B.createdate = DateTime.Now;

            if (error_id == "1")
            {
                
            }
            else
            {
                IEnumerable<XElement> allEle = doc.Element("TX10D0").Elements();
                foreach (XElement xeleTXDETAIL in allEle)
                {
                    string n = xeleTXDETAIL.Name.ToString();
                    string amount = xeleTXDETAIL.Element("AMOUNT").Value.ToString().Trim();
                    string atmcode = xeleTXDETAIL.Element("MEMO1").Value.ToString().Trim();
                    string txdate = xeleTXDETAIL.Element("TX_DATE").Value.ToString().Trim();
                    string txtime = xeleTXDETAIL.Element("TX_TIME").Value.ToString().Trim();

                    Orders order = ordersService.Get().FirstOrDefault(a => a.codeatm == atmcode);
                    Invoicedetails invoicedetail = invoicedetailsService.Get().FirstOrDefault(a => a.orderid == order.orderid);
                    Invoices invoice = invoicesService.GetByID(invoicedetail.invoiceid);

                    if(invoicedetail.price.ToString() == amount)
                    {
                        order.paystatus = (int)EnumPayStatus.已付款;
                        order.paydate = DateTime.ParseExact(txdate + txtime, "yyyyMMddHHmmss", null, DateTimeStyles.AllowWhiteSpaces);

                        ordersService.Update(order);
                        ordersService.SaveChanges();

                        Incomes income = new Incomes();
                        income.incomeid = Guid.NewGuid();
                        income.memberid = order.memberid;
                        income.incomecode = Librarys.NewIncomeCode(DateTime.Now);
                        income.incomedate = order.orderdate;
                        income.amount = Convert.ToInt32(amount);
                        income.createdate = DateTime.Now;

                        incomesService.Create(income);
                        incomesService.SaveChanges();

                        invoice.incomeid = income.incomeid;

                        invoicesService.Update(invoice);
                        invoicesService.SaveChanges();
                    }
                }
            }

            globalmyb2bService.Create(globalMyB2B);
            globalmyb2bService.SaveChanges();

            ViewBag.Res = result;
            ViewBag.Err = error_id;

            return View();
        }
    }
}