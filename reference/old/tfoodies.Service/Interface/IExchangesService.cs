using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IExchangesService
    {
        IResult Create(Exchanges entity);
        IResult Update(Exchanges entity);
        IResult SpecificUpdate(Exchanges entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Exchanges entity);
        Exchanges GetByID(object id);
        IEnumerable<Exchanges> Get();
        void SaveChanges();
        void ExeLog();
    }
}
