using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IZipcodesService
    {
        IResult Create(Zipcodes entity);
        IResult Update(Zipcodes entity);
        IResult SpecificUpdate(Zipcodes entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Zipcodes entity);
        Zipcodes GetByID(object id);
        IEnumerable<Zipcodes> Get();
        void SaveChanges();
        void ExeLog();
    }
}
