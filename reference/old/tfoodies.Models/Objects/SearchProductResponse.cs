using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tfoodies.Models
{
    public class SearchProductResponse
    {
        public string code { get; set; }
        public string message { get; set; }
        public IEnumerable<Products> datas { get; set; }
        public PagingResponse paging { get; set; }
    }
}
