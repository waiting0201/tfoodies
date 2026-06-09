using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IBrandsService
    {
        IResult Create(Brands entity);
        IResult Update(Brands entity);
        IResult SpecificUpdate(Brands entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Brands entity);
        Brands GetByID(object id);
        IEnumerable<Brands> Get();
        void SaveChanges();
        void ExeLog();
    }
}
