using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface ILimsService
    {
        IResult Create(Lims entity);
        IResult Update(Lims entity);
        IResult Delete(object id);
        IResult Delete(Lims entity);
        Lims GetByID(object id);
        IEnumerable<Lims> Get();
        void SaveChanges();
    }
}
