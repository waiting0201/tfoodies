using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IOrdersService
    {
        IResult Create(Orders entity);
        IResult Update(Orders entity);
        IResult SpecificUpdate(Orders entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Orders entity);
        Orders GetByID(object id);
        IQueryable<Orders> Get();
        IEnumerable<Orders> GetUnpayListByMemberID(Guid memberid);
        IEnumerable<Orders> GetListByOrderIDs(Guid[] orderid);
        IEnumerable<Invoicedetails> GetInvoicedetailsByMemberID(Guid memberid);
        void SaveChanges();
        void ExeLog();
    }
}
