using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IExpendituresService
    {
        IResult Create(Expenditures entity);
        IResult Update(Expenditures entity);
        IResult SpecificUpdate(Expenditures entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Expenditures entity);
        Expenditures GetByID(object id);
        IEnumerable<Expenditures> Get();
        IEnumerable<ExpenditureItem> GetList();
        IEnumerable<ExpenditureItem> GetUnpayListBySupplierID(Guid supplierid);
        IEnumerable<ExpenditureItem> GetUnpayList();
        void SaveChanges();
        void ExeLog();
    }
}
