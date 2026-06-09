using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IEventsService
    {
        IResult Create(Events entity);
        IResult Update(Events entity);
        IResult SpecificUpdate(Events entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Events entity);
        Events GetByID(object id);
        IEnumerable<Events> Get();
        void SaveChanges();
        void ExeLog();
    }
}
