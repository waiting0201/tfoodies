using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using tfoodies.Models;

namespace tfoodies.Service
{
    public interface IGlobalmyb2bService
    {
        IResult Create(GlobalMyB2B entity);
        void SaveChanges();
        void ExeLog();
    }
}
