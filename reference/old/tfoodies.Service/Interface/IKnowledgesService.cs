using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IKnowledgesService
    {
        IResult Create(Knowledges entity);
        IResult Update(Knowledges entity);
        IResult SpecificUpdate(Knowledges entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Knowledges entity);
        Knowledges GetByID(object id);
        IEnumerable<Knowledges> Get();
        void SaveChanges();
        void ExeLog();
    }
}
