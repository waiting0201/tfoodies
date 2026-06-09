using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IOrderCodesService
    {
        IResult Create(Ordercodes entity);
        IResult Update(Ordercodes entity);
        IResult SpecificUpdate(Ordercodes entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Ordercodes entity);
        Ordercodes GetByID(object id);
        IEnumerable<Ordercodes> Get();
        void SaveChanges();
        void ExeLog();
    }
}
