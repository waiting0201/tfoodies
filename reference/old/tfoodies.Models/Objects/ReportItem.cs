using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tfoodies.Models
{
    public class ReportItem
    {
        public Guid productid { get; set; }
        public string name { get; set; }
        public int amount { get; set; }
    }
}
