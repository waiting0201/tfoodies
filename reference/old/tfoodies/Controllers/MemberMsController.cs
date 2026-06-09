using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using tfoodies.Libs;
using tfoodies.Filters;
using tfoodies.Models;
using tfoodies.Service;

namespace tfoodies.Controllers
{
    [CheckSession]
    public class MemberMsController : BaseController
    {
        private IMembersService membersService;
        private IOrdersService ordersService;
        private IZipcodesService zipcodesService;

        public MemberMsController()
        {
            membersService = new MembersService();
            ordersService = new OrdersService();
            zipcodesService = new ZipcodesService();
        }

        [CheckSession]
        public ActionResult Orders()
        {
            Guid memberid = new Guid(Session["MemberID"].ToString());
            Members member = membersService.GetByID(memberid);

            ViewBag.Title = "訂單查詢 -" + websitetitle;
            ViewBag.Memberorders = member.Orders.OrderByDescending(a => a.orderdate).ThenByDescending(a => a.ordercode);

            return View();
        }

        [CheckSession]
        [HttpGet]
        public ActionResult OrderDetail(Guid orderid)
        {
            ViewBag.Title = "訂單資料 -" + websitetitle;
            Orders order = ordersService.GetByID(orderid);

            return View(order);
        }

        [CheckSession]
        public ActionResult Mylists()
        {
            Guid memberid = new Guid(Session["MemberID"].ToString());
            Members member = membersService.GetByID(memberid);

            ViewBag.Title = "追蹤清單 -" + websitetitle;

            return View(member);
        }

        [CheckSession]
        public ActionResult EditProfile()
        {
            Guid memberid = new Guid(Session["MemberID"].ToString());
            Members member = membersService.GetByID(memberid);

            ViewBag.Title = "會員資料 -" + websitetitle;
            ViewBag.Citys = zipcodesService.Get().Select(g => g.city).Distinct();

            return View(member);
        }

        [CheckSession]
        public ActionResult EditPassword()
        {
            Guid memberid = new Guid(Session["MemberID"].ToString());
            Members member = membersService.GetByID(memberid);

            ViewBag.Title = "修改密碼 -" + websitetitle;

            return View(member);
        }

        public ActionResult PayResult(string lidm, string status = null)
        {
            Orders order = ordersService.Get().Where(a => a.ordercode == lidm).FirstOrDefault();

            // 刷卡失敗
            if (status != "" && status != null && status != "0")
            {
                order.paytype = (int)EnumPayType.信用卡線上刷卡;
                order.paystatus = (int)EnumPayStatus.未付款;
            }

            // 刷卡成功
            if (status != "" && status != null && status == "0")
            {
                order.paytype = (int)EnumPayType.信用卡線上刷卡;
                order.paystatus = (int)EnumPayStatus.已付款;
                order.paydate = DateTime.Now;
            }
             // 匯款
            if (status == "" || status == null)
            {
                order.paytype = (int)EnumPayType.ATM轉帳付款;
                order.paystatus = (int)EnumPayStatus.未付款;
            }

            ordersService.Update(order);
            ordersService.SaveChanges();

            return RedirectToAction("OrderDetail", new { orderid = order.orderid });
        }

        public ActionResult Logout()
        {
            Session.Abandon();
            Session.Clear();

            if(Request.Cookies["tfd"] != null)
            {
                HttpCookie myCookie = new HttpCookie("tfd");
                myCookie.Expires = DateTime.Now.AddDays(-1d);
                Response.Cookies.Add(myCookie);
            }

            return RedirectToAction("Index", "MainMs");
        }
    }
}