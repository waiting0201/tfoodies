using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IRecipesService
    {
        IResult Create(Recipes entity);
        IResult Update(Recipes entity);
        IResult SpecificUpdate(Recipes entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Recipes entity);
        Recipes GetByID(object id);
        IEnumerable<Recipes> Get();
        void SaveChanges();
        void ExeLog();
    }
}
