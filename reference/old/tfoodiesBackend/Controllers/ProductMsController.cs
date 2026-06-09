using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using PagedList;
using System.IO;
using tfoodiesBackend.Filters;
using tfoodiesBackend.Commons;
using tfoodies.Models;
using tfoodies.Service;
using HtmlAgilityPack;

namespace tfoodiesBackend.Controllers
{
    public class ProductMsController : BaseController
    {
        private tfoodiesEntities db;
        private IBrandsService brandsService;
        private IBrandPhotosService brandphotosService;
        private IProductTypesService producttypesService;
        private ITagsService tagsService;
        private IProductsService productsService;
        private IProductPhotosService productphotosService;
        private ISetProductsService setproductsService;
        private IWarehouseStocksService warehousestocksService;

        public ProductMsController()
        {
            db = new tfoodiesEntities();
            brandsService = new BrandsService();
            brandphotosService = new BrandPhotosService();
            producttypesService = new ProductTypesService();
            tagsService = new TagsService(db);
            productsService = new ProductsService(db);
            productphotosService = new ProductPhotosService();
            setproductsService = new SetProductsService(db);
            warehousestocksService = new WarehouseStocksService();
        }

        [CheckSession(IsAuth = true)]
        public ActionResult Brands(int p = 1)
        {
            var data = brandsService.Get().OrderBy(a => a.brandid);

            ViewBag.pageNumber = p;
            ViewBag.Brands = data.ToPagedList(pageNumber: p, pageSize: 20);
            return View();
        }

        [CheckSession(IsAuth = true)]
        public ActionResult AddBrands(int p)
        {
            ViewBag.pageNumber = p;
            return View();
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        [ValidateInput(false)]
        public ActionResult AddBrands(Brands entity, int p, HttpPostedFileBase patternclass, HttpPostedFileBase storybgclass, HttpPostedFileBase logo, HttpPostedFileBase banner, HttpPostedFileBase ilogo, HttpPostedFileBase peoplephoto)
        {
            if (TryUpdateModel(entity, new string[] { "title", "subtitle", "patternentitle", "patternchtitle", "parttnervideo", "patternmemo", "slogan", "intro", "storyentitle", "storychtitle", "storymemo", "peopletitle", "peopleslogan", "peoplememo", "keyword", "description", "isdisplay" }) && ModelState.IsValid)
            {
                entity.brandid = Guid.NewGuid();

                if (patternclass != null && patternclass.ContentLength > 0)
                {
                    string fileType = string.Empty;
                    fileType = Path.GetExtension(patternclass.FileName).ToLower();
                    string newfileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + fileType;

                    entity.patternclass = newfileName;
                    AzureBlob.UploadFile(container, patternclass, newfileName);
                }

                if (storybgclass != null && storybgclass.ContentLength > 0)
                {
                    string fileType = string.Empty;
                    fileType = Path.GetExtension(storybgclass.FileName).ToLower();
                    string newfileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + fileType;

                    entity.storybgclass = newfileName;
                    AzureBlob.UploadFile(container, storybgclass, newfileName);
                }

                if (logo != null && logo.ContentLength > 0)
                {
                    string fileType = string.Empty;
                    fileType = Path.GetExtension(logo.FileName).ToLower();
                    string newfileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + fileType;

                    entity.logo = newfileName;
                    AzureBlob.UploadFile(container, logo, newfileName);
                }

                if (banner != null && banner.ContentLength > 0)
                {
                    string fileType = string.Empty;
                    fileType = Path.GetExtension(banner.FileName).ToLower();
                    string newfileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + fileType;

                    entity.banner = newfileName;
                    AzureBlob.UploadFile(container, banner, newfileName);
                }

                if (ilogo != null && ilogo.ContentLength > 0)
                {
                    string fileType = string.Empty;
                    fileType = Path.GetExtension(ilogo.FileName).ToLower();
                    string newfileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + fileType;

                    entity.ilogo = newfileName;
                    AzureBlob.UploadFile(container, ilogo, newfileName);
                }

                if (peoplephoto != null && peoplephoto.ContentLength > 0)
                {
                    string fileType = string.Empty;
                    fileType = Path.GetExtension(peoplephoto.FileName).ToLower();
                    string newfileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + fileType;

                    entity.peoplephoto = newfileName;
                    AzureBlob.UploadFile(container, peoplephoto, newfileName);
                }

                brandsService.Create(entity);
                brandsService.SaveChanges();

                return RedirectToAction("Brands", new { p = p });
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
        public ActionResult EditBrands(Guid entityid, int p)
        {
            ViewBag.pageNumber = p;
            Brands brand = brandsService.GetByID(entityid);
            return View(brand);
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        [ValidateInput(false)]
        public ActionResult EditBrands(Guid brandid, int p, HttpPostedFileBase patternclass, HttpPostedFileBase storybgclass, HttpPostedFileBase logo, HttpPostedFileBase banner, HttpPostedFileBase ilogo, HttpPostedFileBase peoplephoto)
        {
            Brands brand = brandsService.GetByID(brandid);

            if (TryUpdateModel(brand, new string[] { "title", "subtitle", "patternentitle", "patternchtitle", "parttnervideo", "patternmemo", "slogan", "intro", "storyentitle", "storychtitle", "storymemo", "peopletitle", "peopleslogan", "peoplememo", "keyword", "description", "isdisplay" }) && ModelState.IsValid)
            {
                if (patternclass != null && patternclass.ContentLength > 0)
                {
                    AzureBlob.DeleteFile(container, brand.patternclass);

                    string fileType = string.Empty;
                    fileType = Path.GetExtension(patternclass.FileName).ToLower();
                    string newfileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + fileType;

                    brand.patternclass = newfileName;
                    AzureBlob.UploadFile(container, patternclass, newfileName);
                }

                if (storybgclass != null && storybgclass.ContentLength > 0)
                {
                    AzureBlob.DeleteFile(container, brand.storybgclass);

                    string fileType = string.Empty;
                    fileType = Path.GetExtension(storybgclass.FileName).ToLower();
                    string newfileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + fileType;

                    brand.storybgclass = newfileName;
                    AzureBlob.UploadFile(container, storybgclass, newfileName);
                }

                if (logo != null && logo.ContentLength > 0)
                {
                    AzureBlob.DeleteFile(container, brand.logo);

                    string fileType = string.Empty;
                    fileType = Path.GetExtension(logo.FileName).ToLower();
                    string newfileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + fileType;

                    brand.logo = newfileName;
                    AzureBlob.UploadFile(container, logo, newfileName);
                }

                if (banner != null && banner.ContentLength > 0)
                {
                    AzureBlob.DeleteFile(container, brand.banner);

                    string fileType = string.Empty;
                    fileType = Path.GetExtension(banner.FileName).ToLower();
                    string newfileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + fileType;

                    brand.banner = newfileName;
                    AzureBlob.UploadFile(container, banner, newfileName);
                }

                if (ilogo != null && ilogo.ContentLength > 0)
                {
                    AzureBlob.DeleteFile(container, brand.ilogo);

                    string fileType = string.Empty;
                    fileType = Path.GetExtension(ilogo.FileName).ToLower();
                    string newfileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + fileType;

                    brand.ilogo = newfileName;
                    AzureBlob.UploadFile(container, ilogo, newfileName);
                }

                if (peoplephoto != null && peoplephoto.ContentLength > 0)
                {
                    AzureBlob.DeleteFile(container, brand.peoplephoto);

                    string fileType = string.Empty;
                    fileType = Path.GetExtension(peoplephoto.FileName).ToLower();
                    string newfileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + fileType;

                    brand.peoplephoto = newfileName;
                    AzureBlob.UploadFile(container, peoplephoto, newfileName);
                }

                brandsService.Update(brand);
                brandsService.SaveChanges();

                return RedirectToAction("Brands", new { p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;
                return View(brand);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult DeleteBrands(Guid brandid, int p)
        {
            Brands brand = brandsService.GetByID(brandid);

            AzureBlob.DeleteFile(container, brand.logo);
            AzureBlob.DeleteFile(container, brand.banner);
            AzureBlob.DeleteFile(container, brand.ilogo);
            AzureBlob.DeleteFile(container, brand.peoplephoto);

            foreach (Brandphotos brandphoto in brand.Brandphotos.ToArray())
            {
                AzureBlob.DeleteFile(container, brandphoto.photo);
            }

            brandsService.Delete(brand);
            brandsService.SaveChanges();

            return RedirectToAction("Brands", new { p = p });
        }

        [CheckSession(IsAuth = true)]
        public ActionResult Brandphotos(Guid brandid, int p)
        {
            ViewBag.pageNumber = p;
            ViewBag.Brandid = brandid;
            ViewBag.Brandphotos = brandphotosService.Get().Where(a => a.brandid == brandid).OrderBy(o => o.sort);
            return View();
        }

        [CheckSession(IsAuth = true)]
        public ActionResult SortBrandphotos(Guid brandid, int p, IEnumerable<Brandphotos> EntityLists = null)
        {
            foreach (Brandphotos brandphoto in EntityLists)
            {
                brandphotosService.SpecificUpdate(brandphoto, new string[] { "sort" });
            }
            brandphotosService.SaveChanges();
            return RedirectToAction("Brandphotos", new { brandid = brandid, p = p });
        }

        [CheckSession(IsAuth = true)]
        public ActionResult AddBrandphotos(Guid brandid, int p)
        {
            ViewBag.pageNumber = p;
            ViewBag.Brandid = brandid;
            return View();
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult AddBrandphotos(Brandphotos entity, Guid brandid, int p, HttpPostedFileBase photo)
        {
            if (TryUpdateModel(entity, new string[] { "sort" }) && ModelState.IsValid)
            {
                entity.brandphotoid = Guid.NewGuid();
                entity.brandid = brandid;

                if (photo != null && photo.ContentLength > 0)
                {
                    string fileType = string.Empty;
                    fileType = Path.GetExtension(photo.FileName).ToLower();
                    string newfileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + fileType;

                    entity.photo = newfileName;
                    AzureBlob.UploadFile(container, photo, newfileName);
                }

                brandphotosService.Create(entity);
                brandphotosService.SaveChanges();

                return RedirectToAction("Brandphotos", new { brandid = brandid, p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;
                ViewBag.Brandid = brandid;
                return View(entity);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult EditBrandphotos(Guid brandphotoid, int p)
        {
            ViewBag.pageNumber = p;
            Brandphotos brandphoto = brandphotosService.GetByID(brandphotoid);
            return View(brandphoto);
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult EditBrandphotos(Guid brandphotoid, int p, HttpPostedFileBase photo)
        {
            Brandphotos brandphoto = brandphotosService.GetByID(brandphotoid);

            if (TryUpdateModel(brandphoto, new string[] { "sort" }) && ModelState.IsValid)
            {
                if (photo != null && photo.ContentLength > 0)
                {
                    AzureBlob.DeleteFile(container, brandphoto.photo);

                    string fileType = string.Empty;
                    fileType = Path.GetExtension(photo.FileName).ToLower();
                    string newfileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + fileType;

                    brandphoto.photo = newfileName;
                    AzureBlob.UploadFile(container, photo, newfileName);
                }

                brandphotosService.Update(brandphoto);
                brandphotosService.SaveChanges();

                return RedirectToAction("Brandphotos", new { brandid = brandphoto.brandid, p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;
                return View(brandphoto);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult DeleteBrandphotos(Guid brandphotoid, int p)
        {
            Brandphotos brandphoto = brandphotosService.GetByID(brandphotoid);

            AzureBlob.DeleteFile(container, brandphoto.photo);

            brandphotosService.Delete(brandphoto);
            brandphotosService.SaveChanges();

            return RedirectToAction("Brandphotos", new { brandid = brandphoto.brandid, p = p });
        }

        [CheckSession(IsAuth = true)]
        public ActionResult Producttypes(int p = 1)
        {
            var data = producttypesService.Get().OrderBy(a => a.sort);

            ViewBag.pageNumber = p;
            ViewBag.Producttypes = data.ToPagedList(pageNumber: p, pageSize: 20);
            return View();
        }

        [CheckSession(IsAuth = true)]
        public ActionResult SortProducttypes(int p, IEnumerable<Producttypes> EntityLists = null)
        {
            foreach (Producttypes producttype in EntityLists)
            {
                producttypesService.SpecificUpdate(producttype, new string[] { "sort" });
            }
            producttypesService.SaveChanges();
            return RedirectToAction("Producttypes", new { p = p });
        }

        [CheckSession(IsAuth = true)]
        public ActionResult AddProducttypes(int p)
        {
            ViewBag.pageNumber = p;
            return View();
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult AddProducttypes(Producttypes entity, int p)
        {
            if (TryUpdateModel(entity, new string[] { "title", "isenable", "keyword", "description", "memo" }) && ModelState.IsValid)
            {
                entity.producttypeid = Guid.NewGuid();

                producttypesService.Create(entity);
                producttypesService.SaveChanges();

                return RedirectToAction("Producttypes", new { p = p });
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
        public ActionResult EditProducttypes(Guid entityid, int p)
        {
            ViewBag.pageNumber = p;
            Producttypes producttype = producttypesService.GetByID(entityid);
            return View(producttype);
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult EditProducttypes(Guid producttypeid, int p, FormCollection form)
        {
            Producttypes producttype = producttypesService.GetByID(producttypeid);

            if (TryUpdateModel(producttype, new string[] { "title", "isenable", "keyword", "description", "memo" }) && ModelState.IsValid)
            {

                producttypesService.Update(producttype);
                producttypesService.SaveChanges();

                return RedirectToAction("Producttypes", new { p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;
                return View(producttype);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult DeleteProducttypes(Guid producttypeid, int p)
        {
            Producttypes producttype = producttypesService.GetByID(producttypeid);

            producttypesService.Delete(producttype);
            producttypesService.SaveChanges();

            return RedirectToAction("Producttypes", new { p = p });
        }

        [CheckSession(IsAuth = true)]
        public ActionResult Tags(int p = 1)
        {
            var data = tagsService.Get().OrderBy(a => a.tagid);

            ViewBag.pageNumber = p;
            ViewBag.Tags = data.ToPagedList(pageNumber: p, pageSize: 20);
            return View();
        }

        [CheckSession(IsAuth = true)]
        public ActionResult AddTags(int p)
        {
            ViewBag.pageNumber = p;
            return View();
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult AddTags(Tags entity, int p)
        {
            if (TryUpdateModel(entity, new string[] { "title" }) && ModelState.IsValid)
            {
                entity.tagid = Guid.NewGuid();

                tagsService.Create(entity);
                tagsService.SaveChanges();

                return RedirectToAction("Tags", new { p = p });
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
        public ActionResult EditTags(Guid entityid, int p)
        {
            ViewBag.pageNumber = p;
            Tags tag = tagsService.GetByID(entityid);
            return View(tag);
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult EditTags(Guid tagid, int p, FormCollection form)
        {
            Tags tag = tagsService.GetByID(tagid);

            if (TryUpdateModel(tag, new string[] { "title" }) && ModelState.IsValid)
            {

                tagsService.Update(tag);
                tagsService.SaveChanges();

                return RedirectToAction("Tags", new { p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;
                return View(tag);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult DeleteTags(Guid tagid, int p)
        {
            Tags tag = tagsService.GetByID(tagid);

            tagsService.Delete(tag);
            tagsService.SaveChanges();

            return RedirectToAction("Tags", new { p = p });
        }

        [CheckSession(IsAuth = true)]
        public ActionResult Products(int p = 1, Guid? producttypeid = null, Guid? brandid = null, bool? isdisabled = null, string k = null)
        {
            IEnumerable<Products> data = productsService.Get().OrderByDescending(o => o.sort);

            if(producttypeid != null)
            {
                data = data.Where(a => a.producttypeid == producttypeid);
            }

            if(brandid != null)
            {
                data = data.Where(a => a.brandid == brandid);
            }

            if(isdisabled != null)
            {
                data = data.Where(a => a.isdisabled == isdisabled);
            }

            if(k != null)
            {
                data = data.Where(a => a.title.Contains(k) || a.productnum == k);
            }

            ViewBag.producttypeid = producttypeid;
            ViewBag.brandid = brandid;
            ViewBag.isdisabled = isdisabled;
            ViewBag.k = k;
            ViewBag.pageNumber = p;
            ViewBag.Products = data.ToPagedList(pageNumber: p, pageSize: 15);

            ViewBag.Producttypes = producttypesService.Get().Where(a => a.isenable == true).OrderBy(o => o.sort);
            ViewBag.Brands = brandsService.Get().OrderBy(o => o.sort);
            return View();
        }

        [CheckSession(IsAuth = true)]
        public ActionResult SortProducts(int p, IEnumerable<Products> EntityLists = null)
        {
            foreach (Products product in EntityLists)
            {
                productsService.SpecificUpdate(product, new string[] { "sort" });
            }
            productsService.SaveChanges();
            return RedirectToAction("Products", new { p = p });
        }

        [CheckSession(IsAuth = true)]
        public ActionResult AddProducts(int p)
        {
            ViewBag.pageNumber = p;
            ViewBag.Tags = tagsService.Get();
            BrandsDropDownList();
            ProducttypesDropDownList();
            return View();
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        [ValidateInput(false)]
        public ActionResult AddProducts(Products entity, int p, HttpPostedFileBase photo, Guid[] tagids, ICollection<Setproducts> setproducts = null)
        {
            if (TryUpdateModel(entity, new string[] { "producttypeid", "brandid", "productnum", "isset", "title", "entitle", "intro", "memo", "fixprice", "price", "capacity", "added", "weight", "unit", "isdisabled", "conversion", "keyword", "description", "isgroupbuy", "shortener" }) && ModelState.IsValid)
            {
                if (entity.isset == true && setproducts == null)
                {
                    ModelState.AddModelError("", "必須設定套組內容");
                    ViewBag.pageNumber = p;
                    ViewBag.Tags = tagsService.Get();
                    BrandsDropDownList();
                    ProducttypesDropDownList();
                    return View(entity);
                }

                entity.productid = Guid.NewGuid();
                entity.ishot = false;
                entity.isnew = false;
                entity.createdate = DateTime.Now;

                if (photo != null && photo.ContentLength > 0)
                {
                    string fileType = string.Empty;
                    fileType = Path.GetExtension(photo.FileName).ToLower();
                    string newfileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + fileType;

                    entity.photo = newfileName;
                    AzureBlob.UploadFile(container, photo, newfileName);
                }

                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(entity.memo);

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

                //            string path = AzureBlob.UploadByteFile(container, arr, newfileName, contenttype);

                //            entity.memo = entity.memo.Replace(src, path);

                //            Productmedias productmedia = new Productmedias();
                //            productmedia.productmediaid = Guid.NewGuid();
                //            productmedia.productid = entity.productid;
                //            productmedia.mediatype = 0;
                //            productmedia.filename = newfileName;
                //            productmedia.filenamepath = path;

                //            entity.Productmedias.Add(productmedia);
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

                //        Productmedias am = entity.Productmedias.Where(a => a.productid == entity.productid && a.filename == v).FirstOrDefault();

                //        if (am == null)
                //        {
                //            Productmedias productmedia = new Productmedias();
                //            productmedia.productmediaid = Guid.NewGuid();
                //            productmedia.productid = entity.productid;
                //            productmedia.mediatype = 1;
                //            productmedia.videourl = videourl;
                //            productmedia.filename = v;
                //            productmedia.filenamepath = tmppath + v + "/" + imagesize;

                //            entity.Productmedias.Add(productmedia);
                //        }
                //    }
                //}

                if (tagids != null)
                {
                    foreach (Guid tagid in tagids)
                    {
                        Tags tag = tagsService.GetByID(tagid);
                        entity.Tags.Add(tag);
                    }
                }

                if(entity.isset == true && setproducts != null)
                {
                    //foreach (Setproducts sp in entity.Setproducts.ToArray())
                    //{
                    //    if (!setproducts.ToList().Exists(x => x.setproductid == sp.setproductid))
                    //    {
                    //        entity.Setproducts.Remove(sp);
                    //        setproductsService.Delete(sp.setproductid);
                    //    }
                    //}

                    //foreach (Setproducts setproduct in setproducts)
                    //{
                    //    if (entity.Setproducts.ToList().Exists(x => x.setproductid == setproduct.setproductid) && setproduct.setproductid != 0)
                    //    {
                    //        Setproducts sp = entity.Setproducts.Where(a => a.setproductid == setproduct.setproductid).FirstOrDefault();
                    //        sp.productid = setproduct.productid;
                    //        sp.qty = setproduct.qty;
                    //    }
                    //    else
                    //    {
                    //        setproduct.oproductid = entity.productid;
                    //        entity.Setproducts.Add(setproduct);
                    //    }
                    //}
                }
                else
                {
                    foreach (Setproducts sp in entity.Setproducts.ToArray())
                    {
                        entity.Setproducts.Remove(sp);
                        setproductsService.Delete(sp.setproductid);
                    }
                }

                productsService.Create(entity);
                productsService.SaveChanges();

                return RedirectToAction("Products", new { p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;
                ViewBag.Tags = tagsService.Get();
                BrandsDropDownList();
                ProducttypesDropDownList();
                return View(entity);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult EditProducts(Guid productid, int p)
        {
            ViewBag.pageNumber = p;
            ViewBag.Tags = tagsService.Get();
            Products product = productsService.GetByID(productid);
            ViewBag.Warehousestocks = warehousestocksService.Get().Where(a => a.Stocks.Purchasedetails.Products.productid == productid);
            BrandsDropDownList(product.brandid);
            ProducttypesDropDownList(product.producttypeid);
            return View(product);
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        [ValidateInput(false)]
        public ActionResult EditProducts(Guid productid, int p, HttpPostedFileBase photo, Guid[] tagids, ICollection<Setproducts> setproducts = null)
        {
            Products product = productsService.GetByID(productid);

            if (TryUpdateModel(product, new string[] { "producttypeid", "brandid", "productnum", "isset", "title", "entitle", "intro", "memo", "fixprice", "price", "capacity", "added", "weight", "unit", "conversion", "ishot", "isnew", "isdisabled", "keyword", "description", "isgroupbuy", "shortener" }) && ModelState.IsValid)
            {
                if (product.isset == true && setproducts == null)
                {
                    ModelState.AddModelError("", "必須設定套組內容");
                    ViewBag.pageNumber = p;
                    ViewBag.Tags = tagsService.Get();
                    BrandsDropDownList();
                    ProducttypesDropDownList();
                    return View(product);
                }

                if (photo != null && photo.ContentLength > 0)
                {
                    AzureBlob.DeleteFile(container, product.photo);

                    string fileType = string.Empty;
                    fileType = Path.GetExtension(photo.FileName).ToLower();
                    string newfileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + fileType;

                    product.photo = newfileName;
                    AzureBlob.UploadFile(container, photo, newfileName);
                }

                //HtmlDocument doc = new HtmlDocument();
                //doc.LoadHtml(product.memo);

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

                //            product.memo = product.memo.Replace(src, path);

                //            Productmedias productmedia = new Productmedias();
                //            productmedia.productmediaid = Guid.NewGuid();
                //            productmedia.productid = product.productid;
                //            productmedia.mediatype = 0;
                //            productmedia.filename = newfileName;
                //            productmedia.filenamepath = path;

                //            product.Productmedias.Add(productmedia);
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

                //        Productmedias am = product.Productmedias.Where(a => a.productid == product.productid && a.filename == v).FirstOrDefault();

                //        if (am == null)
                //        {
                //            Productmedias productmedia = new Productmedias();
                //            productmedia.productmediaid = Guid.NewGuid();
                //            productmedia.productid = product.productid;
                //            productmedia.mediatype = 1;
                //            productmedia.videourl = videourl;
                //            productmedia.filename = v;
                //            productmedia.filenamepath = tmppath + v + "/" + imagesize;

                //            product.Productmedias.Add(productmedia);
                //        }
                //    }
                //}

                var currenttagids = product.Tags.Select(x => x.tagid);

                foreach (Tags tt in tagsService.Get())
                {
                    if (tagids != null && tagids.Contains(tt.tagid))
                    {
                        if (!currenttagids.Contains(tt.tagid))
                        {
                            product.Tags.Add(tt);
                        }
                    }
                    else
                    {
                        if (currenttagids.Contains(tt.tagid))
                        {
                            product.Tags.Remove(tt);
                        }
                    }
                }

                if (product.isset == true && setproducts != null)
                {
                    foreach (Setproducts sp in product.Setproducts.ToArray())
                    {
                        if (!setproducts.ToList().Exists(x => x.setproductid == sp.setproductid))
                        {
                            product.Setproducts.Remove(sp);
                            setproductsService.Delete(sp.setproductid);
                        }
                    }

                    foreach (Setproducts setproduct in setproducts)
                    {
                        if (product.Setproducts.ToList().Exists(x => x.setproductid == setproduct.setproductid) && setproduct.setproductid != 0)
                        {
                            Setproducts sp = product.Setproducts.Where(a => a.setproductid == setproduct.setproductid).FirstOrDefault();
                            sp.productid = setproduct.productid;
                            sp.qty = setproduct.qty;
                        }
                        else
                        {
                            product.Setproducts.Add(setproduct);
                        }
                    }
                }
                else
                {
                    foreach (Setproducts sp in product.Setproducts.ToArray())
                    {
                        product.Setproducts.Remove(sp);
                        setproductsService.Delete(sp.setproductid);
                    }
                }

                productsService.Update(product);
                productsService.SaveChanges();

                return RedirectToAction("Products", new { p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;
                ViewBag.Tags = tagsService.Get();
                ViewBag.Warehousestocks = warehousestocksService.Get().Where(a => a.Stocks.Purchasedetails.Products.productid == productid);
                BrandsDropDownList(product.brandid);
                ProducttypesDropDownList(product.producttypeid);
                return View(product);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult DeleteProducts(Guid productid, int p)
        {
            Products product = productsService.GetByID(productid);

            AzureBlob.DeleteFile(container, product.photo);

            foreach (Productphotos productphoto in product.Productphotos.ToArray())
            {
                AzureBlob.DeleteFile(container, productphoto.photo);
            }

            foreach (Productmedias productmedia in product.Productmedias.Where(a => a.mediatype == 0).ToArray())
            {
                AzureBlob.DeleteFile(container, productmedia.filename);
            }

            product.isdisabled = true;

            productsService.Update(product);
            productsService.SaveChanges();

            return RedirectToAction("Products", new { p = p });
        }

        [CheckSession(IsAuth = true)]
        public ActionResult Productphotos(Guid productid, int p)
        {
            ViewBag.pageNumber = p;
            ViewBag.Productid = productid;
            ViewBag.Productphotos = productphotosService.Get().Where(a => a.productid == productid).OrderBy(o => o.sort);
            return View();
        }

        [CheckSession(IsAuth = true)]
        public ActionResult SortProductphotos(Guid productid, int p, IEnumerable<Productphotos> EntityLists = null)
        {
            foreach (Productphotos productphoto in EntityLists)
            {
                productphotosService.SpecificUpdate(productphoto, new string[] { "sort" });
            }
            productphotosService.SaveChanges();
            return RedirectToAction("Productphotos", new { productid = productid, p = p });
        }

        [CheckSession(IsAuth = true)]
        public ActionResult AddProductphotos(Guid productid, int p)
        {
            ViewBag.pageNumber = p;
            ViewBag.Productid = productid;
            return View();
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult AddProductphotos(Productphotos entity, Guid productid, int p, HttpPostedFileBase photo)
        {
            if (TryUpdateModel(entity, new string[] { "sort" }) && ModelState.IsValid)
            {
                entity.productphotoid = Guid.NewGuid();
                entity.productid = productid;

                if (photo != null && photo.ContentLength > 0)
                {
                    string fileType = string.Empty;
                    fileType = Path.GetExtension(photo.FileName).ToLower();
                    string newfileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + fileType;

                    entity.photo = newfileName;
                    AzureBlob.UploadFile(container, photo, newfileName);
                }

                productphotosService.Create(entity);
                productphotosService.SaveChanges();

                return RedirectToAction("Productphotos", new { productid = productid, p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;
                ViewBag.Productid = productid;
                return View(entity);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult EditProductphotos(Guid productphotoid, int p)
        {
            ViewBag.pageNumber = p;
            Productphotos productphoto = productphotosService.GetByID(productphotoid);
            return View(productphoto);
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult EditProductphotos(Guid productphotoid, int p, HttpPostedFileBase photo)
        {
            Productphotos productphoto = productphotosService.GetByID(productphotoid);

            if (TryUpdateModel(productphoto, new string[] { "sort" }) && ModelState.IsValid)
            {
                if (photo != null && photo.ContentLength > 0)
                {
                    AzureBlob.DeleteFile(container, productphoto.photo);

                    string fileType = string.Empty;
                    fileType = Path.GetExtension(photo.FileName).ToLower();
                    string newfileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + fileType;
                    
                    productphoto.photo = newfileName;
                    AzureBlob.UploadFile(container, photo, newfileName);
                }

                productphotosService.Update(productphoto);
                productphotosService.SaveChanges();

                return RedirectToAction("Productphotos", new { productid = productphoto.productid, p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;
                return View(productphoto);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult DeleteProductphotos(Guid productphotoid, int p)
        {
            Productphotos productphoto = productphotosService.GetByID(productphotoid);

            AzureBlob.DeleteFile(container, productphoto.photo);

            productphotosService.Delete(productphoto);
            productphotosService.SaveChanges();

            return RedirectToAction("Productphotos", new { productid = productphoto.productid, p = p });
        }

        #region -- DropDownList ViewBag --
        private void BrandsDropDownList(Object selectBrands = null)
        {
            var querys = brandsService.Get().OrderBy(a => a.brandid);

            ViewBag.brandid = new SelectList(querys, "brandid", "title", selectBrands);
        }

        private void ProducttypesDropDownList(Object selectProducttypes = null)
        {
            var querys = producttypesService.Get().OrderBy(a => a.producttypeid);

            ViewBag.producttypeid = new SelectList(querys, "producttypeid", "title", selectProducttypes);
        }
        #endregion
    }
}