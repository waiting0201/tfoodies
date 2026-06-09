using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IInvoiceDetailsService
    {
        IResult Create(Invoicedetails entity);
        IResult Update(Invoicedetails entity);
        IResult SpecificUpdate(Invoicedetails entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Invoicedetails entity);
        Invoicedetails GetByID(object id);
        IEnumerable<Invoicedetails> Get();
        void SaveChanges();
        void ExeLog();
    }
}
