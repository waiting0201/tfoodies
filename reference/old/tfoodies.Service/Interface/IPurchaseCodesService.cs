using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IPurchaseCodesService
    {
        IResult Create(Purchasecodes entity);
        IResult Update(Purchasecodes entity);
        IResult SpecificUpdate(Purchasecodes entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Purchasecodes entity);
        Purchasecodes GetByID(object id);
        IEnumerable<Purchasecodes> Get();
        void SaveChanges();
        void ExeLog();
    }
}
