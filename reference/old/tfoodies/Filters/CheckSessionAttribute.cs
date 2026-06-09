using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using tfoodies.Models;
using tfoodies.Service;

namespace tfoodies.Filters
{
    public class CheckSessionAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            bool islogin = (context.HttpContext.Session.Contents["IsLogin"] == null) ? false : (bool)context.HttpContext.Session.Contents["IsLogin"];

            if (!islogin)
            {
                context.HttpContext.Session.Add("IsLogin", false);

                RouteValueDictionary redirectTargetDictionary = new RouteValueDictionary();
                redirectTargetDictionary.Add("action", "Login");
                redirectTargetDictionary.Add("controller", "MainMs");

                context.Result = new RedirectToRouteResult(redirectTargetDictionary);
                return;
            }    

            base.OnActionExecuting(context);
        }
    }
}