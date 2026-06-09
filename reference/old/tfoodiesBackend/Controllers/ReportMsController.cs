using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

using tfoodiesBackend.Filters;
using tfoodiesBackend.Commons;
using tfoodies.Libs;
using tfoodies.Models;
using tfoodies.Service;

namespace tfoodiesBackend.Controllers
{
    public class ReportMsController : BaseController
    {
        private IOrdersService ordersService;

        public ReportMsController()
        {
            ordersService = new OrdersService();
        }

        [CheckSession(IsAuth = true)]
        public ActionResult Salereports(DateTime? ordermonth = null)
        {
            IEnumerable<Orders> data;
            IList<ReportItem> reportitems = new List<ReportItem>();

            if (ordermonth != null)
            {
                data = ordersService.Get().Where(a => a.orderdate.Year == ordermonth.Value.Year && a.orderdate.Month == ordermonth.Value.Month && a.deliverstatus == (int)EnumDeliverStatus.已出貨 || a.deliverstatus == (int)EnumDeliverStatus.退貨).OrderByDescending(a => a.orderdate).ThenByDescending(a => a.ordercode);
                foreach (Orders order in data)
                {
                    foreach (Orderdetails orderdetail in order.Orderdetails)
                    {
                        if (orderdetail.Products.isset)
                        {
                            foreach (Setproducts sp in orderdetail.Products.Setproducts)
                            {
                                ReportItem item = reportitems.Where(a => a.productid == sp.productid).FirstOrDefault();
                                if (item != null)
                                {
                                    item.amount = item.amount + sp.qty;
                                }
                                else
                                {
                                    ReportItem reportItem = new ReportItem();
                                    reportItem.productid = sp.productid;
                                    reportItem.name = sp.Products.title + " " + sp.Products.capacity;
                                    reportItem.amount = sp.qty;

                                    reportitems.Add(reportItem);
                                }
                            }
                        }
                        else
                        {
                            ReportItem item = reportitems.Where(a => a.productid == orderdetail.productid).FirstOrDefault();
                            if (item != null)
                            {
                                item.amount = item.amount + orderdetail.qty;
                            }
                            else
                            {
                                ReportItem reportItem = new ReportItem();
                                reportItem.productid = orderdetail.productid;
                                reportItem.name = orderdetail.Products.title + " " + orderdetail.Products.capacity;
                                reportItem.amount = orderdetail.qty;

                                reportitems.Add(reportItem);
                            }
                        }
                    }
                }
            }

            ViewBag.ReportItems = reportitems;
            return View();
        }

        [CheckSession(IsAuth = true)]
        public ActionResult Amountreports(DateTime? sd = null, DateTime? ed = null, int? paystatus = null)
        {
            IEnumerable<Orders> data = new List<Orders>();

            if(sd != null && ed != null)
            {
                data = ordersService.Get().Where(a => a.orderdate >= sd && a.orderdate <= ed && a.deliverstatus == (int)EnumDeliverStatus.已出貨 || a.deliverstatus == (int)EnumDeliverStatus.退貨).OrderBy(a => a.orderdate).ThenBy(a => a.ordercode);
            }
            else
            {
                ModelState.AddModelError("", "起始日期與結束日期為必填。");
                ViewBag.Orders = data;

                return View();
            }

            if(paystatus != null)
            {
                data = data.Where(a => a.paystatus == paystatus);
            }

            ViewBag.Orders = data;
            return View();
        }
    }
}