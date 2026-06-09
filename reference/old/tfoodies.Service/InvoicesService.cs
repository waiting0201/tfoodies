using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public class InvoicesService : IInvoicesService
    {
        private IRepository<Invoices> repository;

        public InvoicesService()
        {
            repository = new GenericRepository<Invoices>();
        }

        public InvoicesService(tfoodiesEntities context)
        {
            repository = new GenericRepository<Invoices>(context);
        }

        public IResult Create(Invoices entity)
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
        public IResult Update(Invoices entity)
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
        public IResult SpecificUpdate(Invoices entity, string[] Includeproperties)
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
        public IResult Delete(Invoices entity)
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
        public Invoices GetByID(object id)
        {
            return this.repository.GetByID(id);
        }
        public IEnumerable<Invoices> Get()
        {
            return this.repository.Get();
        }
        public IEnumerable<Invoices> GetUnpayListByMemberID(Guid memberid)
        {
            return this.Get().Where(a => (a.incomeid == null) && a.memberid == memberid).OrderByDescending(o => o.invoicecode);
        }
        public IEnumerable<Invoices> GetListByInvoiceIDs(Guid[] invoiceid)
        {
            return this.Get().Where(a => invoiceid.Contains(a.invoiceid));
        }
        public IEnumerable<Invoices> GetListByIncomeID(Guid incomeid)
        {
            return this.Get().Where(a => a.incomeid == incomeid).OrderByDescending(o => o.invoicecode);
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
