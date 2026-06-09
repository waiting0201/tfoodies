using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Configuration;
using tfoodies.Libs;
using tfoodies.Commons;
using tfoodies.Models;
using tfoodies.Service;

namespace tfoodies.Controllers
{
    public class GroupMsController : Controller
    {
        protected string container = ConfigurationManager.AppSettings["azure.blob.container"];
        protected string url = ConfigurationManager.AppSettings["azure.blob.url"];
        private IProductsService productsService;
        private IPreordersService preordersService;
        private IZipcodesService zipcodesService;

        public GroupMsController()
        {
            productsService = new ProductsService();
            preordersService = new PreordersService();
            zipcodesService = new ZipcodesService();
        }

        public ActionResult Index()
        {
            ViewBag.Products = productsService.Get().Where(a => a.isdisabled == false && a.isgroupbuy == true).OrderByDescending(a => a.sort);
            ViewBag.Citys = zipcodesService.Get().Select(g => g.city).Distinct();
            ViewBag.BlobUrl = url + "/" + container + "/";

            return View();
        }
    }
}