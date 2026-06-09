using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IEventPhotosService
    {
        IResult Create(Eventphotos entity);
        IResult Update(Eventphotos entity);
        IResult SpecificUpdate(Eventphotos entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Eventphotos entity);
        Eventphotos GetByID(object id);
        IEnumerable<Eventphotos> Get();
        void SaveChanges();
        void ExeLog();
    }
}
