using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Text;
using PagedList;
using tfoodies.Libs;
using tfoodies.Filters;
using tfoodies.Commons;
using tfoodies.Models;
using tfoodies.Service;
using System.IO;
using System.Configuration;

namespace tfoodies.Controllers
{
    public class MainMsController : BaseController
    {
        private IBannersService bannersService;
        private IProductsService productsService;
        private IProductTypesService producttypesService;
        private IRecipesService recipesService;
        private IIssuesService issuesService;
        private ITagsService tagsService;
        private IZipcodesService zipcodesService;
        private IMembersService membersService;
        private IOrdersService ordersService;
        private INewsService newsService;
        private IBlogsService blogsService;
        private IEventsService eventsService;
        private IInvoicesService invoicesService;
        private IIncomesService incomesService;
        private IKnowledgesService knowledgesService;

        public MainMsController()
        {
            bannersService = new BannersService();
            productsService = new ProductsService();
            producttypesService = new ProductTypesService();
            recipesService = new RecipesService();
            issuesService = new IssuesService();
            tagsService = new TagsService();
            zipcodesService = new ZipcodesService();
            membersService = new MembersService();
            ordersService = new OrdersService();
            newsService = new NewsService();
            blogsService = new BlogsService();
            eventsService = new EventsService();
            invoicesService = new InvoicesService();
            incomesService = new IncomesService();
            knowledgesService = new KnowledgesService();
        }

        [CheckShopCom]
        public ActionResult Index()
        {
            ViewBag.Banners = bannersService.Get().OrderBy(a => a.sort);
            ViewBag.HotProducts = productsService.Get().Where(a => a.ishot == true && a.isdisabled == false).OrderByDescending(a => a.sort).Take(8);
            ViewBag.SortRecipes = recipesService.Get().OrderBy(a => a.sort).Take(3);
            ViewBag.LatestProducts = productsService.Get().Where(a => a.isnew == true && a.isdisabled == false).OrderBy(a => Guid.NewGuid()).Take(9);
            ViewBag.LatestIssues = issuesService.Get().Where(a => a.ispublish == true).OrderByDescending(a => a.createdate).Take(3);
            ViewBag.LatestNews = newsService.Get().OrderByDescending(a => a.publishdate).Take(2);
            ViewBag.LatestEvents = eventsService.Get().OrderByDescending(a => a.eventdate).Take(1);

            ViewBag.Title = websitetitle;
            ViewBag.Keywords = "橄欖油推薦,澳洲樂霸,好處,巴薩米克醋";
            ViewBag.Description = "來自澳洲莊園級橄欖油，從產地到餐桌，新鮮直送！澳洲原瓶原裝進口，多項品質驗證，品油師安全把關。特級初榨橄欖油好處，富含omega-9、單元不飽和脂肪酸、橄欖多酚。適合各式料理，煎、煮、炒、炸皆宜，每日適量喝橄欖油，補充營養元素，健康食用油推薦給您！";

            ViewBag.OgTitle = websitetitle;
            ViewBag.OgDescription = "來自澳洲莊園級橄欖油，從產地到餐桌，新鮮直送！澳洲原瓶原裝進口，多項品質驗證，品油師安全把關。特級初榨橄欖油好處，富含omega-9、單元不飽和脂肪酸、橄欖多酚。適合各式料理，煎、煮、炒、炸皆宜，每日適量喝橄欖油，補充營養元素，健康食用油推薦給您！";
            ViewBag.OgImage = "https://tfoodiesblob.blob.core.windows.net/tfoodies/2017051205225731.jpg";
            ViewBag.OgUrl = "https://www.tfoodies.com";

            return View();
        }

        [CheckShopCom]
        public ActionResult Products(string producttypetitle = null, Guid? brandid = null)
        {
            IEnumerable<Producttypes> producttypes = producttypesService.Get().Where(a => a.isenable == true).OrderBy(a => a.sort);
            Producttypes producttype = null;

            if(producttypetitle == null || producttypetitle == "")
            {
                producttype = producttypes.FirstOrDefault();
            }
            else
            {
                producttypetitle = producttypetitle.Replace("-", "/");
                producttype = producttypesService.Get().Where(a => a.title == producttypetitle).FirstOrDefault();
            }

            ViewBag.Brandid = brandid;
            ViewBag.Producttypes = producttypes;
            ViewBag.Tags = tagsService.Get();

            ViewBag.Title = producttype.title + " - " + websitetitle;
            ViewBag.Keywords = producttype.keyword;
            ViewBag.Description = producttype.description;

            return View(producttype);
        }

        [CheckShopCom]
        public ActionResult ProductDetail(string producttitle)
        {
            producttitle = producttitle.Replace("-", "/");
            Products product = productsService.Get().Where(a => a.title == producttitle && a.isdisabled == false).FirstOrDefault();
            if(product == null)
            {
                return RedirectToAction("Products");
            }

            IList<Products> products = new List<Products>();
            foreach (Setproducts sp in product.Setproducts)
            {
                Products p = productsService.GetByID(sp.productid);
                products.Add(p);
            }

            ViewBag.Products = products;
            ViewBag.Title = product.title + " - " + websitetitle;
            ViewBag.Keywords = product.keyword;
            ViewBag.Description = product.description;

            ViewBag.OgTitle = product.title;
            ViewBag.OgDescription = product.intro;
            ViewBag.OgImage = url + "/" + container + "/" + product.photo;
            ViewBag.OgUrl = product.shortener;

            return View(product);
        }

        public ActionResult Brand(string brandtitle)
        {
            brandtitle = brandtitle.Replace("-", "/");
            Brands brand = brandsService.Get().Where(a => a.title == brandtitle).FirstOrDefault();

            ViewBag.Products = brand.Products.Where(a => a.isdisabled == false).OrderByDescending(a => a.sort).Take(4);
            ViewBag.Title = brand.title + " - " + websitetitle;
            ViewBag.Keywords = brand.keyword;
            ViewBag.Description = brand.description;

            return View(brand);
        }

        public ActionResult News(int p = 1)
        {
            IEnumerable<News> data = newsService.Get().OrderByDescending(o => o.publishdate);

            ViewBag.pageNumber = p;
            ViewBag.News = data.ToPagedList(pageNumber: p, pageSize: 12);

            ViewBag.Title = "最新消息 - " + websitetitle;
            ViewBag.Keywords = "橄欖油,食在呼,活動,巴薩米克醋";
            ViewBag.Description = "食在呼團隊致力於推廣健康食用油的重要性，舉辦各種活動，廚藝課、品油知識課程，學習如何挑選好油、食用橄欖油的好處、使用橄欖油手做料理。";

            return View();
        }

        public ActionResult NewsDetail(Guid newid, int p = 1)
        {
            News entity = newsService.GetByID(newid);

            ViewBag.Newsothers = newsService.Get().Where(a => a.newid != entity.newid).OrderBy(r => Guid.NewGuid()).Take(3);
            ViewBag.pageNumber = p;

            ViewBag.Title = entity.title + " - " + websitetitle;
            ViewBag.Keywords = "橄欖油推薦,澳洲樂霸,好處,EVOO,巴薩米克醋";
            ViewBag.Description = entity.summary;

            ViewBag.OgTitle = entity.title;
            ViewBag.OgDescription = entity.summary;
            ViewBag.OgImage = url + "/" + container + "/" + entity.photo;
            ViewBag.OgUrl = entity.shortener;

            return View(entity);
        }

        public ActionResult Events(int p = 1)
        {
            IEnumerable<Events> data = eventsService.Get().OrderByDescending(o => o.eventdate);

            ViewBag.pageNumber = p;
            ViewBag.Events = data.ToPagedList(pageNumber: p, pageSize: 12);

            ViewBag.Title = "活動花絮 - " + websitetitle;
            ViewBag.Keywords = "橄欖油,料理,活動,巴薩米克醋";
            ViewBag.Description = "食在呼團隊致力於推廣健康食用油的重要性，照片記錄與合作夥伴的活動內容，學習如何分辨油的好壞、食用橄欖油的好處、使用橄欖油手做料理。";

            return View();
        }

        public ActionResult EventsDetail(Guid eventid, int p = 1)
        {
            Events entity = eventsService.GetByID(eventid);

            ViewBag.pageNumber = p;

            ViewBag.Title = entity.title + " - " + websitetitle;
            ViewBag.Keywords = "橄欖油推薦,澳洲樂霸,好處,EVOO,巴薩米克醋";
            ViewBag.Description = entity.summary;

            ViewBag.OgTitle = entity.title;
            ViewBag.OgDescription = entity.summary;
            ViewBag.OgImage = url + "/" + container + "/" + entity.photo;
            ViewBag.OgUrl = entity.shortener;

            return View(entity);
        }

        public ActionResult Reports()
        {
            ViewBag.Title = "檢驗報告 - " + websitetitle;
            ViewBag.Keywords = "橄欖油推薦,澳洲樂霸,好處,EVOO,巴薩米克醋";
            ViewBag.Description = "來自澳洲莊園級橄欖油，從產地到餐桌，新鮮直送！澳洲原瓶原裝進口，多項品質驗證，品油師安全把關。特級初榨橄欖油好處，富含omega-9、單元不飽和脂肪酸、橄欖多酚。適合各式料理，煎、煮、炒、炸皆宜，每日適量喝橄欖油，補充營養元素，健康食用油推薦給您！";

            return View();
        }

        public ActionResult GreenIssues()
        {
            ViewBag.Title = "綠誌 - " + websitetitle;
            ViewBag.Keywords = "橄欖油推薦,澳洲樂霸,EVOO,巴薩米克醋";
            ViewBag.Description = "來自澳洲莊園級橄欖油，從產地到餐桌，新鮮直送！澳洲原瓶原裝進口，多項品質驗證，品油師安全把關。特級初榨橄欖油好處，富含omega-9、單元不飽和脂肪酸、橄欖多酚。適合各式料理，煎、煮、炒、炸皆宜，每日適量喝橄欖油，補充營養元素，健康食用油推薦給您！";

            return View();
        }

        public ActionResult Recipes(int p = 1, string k = null)
        {
            IEnumerable<Recipes> data = recipesService.Get().OrderBy(o => o.sort);
            if (k != null) data = data.Where(a => a.title.Contains(k));

            ViewBag.pageNumber = p;
            ViewBag.Recipes = data.ToPagedList(pageNumber: p, pageSize: 12);

            ViewBag.Title = "美味料理 - 橄欖油食譜 - " + websitetitle;
            ViewBag.Keywords = "橄欖油料理,食譜,家常菜,巴薩米克醋";
            ViewBag.Description = "TFoodies食在呼提供美味的橄欖油料理食譜，簡單上手，讓家人吃到美味健康的家常菜，特級初榨橄欖油可安心煎煮炒炸，烹調中西式料理、湯品、甜點、沙拉。";

            return View();
        }

        public ActionResult RecipeDetail(Guid recipeid, int p = 1)
        {
            Recipes recipe = recipesService.GetByID(recipeid);

            ViewBag.pageNumber = p;
            ViewBag.Title = recipe.title + " - 橄欖油食譜 - " + websitetitle;
            ViewBag.Keywords = recipe.keyword;
            ViewBag.Description = recipe.description;

            ViewBag.OgTitle = recipe.title + " - 橄欖油食譜";
            ViewBag.OgDescription = recipe.intro;
            ViewBag.OgImage = url + "/" + container + "/" + recipe.photo;
            ViewBag.OgUrl = recipe.shortener;

            return View(recipe);
        }

        public ActionResult Issues(int p = 1, string k = null)
        {
            IEnumerable<Issues> data = issuesService.Get().Where(a => a.ispublish == true).OrderBy(o => o.sort);
            if (k != null) data = data.Where(a => a.title.Contains(k));

            ViewBag.pageNumber = p;
            ViewBag.Issues = data.ToPagedList(pageNumber: p, pageSize: 12);

            ViewBag.Title = "綠誌 - " + websitetitle;
            ViewBag.Keywords = "健康,橄欖油,地中海飲食,巴薩米克醋";
            ViewBag.Description = "橄欖油於飲食與生活中的應用，認識各種油品，分辨食用油的好壞，落實地中海飲食的方法，使用特級初榨橄欖油開啟身體健康的關鍵。";

            return View();
        }

        public ActionResult IssueDetail(string issuetitle, int p = 1)
        {
            issuetitle = issuetitle.Replace("-", "/");
            Issues issue = issuesService.Get().Where(a => a.title == issuetitle && a.ispublish == true).FirstOrDefault();
            if(issue == null)
            {
                return RedirectToAction("Issues");
            }

            ViewBag.Issueothers = issuesService.Get().Where(a => a.issueid != issue.issueid && a.ispublish == true).OrderBy(r => Guid.NewGuid()).Take(3);

            ViewBag.pageNumber = p;
            ViewBag.Title = issue.title + " - " + websitetitle;
            ViewBag.Keywords = issue.keyword;
            ViewBag.Description = issue.description;

            ViewBag.OgTitle = issue.title;
            ViewBag.OgDescription = issue.description;
            ViewBag.OgImage = url + "/" + container + "/" + issue.photo;
            ViewBag.OgUrl = issue.shortener;

            return View(issue);
        }

        public ActionResult Knowledges(int p = 1, string k = null)
        {
            IEnumerable<Knowledges> data = knowledgesService.Get().Where(a => a.ispublish == true).OrderBy(o => o.sort);
            if (k != null) data = data.Where(a => a.question.Contains(k));

            ViewBag.pageNumber = p;
            ViewBag.Knowledges = data.ToPagedList(pageNumber: p, pageSize: 12);

            ViewBag.Title = "小知識 - " + websitetitle;
            ViewBag.Keywords = "健康,橄欖油,地中海飲食,巴薩米克醋";
            ViewBag.Description = "橄欖油於飲食與生活中的應用，認識各種油品，分辨食用油的好壞，落實地中海飲食的方法，使用特級初榨橄欖油開啟身體健康的關鍵。";

            return View();
        }

        public ActionResult KnowledgeDetail(Guid knowledgeid, int p = 1)
        {
            Knowledges knowledge = knowledgesService.Get().Where(a => a.knowledgeid == knowledgeid && a.ispublish == true).FirstOrDefault();
            if (knowledge == null)
            {
                return RedirectToAction("Knowledges");
            }

            ViewBag.Issueothers = issuesService.Get().Where(a => a.ispublish == true).OrderBy(r => Guid.NewGuid()).Take(3);

            ViewBag.pageNumber = p;
            ViewBag.Title = knowledge.question + " - " + websitetitle;
            ViewBag.Keywords = knowledge.keyword;
            ViewBag.Description = knowledge.description;

            ViewBag.OgTitle = knowledge.question;
            ViewBag.OgDescription = knowledge.description;
            ViewBag.OgImage = url + "/" + container + "/" + knowledge.photo;
            ViewBag.OgUrl = knowledge.shortener;

            return View(knowledge);
        }

        public ActionResult Blogs()
        {
            ViewBag.Blogs = blogsService.Get().OrderBy(o => o.sort);

            ViewBag.Title = "部落客分享 - " + websitetitle;
            ViewBag.Keywords = "橄欖油,部落客,食譜,巴薩米克醋";
            ViewBag.Description = "TFoodies食在呼與美食部落客合作，運用健康的澳洲樂霸特級初榨橄欖油，製作各式風味料理，不藏私的分享橄欖油食譜。";

            return View();
        }

        public ActionResult About()
        {
            ViewBag.Title = "關於我們 - " + websitetitle;
            ViewBag.Keywords = "橄欖油,食譜,推薦,澳洲樂霸,EVOO,巴薩米克醋";
            ViewBag.Description = "來自澳洲莊園級橄欖油，從產地到餐桌，新鮮直送！澳洲原瓶原裝進口，多項品質驗證，品油師安全把關。特級初榨橄欖油好處，富含omega-9、單元不飽和脂肪酸、橄欖多酚。適合各式料理，煎、煮、炒、炸皆宜，每日適量喝橄欖油，補充營養元素，健康食用油推薦給您！";

            return View();
        }

        public ActionResult Login()
        {
            bool isdisplay = false;
            Cart ca = new Cart();
            if (ca.TotalItems() > 0) isdisplay = true;

            ViewBag.Title = "會員登入 - " + websitetitle;
            ViewBag.Keywords = "橄欖油,食譜,推薦,澳洲樂霸,EVOO,巴薩米克醋";
            ViewBag.Description = "來自澳洲莊園級橄欖油，從產地到餐桌，新鮮直送！澳洲原瓶原裝進口，多項品質驗證，品油師安全把關。特級初榨橄欖油好處，富含omega-9、單元不飽和脂肪酸、橄欖多酚。適合各式料理，煎、煮、炒、炸皆宜，每日適量喝橄欖油，補充營養元素，健康食用油推薦給您！";
            ViewBag.Isdisplay = isdisplay;

            return View();
        }

        public ActionResult Forget()
        {
            ViewBag.Title = "忘記密碼 - " + websitetitle;
            ViewBag.Keywords = "橄欖油,食譜,推薦,澳洲樂霸,EVOO,巴薩米克醋";
            ViewBag.Description = "來自澳洲莊園級橄欖油，從產地到餐桌，新鮮直送！澳洲原瓶原裝進口，多項品質驗證，品油師安全把關。特級初榨橄欖油好處，富含omega-9、單元不飽和脂肪酸、橄欖多酚。適合各式料理，煎、煮、炒、炸皆宜，每日適量喝橄欖油，補充營養元素，健康食用油推薦給您！";

            return View();
        }

        [CheckShoppingCartItem]
        public ActionResult ShoppingCart()
        {          
            Cart cart = new Cart();
            DiscountResponse dr = General.GetDiscount();

            ViewBag.TotalPrice = cart.TotalPrice();
            ViewBag.DiscountPrice = dr.discount;
            ViewBag.FreightPrice = General.GetFreight(cart.TotalPrice());
            ViewBag.AmountPrice = General.GetAmountPrice(cart.TotalPrice());

            ViewBag.Title = "購物車 - " + websitetitle;
            ViewBag.Keywords = "橄欖油,食譜,推薦,澳洲樂霸,EVOO,巴薩米克醋";
            ViewBag.Description = "來自澳洲莊園級橄欖油，從產地到餐桌，新鮮直送！澳洲原瓶原裝進口，多項品質驗證，品油師安全把關。特級初榨橄欖油好處，富含omega-9、單元不飽和脂肪酸、橄欖多酚。適合各式料理，煎、煮、炒、炸皆宜，每日適量喝橄欖油，補充營養元素，健康食用油推薦給您！";

            return View();
        }

        [CheckShoppingCartItem]
        public ActionResult ShoppingProfile()
        {
            ViewBag.Citys = zipcodesService.Get().Select(g => g.city).Distinct();

            if (Session["IsLogin"] != null && (bool)Session["IsLogin"])
            {
                Guid memberid = new Guid(Session["MemberID"].ToString());
                Members member = membersService.GetByID(memberid);
                ViewBag.Membername = member.name;
                ViewBag.Membermobile = member.mobile;
                ViewBag.Zipcodefullid = member.Zipcodes.city + "," + member.zipcodeid;
                ViewBag.Memberaddress = member.address;
            }

            Cart ca = new Cart();

            ViewBag.PurchAmt = General.GetAmountPrice(ca.TotalPrice());
            ViewBag.Title = "訂購人資訊 - " + websitetitle;
            ViewBag.Keywords = "橄欖油,食譜,推薦,澳洲樂霸,EVOO,巴薩米克醋";
            ViewBag.Description = "來自澳洲莊園級橄欖油，從產地到餐桌，新鮮直送！澳洲原瓶原裝進口，多項品質驗證，品油師安全把關。特級初榨橄欖油好處，富含omega-9、單元不飽和脂肪酸、橄欖多酚。適合各式料理，煎、煮、炒、炸皆宜，每日適量喝橄欖油，補充營養元素，健康食用油推薦給您！";

            return View();
        }

        public ActionResult ShoppingSuccess(string lidm, string lastPan4 = null, string status = null)
        {
            if (lidm == null || lidm == "") RedirectToAction("Index");

            Orders order = ordersService.Get().ToList().FirstOrDefault(a => a.ordercode == lidm);

            Invoices invoice = new Invoices();
            invoice.invoiceid = Guid.NewGuid();
            invoice.invoicecode = Librarys.NewInvoiceCode(DateTime.Now);
            invoice.memberid = order.memberid;
            invoice.requestdate = order.orderdate;
            invoice.createdate = DateTime.Now;

            int TotalAmt = order.total + order.freight - Convert.ToInt32(order.discount);
            int Amt = Convert.ToInt32(Math.Round(TotalAmt / 1.05, MidpointRounding.AwayFromZero));
            int TaxAmt = TotalAmt - Amt;

            Invoicedetails invoicedetail = new Invoicedetails();
            invoicedetail.invoicedetailid = Guid.NewGuid();
            invoicedetail.orderid = order.orderid;
            invoicedetail.accountingid = new Guid("f6cfd53f-13ca-4843-881f-141b579b4a5b");
            invoicedetail.price = TotalAmt;
            invoicedetail.tax = TaxAmt;
            invoicedetail.note = order.note;

            invoice.Invoicedetails.Add(invoicedetail);

            if (status != "" && status != null && status != "0")
            {
                ViewBag.OrderResult = "信用卡刷卡失敗，業務人員將會與您聯絡！";
            }

            if (status != "" && status != null && status == "0")
            {
                ViewBag.OrderResult = "信用卡刷卡成功";

                DiscountResponse dr = General.GetDiscount();

                if(dr.rscode == "200")
                {
                    order.discountid = dr.discountid;
                }

                order.paystatus = (int)EnumPayStatus.已付款;
                order.paydate = DateTime.Now;
                order.lastpan4 = lastPan4;
                
                ordersService.Update(order);
                ordersService.SaveChanges();

                Incomes income = new Incomes();
                income.incomeid = Guid.NewGuid();
                income.memberid = order.memberid;
                income.incomecode = Librarys.NewIncomeCode(DateTime.Now);
                income.incomedate = order.orderdate;
                income.amount = TotalAmt;
                income.createdate = DateTime.Now;

                incomesService.Create(income);
                incomesService.SaveChanges();

                invoice.incomeid = income.incomeid;
            }

            invoicesService.Create(invoice);
            invoicesService.SaveChanges();

            if (order.Members.email.ToString().Trim() != "")
            {
                string sSubject = "食在呼 TFoodies–訂單通知 (" + order.ordercode + ")";
                string sFrom = "食在呼 TFoodies";

                StringBuilder sb = new StringBuilder();
                sb.Append("<div style=\"background:#F6F6F6; font-family:Verdana, Arial, Helvetica, sans-serif; font-size:12px; margin:0; padding:0;\">");
                sb.Append("<table cellspacing=\"0\" cellpadding=\"0\" border=\"0\" height=\"100%\" width=\"100%\">");
                sb.Append("<tr>");
                sb.Append("<td align=\"center\" valign=\"top\" style=\"padding:20px 0 20px 0\">");
                sb.Append("<table bgcolor=\"#FFFFFF\" cellspacing=\"0\" cellpadding=\"10\" border=\"0\" width=\"650\" style=\"border:1px solid #E0E0E0;\">");
                sb.Append("<tr><td valign=\"top\"><a href=\"https://www.tfoodies.com\"><img src=\"cid:logo-10.png\" width=\"150\" style=\"margin-bottom:10px;\" border=\"0\"/></a></td></tr>");
                sb.Append("<tr><td valign=\"top\">");
                sb.Append("<h1 style=\"font-size:22px; font-weight:normal; line-height:22px; margin:0 0 11px 0;\">親愛的 顧客 您好</h1>");
                sb.Append("<p style=\"font-size:12px; line-height:16px; margin:0 0 10px 0;\">感謝您訂購《TFoodies 食在呼》的優質商品，我們已經收到您的訂購資訊了！</p>");
                sb.Append("</td></tr>");
                sb.Append("<tr><td><h2 style=\"font-size:18px;font-weight:normal;margin:0\">您的訂單編號：" + order.ordercode + " <small>(" + order.orderdate.ToString("yyyy-MM-dd") + ")</small></h2></td></tr>");

                if (order.paytype == (int)EnumPayType.ATM轉帳付款)
                {
                    sb.Append("<tr><td><p style=\"font-size:18px;font-weight:normal;margin:0\">轉帳銀行：013 國泰世華銀行</p></td></tr>");
                    sb.Append("<tr><td><p style=\"font-size:18px;font-weight:normal;margin:0\">匯款帳號：" + order.codeatm + "</p></td></tr>");
                    sb.Append("<tr><td><p style=\"font-size:18px;font-weight:normal;margin:0\">轉帳金額：" + (order.freight + order.total - order.discount) + "</p></td></tr>");
                    sb.Append("<tr><td><p style=\"font-size:18px;font-weight:normal;margin:0\">繳款截止日：" + Convert.ToDateTime(order.expirepaydate).ToString("yyyy-MM-dd") + "</p></td></tr>");
                }

                sb.Append("<tr><td><p style=\"font-size:12px; line-height:16px; margin:0 0 10px 0;\">《食在呼 TFoodies》再次感謝您的訂購，更多詳細訂單資訊情於【<a href=\"https://www.tfoodies.com\">食在呼 - 會員中心</a>】查詢！</p></td></tr>");
                sb.Append("<tr><td><p style=\"font-size:12px; line-height:16px; margin:0 0 10px 0;\">※此為系統自動通知信，請勿直接回覆！</p></td></tr>");
                sb.Append("<tr><td bgcolor=\"#EAEAEA\" align=\"center\" style=\"background:#EAEAEA; text-align:center;\"><center><p style=\"font-size:12px; margin:0;\"><strong>食在呼 TFoodies</strong>，再次感謝您！</p></center></td></tr>");
                sb.Append("</table>");
                sb.Append("</td>");
                sb.Append("</tr>");
                sb.Append("</table>");
                sb.Append("</div>");
                string[] img = new String[] { Path.Combine(Server.MapPath("~/Content/images/common/"), "logo-10.png") };

                Librarys.SendMail(order.Members.email, sFrom, sSubject, sb.ToString(), img);
            }

            Session.Add("IsLogin", true);
            Session.Add("Username", order.Members.name);
            Session.Add("MemberID", order.Members.memberid);

            Session.Remove("orderCode");
            Session.Remove("myCart");

            ViewBag.LimitDay = Convert.ToInt32(ConfigurationManager.AppSettings["paylimit"]);
            ViewBag.Title = "完成訂購 - " + websitetitle;
            ViewBag.Keywords = "橄欖油,食譜,推薦,澳洲樂霸,EVOO,巴薩米克醋";
            ViewBag.Description = "來自澳洲莊園級橄欖油，從產地到餐桌，新鮮直送！澳洲原瓶原裝進口，多項品質驗證，品油師安全把關。特級初榨橄欖油好處，富含omega-9、單元不飽和脂肪酸、橄欖多酚。適合各式料理，煎、煮、炒、炸皆宜，每日適量喝橄欖油，補充營養元素，健康食用油推薦給您！";

            return View(order);
        }
    }
}