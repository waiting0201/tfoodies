using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IOutcomeCodesService
    {
        IResult Create(Outcomecodes entity);
        IResult Update(Outcomecodes entity);
        IResult SpecificUpdate(Outcomecodes entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Outcomecodes entity);
        Outcomecodes GetByID(object id);
        IEnumerable<Outcomecodes> Get();
        void SaveChanges();
        void ExeLog();
    }
}
