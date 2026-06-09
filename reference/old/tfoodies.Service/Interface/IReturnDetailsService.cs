using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IReturnDetailsService
    {
        IResult Create(Returndetails entity);
        IResult Update(Returndetails entity);
        IResult SpecificUpdate(Returndetails entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Returndetails entity);
        Returndetails GetByID(object id);
        IEnumerable<Returndetails> Get();
        void SaveChanges();
        void ExeLog();
    }
}
