using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IReturnsService
    {
        IResult Create(Returns entity);
        IResult Update(Returns entity);
        IResult SpecificUpdate(Returns entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Returns entity);
        Returns GetByID(object id);
        IEnumerable<Returns> Get();
        IEnumerable<Returns> GetUnpayListByMemberID(Guid memberid);
        void SaveChanges();
        void ExeLog();
    }
}
