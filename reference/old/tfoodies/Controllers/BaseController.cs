using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using tfoodies.Commons;
using tfoodies.Models;
using tfoodies.Service;

namespace tfoodies.Controllers
{
    public class BaseController : Controller
    {
        protected string container = ConfigurationManager.AppSettings["azure.blob.container"];
        protected string url = ConfigurationManager.AppSettings["azure.blob.url"];
        protected string websitetitle = ConfigurationManager.AppSettings["websitetitle"];
        protected IBrandsService brandsService;
        private IMembersService membersService;

        public BaseController()
        {
            brandsService = new BrandsService();
            membersService = new MembersService();
        }

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            ViewBag.BlobUrl = url + "/" + container + "/";

            base.OnActionExecuting(filterContext);
        }

        protected override void OnActionExecuted(ActionExecutedContext filterContext)
        {
            Cart cart = new Cart();

            if (Request.Cookies["tfd"] != null)
            {
                HttpCookie cookie = Request.Cookies["tfd"];
                string qs = General.DecryptCookie(cookie.Value);
                var pa = HttpUtility.ParseQueryString(qs);
                membersService.ValidateUser(pa["mobile"], pa["password"]);
            }

            ViewBag.BlobUrl = url + "/" + container + "/";
            ViewBag.WebsiteTitle = websitetitle;
            ViewBag.CartContents = cart.Contents();
            ViewBag.CartItems = cart.TotalItems();
            ViewBag.AddActive = (ViewBag.CartItems > 0) ? "add-active" : "";
            ViewBag.Brands = brandsService.Get().Where(a => a.isdisplay == 1).OrderBy(a => a.sort);

            //if (General.GetClientIP() != "220.134.168.134")
            //{
            //    DateTime openday = Convert.ToDateTime("2016-12-21 11:00:00");
            //    if (openday > DateTime.Now && (Request.Url.Host == "www.tfoodies.com" || Request.Url.Host == "tfoodies.com" || Request.Url.Host == "www.tfoodies.com.tw" || Request.Url.Host == "tfoodies.com.tw"))
            //    {
            //        RouteValueDictionary redirectTargetDictionary = new RouteValueDictionary();
            //        redirectTargetDictionary.Add("action", "Index");
            //        redirectTargetDictionary.Add("controller", "PreMs");

            //        filterContext.Result = new RedirectToRouteResult(redirectTargetDictionary);
            //        return;
            //    }
            //}      

            base.OnActionExecuted(filterContext);
        }
    }
}