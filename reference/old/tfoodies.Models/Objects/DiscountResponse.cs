using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tfoodies.Models
{
    public class DiscountResponse
    {
        public string rscode { get; set; }
        public string rsmessage { get; set; }
        public int discount { get; set; }
        public Guid discountid { get; set; }
        public string amountprice { get; set; }
    }
}
