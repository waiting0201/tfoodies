using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public class QuestionsService : IQuestionsService
    {
        private IRepository<Questions> repository;

        public QuestionsService()
        {
            repository = new GenericRepository<Questions>();
        }

        public QuestionsService(tfoodiesEntities context)
        {
            repository = new GenericRepository<Questions>(context);
        }

        public IResult Create(Questions entity)
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
        public IResult Update(Questions entity)
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
        public IResult SpecificUpdate(Questions entity, string[] Includeproperties)
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
        public IResult Delete(Questions entity)
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
        public Questions GetByID(object id)
        {
            return this.repository.GetByID(id);
        }
        public IEnumerable<Questions> Get()
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
