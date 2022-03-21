using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chat.Schema
{
    public class Message
    {
        [Id]
        public int Id { get; set; }

        [Name("Message")]
        public string Value { get; set; }

        public string From { get; set; }

        public DateTime Sent { get; set; }
    }
}
