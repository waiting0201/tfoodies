using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tfoodies.Models
{
    public class PagingResponse
    {
        public int total { get; set; }
        public int returned { get; set; }
        public int skip { get; set; }
        public int take { get; set; }

    }
}
