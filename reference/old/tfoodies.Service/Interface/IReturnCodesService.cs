using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IReturnCodesService
    {
        IResult Create(Returncodes entity);
        IResult Update(Returncodes entity);
        IResult SpecificUpdate(Returncodes entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Returncodes entity);
        Returncodes GetByID(object id);
        IEnumerable<Returncodes> Get();
        void SaveChanges();
        void ExeLog();
    }
}
