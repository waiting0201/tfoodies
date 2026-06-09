using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IRecipeStepsService
    {
        IResult Create(Recipesteps entity);
        IResult Update(Recipesteps entity);
        IResult SpecificUpdate(Recipesteps entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Recipesteps entity);
        Recipesteps GetByID(object id);
        IEnumerable<Recipesteps> Get();
        void SaveChanges();
        void ExeLog();
    }
}
