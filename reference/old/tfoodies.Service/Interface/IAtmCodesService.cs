using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IAtmCodesService
    {
        IResult Create(Atmcodes entity);
        IResult Update(Atmcodes entity);
        IResult SpecificUpdate(Atmcodes entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Atmcodes entity);
        Atmcodes GetByID(object id);
        IEnumerable<Atmcodes> Get();
        void SaveChanges();
        void ExeLog();
    }
}
