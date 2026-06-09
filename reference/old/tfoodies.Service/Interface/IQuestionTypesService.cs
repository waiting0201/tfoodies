using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IQuestionTypesService
    {
        IResult Create(Questiontypes entity);
        IResult Update(Questiontypes entity);
        IResult SpecificUpdate(Questiontypes entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Questiontypes entity);
        Questiontypes GetByID(object id);
        IEnumerable<Questiontypes> Get();
        void SaveChanges();
        void ExeLog();
    }
}
