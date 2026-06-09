using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IInvoicesService
    {
        IResult Create(Invoices entity);
        IResult Update(Invoices entity);
        IResult SpecificUpdate(Invoices entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Invoices entity);
        Invoices GetByID(object id);
        IEnumerable<Invoices> Get();
        IEnumerable<Invoices> GetUnpayListByMemberID(Guid memberid);
        IEnumerable<Invoices> GetListByInvoiceIDs(Guid[] invoiceid);
        IEnumerable<Invoices> GetListByIncomeID(Guid incomeid);
        void SaveChanges();
        void ExeLog();
    }
}
