using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tfoodies.Models
{
    public class CheckInventoryResponse
    {
        public string code { get; set; }
        public Guid productid { get; set; }
        public string message { get; set; }
    }
}
