using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chat.Schema
{
    public enum EventType
    {
        NewMessage,
        DeleteMessage,
        ClearMessages,
    }
}
