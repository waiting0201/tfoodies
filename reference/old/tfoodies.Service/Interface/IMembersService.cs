using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IMembersService
    {
        IResult Create(Members entity);
        IResult Update(Members entity);
        IResult SpecificUpdate(Members entity, string[] Includeproperties);
        IResult Delete(object id);
        IResult Delete(Members entity);
        Members GetByID(object id);
        IEnumerable<Members> Get();
        bool ValidateUser(string mobile, string password);
        void SaveChanges();
        void ExeLog();
    }
}
