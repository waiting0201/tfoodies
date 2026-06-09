using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IRecipeIngredientsService
    {
        IResult Create(Recipeingredients entity);
        IResult Update(Recipeingredients entity);
        IResult SpecificUpdate(Recipeingredients entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Recipeingredients entity);
        Recipeingredients GetByID(object id);
        IEnumerable<Recipeingredients> Get();
        void SaveChanges();
        void ExeLog();
    }
}
