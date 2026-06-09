using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IAdminLimsService
    {
        IResult Create(AdminLims entity);
        IResult Update(AdminLims entity);
        IResult Delete(object id);
        IResult Delete(AdminLims entity);
        AdminLims GetByID(object id);
        IEnumerable<AdminLims> Get();
        void SaveChanges();
    }
}
