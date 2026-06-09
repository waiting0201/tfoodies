using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IBrandPhotosService
    {
        IResult Create(Brandphotos entity);
        IResult Update(Brandphotos entity);
        IResult SpecificUpdate(Brandphotos entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Brandphotos entity);
        Brandphotos GetByID(object id);
        IEnumerable<Brandphotos> Get();
        void SaveChanges();
        void ExeLog();
    }
}
