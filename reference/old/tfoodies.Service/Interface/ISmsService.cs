using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface ISmsService
    {
        IResult Create(Sms entity);
        IResult Update(Sms entity);
        IResult SpecificUpdate(Sms entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Sms entity);
        Sms GetByID(object id);
        IEnumerable<Sms> Get();
        void SaveChanges();
        void ExeLog();
    }
}
