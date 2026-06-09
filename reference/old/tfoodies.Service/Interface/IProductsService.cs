using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IProductsService
    {
        IResult Create(Products entity);
        IResult Update(Products entity);
        IResult SpecificUpdate(Products entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Products entity);
        Products GetByID(object id);
        IEnumerable<Products> Get();
        void SaveChanges();
        void ExeLog();
    }
}
