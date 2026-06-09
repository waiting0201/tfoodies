using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IQuestionsService
    {
        IResult Create(Questions entity);
        IResult Update(Questions entity);
        IResult SpecificUpdate(Questions entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Questions entity);
        Questions GetByID(object id);
        IEnumerable<Questions> Get();
        void SaveChanges();
        void ExeLog();
    }
}
