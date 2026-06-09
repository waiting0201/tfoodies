using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IDeclarationsService
    {
        IResult Create(Declarations entity);
        IResult Update(Declarations entity);
        IResult SpecificUpdate(Declarations entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Declarations entity);
        Declarations GetByID(object id);
        IEnumerable<Declarations> Get();
        void SaveChanges();
        void ExeLog();
    }
}
