using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface INewsService
    {
        IResult Create(News entity);
        IResult Update(News entity);
        IResult SpecificUpdate(News entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(News entity);
        News GetByID(object id);
        IEnumerable<News> Get();
        void SaveChanges();
        void ExeLog();
    }
}
