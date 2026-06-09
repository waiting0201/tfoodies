using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IWarehouseStocksService
    {
        IResult Create(Warehousestocks entity);
        IResult Update(Warehousestocks entity);
        IResult SpecificUpdate(Warehousestocks entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Warehousestocks entity);
        Warehousestocks GetByID(object id);
        IEnumerable<Warehousestocks> Get();
        IEnumerable<Warehousestocks> GetStockWarehouses(Guid? warehouseid, Guid productid);
        void SaveChanges();
        void ExeLog();
    }
}