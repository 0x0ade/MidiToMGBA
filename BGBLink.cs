using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MidiToBGB {
    public class BGBLink : IDisposable {

        #region Command constants

        public const byte CmdVersion = 1;
        public const byte CmdJoypad = 101;
        public const byte CmdSync1 = 104;
        public const byte CmdSync2 = 105;
        public const byte CmdSync3 = 106;
        public const byte CmdStatus = 108;
        public const byte CmdWantDisconnect = 109;

        #endregion

        #region Status constants

        public const byte StatusRunning = 0x01;
        public const byte StatusPaused = 0x02;
        public const byte StatusSupportReconnect = 0x04;

        #endregion

        public BGBLinkClient Client;

        private bool Handshaked = false;

        public BGBLink() {
            Client = new BGBLinkClient();
            Client.OnReceive += HandlePacket;
        }

        public void Connect(string hostname = "127.0.0.1", int port = 8765) {
            Client.Connect(hostname, port);
            Client.Send(new BGBPacket(CmdVersion, 1, 4, 0, 0));
        }

        public void Dispose() {
            Client?.Dispose();
        }
        
        public void HandlePacket(BGBPacket packet) {
            if (!Handshaked) {
                // Version - must be 1, 4, 0, 0
                Console.WriteLine($"Received version: {packet}");
                if (packet.B1 != CmdVersion ||
                    packet.B2 != 1 ||
                    packet.B3 != 4 ||
                    packet.B4 != 0 ||
                    packet.I1 != 0)
                    throw new NotSupportedException($"Unsupported BGB version: {packet}");
                Handshaked = true;
                return;
            }

            switch (packet.B1) {
                case CmdJoypad:
                    Console.WriteLine($"Received joypad: {packet}");
                    break;

                case CmdSync1:
                    Console.WriteLine($"Received sync1: {packet}");
                    break;

                case CmdSync2:
                    Console.WriteLine($"Received sync2: {packet}");
                    break;

                case CmdSync3:
                    Console.WriteLine($"Received sync3: {packet}");
                    break;

                case CmdStatus:
                    Console.WriteLine($"Received status: {packet}");
                    if ((packet.B2 & StatusRunning) == StatusRunning)
                        Console.Write("running ");
                    if ((packet.B2 & StatusPaused) == StatusPaused)
                        Console.Write("paused ");
                    if ((packet.B2 & StatusSupportReconnect) == StatusSupportReconnect)
                        Console.Write("supportreconnect ");
                    Console.WriteLine();
                    break;

                case CmdWantDisconnect:
                    Console.WriteLine($"Received wantdisconnect: {packet}");
                    break;

                default:
                    Console.WriteLine($"Received unknown: {packet}");
                    break;
            }

        }

    }
}
