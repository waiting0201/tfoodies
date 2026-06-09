using System;
using System.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Script.Serialization;
using System.Text;
using Newtonsoft.Json;
using System.IO;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using MongoDB.Bson;
using MongoDB.Driver;
using tfoodies.Libs;
using tfoodies.Commons;
using tfoodies.Models;
using tfoodies.Service;
using tfoodies.Filters;
using UAParser;
using System.Net.Http;
using System.Threading.Tasks;

namespace tfoodies.Controllers
{
    public class AjaxController : BaseController
    {
        private tfoodiesEntities db;
        private IProductsService productsService;
        private IMembersService membersService;
        private IOrdersService ordersService;
        private IOrderDetailsService orderdetailsService;
        private IZipcodesService zipcodesService;
        private IOutofnoticesService outofnoticesService;
        private IPreordersService preordersService;
        private IDiscountsService discountsService;
        private IViewLogsService viewlogsService;

        public AjaxController()
        {
            db = new tfoodiesEntities();
            productsService = new ProductsService(db);
            membersService = new MembersService(db);
            ordersService = new OrdersService(db);
            orderdetailsService = new OrderDetailsService(db);
            zipcodesService = new ZipcodesService();
            outofnoticesService = new OutofnoticesService();
            preordersService = new PreordersService();
            discountsService = new DiscountsService();
            viewlogsService = new ViewLogsService();
        }

        [HttpPost]
        public ActionResult GetZipcodeByCity(string city)
        {
            IEnumerable<Zipcodes> zipcodes = zipcodesService.Get().Where(a => a.city == city).OrderBy(a => a.zipcode);
            string option = "<option value=\"\">鄉鎮市區</option>";

            foreach(Zipcodes zipcode in zipcodes)
            {
                option += "<option value=\"" + zipcode.zipcodeid + "\">" + zipcode.area + "</option>";
            }

            return Content(option);
        }

        [HttpPost]
        public ActionResult AddToCart(Guid productid, int qty)
        {
            Cart cart = new Cart();
            int cartqty = 0;

            Products product = productsService.GetByID(productid);
            CartItem ci = cart.FindByProductid(product.productid);
            if (ci != null)
            {
                cartqty = ci.Quantity + qty;
            }
            else
            {
                cartqty = qty;
            }

            if(product.added < cartqty)
            {
                return Json(new { code = "220", message = product.added });
            }
            else
            {
                CartItem item = new CartItem()
                {
                    ProductId = product.productid,
                    Name = product.title,
                    Capacity = product.capacity,
                    Photo = product.photo,
                    FixPrice = Convert.ToInt32(product.fixprice),
                    Price = product.price,
                    Quantity = qty,
                    Added = product.added
                };

                cart.Insert(item);

                return PartialView("_PartialCartItem", cart.Contents());
            }
        }

        [HttpPost]
        public ActionResult RemoveToCart(Guid rowid)
        {
            string rscode = string.Empty;
            string rsmessage = string.Empty;

            Cart cart = new Cart();
            bool isremove = cart.Remove(rowid);

            if(isremove)
            {
                rscode = "200";
                rsmessage = "成功刪除";
            }else
            {
                rscode = "201";
                rsmessage = "未刪除";
            }

            return Json(new { code = rscode, message = rsmessage });
        }

        [HttpPost]
        public ActionResult UpdateToCart(Guid rowid, int qty)
        {
            string rscode = string.Empty;
            string rsmessage = string.Empty;
            string subtotal = string.Empty;
            string totalprice = string.Empty;
            string freightprice = string.Empty;
            string discountprice = string.Empty;
            string amountprice = string.Empty;

            Cart cart = new Cart();
            CartItem obj = cart.FindByRowid(rowid);

            if (obj != null)
            {
                Products product = productsService.GetByID(obj.ProductId);

                if (product.added >= obj.Quantity + qty)
                {
                    CartItem item = cart.Update(rowid, qty);
                    //必須在cart.Update之後
                    DiscountResponse dr = General.GetDiscount();

                    rscode = "200";
                    rsmessage = "成功更新";
                    subtotal = item.SubTotal.ToString("#,0");
                    totalprice = cart.TotalPrice().ToString("#,0");
                    freightprice = General.GetFreight(cart.TotalPrice()).ToString("#,0");
                    discountprice = dr.discount.ToString("#,0");
                    amountprice = General.GetAmountPrice(cart.TotalPrice()).ToString("#,0");
                }
                else
                {
                    rscode = "230";
                    rsmessage = "庫存不足！";
                }
            }
            else
            {
                rscode = "220";
                rsmessage = "未更新";
            }

            return Json(new { code = rscode, message = rsmessage, subtotal = subtotal, totalprice = totalprice, freightprice = freightprice, discountprice = discountprice, amountprice = amountprice });
        }

        [HttpPost]
        public async Task<ActionResult> Login(string mobile, string password, FormCollection form, string isremember = null)
        {
            string rscode = string.Empty;
            string rsmessage = string.Empty;
            string rscart = string.Empty;

            //bool isVerify = new GoogleReCaptcha().GetCaptchaResponse(form["g-recaptcha-response"]);
            bool isVerify = await IsCaptchaValid(form["GoogleCaptchaToken"]);
            //bool isVerify = true;
            if (isVerify)
            {
                if(membersService.ValidateUser(mobile, password))
                {
                    if (isremember == "y")
                    {
                        string source = "mobile=" + mobile + "&password=" + password;

                        if (Request.Cookies["tfd"] == null)
                        {
                            HttpCookie cookie = new HttpCookie("tfd");
                            cookie.Value = General.EncryptCookie(source);    
                            cookie.Expires = DateTime.Now.AddMonths(3);
                            Response.Cookies.Add(cookie);
                        }
                        else
                        {
                            HttpCookie cookie = Request.Cookies["tfd"];
                            cookie.Value = General.EncryptCookie(source);
                            cookie.Expires = DateTime.Now.AddMonths(3);
                            Request.Cookies.Set(cookie);
                        }
                    }

                    Cart cart = new Cart();
                    if (cart.TotalItems() > 0) rscart = "y";

                    rscode = "200";
                    rsmessage = "登入成功";
                }
                else
                {
                    rscode = "201";
                    rsmessage = "登入失敗";
                }
            }
            else
            {
                rscode = "300";
                rsmessage = "驗證碼錯誤";
            }

            return Json(new { code = rscode, message = rsmessage, iscart = rscart });
        }

        [HttpPost]
        public ActionResult PostOutofnotice(Guid productid, string name, string email, string mobile, string inputcode, Guid? memberid = null)
        {
            string rscode = string.Empty;
            string rsmessage = string.Empty;

            if (inputcode != null && inputcode.Trim().ToLower().Equals(Session["ValidateCode"].ToString().ToLower()))
            {
                Outofnotices outofnotice = new Outofnotices();
                outofnotice.outofnoticeid = Guid.NewGuid();
                outofnotice.productid = productid;
                outofnotice.name = name;
                outofnotice.email = email;
                outofnotice.mobile = mobile;
                outofnotice.createdate = DateTime.Now;
                outofnotice.isnotice = false;
                if (memberid != null) outofnotice.memberid = memberid;

                outofnoticesService.Create(outofnotice);
                outofnoticesService.SaveChanges();

                rscode = "200";
                rsmessage = "感謝您，已成功通知！";
            }
            else
            {
                rscode = "300";
                rsmessage = "驗證碼錯誤";
            }

            return Json(new { code = rscode, message = rsmessage });
        }

        [HttpPost]
        public async Task<ActionResult> PostOrder(string name, int zipcodeid, string address, string recivername, string recivermobile, int reciverzipcodeid, string reciveraddress, int recivertime, int invoicetype, int paytype, FormCollection form, string mobile = null, string password = null, string email = null, string companynumber = null, string companytitle = null, string remark = null, int? gender = null, int? year = null, int? month = null, int? day = null)
        {
            string rscode = string.Empty;
            string rsmessage = string.Empty;
            string rsordercode = string.Empty;
            string rspaytype = string.Empty;

            //bool isVerify = new GoogleReCaptcha().GetCaptchaResponse(form["GoogleCaptchaToken"]);
            bool isVerify = await IsCaptchaValid(form["GoogleCaptchaToken"]);
            //bool isVerify = true;
            if (isVerify)
            {
                Members member = null;
                Orders order = null;
                bool isvalid = true;

                Cart ca = new Cart();

                if(ca.Contents().Count() > 0)
                {
                    if (Session["IsLogin"] != null && (bool)Session["IsLogin"])
                    {
                        Guid memberid = new Guid(Session["MemberID"].ToString());

                        member = membersService.GetByID(memberid);
                    }
                    else
                    {
                        member = membersService.Get().Where(a => a.mobile == mobile).FirstOrDefault();
                        if (member == null)
                        {
                            member = new Members();
                            member.memberid = Guid.NewGuid();
                            member.name = name;
                            member.mobile = mobile;
                            member.password = password;
                            member.email = email;
                            member.zipcodeid = zipcodeid;
                            member.address = address;
                            member.gender = gender;
                            if(year != null && month != null && day != null) member.birthday = Convert.ToDateTime(year + "-" + month + "-" + day);
                            member.createdate = DateTime.Now;
                            member.ismember = 1;
                            member.isenable = true;

                            membersService.Create(member);
                            membersService.SaveChanges();
                        }
                        else if (member != null && member.ismember == 2 && member.isenable == true)
                        {
                            member.name = name;
                            member.password = password;
                            member.email = email;
                            member.zipcodeid = zipcodeid;
                            member.address = address;
                            member.ismember = 1;

                            membersService.Update(member);
                            membersService.SaveChanges();

                            membersService.ValidateUser(mobile, password);
                        }
                        else
                        {
                            rscode = "220";
                            rsmessage = "系統出現重複問題，請通知管理員！";
                            isvalid = false;
                        }
                    }

                    if (isvalid)
                    {
                        string ordercode = string.Empty;
                        DiscountResponse dr = General.GetDiscount();

                        // 判斷是否同一個session下第一次送出訂單，當選擇刷卡的訂單會跳出popup，如果關popup，再按一次送出就會變成第二次送出，然後更新訂單資料
                        if (Session["orderCode"] != null && (string)Session["orderCode"] != "")
                        {
                            ordercode = (string)Session["orderCode"];

                            order = ordersService.Get().Where(a => a.ordercode == ordercode).FirstOrDefault();

                            //判斷付款方式是否改為虛擬帳號
                            if (paytype == (int)EnumPayType.ATM轉帳付款)
                            {
                                int newtotal = General.GetAmountPrice(ca.TotalPrice());

                                int limit = Convert.ToInt32(ConfigurationManager.AppSettings["paylimit"]);
                                order.codeatm = Librarys.GetAtmCode(newtotal, limit);
                                order.expirepaydate = DateTime.Now.AddDays(limit);

                                //表示折扣碼輸入成功時，要把discountid這個外鍵給訂單，刷卡的話則要在刷卡成功再給
                                if (dr.rscode == "200")
                                {
                                    order.discountid = dr.discountid;
                                }
                            }
                            else if (paytype == (int)EnumPayType.宅配貨到付款)
                            {
                                //表示折扣碼輸入成功時，要把discountid這個外鍵給訂單，刷卡的話則要在刷卡成功再給
                                if (dr.rscode == "200")
                                {
                                    order.discountid = dr.discountid;
                                }
                            }

                            order.memberid = member.memberid;
                            order.orderdate = DateTime.Now;
                            order.recivername = recivername;
                            order.recivermobile = recivermobile;
                            order.reciverzipcodeid = reciverzipcodeid;
                            order.reciveraddress = reciveraddress;
                            order.recivertime = recivertime;
                            order.freight = General.GetFreight(ca.TotalPrice());
                            order.discount = dr.discount;
                            order.total = ca.TotalPrice();
                            order.paytype = paytype;
                            order.invoicetype = invoicetype;
                            order.companynumber = companynumber;
                            order.companytitle = companytitle;
                            order.createdate = DateTime.Now;
                            order.isdeclaration = false;
                            order.remark = remark;
                            if (Session["RID"] != null) order.RID = (string)Session["RID"];
                            if (Session["Click_ID"] != null) order.Click_ID = (string)Session["Click_ID"];

                            foreach (Orderdetails od in order.Orderdetails.ToArray())
                            {
                                if (!ca.Contents().ToList().Exists(x => x.ProductId == od.productid))
                                {
                                    order.Orderdetails.Remove(od);
                                    orderdetailsService.Delete(od.orderdetailid);
                                }
                            }

                            foreach (CartItem item in ca.Contents())
                            {
                                if (order.Orderdetails.ToList().Exists(x => x.productid == item.ProductId))
                                {
                                    Orderdetails od = order.Orderdetails.Where(a => a.productid == item.ProductId).FirstOrDefault();
                                    od.qty = item.Quantity;
                                    od.price = item.Price;
                                    od.subtotal = item.SubTotal;
                                    od.status = 0;
                                }
                                else
                                {
                                    Orderdetails detail = new Orderdetails();
                                    detail.orderdetailid = Guid.NewGuid();
                                    detail.orderid = order.orderid;
                                    detail.productid = item.ProductId;
                                    detail.qty = item.Quantity;
                                    detail.price = item.Price;
                                    detail.subtotal = item.SubTotal;
                                    detail.status = 0;

                                    order.Orderdetails.Add(detail);
                                }
                            }

                            ordersService.Update(order);
                        }
                        else
                        {
                            ordercode = Librarys.NewOrderCode(DateTime.Now);

                            order = new Orders();
                            order.orderid = Guid.NewGuid();
                            order.memberid = member.memberid;
                            order.ordercode = ordercode;
                            order.ordertype = (int)EnumOrderType.線上單;
                            order.warehousetypeid = (int)EnumWarehouseType.線上倉;
                            order.orderdate = DateTime.Now;
                            order.recivername = recivername;
                            order.recivermobile = recivermobile;
                            order.reciverzipcodeid = reciverzipcodeid;
                            order.reciveraddress = reciveraddress;
                            order.recivertime = recivertime;
                            order.freight = General.GetFreight(ca.TotalPrice());
                            order.discount = dr.discount;
                            order.total = ca.TotalPrice();
                            order.paytype = paytype;
                            order.invoicetype = invoicetype;
                            order.companynumber = companynumber;
                            order.companytitle = companytitle;
                            order.createdate = DateTime.Now;
                            order.isdeclaration = false;
                            order.remark = remark;
                            if (Session["RID"] != null) order.RID = (string)Session["RID"];
                            if (Session["Click_ID"] != null) order.Click_ID = (string)Session["Click_ID"];

                            if (paytype == (int)EnumPayType.ATM轉帳付款)
                            {
                                int limit = Convert.ToInt32(ConfigurationManager.AppSettings["paylimit"]);
                                order.codeatm = Librarys.GetAtmCode(General.GetAmountPrice(ca.TotalPrice()), limit);
                                order.expirepaydate = DateTime.Now.AddDays(limit);

                                //表示折扣碼輸入成功時，要把discountid這個外鍵給訂單，刷卡的話則要在刷卡成功再給
                                if (dr.rscode == "200")
                                {
                                    order.discountid = dr.discountid;
                                }
                            }
                            else if (paytype == (int)EnumPayType.宅配貨到付款)
                            {
                                //表示折扣碼輸入成功時，要把discountid這個外鍵給訂單，刷卡的話則要在刷卡成功再給
                                if (dr.rscode == "200")
                                {
                                    order.discountid = dr.discountid;
                                }
                            }

                            foreach (CartItem item in ca.Contents())
                            {
                                Orderdetails detail = new Orderdetails();
                                detail.orderdetailid = Guid.NewGuid();
                                detail.orderid = order.orderid;
                                detail.productid = item.ProductId;
                                detail.qty = item.Quantity;
                                detail.price = item.Price;
                                detail.subtotal = item.SubTotal;
                                detail.status = 0;

                                order.Orderdetails.Add(detail);
                            }

                            ordersService.Create(order);
                        }

                        ordersService.SaveChanges();

                        rscode = "200";
                        rsmessage = "訂購成功";
                        rsordercode = ordercode;
                        rspaytype = Librarys.GetPayType(paytype);
                    }
                }
                else
                {
                    rscode = "201";
                    rsmessage = "訂單錯誤";
                }
            }
            else
            {
                rscode = "300";
                rsmessage = "驗證碼錯誤";
            }

            return Json(new { code = rscode, message = rsmessage, ordercode = rsordercode, paytype = rspaytype });
        }

        [HttpPost]
        public ActionResult PostPreorder(string recivername, string recivermobile, int reciverzipcodeid, string reciveraddress, int recivertime, int invoicetype, int paytype, string inputcode, string companynumber = null, string companytitle = null)
        {
            string rscode = string.Empty;
            string rsmessage = string.Empty;
            string rsordercode = string.Empty;
            string rspaytype = string.Empty;

            if (inputcode != null && inputcode.Trim().ToLower().Equals(Session["ValidateCode"].ToString().ToLower()))
            {
                Preorders preorder = null;
                bool isvalid = true;

                if (isvalid)
                {
                    string preordercode = string.Empty;
                    
                    preordercode = Librarys.NewOrderCode(DateTime.Now);

                    preorder = new Preorders();
                    preorder.preorderid = Guid.NewGuid();
                    preorder.preordercode = preordercode;
                    preorder.preorderdate = DateTime.Now;
                    preorder.recivername = recivername;
                    preorder.recivermobile = recivermobile;
                    preorder.reciverzipcodeid = reciverzipcodeid;
                    preorder.reciveraddress = reciveraddress;
                    preorder.recivertime = recivertime;
                    //preorder.total = ca.TotalPrice();
                    preorder.paytype = paytype;
                    preorder.invoicetype = invoicetype;
                    preorder.companynumber = companynumber;
                    preorder.companytitle = companytitle;
                    preorder.createdate = DateTime.Now;

                    if (paytype == (int)EnumPayType.ATM轉帳付款)
                    {
                        int limit = Convert.ToInt32(ConfigurationManager.AppSettings["paylimit"]);
                        preorder.codeatm = Librarys.GetAtmCode(preorder.total, limit);
                        preorder.expirepaydate = DateTime.Now.AddDays(limit);
                    }

                    //foreach (CartItem item in ca.Contents())
                    //{
                    //    Orderdetails detail = new Orderdetails();
                    //    detail.orderdetailid = Guid.NewGuid();
                    //    detail.orderid = order.orderid;
                    //    detail.productid = item.ProductId;
                    //    detail.qty = item.Quantity;
                    //    detail.price = item.Price;
                    //    detail.subtotal = item.SubTotal;
                    //    detail.status = 0;

                    //    order.Orderdetails.Add(detail);
                    //}

                    preordersService.Create(preorder);

                    preordersService.SaveChanges();

                    rscode = "200";
                    rsmessage = "訂購成功";
                    rsordercode = preordercode;
                    rspaytype = Librarys.GetPayType(paytype);
                }
            }
            else
            {
                rscode = "300";
                rsmessage = "驗證碼錯誤";
            }

            return Json(new { code = rscode, message = rsmessage, ordercode = rsordercode, paytype = rspaytype });
        }

        [CheckSession]
        [HttpPost]
        public ActionResult EditProfile(string name, string email, int zipcodeid, string address, int? gender = null, int? year = null, int? month = null, int? day = null)
        {
            string rscode = string.Empty;
            string rsmessage = string.Empty;

            if (Session["IsLogin"] != null && (bool)Session["IsLogin"])
            {
                Guid memberid = new Guid(Session["MemberID"].ToString());
                Members member = membersService.GetByID(memberid);

                member.name = name;
                member.email = email;
                member.zipcodeid = zipcodeid;
                member.address = address;
                if (gender != null) member.gender = gender;
                if (year != null && month != null && day != null) member.birthday = Convert.ToDateTime(year + "-" + month + "-" + day);

                membersService.Update(member);
                membersService.SaveChanges();

                rscode = "200";
            }

            return Json(new { code = rscode });
        }

        [CheckSession]
        [HttpPost]
        public ActionResult EditPassword(string password, string cpassword)
        {
            string rscode = string.Empty;
            string rsmessage = string.Empty;

            if (Session["IsLogin"] != null && (bool)Session["IsLogin"])
            {
                if(password == cpassword)
                {
                    Guid memberid = new Guid(Session["MemberID"].ToString());
                    Members member = membersService.GetByID(memberid);

                    member.password = password;

                    membersService.Update(member);
                    membersService.SaveChanges();

                    rscode = "200";
                }
                else
                {
                    rscode = "220";
                }
                
            }

            return Json(new { code = rscode });
        }

        [HttpPost]
        public async Task<ActionResult> PasswordSend(string mobile, string email, FormCollection form)
        {
            string rscode = string.Empty;
            string rsmessage = string.Empty;

            bool isVerify = await IsCaptchaValid(form["GoogleCaptchaToken"]);
            if (isVerify)
            {
                Members member = membersService.Get().Where(a => a.mobile == mobile && a.email == email).FirstOrDefault();
                if (member != null)
                {
                    string letters = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz023456789";
                    Random random = new Random();
                    StringBuilder sb = new StringBuilder();
                    for (int word = 0; word < 6; word++)
                    {
                        string letter = letters.Substring(random.Next(0, letters.Length - 1), 1);
                        sb.Append(letter);
                    }

                    member.password = sb.ToString();

                    membersService.Update(member);
                    membersService.SaveChanges();

                    string sSubject = "食在呼 TFoodies–忘記密碼通知";
                    string sFrom = "食在呼 TFoodies";

                    StringBuilder mailsb = new StringBuilder();
                    mailsb.Append("<div style=\"background:#F6F6F6; font-family:Verdana, Arial, Helvetica, sans-serif; font-size:12px; margin:0; padding:0;\">");
                    mailsb.Append("<table cellspacing=\"0\" cellpadding=\"0\" border=\"0\" height=\"100%\" width=\"100%\">");
                    mailsb.Append("<tr>");
                    mailsb.Append("<td align=\"center\" valign=\"top\" style=\"padding:20px 0 20px 0\">");
                    mailsb.Append("<table bgcolor=\"#FFFFFF\" cellspacing=\"0\" cellpadding=\"10\" border=\"0\" width=\"650\" style=\"border:1px solid #E0E0E0;\">");
                    mailsb.Append("<tr><td valign=\"top\"><a href=\"http://www.tfoodies.com\"><img src=\"cid:logo-10.png\" width=\"150\" style=\"margin-bottom:10px;\" border=\"0\"/></a></td></tr>");
                    mailsb.Append("<tr><td valign=\"top\">");
                    mailsb.Append("<h1 style=\"font-size:22px; font-weight:normal; line-height:22px; margin:0 0 11px 0;\">親愛的 顧客 您好</h1>");
                    mailsb.Append("</td></tr>");
                    mailsb.Append("<tr><td><h2 style=\"font-size:18px;font-weight:normal;margin:0\">您的新密碼：" + member.password + "</h2></td></tr>");
                    mailsb.Append("<tr><td><p style=\"font-size:12px; line-height:16px; margin:0 0 10px 0;\">請登入【<a href=\"https://www.tfoodies.com/Login\">食在呼 - 會員中心</a>】，並且更新您的密碼，謝謝！</p></td></tr>");
                    mailsb.Append("<tr><td><p style=\"font-size:12px; line-height:16px; margin:0 0 10px 0;\">※此為系統自動通知信，請勿直接回覆！</p></td></tr>");
                    mailsb.Append("<tr><td bgcolor=\"#EAEAEA\" align=\"center\" style=\"background:#EAEAEA; text-align:center;\"><center><p style=\"font-size:12px; margin:0;\"><strong>食在呼 TFoodies</strong>，再次感謝您！</p></center></td></tr>");
                    mailsb.Append("</table>");
                    mailsb.Append("</td>");
                    mailsb.Append("</tr>");
                    mailsb.Append("</table>");
                    mailsb.Append("</div>");
                    string[] img = new String[] { Path.Combine(Server.MapPath("~/Content/images/common/"), "logo-10.png") };

                    Librarys.SendMail(member.email, sFrom, sSubject, mailsb.ToString(), img);

                    rscode = "200";
                    rsmessage = "密碼已寄出，請至您的Email信箱收信！";
                }
                else
                {
                    rscode = "220";
                    rsmessage = "查無此會員";
                }
            }
            else
            {
                rscode = "300";
                rsmessage = "驗證碼錯誤";
            }

            return Json(new { code = rscode, message = rsmessage });
        }

        [HttpGet]
        public ActionResult Checkmobile(string mobile)
        {
            Members member = membersService.Get().Where(a => a.mobile == mobile.Trim() && a.ismember == 1).FirstOrDefault();
            if(member == null)
            {
                return Json(true, JsonRequestBehavior.AllowGet);
            }else
            {
                return Json(false, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        public ActionResult CheckCompanynumber(string companynumber)
        {
            string jsonData = Librarys.GetCompanynumber(companynumber);

            if(jsonData == null || jsonData == "")
            {
                return Json(false);
            }
            else
            {
                return Json(true);
            }
        }

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

        [HttpPost]
        public ActionResult GetSubCompanyTitle(string companynumber)
        {
            string jsonString = Librarys.GetSubCompanytitle(companynumber);

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

        public ActionResult CheckShoppingCartItem()
        {
            string rscode = string.Empty;
            string rsmessage = string.Empty;

            Cart ca = new Cart();
            if(ca.TotalItems() > 0)
            {
                rscode = "200";
            }else
            {
                rscode = "201";
            }

            return Json(new { code = rscode });
        }

        [HttpPost]
        public ActionResult AddToMylist(Guid productid)
        {
            string rscode = string.Empty;
            string rsmessage = string.Empty;

            if(Session["IsLogin"] != null && (bool)Session["IsLogin"])
            {
                Guid memberid = new Guid(Session["MemberID"].ToString());

                Products product = productsService.GetByID(productid);
                Members member = membersService.GetByID(memberid);
                member.Products.Add(product);
                membersService.SaveChanges();

                rscode = "200";
            }
            else
            {
                rscode = "201";
            }

            return Json(new { code = rscode });
        }

        [CheckSession]
        [HttpPost]
        public ActionResult RemoveToMylist(Guid productid)
        {
            string rscode = string.Empty;
            string rsmessage = string.Empty;

            if (Session["IsLogin"] != null && (bool)Session["IsLogin"])
            {
                Guid memberid = new Guid(Session["MemberID"].ToString());

                Products product = productsService.GetByID(productid);
                Members member = membersService.GetByID(memberid);
                member.Products.Remove(product);
                membersService.SaveChanges();

                rscode = "200";
            }
            else
            {
                rscode = "201";
            }

            return Json(new { code = rscode });
        }

        [HttpPost]
        public ActionResult RecordLog(string url, string referrerurl = null)
        {
            if (General.GetClientIP() == "220.134.168.134")
            {
                return Json(false);
            }

            if (ConfigurationManager.AppSettings["mongodb.isrecord"] == "true")
            {
                try 
                {
                    ViewLog vl = new ViewLog();
                    vl.sessionid = Session.SessionID;

                    string detailurl = string.Format("http://www.geoplugin.net/json.gp?ip={0}", General.GetClientIP());

                    string jsonString = Librarys.GetRemoteUrl(detailurl);
                    
                    if (jsonString == null || jsonString == "")
                    {
                    }
                    else
                    {
                        Geoplugin geo = JsonConvert.DeserializeObject<Geoplugin>(jsonString);
                        vl.city = geo.geoplugin_city;
                        vl.country = geo.geoplugin_countryName;
                    }

                    HttpBrowserCapabilitiesBase hbc = Request.Browser;
                    
                    vl.memberid = (Session.Contents["MemberID"] == null) ? null : Session.Contents["MemberID"].ToString();
                    vl.browser = hbc.Browser;

                    if (hbc.IsMobileDevice)
                    {
                        vl.device = "mobile";
                        vl.platform = hbc.MobileDeviceManufacturer;
                    }
                    else
                    {
                        vl.device = "pc";
                        vl.platform = hbc.Platform;
                    }

                    vl.url = url;
                    if(referrerurl != null)
                    {
                        Uri uri = new Uri(referrerurl);
                        vl.referrerurl = referrerurl;
                        vl.referrerdns = uri.Host;
                    }
                    else
                    {
                        vl.referrerdns = null;
                        vl.referrerurl = null;
                    }
                    vl.createdate = DateTime.Now;

                    if (Request.Cookies["_wa"] == null)
                    {
                        HttpCookie cookie = new HttpCookie("_wa");
                        cookie.Value = vl.sessionid;
                        cookie.Expires = DateTime.Now.AddMonths(1);
                        Response.Cookies.Add(cookie);

                        vl.referrersessionid = null;
                    }
                    else
                    {
                        HttpCookie cookie = Request.Cookies["_wa"];

                        vl.referrersessionid = cookie.Value;

                        cookie.Value = vl.sessionid;
                        cookie.Expires = DateTime.Now.AddMonths(1);
                        Request.Cookies.Set(cookie);
                    }

                    //DocumentClient client = new DocumentClient(new Uri(ConfigurationManager.AppSettings["azure.documentdb.url"]), ConfigurationManager.AppSettings["azure.documentdb.key"]);
                    //client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri("tfoodies", "viewsessions"), vs);
                    //client.CreateDocumentAsync(UriFactory.CreateDocumentCollectionUri("tfoodies", "viewlogs"), vl);

                    MongoClient client = new MongoClient(ConfigurationManager.AppSettings["mongodb.url"]);
                    MongoDatabaseBase db = (MongoDatabaseBase)client.GetDatabase("tfoodies");

                    IMongoCollection<ViewLog> viewlogs = db.GetCollection<ViewLog>("viewlogs");
                    viewlogs.InsertOne(vl);

                    return Json(true);
                }
                catch (Exception ex)
                {
                    return Json(ex);
                }
            }
            else
            {
                try
                {
                    Viewlogs vl = new Viewlogs();
                    vl.viewlogid = Guid.NewGuid();
                    vl.sessionid = Session.SessionID;

                    string detailurl = string.Format("http://www.geoplugin.net/json.gp?ip={0}", General.GetClientIP());

                    string jsonString = Librarys.GetRemoteUrl(detailurl);

                    if (jsonString == null || jsonString == "")
                    {
                    }
                    else
                    {
                        Geoplugin geo = JsonConvert.DeserializeObject<Geoplugin>(jsonString);
                        vl.city = geo.geoplugin_city;
                        vl.country = geo.geoplugin_countryName;
                    }

                    HttpBrowserCapabilitiesBase hbc = Request.Browser;
                    
                    vl.memberid = (Session.Contents["MemberID"] == null) ? Guid.Empty : new Guid(Session.Contents["MemberID"].ToString());
                    vl.browser = hbc.Browser;

                    string userAgent = Request.UserAgent;

                    Parser uaParser = Parser.GetDefault();
                    ClientInfo c = uaParser.Parse(userAgent);

                    vl.device = c.Device.Family;
                    vl.platform = c.OS.Family;

                    vl.url = url;
                    if (referrerurl != null && referrerurl != "")
                    {
                        Uri uri = new Uri(referrerurl);
                        vl.referrerurl = referrerurl;
                        vl.referrerdns = uri.Host;
                    }
                    else
                    {
                        vl.referrerdns = null;
                        vl.referrerurl = null;
                    }
                    vl.createdate = DateTime.Now;

                    if (Request.Cookies["_wa"] == null)
                    {
                        HttpCookie cookie = new HttpCookie("_wa");
                        cookie.Value = vl.sessionid;
                        cookie.Expires = DateTime.Now.AddMonths(1);
                        Response.Cookies.Add(cookie);

                        vl.referrersessionid = null;
                    }
                    else
                    {
                        HttpCookie cookie = Request.Cookies["_wa"];

                        vl.referrersessionid = cookie.Value;

                        cookie.Value = vl.sessionid;
                        cookie.Expires = DateTime.Now.AddMonths(1);
                        Request.Cookies.Set(cookie);
                    }

                    viewlogsService.Create(vl);
                    viewlogsService.SaveChanges();

                    return Json(true);
                }
                catch(Exception ex)
                {
                    return Json(ex);
                }
            }
        }

        public ActionResult GetBrandMoreProducts(Guid brandid, int skip, int take)
        {
            Brands brand = brandsService.GetByID(brandid);
            IEnumerable<Products> data = brand.Products.Where(a => a.isdisabled == false).OrderByDescending(a => a.sort).Skip(skip);

            bool isclose = data.Count() > take ? false : true;

            string html = PartialView("_PartialProductList", data.Take(take)).RenderToString();

            return Json(new { isclose = isclose, partial = html });
        }

        [HttpPost]
        public ActionResult GetDiscountCode(string discountcode)
        {
            DiscountResponse dr;
            Cart ca = new Cart();

            if (discountcode != "")
            {
                Session["DiscountCode"] = discountcode;
                dr = General.GetDiscount();
                dr.amountprice = General.GetAmountPrice(ca.TotalPrice()).ToString("#,0");
            }
            else
            {
                Session["DiscountCode"] = "";
                dr = new DiscountResponse {
                    rscode = "",
                    rsmessage = "",
                    discount = 0,
                    amountprice = General.GetAmountPrice(ca.TotalPrice()).ToString("#,0")
                };
            }

            return Json(dr);
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
                return false;
            }

        }
    }
}