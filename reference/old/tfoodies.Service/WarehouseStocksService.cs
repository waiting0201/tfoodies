using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public class WarehouseStocksService : IWarehouseStocksService
    {
        private IRepository<Warehousestocks> repository;

        public WarehouseStocksService()
        {
            repository = new GenericRepository<Warehousestocks>();
        }

        public WarehouseStocksService(tfoodiesEntities context)
        {
            repository = new GenericRepository<Warehousestocks>(context);
        }

        public IResult Create(Warehousestocks entity)
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
        public IResult Update(Warehousestocks entity)
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
        public IResult SpecificUpdate(Warehousestocks entity, string[] Includeproperties)
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
        public IResult Delete(Warehousestocks entity)
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
        public Warehousestocks GetByID(object id)
        {
            return this.repository.GetByID(id);
        }
        public IEnumerable<Warehousestocks> Get()
        {
            return this.repository.Get();
        }
        public IEnumerable<Warehousestocks> GetStockWarehouses(Guid? warehouseid, Guid productid)
        {
            return this.repository.Get().Where(a => a.warehouseid == warehouseid && a.Stocks.Purchasedetails.productid == productid && a.quantity_left != 0).OrderBy(o => o.Stocks.expiredate).ThenBy(o => o.quantity);
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
