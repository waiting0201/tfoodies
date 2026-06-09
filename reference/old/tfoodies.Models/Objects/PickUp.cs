using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tfoodies.Models
{
    public class PickUp
    {
        public Guid RowId { get; set; }
        public string Barcode { get; set; }
        public string Noticenumber { get; set; }
        public DateTime Expiredate { get; set; }
        public string Product { get; set; }
        public string Warehouse { get; set; }
        public int Quantity { get; set; }
        public DateTime Pickupdate { get; set; }
    }
}
