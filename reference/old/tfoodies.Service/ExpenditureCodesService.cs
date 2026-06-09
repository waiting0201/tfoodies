using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public class ExpenditureCodesService : IExpenditureCodesService
    {
        private IRepository<Expenditurecodes> repository;

        public ExpenditureCodesService()
        {
            repository = new GenericRepository<Expenditurecodes>();
        }

        public ExpenditureCodesService(tfoodiesEntities context)
        {
            repository = new GenericRepository<Expenditurecodes>(context);
        }

        public IResult Create(Expenditurecodes entity)
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
        public IResult Update(Expenditurecodes entity)
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
        public IResult SpecificUpdate(Expenditurecodes entity, string[] Includeproperties)
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
        public IResult Delete(Expenditurecodes entity)
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
        public Expenditurecodes GetByID(object id)
        {
            return this.repository.GetByID(id);
        }
        public IEnumerable<Expenditurecodes> Get()
        {
            return this.repository.Get();
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
