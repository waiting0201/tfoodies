using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IRefoundsService
    {
        IResult Create(Refounds entity);
        IResult Update(Refounds entity);
        IResult SpecificUpdate(Refounds entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Refounds entity);
        Refounds GetByID(object id);
        IEnumerable<Refounds> Get();
        void SaveChanges();
        void ExeLog();
    }
}
