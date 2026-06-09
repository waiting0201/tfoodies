using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IWarehousesService
    {
        IResult Create(Warehouses entity);
        IResult Update(Warehouses entity);
        IResult SpecificUpdate(Warehouses entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Warehouses entity);
        Warehouses GetByID(object id);
        IEnumerable<Warehouses> Get();
        void SaveChanges();
        void ExeLog();
    }
}
