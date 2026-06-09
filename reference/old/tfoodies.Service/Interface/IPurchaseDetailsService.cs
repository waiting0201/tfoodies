using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IPurchaseDetailsService
    {
        IResult Create(Purchasedetails entity);
        IResult Update(Purchasedetails entity);
        IResult SpecificUpdate(Purchasedetails entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Purchasedetails entity);
        Purchasedetails GetByID(object id);
        IEnumerable<Purchasedetails> Get();
        void SaveChanges();
        void ExeLog();
    }
}
