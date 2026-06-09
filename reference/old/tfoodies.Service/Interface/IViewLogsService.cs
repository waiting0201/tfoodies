using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IViewLogsService
    {
        IResult Create(Viewlogs entity);
        IResult Update(Viewlogs entity);
        IResult SpecificUpdate(Viewlogs entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Viewlogs entity);
        Viewlogs GetByID(object id);
        IEnumerable<Viewlogs> Get();
        void SaveChanges();
        void ExeLog();
    }
}
