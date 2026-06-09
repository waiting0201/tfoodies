using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IOrderDetailStocksService
    {
        IResult Create(Orderdetailstocks entity);
        IResult Update(Orderdetailstocks entity);
        IResult SpecificUpdate(Orderdetailstocks entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Orderdetailstocks entity);
        Orderdetailstocks GetByID(object id);
        IEnumerable<Orderdetailstocks> Get();
        void SaveChanges();
        void ExeLog();
    }
}
