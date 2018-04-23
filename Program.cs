using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace MidiToBGB {
    class Program {

        static void Main(string[] args) {

            using (BGBLink link = new BGBLink()) {
                link.Connect();
                while (link.Client.Client.Connected)
                    Thread.Sleep(0);
            }

        }

    }
}
