using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using PagedList;

using tfoodiesBackend.Filters;
using tfoodiesBackend.Commons;
using tfoodies.Libs;
using tfoodies.Models;
using tfoodies.Service;
using System.Data.Entity.Validation;

namespace tfoodiesBackend.Controllers
{
    public class StatementMsController : BaseController
    {
        public StatementMsController()
        {

        }

        [CheckSession(IsAuth = true)]
        public ActionResult Incomestatements()
        {
            return View();
        }
    }
}