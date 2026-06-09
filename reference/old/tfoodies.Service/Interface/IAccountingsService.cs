using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IAccountingsService
    {
        IResult Create(Accountings entity);
        IResult Update(Accountings entity);
        IResult SpecificUpdate(Accountings entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Accountings entity);
        Accountings GetByID(object id);
        IEnumerable<Accountings> Get();
        void SaveChanges();
        void ExeLog();
    }
}
