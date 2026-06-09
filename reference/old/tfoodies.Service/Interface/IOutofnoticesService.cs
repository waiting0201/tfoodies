using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IOutofnoticesService
    {
        IResult Create(Outofnotices entity);
        IResult Update(Outofnotices entity);
        IResult SpecificUpdate(Outofnotices entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Outofnotices entity);
        Outofnotices GetByID(object id);
        IEnumerable<Outofnotices> Get();
        void SaveChanges();
        void ExeLog();
    }
}
