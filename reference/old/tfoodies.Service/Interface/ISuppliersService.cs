using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface ISuppliersService
    {
        IResult Create(Suppliers entity);
        IResult Update(Suppliers entity);
        IResult SpecificUpdate(Suppliers entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Suppliers entity);
        Suppliers GetByID(object id);
        IEnumerable<Suppliers> Get();
        void SaveChanges();
    }
}
