using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public class AdminsService : BaseService<Admins>
    {
        public AdminsService()
        {
            repository = new GenericRepository<Admins>();
        }

        public AdminsService(tfoodiesEntities context)
        {
            repository = new GenericRepository<Admins>(context);
        }
    }
}
