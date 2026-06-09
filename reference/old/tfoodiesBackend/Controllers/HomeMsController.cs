using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using PagedList;
using System.IO;
using HtmlAgilityPack;
using tfoodiesBackend.Filters;
using tfoodiesBackend.Commons;
using tfoodies.Models;
using tfoodies.Service;

namespace tfoodiesBackend.Controllers
{
    public class HomeMsController : BaseController
    {
        private string bloburl;
        private tfoodiesEntities db;
        private IBannersService bannersService;
        private IIssuesService issuesService;
        private IRecipesService recipesService;
        private IProductsService productsService;
        private IRecipeIngredientsService recipeingredientsServier;
        private IRecipeSeasoningsService recipeseasoningsService;
        private IRecipeStepsService recipestepsService;
        private INewsService newsService;
        private IBlogsService blogsService;
        private IEventsService eventsService;
        private IEventPhotosService eventphotosService;
        private IKnowledgesService knowledgesService;

        public HomeMsController()
        {
            db = new tfoodiesEntities();
            bannersService = new BannersService();
            issuesService = new IssuesService(db);
            recipesService = new RecipesService(db);
            productsService = new ProductsService(db);
            recipeingredientsServier = new RecipeIngredientsService(db);
            recipeseasoningsService = new RecipeSeasoningsService(db);
            recipestepsService = new RecipeStepsService(db);
            newsService = new NewsService();
            blogsService = new BlogsService();
            eventsService = new EventsService();
            eventphotosService = new EventPhotosService();
            knowledgesService = new KnowledgesService();
            bloburl = "/Content/img/watermark.png";
        }

        [CheckSession(IsAuth = true)]
        public ActionResult Banners(int p = 1)
        {
            var data = bannersService.Get().OrderBy(o => o.sort);

            ViewBag.pageNumber = p;
            ViewBag.Banners = data.ToPagedList(pageNumber: p, pageSize: 20);
            return View();
        }

        [CheckSession(IsAuth = true)]
        public ActionResult SortBanners(int p, IEnumerable<Banners> EntityLists = null)
        {
            foreach (Banners banner in EntityLists)
            {
                bannersService.SpecificUpdate(banner, new string[] { "sort" });
            }
            bannersService.SaveChanges();
            return RedirectToAction("Banners", new { p = p });
        }

        [CheckSession(IsAuth = true)]
        public ActionResult AddBanners(int p)
        {
            ViewBag.pageNumber = p;
            return View();
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult AddBanners(Banners entity, int p, HttpPostedFileBase photo)
        {
            if (TryUpdateModel(entity, new string[] { "title", "subtitle", "style", "url" }) && ModelState.IsValid)
            {
                entity.bannerid = Guid.NewGuid();

                if (photo != null && photo.ContentLength > 0)
                {
                    string fileType = string.Empty;
                    fileType = Path.GetExtension(photo.FileName).ToLower();
                    string newfileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + fileType;

                    entity.photo = newfileName;
                    AzureBlob.UploadFile(container, photo, newfileName);
                }

                bannersService.Create(entity);
                bannersService.SaveChanges();

                return RedirectToAction("Banners", new { p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;
                return View(entity);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult EditBanners(Guid bannerid, int p)
        {
            ViewBag.pageNumber = p;
            Banners banner = bannersService.GetByID(bannerid);
            return View(banner);
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult EditBanners(Guid bannerid, int p, HttpPostedFileBase photo)
        {
            Banners banner = bannersService.GetByID(bannerid);

            if (TryUpdateModel(banner, new string[] { "title", "subtitle", "style", "url" }) && ModelState.IsValid)
            {
                if (photo != null && photo.ContentLength > 0)
                {
                    AzureBlob.DeleteFile(container, banner.photo);

                    string fileType = string.Empty;
                    fileType = Path.GetExtension(photo.FileName).ToLower();
                    string newfileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + fileType;

                    banner.photo = newfileName;
                    AzureBlob.UploadFile(container, photo, newfileName);
                }

                bannersService.Update(banner);
                bannersService.SaveChanges();

                return RedirectToAction("Banners", new { p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;
                return View(banner);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult DeleteBanners(Guid bannerid, int p)
        {
            Banners banner = bannersService.GetByID(bannerid);

            AzureBlob.DeleteFile(container, banner.photo);

            bannersService.Delete(banner);
            bannersService.SaveChanges();

            return RedirectToAction("Banners", new { p = p });
        }

        [CheckSession(IsAuth = true)]
        public ActionResult Recipes(int p = 1, string title = null)
        {
            IEnumerable<Recipes> data = recipesService.Get().OrderBy(o => o.sort);

            if (title != null)
            {
                data = data.Where(a => a.title.Contains(title));
            }

            ViewBag.pageNumber = p;
            ViewBag.Recipes = data.ToPagedList(pageNumber: p, pageSize: 20);
            return View();
        }

        [CheckSession(IsAuth = true)]
        public ActionResult SortRecipes(int p, IEnumerable<Recipes> EntityLists = null)
        {
            foreach (Recipes recipe in EntityLists)
            {
                recipesService.SpecificUpdate(recipe, new string[] { "sort" });
            }
            recipesService.SaveChanges();
            return RedirectToAction("Recipes", new { p = p });
        }

        [CheckSession(IsAuth = true)]
        public ActionResult AddRecipes(int p)
        {
            ViewBag.pageNumber = p;
            ViewBag.Products = productsService.Get();
            return View();
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult AddRecipes(Recipes entity, int p, HttpPostedFileBase rphoto, HttpPostedFileBase photo, Guid[] productids)
        {
            if (TryUpdateModel(entity, new string[] { "title", "duration", "portion", "intro", "youtube", "keyword", "description", "shortener" }) && ModelState.IsValid)
            {
                entity.recipeid = Guid.NewGuid();
                if (entity.youtube != null && entity.youtube != "")
                {
                    NameValueCollection col = General.GetQueryString(entity.youtube);
                    entity.v = col["v"];
                }

                foreach(Recipeingredients recipeingredient in entity.Recipeingredients)
                {
                    recipeingredient.recipeingredientid = Guid.NewGuid();
                }

                foreach(Recipeseasonings recipeseasoning in entity.Recipeseasonings)
                {
                    recipeseasoning.recipeseasoningid = Guid.NewGuid();
                }

                foreach(Recipesteps recipestep in entity.Recipesteps)
                {
                    recipestep.recipestepid = Guid.NewGuid();
                }

                if (rphoto != null && rphoto.ContentLength > 0)
                {
                    string fileType = string.Empty;
                    fileType = Path.GetExtension(rphoto.FileName).ToLower();
                    string newfileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + fileType;

                    entity.rphoto = newfileName;
                    AzureBlob.UploadFile(container, rphoto, newfileName);
                }

                if (photo != null && photo.ContentLength > 0)
                {
                    string fileType = string.Empty;
                    fileType = Path.GetExtension(photo.FileName).ToLower();
                    string newfileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + fileType;

                    entity.photo = newfileName;
                    AzureBlob.UploadFile(container, photo, newfileName);
                }

                if (productids != null)
                {
                    foreach (Guid productid in productids)
                    {
                        Products product = productsService.GetByID(productid);
                        entity.Products.Add(product);
                    }
                }

                recipesService.Create(entity);
                recipesService.SaveChanges();

                return RedirectToAction("Recipes", new { p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;
                ViewBag.Products = productsService.Get();
                return View(entity);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult EditRecipes(Guid entityid, int p)
        {
            ViewBag.pageNumber = p;
            ViewBag.Products = productsService.Get();
            Recipes recipe = recipesService.GetByID(entityid);
            return View(recipe);
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult EditRecipes(Guid recipeid, int p, HttpPostedFileBase rphoto, HttpPostedFileBase photo, ICollection<Recipeingredients> recipeingredients, ICollection<Recipeseasonings> recipeseasonings, ICollection<Recipesteps> recipesteps, Guid[] productids)
        {
            Recipes recipe = recipesService.GetByID(recipeid);

            if (TryUpdateModel(recipe, new string[] { "title", "duration", "portion", "intro", "youtube", "keyword", "description", "shortener" }) && ModelState.IsValid)
            {
                if (recipe.youtube != null && recipe.youtube != "")
                {
                    NameValueCollection col = General.GetQueryString(recipe.youtube);
                    recipe.v = col["v"];
                }else
                {
                    recipe.v = null;
                }

                //************************************************************************************************************************************************
                foreach (Recipeingredients rg in recipe.Recipeingredients.ToArray())
                {
                    if (!recipeingredients.ToList().Exists(x => x.recipeingredientid == rg.recipeingredientid))
                    {
                        recipe.Recipeingredients.Remove(rg);
                        recipeingredientsServier.Delete(rg.recipeingredientid);
                    }
                }

                foreach(Recipeingredients recipeingredient in recipeingredients)
                {
                    if(recipe.Recipeingredients.ToList().Exists(x => x.recipeingredientid == recipeingredient.recipeingredientid))
                    {
                        Recipeingredients ri = recipe.Recipeingredients.Where(a => a.recipeingredientid == recipeingredient.recipeingredientid).FirstOrDefault();
                        ri.title = recipeingredient.title;
                        ri.value = recipeingredient.value;
                        ri.sort = recipeingredient.sort;
                    }
                    else
                    {
                        recipeingredient.recipeingredientid = Guid.NewGuid();
                        recipeingredient.recipeid = recipe.recipeid;
                        recipe.Recipeingredients.Add(recipeingredient);
                    }
                }

                //************************************************************************************************************************************************
                foreach(Recipeseasonings rs in recipe.Recipeseasonings.ToArray())
                {
                    if (!recipeseasonings.ToList().Exists(x => x.recipeseasoningid == rs.recipeseasoningid))
                    {
                        recipe.Recipeseasonings.Remove(rs);
                        recipeseasoningsService.Delete(rs.recipeseasoningid);
                    }
                }

                foreach(Recipeseasonings recipeseasoning in recipeseasonings)
                {
                    if(recipe.Recipeseasonings.ToList().Exists(x => x.recipeseasoningid == recipeseasoning.recipeseasoningid))
                    {
                        Recipeseasonings rsg = recipe.Recipeseasonings.Where(a => a.recipeseasoningid == recipeseasoning.recipeseasoningid).FirstOrDefault();
                        rsg.title = recipeseasoning.title;
                        rsg.value = recipeseasoning.value;
                        rsg.sort = recipeseasoning.sort;
                    }
                    else
                    {
                        recipeseasoning.recipeseasoningid = Guid.NewGuid();
                        recipeseasoning.recipeid = recipe.recipeid;
                        recipe.Recipeseasonings.Add(recipeseasoning);
                    }
                }

                //************************************************************************************************************************************************
                foreach (Recipesteps rs in recipe.Recipesteps.ToArray())
                {
                    if (!recipesteps.ToList().Exists(x => x.recipestepid == rs.recipestepid))
                    {
                        recipe.Recipesteps.Remove(rs);
                        recipestepsService.Delete(rs.recipestepid);
                    }
                }

                foreach(Recipesteps recipestep in recipesteps)
                {
                    if(recipe.Recipesteps.ToList().Exists(x => x.recipestepid == recipestep.recipestepid))
                    {
                        Recipesteps rsp = recipe.Recipesteps.Where(a => a.recipestepid == recipestep.recipestepid).FirstOrDefault();
                        rsp.title = recipestep.title;
                        rsp.value = recipestep.value;
                        rsp.sort = recipestep.sort;
                    }
                    else
                    {
                        recipestep.recipestepid = Guid.NewGuid();
                        recipestep.recipeid = recipe.recipeid;
                        recipe.Recipesteps.Add(recipestep);
                    }
                }

                if (rphoto != null && rphoto.ContentLength > 0)
                {
                    AzureBlob.DeleteFile(container, recipe.rphoto);

                    string fileType = string.Empty;
                    fileType = Path.GetExtension(rphoto.FileName).ToLower();
                    string newfileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + fileType;

                    recipe.rphoto = newfileName;
                    AzureBlob.UploadFile(container, rphoto, newfileName);
                }

                if (photo != null && photo.ContentLength > 0)
                {
                    AzureBlob.DeleteFile(container, recipe.photo);

                    string fileType = string.Empty;
                    fileType = Path.GetExtension(photo.FileName).ToLower();
                    string newfileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + fileType;

                    recipe.photo = newfileName;
                    AzureBlob.UploadFile(container, photo, newfileName);
                }

                var currentproductids = recipe.Products.Select(x => x.productid);

                foreach(Products tt in productsService.Get())
                {
                    if(productids != null && productids.Contains(tt.productid))
                    {
                        if(!currentproductids.Contains(tt.productid))
                        {
                            recipe.Products.Add(tt);
                        }
                    }
                    else
                    {
                        if(currentproductids.Contains(tt.productid))
                        {
                            recipe.Products.Remove(tt);
                        }
                    }
                }

                recipesService.Update(recipe);
                recipesService.SaveChanges();

                return RedirectToAction("Recipes", new { p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;
                ViewBag.Products = productsService.Get();
                return View(recipe);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult DeleteRecipes(Guid recipeid, int p)
        {
            Recipes recipe = recipesService.GetByID(recipeid);

            AzureBlob.DeleteFile(container, recipe.rphoto);
            AzureBlob.DeleteFile(container, recipe.photo);

            recipesService.Delete(recipeid);
            recipesService.SaveChanges();

            return RedirectToAction("Recipes", new { p = p });
        }

        [CheckSession(IsAuth = true)]
        public ActionResult Issues(int p = 1, string k = null)
        {
            IEnumerable<Issues> data = issuesService.Get().OrderBy(o => o.sort);

            if (k != null)
            {
                data = data.Where(a => a.title.Contains(k));
            }

            ViewBag.pageNumber = p;
            ViewBag.k = k;
            ViewBag.Issues = data.ToPagedList(pageNumber: p, pageSize: 20);
            return View();
        }

        [CheckSession(IsAuth = true)]
        public ActionResult SortIssues(int p, IEnumerable<Issues> EntityLists = null)
        {
            foreach (Issues issue in EntityLists)
            {
                issuesService.SpecificUpdate(issue, new string[] { "sort" });
            }
            issuesService.SaveChanges();
            return RedirectToAction("Issues", new { p = p });
        }

        [CheckSession(IsAuth = true)]
        public ActionResult AddIssues(int p)
        {
            ViewBag.pageNumber = p;
            ViewBag.Recipes = recipesService.Get();
            ViewBag.Products = productsService.Get();
            return View();
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        [ValidateInput(false)]
        public ActionResult AddIssues(Issues entity, int p, HttpPostedFileBase photo, Guid[] recipeids, Guid[] productids)
        {
            if (TryUpdateModel(entity, new string[] { "ispublish", "title", "intro", "keyword", "description", "shortener" }) && ModelState.IsValid)
            {
                entity.issueid = Guid.NewGuid();
                entity.createdate = DateTime.Now;

                if (photo != null && photo.ContentLength > 0)
                {
                    string fileType = string.Empty;
                    fileType = Path.GetExtension(photo.FileName).ToLower();
                    string newfileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + fileType;

                    entity.photo = newfileName;
                    AzureBlob.UploadFile(container, photo, newfileName);
                }

                //HtmlDocument doc = new HtmlDocument();
                //doc.LoadHtml(entity.intro);

                //HtmlNodeCollection imageNodes = doc.DocumentNode.SelectNodes(@"//img");
                //if (imageNodes != null)
                //{
                //    foreach (HtmlNode node in imageNodes)
                //    {
                //        string src = node.Attributes["src"].Value;

                //        if (src.StartsWith("data:") != false)
                //        {
                //            string filename = node.Attributes["data-filename"].Value;

                //            string data = src.Replace("data:", "");
                //            string[] ar = data.Split(';');
                //            string[] arbase = ar[1].Split(',');
                //            string contenttype = ar[0];
                //            string base64 = arbase[1];

                //            string fileType = Path.GetExtension(filename).ToLower();

                //            byte[] arr = Convert.FromBase64String(base64);

                //            string path = AzureBlob.UploadByteFile(container, arr, filename, contenttype, DateTime.Now.ToString("yyyyMMdd"));

                //            entity.intro = entity.intro.Replace(src, path);

                //            Issuemedias issuemedia = new Issuemedias();
                //            issuemedia.issuemediaid = Guid.NewGuid();
                //            issuemedia.issueid = entity.issueid;
                //            issuemedia.mediatype = 0;
                //            issuemedia.filename = filename;
                //            issuemedia.filenamepath = path;

                //            entity.Issuemedias.Add(issuemedia);
                //        }
                //    }
                //}

                //HtmlNodeCollection nameNodes = doc.DocumentNode.SelectNodes(@"//iframe[@class='note-video-clip']");
                //if (nameNodes != null)
                //{
                //    foreach (HtmlNode node in nameNodes)
                //    {
                //        string videourl = node.Attributes["src"].Value;
                //        Uri tmp = new Uri(videourl);
                //        //string[] pathsegments = tmp.Segments;
                //        string v = tmp.AbsolutePath.Replace("/", "").Replace("embed", "");
                //        string tmppath = "http://img.youtube.com/vi/";
                //        string imagesize = "mqdefault.jpg";

                //        Issuemedias am = entity.Issuemedias.Where(a => a.issueid == entity.issueid && a.filename == v).FirstOrDefault();

                //        if (am == null)
                //        {
                //            Issuemedias issuemedia = new Issuemedias();
                //            issuemedia.issuemediaid = Guid.NewGuid();
                //            issuemedia.issueid = entity.issueid;
                //            issuemedia.mediatype = 1;
                //            issuemedia.videourl = videourl;
                //            issuemedia.filename = v;
                //            issuemedia.filenamepath = tmppath + v + "/" + imagesize;

                //            entity.Issuemedias.Add(issuemedia);
                //        }
                //    }
                //}

                if (recipeids != null)
                {
                    foreach (Guid recipeid in recipeids)
                    {
                        Recipes recipe = recipesService.GetByID(recipeid);
                        entity.Recipes.Add(recipe);
                    }
                }

                if (productids != null)
                {
                    foreach (Guid productid in productids)
                    {
                        Products product = productsService.GetByID(productid);
                        entity.Products.Add(product);
                    }
                }

                issuesService.Create(entity);
                issuesService.SaveChanges();

                return RedirectToAction("Issues", new { p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;
                ViewBag.Recipes = recipesService.Get();
                ViewBag.Products = productsService.Get();
                return View(entity);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult EditIssues(Guid entityid, int p)
        {
            ViewBag.pageNumber = p;
            ViewBag.Recipes = recipesService.Get();
            ViewBag.Products = productsService.Get();
            Issues issue = issuesService.GetByID(entityid);
            return View(issue);
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        [ValidateInput(false)]
        public ActionResult EditIssues(Guid issueid, int p, HttpPostedFileBase photo, Guid[] recipeids, Guid[] productids)
        {
            Issues issue = issuesService.GetByID(issueid);

            if (TryUpdateModel(issue, new string[] { "ispublish", "title", "intro", "keyword", "description", "shortener" }) && ModelState.IsValid)
            {
                if (photo != null && photo.ContentLength > 0)
                {
                    AzureBlob.DeleteFile(container, issue.photo);

                    string fileType = string.Empty;
                    fileType = Path.GetExtension(photo.FileName).ToLower();
                    string newfileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + fileType;

                    issue.photo = newfileName;
                    AzureBlob.UploadFile(container, photo, newfileName);
                }

                //HtmlDocument doc = new HtmlDocument();
                //doc.LoadHtml(issue.intro);

                //HtmlNodeCollection imageNodes = doc.DocumentNode.SelectNodes(@"//img");
                //if (imageNodes != null)
                //{
                //    foreach (HtmlNode node in imageNodes)
                //    {
                //        string src = node.Attributes["src"].Value;

                //        if (src.StartsWith("data:") != false)
                //        {
                //            string filename = node.Attributes["data-filename"].Value;

                //            string data = src.Replace("data:", "");
                //            string[] ar = data.Split(';');
                //            string[] arbase = ar[1].Split(',');
                //            string contenttype = ar[0];
                //            string base64 = arbase[1];

                //            string fileType = Path.GetExtension(filename).ToLower();
                //            //string newfileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + fileType;

                //            byte[] arr = Convert.FromBase64String(base64);
                //            //arr = ImageResize.MakeResize(arr, 840, contenttype);

                //            string path = AzureBlob.UploadByteFile(container, arr, filename, contenttype, DateTime.Now.ToString("yyyyMMdd"));

                //            issue.intro = issue.intro.Replace(src, path);

                //            Issuemedias issuemedia = new Issuemedias();
                //            issuemedia.issuemediaid = Guid.NewGuid();
                //            issuemedia.issueid = issue.issueid;
                //            issuemedia.mediatype = 0;
                //            issuemedia.filename = filename;
                //            issuemedia.filenamepath = path;

                //            issue.Issuemedias.Add(issuemedia);
                //        }
                //    }
                //}

                //HtmlNodeCollection nameNodes = doc.DocumentNode.SelectNodes(@"//iframe[@class='note-video-clip']");
                //if (nameNodes != null)
                //{
                //    foreach (HtmlNode node in nameNodes)
                //    {
                //        string videourl = node.Attributes["src"].Value;
                //        Uri tmp = new Uri(videourl);
                //        //string[] pathsegments = tmp.Segments;
                //        string v = tmp.AbsolutePath.Replace("/", "").Replace("embed", "");
                //        string tmppath = "http://img.youtube.com/vi/";
                //        string imagesize = "mqdefault.jpg";

                //        Issuemedias am = issue.Issuemedias.Where(a => a.issueid == issue.issueid && a.filename == v).FirstOrDefault();

                //        if (am == null)
                //        {
                //            Issuemedias issuemedia = new Issuemedias();
                //            issuemedia.issuemediaid = Guid.NewGuid();
                //            issuemedia.issueid = issue.issueid;
                //            issuemedia.mediatype = 1;
                //            issuemedia.videourl = videourl;
                //            issuemedia.filename = v;
                //            issuemedia.filenamepath = tmppath + v + "/" + imagesize;

                //            issue.Issuemedias.Add(issuemedia);
                //        }
                //    }
                //}

                var currentrecipeids = issue.Recipes.Select(x => x.recipeid);

                foreach(Recipes tt in recipesService.Get())
                {
                    if(recipeids != null && recipeids.Contains(tt.recipeid))
                    {
                        if(!currentrecipeids.Contains(tt.recipeid))
                        {
                            issue.Recipes.Add(tt);
                        }
                    }
                    else
                    {
                        if(currentrecipeids.Contains(tt.recipeid))
                        {
                            issue.Recipes.Remove(tt);
                        }
                    }
                }

                var currentproductids = issue.Products.Select(x => x.productid);

                foreach (Products tt in productsService.Get())
                {
                    if (productids != null && productids.Contains(tt.productid))
                    {
                        if (!currentproductids.Contains(tt.productid))
                        {
                            issue.Products.Add(tt);
                        }
                    }
                    else
                    {
                        if (currentproductids.Contains(tt.productid))
                        {
                            issue.Products.Remove(tt);
                        }
                    }
                }

                issuesService.Update(issue);
                issuesService.SaveChanges();

                return RedirectToAction("Issues", new { p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;
                ViewBag.Recipes = recipesService.Get();
                ViewBag.Products = productsService.Get();
                return View(issue);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult DeleteIssues(Guid issueid, int p)
        {
            Issues issue = issuesService.GetByID(issueid);

            AzureBlob.DeleteFile(container, issue.photo);

            foreach (Issuemedias issuemedia in issue.Issuemedias.Where(a => a.mediatype == 0).ToArray())
            {
                AzureBlob.DeleteFile(container, issuemedia.filename);
            }

            issuesService.Delete(issue);
            issuesService.SaveChanges();

            return RedirectToAction("Issues", new { p = p });
        }

        [CheckSession(IsAuth = true)]
        public ActionResult News(int p = 1)
        {
            var data = newsService.Get().OrderByDescending(a => a.publishdate);

            ViewBag.pageNumber = p;
            ViewBag.News = data.ToPagedList(pageNumber: p, pageSize: 20);
            return View();
        }

        [CheckSession(IsAuth = true)]
        public ActionResult AddNews(int p)
        {
            ViewBag.pageNumber = p;

            return View();
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        [ValidateInput(false)]
        public ActionResult AddNews(News entity, int p, HttpPostedFileBase photo)
        {
            if (TryUpdateModel(entity, new string[] { "title", "summary", "intro", "activitydate", "activityschedule", "publishdate", "shortener" }) && ModelState.IsValid)
            {
                entity.newid = Guid.NewGuid();

                if (photo != null && photo.ContentLength > 0)
                {
                    string fileType = string.Empty;
                    fileType = Path.GetExtension(photo.FileName).ToLower();
                    string newfileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + fileType;

                    entity.photo = newfileName;
                    AzureBlob.UploadFile(container, photo, newfileName);
                }

                //HtmlDocument doc = new HtmlDocument();
                //doc.LoadHtml(entity.intro);

                //HtmlNodeCollection imageNodes = doc.DocumentNode.SelectNodes(@"//img");
                //if (imageNodes != null)
                //{
                //    foreach (HtmlNode node in imageNodes)
                //    {
                //        string src = node.Attributes["src"].Value;

                //        if (src.StartsWith("data:") != false)
                //        {
                //            string filename = node.Attributes["data-filename"].Value;

                //            string data = src.Replace("data:", "");
                //            string[] ar = data.Split(';');
                //            string[] arbase = ar[1].Split(',');
                //            string contenttype = ar[0];
                //            string base64 = arbase[1];

                //            string fileType = Path.GetExtension(filename).ToLower();
                //            string newfileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + fileType;

                //            byte[] arr = Convert.FromBase64String(base64);
                //            //arr = ImageResize.MakeResize(arr, 840, contenttype);

                //            string path = AzureBlob.UploadByteFile(container, arr, newfileName, contenttype);

                //            entity.intro = entity.intro.Replace(src, path);

                //            Newmedias newmedia = new Newmedias();
                //            newmedia.newmediaid = Guid.NewGuid();
                //            newmedia.newid = entity.newid;
                //            newmedia.mediatype = 0;
                //            newmedia.filename = newfileName;
                //            newmedia.filenamepath = path;

                //            entity.Newmedias.Add(newmedia);
                //        }
                //    }
                //}

                //HtmlNodeCollection nameNodes = doc.DocumentNode.SelectNodes(@"//iframe[@class='note-video-clip']");
                //if (nameNodes != null)
                //{
                //    foreach (HtmlNode node in nameNodes)
                //    {
                //        string videourl = node.Attributes["src"].Value;
                //        Uri tmp = new Uri(videourl);
                //        //string[] pathsegments = tmp.Segments;
                //        string v = tmp.AbsolutePath.Replace("/", "").Replace("embed", "");
                //        string tmppath = "http://img.youtube.com/vi/";
                //        string imagesize = "mqdefault.jpg";

                //        Newmedias am = entity.Newmedias.Where(a => a.newid == entity.newid && a.filename == v).FirstOrDefault();

                //        if (am == null)
                //        {
                //            Newmedias newmedia = new Newmedias();
                //            newmedia.newmediaid = Guid.NewGuid();
                //            newmedia.newid = entity.newid;
                //            newmedia.mediatype = 1;
                //            newmedia.videourl = videourl;
                //            newmedia.filename = v;
                //            newmedia.filenamepath = tmppath + v + "/" + imagesize;

                //            entity.Newmedias.Add(newmedia);
                //        }
                //    }
                //}

                newsService.Create(entity);
                newsService.SaveChanges();

                return RedirectToAction("News", new { p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;
                return View(entity);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult EditNews(Guid newid, int p)
        {
            ViewBag.pageNumber = p;

            News news = newsService.GetByID(newid);
            
            return View(news);
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        [ValidateInput(false)]
        public ActionResult EditNews(Guid newid, int p, HttpPostedFileBase photo)
        {
            News entity = newsService.GetByID(newid);

            if (TryUpdateModel(entity, new string[] { "title", "summary", "intro", "activitydate", "activityschedule", "publishdate", "shortener" }) && ModelState.IsValid)
            {
                if (photo != null && photo.ContentLength > 0)
                {
                    AzureBlob.DeleteFile(container, entity.photo);

                    string fileType = string.Empty;
                    fileType = Path.GetExtension(photo.FileName).ToLower();
                    string newfileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + fileType;

                    entity.photo = newfileName;
                    AzureBlob.UploadFile(container, photo, newfileName);
                }

                //HtmlDocument doc = new HtmlDocument();
                //doc.LoadHtml(entity.intro);

                //HtmlNodeCollection imageNodes = doc.DocumentNode.SelectNodes(@"//img");
                //if (imageNodes != null)
                //{
                //    foreach (HtmlNode node in imageNodes)
                //    {
                //        string src = node.Attributes["src"].Value;

                //        if (src.StartsWith("data:") != false)
                //        {
                //            string filename = node.Attributes["data-filename"].Value;

                //            string data = src.Replace("data:", "");
                //            string[] ar = data.Split(';');
                //            string[] arbase = ar[1].Split(',');
                //            string contenttype = ar[0];
                //            string base64 = arbase[1];

                //            string fileType = Path.GetExtension(filename).ToLower();
                //            string newfileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + fileType;

                //            byte[] arr = Convert.FromBase64String(base64);
                //            //arr = ImageResize.MakeResize(arr, 840, contenttype);

                //            string path = AzureBlob.UploadByteFile(container, arr, newfileName, contenttype);

                //            entity.intro = entity.intro.Replace(src, path);

                //            Newmedias newmedia = new Newmedias();
                //            newmedia.newmediaid = Guid.NewGuid();
                //            newmedia.newid = entity.newid;
                //            newmedia.mediatype = 0;
                //            newmedia.filename = newfileName;
                //            newmedia.filenamepath = path;

                //            entity.Newmedias.Add(newmedia);
                //        }
                //    }
                //}

                //HtmlNodeCollection nameNodes = doc.DocumentNode.SelectNodes(@"//iframe[@class='note-video-clip']");
                //if (nameNodes != null)
                //{
                //    foreach (HtmlNode node in nameNodes)
                //    {
                //        string videourl = node.Attributes["src"].Value;
                //        Uri tmp = new Uri(videourl);
                //        //string[] pathsegments = tmp.Segments;
                //        string v = tmp.AbsolutePath.Replace("/", "").Replace("embed", "");
                //        string tmppath = "http://img.youtube.com/vi/";
                //        string imagesize = "mqdefault.jpg";

                //        Newmedias am = entity.Newmedias.Where(a => a.newid == entity.newid && a.filename == v).FirstOrDefault();

                //        if (am == null)
                //        {
                //            Newmedias newmedia = new Newmedias();
                //            newmedia.newmediaid = Guid.NewGuid();
                //            newmedia.newid = entity.newid;
                //            newmedia.mediatype = 1;
                //            newmedia.videourl = videourl;
                //            newmedia.filename = v;
                //            newmedia.filenamepath = tmppath + v + "/" + imagesize;

                //            entity.Newmedias.Add(newmedia);
                //        }
                //    }
                //}

                newsService.Update(entity);
                newsService.SaveChanges();

                return RedirectToAction("News", new { p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;
                return View(entity);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult DeleteNews(Guid newid, int p)
        {
            News entity = newsService.GetByID(newid);

            AzureBlob.DeleteFile(container, entity.photo);

            foreach (Newmedias newmedia in entity.Newmedias.Where(a => a.mediatype == 0).ToArray())
            {
                AzureBlob.DeleteFile(container, newmedia.filename);
            }

            newsService.Delete(entity);
            newsService.SaveChanges();

            return RedirectToAction("News", new { p = p });
        }

        [CheckSession(IsAuth = true)]
        public ActionResult Blogs(int p = 1)
        {
            var data = blogsService.Get().OrderBy(o => o.sort);

            ViewBag.pageNumber = p;
            ViewBag.Blogs = data.ToPagedList(pageNumber: p, pageSize: 20);
            return View();
        }

        [CheckSession(IsAuth = true)]
        public ActionResult SortBlogs(int p, IEnumerable<Blogs> EntityLists = null)
        {
            foreach (Blogs blog in EntityLists)
            {
                blogsService.SpecificUpdate(blog, new string[] { "sort" });
            }

            blogsService.SaveChanges();
            return RedirectToAction("Blogs", new { p = p });
        }

        [CheckSession(IsAuth = true)]
        public ActionResult AddBlogs(int p)
        {
            ViewBag.pageNumber = p;

            return View();
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult AddBlogs(Blogs entity, int p, HttpPostedFileBase photo)
        {
            if (TryUpdateModel(entity, new string[] { "title", "link" }) && ModelState.IsValid)
            {
                entity.blogid = Guid.NewGuid();

                if (photo != null && photo.ContentLength > 0)
                {
                    string fileType = string.Empty;
                    fileType = Path.GetExtension(photo.FileName).ToLower();
                    string newfileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + fileType;

                    entity.photo = newfileName;
                    AzureBlob.UploadFile(container, photo, newfileName);
                }

                blogsService.Create(entity);
                blogsService.SaveChanges();

                return RedirectToAction("Blogs", new { p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;
                return View(entity);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult EditBlogs(Guid blogid, int p)
        {
            ViewBag.pageNumber = p;
            Blogs blog = blogsService.GetByID(blogid);
            return View(blog);
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult EditBlogs(Guid blogid, int p, HttpPostedFileBase photo)
        {
            Blogs blog = blogsService.GetByID(blogid);

            if (TryUpdateModel(blog, new string[] { "title", "link" }) && ModelState.IsValid)
            {
                if (photo != null && photo.ContentLength > 0)
                {
                    AzureBlob.DeleteFile(container, blog.photo);

                    string fileType = string.Empty;
                    fileType = Path.GetExtension(photo.FileName).ToLower();
                    string newfileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + fileType;

                    blog.photo = newfileName;
                    AzureBlob.UploadFile(container, photo, newfileName);
                }

                blogsService.Update(blog);
                blogsService.SaveChanges();

                return RedirectToAction("Blogs", new { p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;
                return View(blog);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult DeleteBlogs(Guid blogid, int p)
        {
            Blogs blog = blogsService.GetByID(blogid);

            AzureBlob.DeleteFile(container, blog.photo);

            blogsService.Delete(blog);
            blogsService.SaveChanges();

            return RedirectToAction("Blogs", new { p = p });
        }

        [CheckSession(IsAuth = true)]
        public ActionResult Events(int p = 1)
        {
            IEnumerable<Events> data = eventsService.Get().OrderByDescending(o => o.sort);

            ViewBag.pageNumber = p;
            ViewBag.Events = data.ToPagedList(pageNumber: p, pageSize: 15);

            return View();
        }

        [CheckSession(IsAuth = true)]
        public ActionResult SortEvents(int p, IEnumerable<Events> EntityLists = null)
        {
            foreach (Events entity in EntityLists)
            {
                eventsService.SpecificUpdate(entity, new string[] { "sort" });
            }
            eventsService.SaveChanges();
            return RedirectToAction("Events", new { p = p });
        }

        [CheckSession(IsAuth = true)]
        public ActionResult AddEvents(int p)
        {
            ViewBag.pageNumber = p;

            return View();
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        [ValidateInput(false)]
        public ActionResult AddEvents(Events entity, int p, HttpPostedFileBase photo)
        {
            if (TryUpdateModel(entity, new string[] { "title", "intro", "summary", "eventdate", "keyword", "description", "shortener" }) && ModelState.IsValid)
            {
                entity.eventid = Guid.NewGuid();
                entity.createdate = DateTime.Now;

                if (photo != null && photo.ContentLength > 0)
                {
                    string fileType = string.Empty;
                    fileType = Path.GetExtension(photo.FileName).ToLower();
                    string newfileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + fileType;

                    entity.photo = newfileName;
                    AzureBlob.UploadFile(container, photo, newfileName);
                }

                eventsService.Create(entity);
                eventsService.SaveChanges();

                return RedirectToAction("Events", new { p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;
                return View(entity);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult EditEvents(Guid eventid, int p)
        {
            ViewBag.pageNumber = p;

            Events entity = eventsService.GetByID(eventid);
            return View(entity);
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        [ValidateInput(false)]
        public ActionResult EditEvents(Guid eventid, int p, HttpPostedFileBase photo)
        {
            Events entity = eventsService.GetByID(eventid);

            if (TryUpdateModel(entity, new string[] { "title", "intro", "summary", "eventdate", "keyword", "description", "shortener" }) && ModelState.IsValid)
            {
                if (photo != null && photo.ContentLength > 0)
                {
                    AzureBlob.DeleteFile(container, entity.photo);

                    string fileType = string.Empty;
                    fileType = Path.GetExtension(photo.FileName).ToLower();
                    string newfileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + fileType;

                    entity.photo = newfileName;
                    AzureBlob.UploadFile(container, photo, newfileName);
                }

                eventsService.Update(entity);
                eventsService.SaveChanges();

                return RedirectToAction("Events", new { p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;

                return View(entity);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult DeleteEvents(Guid eventid, int p)
        {
            Events entity = eventsService.GetByID(eventid);

            AzureBlob.DeleteFile(container, entity.photo);

            foreach (Eventphotos eventphoto in entity.Eventphotos.ToArray())
            {
                AzureBlob.DeleteFile(container, eventphoto.photo);
            }

            eventsService.Delete(eventid);
            eventsService.SaveChanges();

            return RedirectToAction("Events", new { p = p });
        }

        [CheckSession(IsAuth = true)]
        public ActionResult Eventphotos(Guid eventid, int p)
        {
            ViewBag.pageNumber = p;
            ViewBag.Eventid = eventid;
            ViewBag.Eventphotos = eventphotosService.Get().Where(a => a.eventid == eventid).OrderBy(o => o.sort);
            return View();
        }

        [CheckSession(IsAuth = true)]
        public ActionResult SortEventphotos(Guid eventid, int p, IEnumerable<Eventphotos> EntityLists = null)
        {
            foreach (Eventphotos eventphoto in EntityLists)
            {
                eventphotosService.SpecificUpdate(eventphoto, new string[] { "sort" });
            }
            eventphotosService.SaveChanges();
            return RedirectToAction("Eventphotos", new { eventid = eventid, p = p });
        }

        [CheckSession(IsAuth = true)]
        public ActionResult AddEventphotos(Guid eventid, int p)
        {
            ViewBag.pageNumber = p;
            ViewBag.Eventid = eventid;
            return View();
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult AddEventphotos(Eventphotos entity, Guid eventid, int p, HttpPostedFileBase photo)
        {
            if (TryUpdateModel(entity, new string[] { "sort" }) && ModelState.IsValid)
            {
                entity.eventphotoid = Guid.NewGuid();
                entity.eventid = eventid;

                if (photo != null && photo.ContentLength > 0)
                {
                    string fileType = string.Empty;
                    fileType = Path.GetExtension(photo.FileName).ToLower();
                    string newfileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + fileType;
                    string contenttype = photo.ContentType;

                    byte[] arr;
                    using (Stream inputStream = photo.InputStream)
                    {
                        MemoryStream memoryStream = inputStream as MemoryStream;
                        if (memoryStream == null)
                        {
                            memoryStream = new MemoryStream();
                            inputStream.CopyTo(memoryStream);
                        }
                        arr = memoryStream.ToArray();
                        arr = Watermark.MakeWatermark(arr, bloburl, contenttype);
                    }

                    entity.photo = newfileName;
                    //AzureBlob.UploadFile(container, photo, newfileName);
                    AzureBlob.UploadByteFile(container, arr, newfileName, contenttype);
                }

                eventphotosService.Create(entity);
                eventphotosService.SaveChanges();

                return RedirectToAction("Eventphotos", new { eventid = eventid, p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;
                ViewBag.Eventid = eventid;
                return View(entity);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult EditEventphotos(Guid eventphotoid, int p)
        {
            ViewBag.pageNumber = p;
            Eventphotos eventphoto = eventphotosService.GetByID(eventphotoid);
            return View(eventphoto);
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult EditEventphotos(Guid eventphotoid, int p, HttpPostedFileBase photo)
        {
            Eventphotos eventphoto = eventphotosService.GetByID(eventphotoid);

            if (TryUpdateModel(eventphoto, new string[] { "sort" }) && ModelState.IsValid)
            {
                if (photo != null && photo.ContentLength > 0)
                {
                    AzureBlob.DeleteFile(container, eventphoto.photo);

                    string fileType = string.Empty;
                    fileType = Path.GetExtension(photo.FileName).ToLower();
                    string newfileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + fileType;
                    string contenttype = photo.ContentType;

                    byte[] arr;
                    using (Stream inputStream = photo.InputStream)
                    {
                        MemoryStream memoryStream = inputStream as MemoryStream;
                        if (memoryStream == null)
                        {
                            memoryStream = new MemoryStream();
                            inputStream.CopyTo(memoryStream);
                        }
                        arr = memoryStream.ToArray();
                        arr = Watermark.MakeWatermark(arr, bloburl, contenttype);
                    }

                    eventphoto.photo = newfileName;
                    //AzureBlob.UploadFile(container, photo, newfileName);
                    AzureBlob.UploadByteFile(container, arr, newfileName, contenttype);
                }

                eventphotosService.Update(eventphoto);
                eventphotosService.SaveChanges();

                return RedirectToAction("Eventphotos", new { eventid = eventphoto.eventid, p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;
                return View(eventphoto);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult DeleteEventphotos(Guid eventphotoid, int p)
        {
            Eventphotos eventphoto = eventphotosService.GetByID(eventphotoid);

            AzureBlob.DeleteFile(container, eventphoto.photo);

            eventphotosService.Delete(eventphoto);
            eventphotosService.SaveChanges();

            return RedirectToAction("Eventphotos", new { eventid = eventphoto.eventid, p = p });
        }

        [CheckSession(IsAuth = true)]
        public ActionResult Knowledges(int p = 1, string k = null)
        {
            IEnumerable<Knowledges> data = knowledgesService.Get().OrderBy(o => o.sort);

            if (k != null)
            {
                data = data.Where(a => a.question.Contains(k));
            }

            ViewBag.pageNumber = p;
            ViewBag.k = k;
            ViewBag.Knowledges = data.ToPagedList(pageNumber: p, pageSize: 20);
            return View();
        }

        [CheckSession(IsAuth = true)]
        public ActionResult SortKnowledges(int p, IEnumerable<Knowledges> EntityLists = null)
        {
            foreach (Knowledges knowledge in EntityLists)
            {
                knowledgesService.SpecificUpdate(knowledge, new string[] { "sort" });
            }
            knowledgesService.SaveChanges();
            return RedirectToAction("Knowledges", new { p = p });
        }

        [CheckSession(IsAuth = true)]
        public ActionResult AddKnowledges(int p)
        {
            ViewBag.pageNumber = p;

            return View();
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        [ValidateInput(false)]
        public ActionResult AddKnowledges(Knowledges entity, int p, HttpPostedFileBase photo)
        {
            if (TryUpdateModel(entity, new string[] { "ispublish", "question", "answer", "keyword", "description", "shortener" }) && ModelState.IsValid)
            {
                entity.knowledgeid = Guid.NewGuid();
                entity.createdate = DateTime.Now;

                if (photo != null && photo.ContentLength > 0)
                {
                    string fileType = string.Empty;
                    fileType = Path.GetExtension(photo.FileName).ToLower();
                    string newfileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + fileType;

                    entity.photo = newfileName;
                    AzureBlob.UploadFile(container, photo, newfileName);
                }

                //HtmlDocument doc = new HtmlDocument();
                //doc.LoadHtml(entity.answer);

                //HtmlNodeCollection imageNodes = doc.DocumentNode.SelectNodes(@"//img");
                //if (imageNodes != null)
                //{
                //    foreach (HtmlNode node in imageNodes)
                //    {
                //        string src = node.Attributes["src"].Value;

                //        if (src.StartsWith("data:") != false)
                //        {
                //            string filename = node.Attributes["data-filename"].Value;

                //            string data = src.Replace("data:", "");
                //            string[] ar = data.Split(';');
                //            string[] arbase = ar[1].Split(',');
                //            string contenttype = ar[0];
                //            string base64 = arbase[1];

                //            string fileType = Path.GetExtension(filename).ToLower();
                //            //string newfileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + fileType;

                //            byte[] arr = Convert.FromBase64String(base64);
                //            //arr = ImageResize.MakeResize(arr, 840, contenttype);

                //            string path = AzureBlob.UploadByteFile(container, arr, filename, contenttype, DateTime.Now.ToString("yyyyMMdd"));

                //            entity.answer = entity.answer.Replace(src, path);

                //            Knowledgemedias knowledgemedia = new Knowledgemedias();
                //            knowledgemedia.knowledgemediaid = Guid.NewGuid();
                //            knowledgemedia.knowledgeid = entity.knowledgeid;
                //            knowledgemedia.mediatype = 0;
                //            knowledgemedia.filename = filename;
                //            knowledgemedia.filenamepath = path;

                //            entity.Knowledgemedias.Add(knowledgemedia);
                //        }
                //    }
                //}

                //HtmlNodeCollection nameNodes = doc.DocumentNode.SelectNodes(@"//iframe[@class='note-video-clip']");
                //if (nameNodes != null)
                //{
                //    foreach (HtmlNode node in nameNodes)
                //    {
                //        string videourl = node.Attributes["src"].Value;
                //        Uri tmp = new Uri(videourl);
                //        //string[] pathsegments = tmp.Segments;
                //        string v = tmp.AbsolutePath.Replace("/", "").Replace("embed", "");
                //        string tmppath = "http://img.youtube.com/vi/";
                //        string imagesize = "mqdefault.jpg";

                //        Knowledgemedias km = entity.Knowledgemedias.Where(a => a.knowledgeid == entity.knowledgeid && a.filename == v).FirstOrDefault();

                //        if(km == null)
                //        {
                //            Knowledgemedias knowledgemedia = new Knowledgemedias();
                //            knowledgemedia.knowledgemediaid = Guid.NewGuid();
                //            knowledgemedia.knowledgeid = entity.knowledgeid;
                //            knowledgemedia.mediatype = 1;
                //            knowledgemedia.videourl = videourl;
                //            knowledgemedia.filename = v;
                //            knowledgemedia.filenamepath = tmppath + v + "/" + imagesize;

                //            entity.Knowledgemedias.Add(knowledgemedia);
                //        }
                //    }
                //}

                knowledgesService.Create(entity);
                knowledgesService.SaveChanges();

                return RedirectToAction("Knowledges", new { p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;

                return View(entity);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult EditKnowledges(Guid entityid, int p)
        {
            ViewBag.pageNumber = p;

            Knowledges knowledge = knowledgesService.GetByID(entityid);
            return View(knowledge);
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        [ValidateInput(false)]
        public ActionResult EditKnowledges(Guid knowledgeid, int p, HttpPostedFileBase photo)
        {
            Knowledges knowledge = knowledgesService.GetByID(knowledgeid);

            if (TryUpdateModel(knowledge, new string[] { "ispublish", "question", "answer", "keyword", "description", "shortener" }) && ModelState.IsValid)
            {
                if (photo != null && photo.ContentLength > 0)
                {
                    AzureBlob.DeleteFile(container, knowledge.photo);

                    string fileType = string.Empty;
                    fileType = Path.GetExtension(photo.FileName).ToLower();
                    string newfileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + fileType;

                    knowledge.photo = newfileName;
                    AzureBlob.UploadFile(container, photo, newfileName);
                }

                //HtmlDocument doc = new HtmlDocument();
                //doc.LoadHtml(knowledge.answer);

                //HtmlNodeCollection imageNodes = doc.DocumentNode.SelectNodes(@"//img");
                //if (imageNodes != null)
                //{
                //    foreach (HtmlNode node in imageNodes)
                //    {
                //        string src = node.Attributes["src"].Value;

                //        if (src.StartsWith("data:") != false)
                //        {
                //            string filename = node.Attributes["data-filename"].Value;

                //            string data = src.Replace("data:", "");
                //            string[] ar = data.Split(';');
                //            string[] arbase = ar[1].Split(',');
                //            string contenttype = ar[0];
                //            string base64 = arbase[1];

                //            string fileType = Path.GetExtension(filename).ToLower();
                //            //string newfileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + fileType;

                //            byte[] arr = Convert.FromBase64String(base64);
                //            //arr = ImageResize.MakeResize(arr, 840, contenttype);

                //            string path = AzureBlob.UploadByteFile(container, arr, filename, contenttype, DateTime.Now.ToString("yyyyMMdd"));

                //            knowledge.answer = knowledge.answer.Replace(src, path);

                //            Knowledgemedias knowledgemedia = new Knowledgemedias();
                //            knowledgemedia.knowledgemediaid = Guid.NewGuid();
                //            knowledgemedia.knowledgeid = knowledge.knowledgeid;
                //            knowledgemedia.mediatype = 0;
                //            knowledgemedia.filename = filename;
                //            knowledgemedia.filenamepath = path;

                //            knowledge.Knowledgemedias.Add(knowledgemedia);
                //        }
                //    }
                //}

                //HtmlNodeCollection nameNodes = doc.DocumentNode.SelectNodes(@"//iframe[@class='note-video-clip']");
                //if (nameNodes != null)
                //{
                //    foreach (HtmlNode node in nameNodes)
                //    {
                //        string videourl = node.Attributes["src"].Value;
                //        Uri tmp = new Uri(videourl);
                //        //string[] pathsegments = tmp.Segments;
                //        string v = tmp.AbsolutePath.Replace("/", "").Replace("embed", "");
                //        string tmppath = "http://img.youtube.com/vi/";
                //        string imagesize = "mqdefault.jpg";

                //        Knowledgemedias km = knowledge.Knowledgemedias.Where(a => a.knowledgeid == knowledge.knowledgeid && a.filename == v).FirstOrDefault();

                //        if (km == null)
                //        {
                //            Knowledgemedias knowledgemedia = new Knowledgemedias();
                //            knowledgemedia.knowledgemediaid = Guid.NewGuid();
                //            knowledgemedia.knowledgeid = knowledge.knowledgeid;
                //            knowledgemedia.mediatype = 1;
                //            knowledgemedia.videourl = videourl;
                //            knowledgemedia.filename = v;
                //            knowledgemedia.filenamepath = tmppath + v + "/" + imagesize;

                //            knowledge.Knowledgemedias.Add(knowledgemedia);
                //        }
                //    }
                //}

                knowledgesService.Update(knowledge);
                knowledgesService.SaveChanges();

                return RedirectToAction("Knowledges", new { p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;
                return View(knowledge);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult DeleteKnowledges(Guid knowledgeid, int p)
        {
            Knowledges knowledge = knowledgesService.GetByID(knowledgeid);

            AzureBlob.DeleteFile(container, knowledge.photo);

            foreach (Knowledgemedias knowledgemedia in knowledge.Knowledgemedias.Where(a => a.mediatype == 0).ToArray())
            {
                AzureBlob.DeleteFile(container, knowledgemedia.filename);
            }

            knowledgesService.Delete(knowledge);
            knowledgesService.SaveChanges();

            return RedirectToAction("Knowledges", new { p = p });
        }

        #region -- DropDownList ViewBag --

        #endregion
    }
}