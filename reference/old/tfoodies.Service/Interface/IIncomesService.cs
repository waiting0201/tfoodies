using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IIncomesService
    {
        IResult Create(Incomes entity);
        IResult Update(Incomes entity);
        IResult SpecificUpdate(Incomes entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Incomes entity);
        Incomes GetByID(object id);
        IEnumerable<Incomes> Get();
        void SaveChanges();
        void ExeLog();
    }
}
