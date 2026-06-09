using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IDiscountsService
    {
        IResult Create(Discounts entity);
        IResult Update(Discounts entity);
        IResult SpecificUpdate(Discounts entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Discounts entity);
        Discounts GetByID(object id);
        IEnumerable<Discounts> Get();
        void SaveChanges();
        void ExeLog();
    }
}
