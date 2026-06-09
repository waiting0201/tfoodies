using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IIssuesService
    {
        IResult Create(Issues entity);
        IResult Update(Issues entity);
        IResult SpecificUpdate(Issues entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Issues entity);
        Issues GetByID(object id);
        IEnumerable<Issues> Get();
        void SaveChanges();
        void ExeLog();
    }
}
