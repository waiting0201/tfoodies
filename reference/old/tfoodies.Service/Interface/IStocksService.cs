using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IStocksService
    {
        IResult Create(Stocks entity);
        IResult Update(Stocks entity);
        IResult SpecificUpdate(Stocks entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Stocks entity);
        Stocks GetByID(object id);
        IEnumerable<Stocks> Get();
        void SaveChanges();
        void ExeLog();
    }
}
