using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using tfoodiesBackend.Filters;
using tfoodies.Models;
using tfoodies.Service;

namespace tfoodiesBackend.Controllers
{
    public class BaseController : Controller
    {
        private ILimsService limsService;
        protected string container = ConfigurationManager.AppSettings["azure.blob.container"];
        protected string url = ConfigurationManager.AppSettings["azure.blob.url"];

        public BaseController()
        {
            limsService = new LimsService();
        }

        protected override void OnActionExecuted(ActionExecutedContext filterContext)
        {
            ViewBag.SiteLinks = limsService.Get().OrderBy(a => a.ParentID).ThenBy(i => i.Sort);
            ViewBag.BlobUrl = url + "/" + container + "/";
            base.OnActionExecuted(filterContext);
        }
    }
}