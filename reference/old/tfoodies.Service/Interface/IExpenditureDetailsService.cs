using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IExpenditureDetailsService
    {
        IResult Create(Expendituredetails entity);
        IResult Update(Expendituredetails entity);
        IResult SpecificUpdate(Expendituredetails entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Expendituredetails entity);
        Expendituredetails GetByID(object id);
        IEnumerable<Expendituredetails> Get();
        void SaveChanges();
        void ExeLog();
    }
}
