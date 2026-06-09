using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.IO;
using PagedList;
using tfoodiesBackend.Filters;
using tfoodiesBackend.Commons;
using tfoodies.Models;
using tfoodies.Service;
using HtmlAgilityPack;

namespace tfoodiesBackend.Controllers
{
    public class SettingMsController : BaseController
    {
        private tfoodiesEntities db;
        private AdminsService adminsService;
        private ILimsService limsService;
        private IAdminLimsService adminlimsService;
        private IQuestionTypesService questiontypesService;
        private IQuestionsService questionsService;
        private IDiscountsService discountsService;

        public SettingMsController()
        {
            db = new tfoodiesEntities();
            adminsService = new AdminsService(db);
            limsService = new LimsService(db);
            adminlimsService = new AdminLimsService(db);
            questiontypesService = new QuestionTypesService();
            questionsService = new QuestionsService();
            discountsService = new DiscountsService();
        }

        [CheckSession(IsAuth = true)]
        public ActionResult Admins(int p = 1)
        {
            var data = adminsService.Get().OrderBy(o => o.AdminID);

            ViewBag.pageNumber = p;
            ViewBag.Admins = data.ToPagedList(pageNumber: p, pageSize: 20);
            return View();
        }

        [CheckSession(IsAuth = true)]
        public ActionResult AddAdmins(int p)
        {
            ViewBag.pageNumber = p;
            ViewBag.Lims = limsService.Get().Where(a => a.ParentID == null).OrderBy(a => a.Sort);
            return View();
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult AddAdmins(Admins admin, int p)
        {
            foreach (var ms in ModelState.ToArray())
            {
                if (ms.Key.StartsWith("AdminLims["))
                {
                    ModelState.Remove(ms);
                }
            }

            if (TryUpdateModel(admin, new string[] { "username", "password" }) && ModelState.IsValid)
            {
                admin.Isenable = 1;

                if (admin.AdminLims != null)
                {
                    foreach (AdminLims adminlim in admin.AdminLims.ToArray())
                    {
                        if (adminlim.LimID != 0)
                        {
                            adminlim.AdminLimID = Guid.NewGuid();
                        }
                        else
                        {
                            admin.AdminLims.Remove(adminlim);
                        }
                    }
                }

                adminsService.Create(admin);
                adminsService.SaveChanges();

                return RedirectToAction("Admins", new { p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;
                ViewBag.Lims = limsService.Get().Where(a => a.ParentID == null).OrderBy(a => a.Sort);
                return View(admin);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult EditAdmins(int adminid, int p)
        {
            ViewBag.pageNumber = p;
            ViewBag.Lims = limsService.Get().Where(a => a.ParentID == null).OrderBy(a => a.Sort);
            Admins admin = adminsService.GetByID(adminid);        

            return View(admin);
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult EditAdmins(int adminid, int p, ICollection<AdminLims> AdminLims, int isenable = 0, string password = null)
        {
            Admins admin = adminsService.GetByID(adminid);

            if (ModelState.IsValid)
            {
                admin.Isenable = Convert.ToByte(isenable);
                if (password != null && password != "") admin.Password = password;

                if (AdminLims != null)
                {
                    AdminLims = AdminLims.Where(a => a.LimID != 0).ToList();

                    foreach (AdminLims adminlim in admin.AdminLims.ToArray())
                    {
                        if (!AdminLims.ToList().Exists(a => a.LimID == adminlim.LimID))
                        {
                            admin.AdminLims.Remove(adminlim);
                            adminlimsService.Delete(adminlim.AdminLimID);
                        }
                    }

                    foreach (AdminLims al in AdminLims)
                    {
                        if (admin.AdminLims.ToList().Exists(a => a.LimID == al.LimID))
                        {
                            AdminLims cd = admin.AdminLims.Where(a => a.LimID == al.LimID).FirstOrDefault();
                            cd.IsAdd = al.IsAdd;
                            cd.IsUpdate = al.IsUpdate;
                            cd.IsDelete = al.IsDelete;
                        }
                        else
                        {
                            al.AdminLimID = Guid.NewGuid();
                            al.AdminID = admin.AdminID;
                            admin.AdminLims.Add(al);
                        }
                    }
                }

                adminsService.Update(admin);
                adminsService.SaveChanges();

                return RedirectToAction("Admins", new { p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;
                ViewBag.Lims = limsService.Get().Where(a => a.ParentID == null).OrderBy(a => a.Sort);
                return View(admin);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult DeleteAdmins(int adminid, int p)
        {
            Admins admin = adminsService.GetByID(adminid);

            admin.Isenable = 0;

            adminsService.SaveChanges();
            return RedirectToAction("Admins", new { p = p });
        }

        [CheckSession(IsAuth = true)]
        public ActionResult Questiontypes(int p = 1)
        {
            var data = questiontypesService.Get().OrderBy(a => a.sort);

            ViewBag.pageNumber = p;
            ViewBag.QuestionTypes = data.ToPagedList(pageNumber: p, pageSize: 20);
            return View();
        }

        [CheckSession(IsAuth = true)]
        public ActionResult SortQuestiontypes(int p, IEnumerable<Questiontypes> EntityLists = null)
        {
            foreach (Questiontypes questiontype in EntityLists)
            {
                questiontypesService.SpecificUpdate(questiontype, new string[] { "sort" });
            }
            questiontypesService.SaveChanges();
            return RedirectToAction("Questiontypes", new { p = p });
        }

        [CheckSession(IsAuth = true)]
        public ActionResult AddQuestiontypes(int p)
        {
            ViewBag.pageNumber = p;
            return View();
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult AddQuestiontypes(Questiontypes entity, int p)
        {
            if (TryUpdateModel(entity, new string[] { "title" }) && ModelState.IsValid)
            {
                entity.questiontypeid = Guid.NewGuid();

                questiontypesService.Create(entity);
                questiontypesService.SaveChanges();

                return RedirectToAction("Questiontypes", new { p = p });
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
        public ActionResult EditQuestiontypes(Guid entityid, int p)
        {
            ViewBag.pageNumber = p;
            Questiontypes questiontype = questiontypesService.GetByID(entityid);
            return View(questiontype);
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult EditQuestiontypes(Guid questiontypeid, int p, FormCollection form)
        {
            Questiontypes questiontype = questiontypesService.GetByID(questiontypeid);

            if (TryUpdateModel(questiontype, new string[] { "title" }) && ModelState.IsValid)
            {
                questiontypesService.Update(questiontype);
                questiontypesService.SaveChanges();

                return RedirectToAction("Questiontypes", new { p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;
                return View(questiontype);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult DeleteQuestiontypes(Guid entityid, int p)
        {
            questiontypesService.Delete(entityid);
            questiontypesService.SaveChanges();

            return RedirectToAction("Questiontypes", new { p = p });
        }

        [CheckSession(IsAuth = true)]
        public ActionResult Questions(int p = 1)
        {
            var data = questionsService.Get().OrderBy(a => a.sort);

            ViewBag.pageNumber = p;
            ViewBag.Questions = data.ToPagedList(pageNumber: p, pageSize: 20);
            return View();
        }

        [CheckSession(IsAuth = true)]
        public ActionResult SortQuestions(int p, IEnumerable<Questions> EntityLists = null)
        {
            foreach (Questions question in EntityLists)
            {
                questionsService.SpecificUpdate(question, new string[] { "sort" });
            }
            questionsService.SaveChanges();
            return RedirectToAction("Questions", new { p = p });
        }

        [CheckSession(IsAuth = true)]
        public ActionResult AddQuestions(int p)
        {
            ViewBag.pageNumber = p;
            QuestiontypesDropDownList();
            return View();
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        [ValidateInput(false)]
        public ActionResult AddQuestions(Questions entity, int p)
        {
            if (TryUpdateModel(entity, new string[] { "title", "questiontypeid", "answer" }) && ModelState.IsValid)
            {
                entity.questionid = Guid.NewGuid();

                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(entity.answer);

                HtmlNodeCollection imageNodes = doc.DocumentNode.SelectNodes(@"//img");
                if (imageNodes != null)
                {
                    foreach (HtmlNode node in imageNodes)
                    {
                        string src = node.Attributes["src"].Value;

                        if (src.StartsWith("data:") != false)
                        {
                            string filename = node.Attributes["data-filename"].Value;

                            string data = src.Replace("data:", "");
                            string[] ar = data.Split(';');
                            string[] arbase = ar[1].Split(',');
                            string contenttype = ar[0];
                            string base64 = arbase[1];

                            string fileType = Path.GetExtension(filename).ToLower();
                            string newfileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + fileType;

                            byte[] arr = Convert.FromBase64String(base64);
                            //arr = ImageResize.MakeResize(arr, 840, contenttype);

                            string path = AzureBlob.UploadByteFile(container, arr, newfileName, contenttype);

                            entity.answer = entity.answer.Replace(src, path);

                            Questionmedias questionmedia = new Questionmedias();
                            questionmedia.questionmediaid = Guid.NewGuid();
                            questionmedia.questionid = entity.questionid;
                            questionmedia.mediatype = 0;
                            questionmedia.filename = newfileName;
                            questionmedia.filenamepath = path;

                            entity.Questionmedias.Add(questionmedia);
                        }
                    }
                }

                HtmlNodeCollection nameNodes = doc.DocumentNode.SelectNodes(@"//iframe[@class='note-video-clip']");
                if (nameNodes != null)
                {
                    foreach (HtmlNode node in nameNodes)
                    {
                        string videourl = node.Attributes["src"].Value;
                        Uri tmp = new Uri(videourl);
                        //string[] pathsegments = tmp.Segments;
                        string v = tmp.AbsolutePath.Replace("/", "").Replace("embed", "");
                        string tmppath = "http://img.youtube.com/vi/";
                        string imagesize = "mqdefault.jpg";

                        Questionmedias am = entity.Questionmedias.Where(a => a.questionid == entity.questionid && a.filename == v).FirstOrDefault();

                        if (am == null)
                        {
                            Questionmedias questionmedia = new Questionmedias();
                            questionmedia.questionmediaid = Guid.NewGuid();
                            questionmedia.questionid = entity.questionid;
                            questionmedia.mediatype = 1;
                            questionmedia.videourl = videourl;
                            questionmedia.filename = v;
                            questionmedia.filenamepath = tmppath + v + "/" + imagesize;

                            entity.Questionmedias.Add(questionmedia);
                        }
                    }
                }

                questionsService.Create(entity);
                questionsService.SaveChanges();

                return RedirectToAction("Questions", new { p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;
                QuestiontypesDropDownList();
                return View(entity);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult EditQuestions(Guid entityid, int p)
        {
            ViewBag.pageNumber = p;
            Questions question = questionsService.GetByID(entityid);

            QuestiontypesDropDownList(question.questiontypeid);

            return View(question);
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        [ValidateInput(false)]
        public ActionResult EditQuestions(Guid questionid, int p, FormCollection form)
        {
            Questions question = questionsService.GetByID(questionid);

            if (TryUpdateModel(question, new string[] { "title", "questiontypeid", "answer" }) && ModelState.IsValid)
            {
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(question.answer);

                HtmlNodeCollection imageNodes = doc.DocumentNode.SelectNodes(@"//img");
                if (imageNodes != null)
                {
                    foreach (HtmlNode node in imageNodes)
                    {
                        string src = node.Attributes["src"].Value;

                        if (src.StartsWith("data:") != false)
                        {
                            string filename = node.Attributes["data-filename"].Value;

                            string data = src.Replace("data:", "");
                            string[] ar = data.Split(';');
                            string[] arbase = ar[1].Split(',');
                            string contenttype = ar[0];
                            string base64 = arbase[1];

                            string fileType = Path.GetExtension(filename).ToLower();
                            string newfileName = DateTime.Now.ToString("yyyyMMddHHmmssff") + fileType;

                            byte[] arr = Convert.FromBase64String(base64);
                            //arr = ImageResize.MakeResize(arr, 840, contenttype);

                            string path = AzureBlob.UploadByteFile(container, arr, newfileName, contenttype);

                            question.answer = question.answer.Replace(src, path);

                            Questionmedias questionmedia = new Questionmedias();
                            questionmedia.questionmediaid = Guid.NewGuid();
                            questionmedia.questionid = question.questionid;
                            questionmedia.mediatype = 0;
                            questionmedia.filename = newfileName;
                            questionmedia.filenamepath = path;

                            question.Questionmedias.Add(questionmedia);
                        }
                    }
                }

                HtmlNodeCollection nameNodes = doc.DocumentNode.SelectNodes(@"//iframe[@class='note-video-clip']");
                if (nameNodes != null)
                {
                    foreach (HtmlNode node in nameNodes)
                    {
                        string videourl = node.Attributes["src"].Value;
                        Uri tmp = new Uri(videourl);
                        //string[] pathsegments = tmp.Segments;
                        string v = tmp.AbsolutePath.Replace("/", "").Replace("embed", "");
                        string tmppath = "http://img.youtube.com/vi/";
                        string imagesize = "mqdefault.jpg";

                        Questionmedias am = question.Questionmedias.Where(a => a.questionid == question.questionid && a.filename == v).FirstOrDefault();

                        if (am == null)
                        {
                            Questionmedias questionmedia = new Questionmedias();
                            questionmedia.questionmediaid = Guid.NewGuid();
                            questionmedia.questionid = question.questionid;
                            questionmedia.mediatype = 1;
                            questionmedia.videourl = videourl;
                            questionmedia.filename = v;
                            questionmedia.filenamepath = tmppath + v + "/" + imagesize;

                            question.Questionmedias.Add(questionmedia);
                        }
                    }
                }

                questionsService.Update(question);
                questionsService.SaveChanges();

                return RedirectToAction("Questions", new { p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;
                QuestiontypesDropDownList(question.questiontypeid);
                return View(question);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult DeleteQuestions(Guid entityid, int p)
        {
            questionsService.Delete(entityid);
            questionsService.SaveChanges();

            return RedirectToAction("Questions", new { p = p });
        }

        [CheckSession(IsAuth = true)]
        public ActionResult Discounts(int p = 1)
        {
            var data = discountsService.Get().OrderByDescending(a => a.startdate);

            ViewBag.pageNumber = p;
            ViewBag.Discounts = data.ToPagedList(pageNumber: p, pageSize: 20);
            return View();
        }

        [CheckSession(IsAuth = true)]
        public ActionResult AddDiscounts(int p)
        {
            ViewBag.pageNumber = p;
            return View();
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult AddDiscounts(Discounts entity, int p)
        {
            if (TryUpdateModel(entity, new string[] { "discountcode", "istype", "startdate", "expiredate", "isonetime", "v", "isdisable", "memo" }) && ModelState.IsValid)
            {
                entity.discountid = Guid.NewGuid();

                discountsService.Create(entity);
                discountsService.SaveChanges();

                return RedirectToAction("Discounts", new { p = p });
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
        public ActionResult EditDiscounts(Guid discountid, int p)
        {
            ViewBag.pageNumber = p;

            Discounts discount = discountsService.GetByID(discountid);

            return View(discount);
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult EditDiscounts(Guid discountid, int p, FormCollection form)
        {
            Discounts discount = discountsService.GetByID(discountid);

            if (TryUpdateModel(discount, new string[] { "discountcode", "istype", "startdate", "expiredate", "isonetime", "v", "isdisable", "memo" }) && ModelState.IsValid)
            {
                discountsService.Update(discount);
                discountsService.SaveChanges();

                return RedirectToAction("Discounts", new { p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;
                return View(discount);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult DeleteDiscounts(Guid discountid, int p)
        {
            Discounts discount = discountsService.GetByID(discountid);

            discount.isdisable = 1;

            discountsService.Update(discount);
            discountsService.SaveChanges();

            return RedirectToAction("Discounts", new { p = p });
        }

        #region -- DropDownList ViewBag --
        private void QuestiontypesDropDownList(Object selectQuestiontypes = null)
        {
            var querys = questiontypesService.Get().OrderBy(a => a.sort);

            ViewBag.questiontypeid = new SelectList(querys, "questiontypeid", "title", selectQuestiontypes);
        }
        #endregion
    }
}