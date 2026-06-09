using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IOutcomesService
    {
        IResult Create(Outcomes entity);
        IResult Update(Outcomes entity);
        IResult SpecificUpdate(Outcomes entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Outcomes entity);
        Outcomes GetByID(object id);
        IEnumerable<Outcomes> Get();
        void SaveChanges();
        void ExeLog();
    }
}
