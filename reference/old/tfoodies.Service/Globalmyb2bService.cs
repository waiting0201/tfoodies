using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public class Globalmyb2bService : IGlobalmyb2bService
    {
        private IRepository<GlobalMyB2B> repository;

        public Globalmyb2bService()
        {
            repository = new GenericRepository<GlobalMyB2B>();
        }

        public Globalmyb2bService(tfoodiesEntities context)
        {
            repository = new GenericRepository<GlobalMyB2B>(context);
        }

        public IResult Create(GlobalMyB2B entity)
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
