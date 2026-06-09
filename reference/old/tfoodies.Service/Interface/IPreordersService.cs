using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IPreordersService
    {
        IResult Create(Preorders entity);
        IResult Update(Preorders entity);
        IResult SpecificUpdate(Preorders entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Preorders entity);
        Preorders GetByID(object id);
        IEnumerable<Preorders> Get();
        void SaveChanges();
        void ExeLog();
    }
}
