using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IBlogsService
    {
        IResult Create(Blogs entity);
        IResult Update(Blogs entity);
        IResult SpecificUpdate(Blogs entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Blogs entity);
        Blogs GetByID(object id);
        IEnumerable<Blogs> Get();
        void SaveChanges();
        void ExeLog();
    }
}
