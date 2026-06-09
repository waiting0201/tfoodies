using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface ISmsdetailsService
    {
        IResult Create(Smsdetails entity);
        IResult Update(Smsdetails entity);
        IResult SpecificUpdate(Smsdetails entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Smsdetails entity);
        Smsdetails GetByID(object id);
        IEnumerable<Smsdetails> Get();
        void SaveChanges();
        void ExeLog();
    }
}
