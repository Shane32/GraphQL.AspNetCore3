using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chat.Schema
{
    public class Event
    {
        public EventType Type { get; set; }
        public Message? Message { get; set; }
    }
}
