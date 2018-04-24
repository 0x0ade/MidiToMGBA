using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MidiToBGB {
    public enum BGBCommand : byte {
        Version = 1,
        Joypad = 101,
        Sync1 = 104,
        Sync2 = 105,
        Sync3 = 106,
        Status = 108,
        WantDisconnect = 109
    }
}
