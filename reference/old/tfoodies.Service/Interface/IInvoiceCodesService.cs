using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IInvoiceCodesService
    {
        IResult Create(Invoicecodes entity);
        IResult Update(Invoicecodes entity);
        IResult SpecificUpdate(Invoicecodes entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Invoicecodes entity);
        Invoicecodes GetByID(object id);
        IEnumerable<Invoicecodes> Get();
        void SaveChanges();
        void ExeLog();
    }
}
