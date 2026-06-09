using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using tfoodies.Commons;
using tfoodies.Models;
using tfoodies.Service;

namespace tfoodies.Filters
{
    public class CheckShoppingCartItemAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            Cart ca = new Cart();
            if (ca.TotalItems() == 0)
            {
                RouteValueDictionary redirectTargetDictionary = new RouteValueDictionary();
                redirectTargetDictionary.Add("action", "Products");
                redirectTargetDictionary.Add("controller", "MainMs");

                context.Result = new RedirectToRouteResult(redirectTargetDictionary);
                return;
            }

            base.OnActionExecuting(context);
        }
    }
}