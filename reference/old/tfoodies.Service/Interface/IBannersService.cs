using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IBannersService
    {
        IResult Create(Banners entity);
        IResult Update(Banners entity);
        IResult SpecificUpdate(Banners entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Banners entity);
        Banners GetByID(object id);
        IEnumerable<Banners> Get();
        void SaveChanges();
        void ExeLog();
    }
}
