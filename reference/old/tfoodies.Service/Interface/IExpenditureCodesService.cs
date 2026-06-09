using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IExpenditureCodesService
    {
        IResult Create(Expenditurecodes entity);
        IResult Update(Expenditurecodes entity);
        IResult SpecificUpdate(Expenditurecodes entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Expenditurecodes entity);
        Expenditurecodes GetByID(object id);
        IEnumerable<Expenditurecodes> Get();
        void SaveChanges();
        void ExeLog();
    }
}
