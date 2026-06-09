using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using tfoodies.Filters;
using tfoodies.Commons;
using tfoodies.Models;
using tfoodies.Service;

namespace tfoodies.Controllers
{
    public class PageMsController : BaseController
    {
        private IQuestionTypesService questiontypesService;

        public PageMsController() 
        {
            questiontypesService = new QuestionTypesService();
        }

        public ActionResult Contact()
        {
            ViewBag.Title = "聯絡TFoodies - " + websitetitle;
            ViewBag.Keywords = "橄欖油";
            ViewBag.Description = "";

            return View();
        }

        public ActionResult Howtobuy(Guid? questiontypeid = null)
        {
            ViewBag.Title = "購物說明 - " + websitetitle;
            ViewBag.Keywords = "橄欖油";
            ViewBag.Description = "";

            IEnumerable<Questiontypes> questiontypes = questiontypesService.Get().OrderBy(a => a.sort);
            if (questiontypeid != null)
            {
                ViewBag.Questions = questiontypesService.GetByID(questiontypeid).Questions.OrderBy(a => a.sort);
            }
            else
            {
                ViewBag.Questions = questiontypes.FirstOrDefault().Questions.OrderBy(a => a.sort);
            }

            ViewBag.QuestionTypes = questiontypes;

            return View();
        }

        public ActionResult Terms()
        {
            ViewBag.Title = "網站使用條款 - " + websitetitle;
            ViewBag.Keywords = "橄欖油";
            ViewBag.Description = "";

            return View();
        }

        public ActionResult Policy()
        {
            ViewBag.Title = "隱私權政策 - " + websitetitle;
            ViewBag.Keywords = "橄欖油";
            ViewBag.Description = "";

            return View();
        }

        public ActionResult Disclaimer()
        {
            ViewBag.Title = "免責聲明 - " + websitetitle;
            ViewBag.Keywords = "橄欖油";
            ViewBag.Description = "";

            return View();
        }
    }
}