using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.Entity;
using tfoodies.Models;

namespace tfoodies.Service
{
    public class OrdersService : IOrdersService
    {
        private IRepository<Orders> repository;

        public OrdersService()
        {
            repository = new GenericRepository<Orders>();
        }

        public OrdersService(tfoodiesEntities context)
        {
            repository = new GenericRepository<Orders>(context);
        }

        public IResult Create(Orders entity)
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
        public IResult Update(Orders entity)
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
        public IResult SpecificUpdate(Orders entity, string[] Includeproperties)
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
        public IResult Delete(Orders entity)
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
        public Orders GetByID(object id)
        {
            return this.repository.GetByID(id);
        }
        public IQueryable<Orders> Get()
        {
            return this.repository.Get();
        }
        public IEnumerable<Orders> GetUnpayListByMemberID(Guid memberid)
        {
            return this.Get().Where(a => (a.paystatus == 0 || a.paystatus == 5) && a.memberid == memberid).OrderByDescending(o => o.ordercode);
        }
        public IEnumerable<Orders> GetListByOrderIDs(Guid[] orderid)
        {
            return this.Get().Where(a => orderid.Contains(a.orderid));
        }
        public IEnumerable<Invoicedetails> GetInvoicedetailsByMemberID(Guid memberid)
        {
            return this.Get().Where(a => (a.paystatus == 0 || a.paystatus == 5) && a.memberid == memberid).OrderByDescending(o => o.ordercode).AsEnumerable().Select(a => new Invoicedetails {
                orderid = a.orderid,
                price = a.total + a.freight - Convert.ToInt32(a.discount),
                tax = (a.total + a.freight - Convert.ToInt32(a.discount)) - Convert.ToInt32(Math.Round((a.total + a.freight - Convert.ToInt32(a.discount)) / 1.05, MidpointRounding.AwayFromZero)),
                Orders = a
            });
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
