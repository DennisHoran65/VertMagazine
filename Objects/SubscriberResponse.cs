using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MagazineSubscriptions.Objects
{
    public class SubscriberResponse
    {
        public Subscriber[] data { get; set; }
        public bool success { get; set; }
        public string token { get; set; }
    }
}
