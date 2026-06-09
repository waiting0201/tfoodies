using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IRecipeSeasoningsService
    {
        IResult Create(Recipeseasonings entity);
        IResult Update(Recipeseasonings entity);
        IResult SpecificUpdate(Recipeseasonings entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Recipeseasonings entity);
        Recipeseasonings GetByID(object id);
        IEnumerable<Recipeseasonings> Get();
        void SaveChanges();
        void ExeLog();
    }
}
