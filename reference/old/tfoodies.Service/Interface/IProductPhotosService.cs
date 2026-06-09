using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IProductPhotosService
    {
        IResult Create(Productphotos entity);
        IResult Update(Productphotos entity);
        IResult SpecificUpdate(Productphotos entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Productphotos entity);
        Productphotos GetByID(object id);
        IEnumerable<Productphotos> Get();
        void SaveChanges();
        void ExeLog();
    }
}
