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

namespace tfoodiesBackend.Controllers
{
    public class MemberMsController : BaseController
    {
        private IMembersService membersService;
        private IZipcodesService zipcodesService;
        private ISmsService smsService;
        private ISmsdetailsService smsdetailsService;

        public MemberMsController()
        {
            membersService = new MembersService();
            zipcodesService = new ZipcodesService();
            smsService = new SmsService();
            smsdetailsService = new SmsdetailsService();
        }

        [CheckSession(IsAuth = true)]
        public ActionResult Members(int p = 1, string field = null, string v = null)
        {
            IEnumerable<Members> data = membersService.Get().OrderBy(a => a.name).ToList();

            if (field != null)
            {
                switch (field)
                {
                    case "name":
                        data = data.Where(a => a.name.Contains(v));
                        p = 1;
                        break;
                    case "mobile":
                        data = data.Where(a => a.mobile == v);
                        p = 1;
                        break;
                }
            }

            ViewBag.pageNumber = p;
            ViewBag.Field = field;
            ViewBag.V = v;
            ViewBag.Members = data.ToPagedList(pageNumber: p, pageSize: 20);
            return View();
        }

        [CheckSession(IsAuth = true)]
        public ActionResult AddMembers(int p)
        {
            ViewBag.pageNumber = p;
            ViewBag.Citys = zipcodesService.Get().Select(g => g.city).Distinct();
            return View();
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult AddMembers(Members entity, int p)
        {
            if (TryUpdateModel(entity, new string[] { "name", "mobile", "password", "email", "gender", "birthday", "zipcodeid", "address", "ismember", "memo" }) && ModelState.IsValid)
            {
                entity.memberid = Guid.NewGuid();
                entity.isagent = false;
                entity.agentdiscount = 1;
                entity.createdate = DateTime.Now;
                entity.isenable = true;

                membersService.Create(entity);
                membersService.SaveChanges();

                return RedirectToAction("Members", new { p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;
                ViewBag.Citys = zipcodesService.Get().Select(g => g.city).Distinct();
                return View(entity);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult EditMembers(Guid entityid, int p)
        {
            ViewBag.pageNumber = p;
            ViewBag.Citys = zipcodesService.Get().Select(g => g.city).Distinct();
            Members member = membersService.GetByID(entityid);

            return View(member);
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult EditMembers(Guid memberid, int p, string password = null)
        {
            Members member = membersService.GetByID(memberid);

            if (TryUpdateModel(member, new string[] { "isenable", "name", "mobile", "email", "gender", "birthday", "zipcodeid", "address", "ismember", "memo" }) && ModelState.IsValid)
            {
                if (password != null && password != "") member.password = password;

                membersService.Update(member);
                membersService.SaveChanges();

                return RedirectToAction("Members", new { p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;
                ViewBag.Citys = zipcodesService.Get().Select(g => g.city).Distinct();
                return View(member);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult DeleteMembers(Guid entityid, int p)
        {
            Members member = membersService.GetByID(entityid);

            member.isenable = false;

            membersService.Update(member);
            membersService.SaveChanges();

            return RedirectToAction("Members", new { p = p });
        }

        [CheckSession(IsAuth = true)]
        public ActionResult Sms(int p = 1)
        {
            var data = smsService.Get().OrderByDescending(a => a.dlvtime);

            ViewBag.pageNumber = p;
            ViewBag.Sms = data.ToPagedList(pageNumber: p, pageSize: 20);

            return View();
        }

        [CheckSession(IsAuth = true)]
        public ActionResult AddSms(int p)
        {
            ViewBag.pageNumber = p;

            return View();
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult AddSms(Sms entity, int p)
        {
            if (TryUpdateModel(entity, new string[] { "title", "smbody", "dlvtime" }) && ModelState.IsValid)
            {
                entity.smsid = Guid.NewGuid();

                smsService.Create(entity);
                smsService.SaveChanges();

                return RedirectToAction("Sms", new { p = p });
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
        public ActionResult EditSms(Guid smsid, int p)
        {
            ViewBag.pageNumber = p;

            Sms sms = smsService.GetByID(smsid);

            return View(sms);
        }

        [CheckSession(IsAuth = true)]
        [HttpPost]
        public ActionResult EditSms(Guid smsid, int p, FormCollection form)
        {
            Sms sms = smsService.GetByID(smsid);

            if (TryUpdateModel(sms, new string[] { "title", "smbody", "dlvtime" }) && ModelState.IsValid)
            {
                smsService.Update(sms);
                smsService.SaveChanges();

                return RedirectToAction("Sms", new { p = p });
            }
            else
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                ViewBag.pageNumber = p;

                return View(sms);
            }
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult DeleteSms(Guid smsid, int p)
        {
            Sms sms = smsService.GetByID(smsid);

            if (sms.Smsdetails.Count() > 0)
            {
                return RedirectToAction("Sms", new { p = p });
            }

            smsService.Delete(sms);
            smsService.SaveChanges();

            return RedirectToAction("Sms", new { p = p });
        }

        [CheckSession(IsAuth = true)]
        public ActionResult Smsdetails(Guid smsid, int smsp = 1, int p = 1, int? issend = null)
        {
            Sms sms = smsService.GetByID(smsid);
            IEnumerable<Members> allmembers = membersService.Get();
            IEnumerable<Members> sddmembers = sms.Smsdetails.Join(membersService.Get(), j => j.memberid, a => a.memberid, (j, a) => a);

            IEnumerable<Smsdetails> data = sms.Smsdetails.OrderBy(o => o.section);

            if(issend != null)
            {
                data = data.Where(a => a.issend == issend);
            }

            ViewBag.Members = allmembers.Except(sddmembers);
            ViewBag.SmsID = smsid;
            ViewBag.smsp = smsp;
            ViewBag.pageNumber = p;
            ViewBag.issend = issend;
            ViewBag.Smsdetails = data.ToPagedList(pageNumber: p, pageSize: 20);

            return View();
        }

        [CheckSession(IsAuth = true)]
        [HttpGet]
        public ActionResult DeleteSmsdetails(Guid smsdetailid, int p, int smsp)
        {
            Smsdetails smsdetail = smsdetailsService.GetByID(smsdetailid);

            if (smsdetail.issend == 1)
            {
                return RedirectToAction("Smsdetails", new { smsid = smsdetail.smsid, smsp = smsp, p = p });
            }

            smsdetailsService.Delete(smsdetail);
            smsdetailsService.SaveChanges();

            return RedirectToAction("Smsdetails", new { smsid = smsdetail.smsid, smsp = smsp, p = p });
        }
    }
}