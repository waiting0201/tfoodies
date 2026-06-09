using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IProductTypesService
    {
        IResult Create(Producttypes entity);
        IResult Update(Producttypes entity);
        IResult SpecificUpdate(Producttypes entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Producttypes entity);
        Producttypes GetByID(object id);
        IEnumerable<Producttypes> Get();
        void SaveChanges();
        void ExeLog();
    }
}
