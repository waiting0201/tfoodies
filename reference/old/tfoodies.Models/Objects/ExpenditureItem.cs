using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tfoodies.Models
{
    public class ExpenditureItem
    {
        public Guid expenditureid { get; set; }
        public string expenditurecode { get; set; }
        public Guid? supplierid { get; set; }
        public string suppliertitle { get; set; }
        public string exchangetitle { get; set; }
        public DateTime expendituredate { get; set; }
        public int totalsum { get; set; }
        public int totalpaid { get; set; }
        public int status { get; set; }
    }
}
