using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IPurchasesService
    {
        IResult Create(Purchases entity);
        IResult Update(Purchases entity);
        IResult SpecificUpdate(Purchases entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Purchases entity);
        Purchases GetByID(object id);
        IEnumerable<Purchases> Get();
        void SaveChanges();
        void ExeLog();
    }
}
