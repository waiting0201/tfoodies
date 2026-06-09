using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface ITagsService
    {
        IResult Create(Tags entity);
        IResult Update(Tags entity);
        IResult SpecificUpdate(Tags entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Tags entity);
        Tags GetByID(object id);
        IEnumerable<Tags> Get();
        void SaveChanges();
        void ExeLog();
    }
}
