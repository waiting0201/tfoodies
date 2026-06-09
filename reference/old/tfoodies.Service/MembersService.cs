using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using tfoodies.Models;

namespace tfoodies.Service
{
    public class MembersService : IMembersService
    {
        private IRepository<Members> repository;

        public MembersService()
        {
            repository = new GenericRepository<Members>();
        }

        public MembersService(tfoodiesEntities context)
        {
            repository = new GenericRepository<Members>(context);
        }

        public IResult Create(Members entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException();
            }

            IResult result = new Result(false);
            try
            {
                this.repository.Insert(entity);
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Exception = ex;
            }
            return result;
        }
        public IResult Update(Members entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException();
            }

            IResult result = new Result(false);
            try
            {
                this.repository.Update(entity);
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Exception = ex;
            }
            return result;
        }
        public IResult SpecificUpdate(Members entity, string[] Includeproperties)
        {
            if (entity == null)
            {
                throw new ArgumentNullException();
            }

            IResult result = new Result(false);
            try
            {
                this.repository.SpecificUpdate(entity, Includeproperties);
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Exception = ex;
            }
            return result;
        }
        public IResult Delete(object id)
        {
            IResult result = new Result(false);

            var entity = this.GetByID(id);

            if (entity == null)
            {
                result.Message = "找不到資料";
            }

            try
            {
                this.repository.Delete(entity);
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Exception = ex;
            }
            return result;
        }
        public IResult Delete(Members entity)
        {
            IResult result = new Result(false);

            if (entity == null)
            {
                result.Message = "找不到資料";
            }

            try
            {
                this.repository.Delete(entity);
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Exception = ex;
            }
            return result;
        }
        public Members GetByID(object id)
        {
            return this.repository.GetByID(id);
        }
        public IEnumerable<Members> Get()
        {
            return this.repository.Get();
        }

        public bool ValidateUser(string mobile, string password)
        {
            //if (mobile == "itadmin" && password == "QQQQQ")
            //{
            //    HttpContext.Current.Session.Add("IsLogin", true);
            //    HttpContext.Current.Session.Add("Username", "itadmin");

            //    return true;
            //}

            Members member = this.Get().Where(a => a.mobile == mobile && a.isenable == true && a.ismember == 1).FirstOrDefault();
            if (member == null)
                return false;

            if (member.password != password)
                return false;

            HttpContext.Current.Session.Add("IsLogin", true);
            HttpContext.Current.Session.Add("Username", member.name);
            HttpContext.Current.Session.Add("MemberID", member.memberid);
            HttpContext.Current.Session.Add("MemberEmail", member.email);
            HttpContext.Current.Session.Add("MemberMobile", member.mobile);

            return true;
        }

        public void SaveChanges()
        {
            this.repository.SaveChanges();
        }
        public void ExeLog()
        {
            this.repository.ExeLog();
        }
    }
}
