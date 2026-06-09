using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public class ExpendituresService : IExpendituresService
    {
        private IRepository<Expenditures> repository;

        public ExpendituresService()
        {
            repository = new GenericRepository<Expenditures>();
        }

        public ExpendituresService(tfoodiesEntities context)
        {
            repository = new GenericRepository<Expenditures>(context);
        }

        public IResult Create(Expenditures entity)
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
        public IResult Update(Expenditures entity)
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
        public IResult SpecificUpdate(Expenditures entity, string[] Includeproperties)
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
        public IResult Delete(Expenditures entity)
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
        public Expenditures GetByID(object id)
        {
            return this.repository.GetByID(id);
        }
        public IEnumerable<Expenditures> Get()
        {
            return this.repository.Get();
        }
        public IEnumerable<ExpenditureItem> GetList()
        {
            return this.Get().Select(a => new ExpenditureItem
            {
                expenditureid = a.expenditureid,
                expenditurecode = a.expenditurecode,
                supplierid = a.supplierid,
                suppliertitle = a.Suppliers.title,
                exchangetitle = a.Purchases.Exchanges.title,
                expendituredate = a.expendituredate,
                totalsum = a.Expendituredetails.Sum(s => s.price),
                totalpaid = a.Outcomes.Sum(s => s.amount),
                status = a.status
            }).OrderByDescending(o => o.expenditurecode);
        }
        public IEnumerable<ExpenditureItem> GetUnpayListBySupplierID(Guid supplierid)
        {
            return this.Get().Select(a => new ExpenditureItem
            {
                expenditureid = a.expenditureid,
                expenditurecode = a.expenditurecode,
                supplierid = a.supplierid,
                suppliertitle = a.Suppliers.title,
                exchangetitle = a.Purchases.Exchanges.title,
                expendituredate = a.expendituredate,
                totalsum = a.Expendituredetails.Sum(s => s.price),
                totalpaid = a.Outcomes.Sum(s => s.amount),
                status = a.status
            }).Where(a => a.status < 2 && a.supplierid == supplierid).OrderByDescending(o => o.expenditurecode);
        }
        public IEnumerable<ExpenditureItem> GetUnpayList()
        {
            return this.Get().Where(a => a.status < 2).Select(a => new ExpenditureItem
            {
                expenditureid = a.expenditureid,
                expenditurecode = a.expenditurecode,
                supplierid = a.supplierid,
                suppliertitle = a.Suppliers.title,
                exchangetitle = a.Purchases.Exchanges.title,
                expendituredate = a.expendituredate,
                totalsum = a.Expendituredetails.Sum(s => s.price),
                totalpaid = a.Outcomes.Sum(s => s.amount),
                status = a.status
            }).OrderByDescending(o => o.expenditurecode);
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
