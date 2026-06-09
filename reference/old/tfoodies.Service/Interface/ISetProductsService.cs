using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface ISetProductsService
    {
        IResult Create(Setproducts entity);
        IResult Update(Setproducts entity);
        IResult SpecificUpdate(Setproducts entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Setproducts entity);
        Setproducts GetByID(object id);
        IEnumerable<Setproducts> Get();
        void SaveChanges();
        void ExeLog();
    }
}
