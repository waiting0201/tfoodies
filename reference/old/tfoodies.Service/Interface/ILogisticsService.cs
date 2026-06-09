using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface ILogisticsService
    {
        IResult Create(Logistics entity);
        IResult Update(Logistics entity);
        IResult SpecificUpdate(Logistics entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Logistics entity);
        Logistics GetByID(object id);
        IEnumerable<Logistics> Get();
        void SaveChanges();
        void ExeLog();
    }
}
