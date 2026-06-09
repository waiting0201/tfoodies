using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace tfoodies.Filters
{
    public class CheckShopComAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if(context.HttpContext.Request.QueryString["RID"] != null && context.HttpContext.Request.QueryString["Click_ID"] != null)
            {
                context.HttpContext.Session.Add("RID", context.HttpContext.Request.QueryString["RID"]);
                context.HttpContext.Session.Add("Click_ID", context.HttpContext.Request.QueryString["Click_ID"]);
            }

            base.OnActionExecuting(context);
        }
    }
}