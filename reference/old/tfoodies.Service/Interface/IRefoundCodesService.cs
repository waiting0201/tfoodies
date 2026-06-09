using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IRefoundCodesService
    {
        IResult Create(Refoundcodes entity);
        IResult Update(Refoundcodes entity);
        IResult SpecificUpdate(Refoundcodes entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Refoundcodes entity);
        Refoundcodes GetByID(object id);
        IEnumerable<Refoundcodes> Get();
        void SaveChanges();
        void ExeLog();
    }
}
