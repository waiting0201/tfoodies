using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tfoodies.Models
{
    [Serializable]
    public class CartItem
    {
        public Guid RowId { get; set; }
        public Guid ProductId { get; set; }
        public string Name { get; set; }
        public string Photo { get; set; }
        public string Capacity { get; set; }
        public int FixPrice { get; set; }
        public int Price { get; set; }
        public int Quantity { get; set; }
        public int Added { get; set; }
        public int SubTotal {
            get {
                return this.Price * this.Quantity;
            }
        }
    }
}