using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tfoodies.Models
{
    public class ViewLog
    {
        public string sessionid { get; set; }
        public string referrersessionid { get; set; }
        public string memberid { get; set; }
        public string country { get; set; }
        public string city { get; set; }
        public string browser { get; set; }
        public string device { get; set; }
        public string platform { get; set; }
        public string url { get; set; }
        public string referrerdns { get; set; }
        public string referrerurl { get; set; }
        public DateTime createdate { get; set; }
    }
}
