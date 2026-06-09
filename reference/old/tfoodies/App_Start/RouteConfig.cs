using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace tfoodies
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            // Products
            routes.MapRoute(
                "ProductTypes_Default",
                "Products",
                new { controller = "MainMs", action = "Products", producttypetitle = "" }
            );

            routes.MapRoute(
                "ProductTypes",
                "Products/{producttypetitle}",
                new { controller = "MainMs", action = "Products", producttypetitle = "" }
            );

            routes.MapRoute(
                "ProductDetail",
                "Product/{producttitle}",
                new { controller = "MainMs", action = "ProductDetail", producttitle = "" }
            );

            routes.MapRoute(
                "Brand_Default",
                "Brand/{brandtitle}",
                new { controller = "MainMs", action = "Brand", brandtitle = "" }
            );

            routes.MapRoute(
                "News_Default",
                "News",
                new { controller = "MainMs", action = "News", p = UrlParameter.Optional }
            );

            routes.MapRoute(
                "News_Page",
                "News/{p}",
                new { controller = "MainMs", action = "News", p = UrlParameter.Optional }
            );

            routes.MapRoute(
                "NewsDetail",
                "NewsDetail/{newid}/{p}",
                new { controller = "MainMs", action = "NewsDetail", newid = "", p = UrlParameter.Optional }
            );

            routes.MapRoute(
                "Recipes_Default",
                "Recipes",
                new { controller = "MainMs", action = "Recipes", p = UrlParameter.Optional }
            );

            routes.MapRoute(
                "Recipes_Page",
                "Recipes/{p}",
                new { controller = "MainMs", action = "Recipes", p = UrlParameter.Optional }
            );

            routes.MapRoute(
                "RecipeDetail",
                "Recipe/{recipeid}/{p}",
                new { controller = "MainMs", action = "RecipeDetail", recipeid = "", p = UrlParameter.Optional }
            );

            routes.MapRoute(
                "Issues_Default",
                "Issues",
                new { controller = "MainMs", action = "Issues", p = UrlParameter.Optional }
            );

            routes.MapRoute(
                "Issues_Page",
                "Issues/{p}",
                new { controller = "MainMs", action = "Issues", p = UrlParameter.Optional }
            );

            routes.MapRoute(
                "IssueDetail",
                "Issue/{issuetitle}/{p}",
                new { controller = "MainMs", action = "IssueDetail", issuetitle = "", p = UrlParameter.Optional }
            );

            routes.MapRoute(
                "Blogs_Default",
                "Blogs/{p}",
                new { controller = "MainMs", action = "Blogs", p = UrlParameter.Optional }
            );

            routes.MapRoute(
                "About_Default",
                "TFoodies",
                new { controller = "MainMs", action = "About" }
            );

            routes.MapRoute(
                "Login_Default",
                "Login",
                new { controller = "MainMs", action = "Login" }
            );

            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "MainMs", action = "Index", id = UrlParameter.Optional }
            );
        }
    }
}
