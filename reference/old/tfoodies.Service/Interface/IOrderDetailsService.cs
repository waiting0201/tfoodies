using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IOrderDetailsService
    {
        IResult Create(Orderdetails entity);
        IResult Update(Orderdetails entity);
        IResult SpecificUpdate(Orderdetails entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Orderdetails entity);
        Orderdetails GetByID(object id);
        IEnumerable<Orderdetails> Get();
        void SaveChanges();
        void ExeLog();
    }
}
