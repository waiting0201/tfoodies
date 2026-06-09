using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IIncomeCodesService
    {
        IResult Create(Incomecodes entity);
        IResult Update(Incomecodes entity);
        IResult SpecificUpdate(Incomecodes entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Incomecodes entity);
        Incomecodes GetByID(object id);
        IEnumerable<Incomecodes> Get();
        void SaveChanges();
        void ExeLog();
    }
}
