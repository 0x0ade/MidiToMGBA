using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MidiToBGB {
    public class BGBLink : IDisposable {

        #region Status constants

        public const byte StatusRunning = 0x01;
        public const byte StatusPaused = 0x02;
        public const byte StatusSupportReconnect = 0x04;

        #endregion

        public BGBLinkClient Client;

        public int Time {
            get {
                // TODO: Fix timing.
                return 0x7FFFFFFF & ((int) (Clock.ElapsedTicks * 0.7649));
            }
        }

        private bool Handshaked = false;

        private Stopwatch Clock;
        private int TimeBGB;

        public event Action<byte> OnReceive;

        public BGBLink(string hostname = "127.0.0.1", int port = 8765) {
            Clock = new Stopwatch();
            Clock.Start();
            Client = new BGBLinkClient();
            Client.OnReceive += HandlePacket;
            Client.Connect(hostname, port);
            Client.Send(new BGBPacket(BGBCommand.Version, 1, 4, 0, 0));
            Client.Send(new BGBPacket(BGBCommand.Status, 0x01 | 0x04, 0, 0, 0));
        }

        public void Dispose() {
            Client?.Dispose();
            Clock?.Stop();
        }
        
        public void HandlePacket(BGBPacket packet) {
            if (!Handshaked) {
                // Version - must be 1, 4, 0, 0
                Console.WriteLine($"[BGB] [RX] [HANDSHAKE] {packet}");
                if (packet.Command != BGBCommand.Version ||
                    packet.B2 != 1 ||
                    packet.B3 != 4 ||
                    packet.B4 != 0 ||
                    packet.I1 != 0)
                    throw new NotSupportedException($"Unsupported BGB version: {packet}");
                Handshaked = true;
                return;
            }

            switch (packet.Command) {
                case BGBCommand.Joypad:
                    Console.WriteLine($"[BGB] [RX] [JOYPAD] {packet.B2 & 0x07} {((packet.B2 & 0x08) == 0x08 ? "+" : "-")}");
                    break;

                case BGBCommand.Sync1:
                    Console.WriteLine($"[BGB] [RX] [SYNC1] 0x{packet.B2.ToString("X2")} 0x{packet.B3.ToString("X2")} {packet.I1}");
                    break;

                case BGBCommand.Sync2:
                    Console.WriteLine($"[BGB] [RX] [SYNC2] 0x{packet.B2.ToString("X2")}");
                    OnReceive?.Invoke(packet.B2);
                    break;

                case BGBCommand.Sync3:
                    if (packet.B2 == 0x00) {
                        int time = Time;
                        // Console.WriteLine($"[BGB] [TIME] BGB: {packet.I1}; self: {time}; self - BGB: {time - packet.I1}");
                        SendTime();
                    }
                    break;

                case BGBCommand.Status:
                    Console.Write("[BGB] [RX] [STATUS] ");
                    if ((packet.B2 & StatusRunning) == StatusRunning)
                        Console.Write("running ");
                    if ((packet.B2 & StatusPaused) == StatusPaused)
                        Console.Write("paused ");
                    if ((packet.B2 & StatusSupportReconnect) == StatusSupportReconnect)
                        Console.Write("supportreconnect ");
                    Console.WriteLine();
                    break;

                case BGBCommand.WantDisconnect:
                    // TODO: Respect WantDisconnect
                    break;

                default:
                    break;
            }

        }

        public void Joypad(byte button, bool down) {
            Client.Send(new BGBPacket(
                BGBCommand.Joypad,
                (byte) ((button & 0x07) | (down ? 0x08 : 0x00)),
                0,
                0,
                0
            ));
        }

        public void SendMaster(byte data) {
            Console.WriteLine($"[BGB] [TX] [SYNC1] 0x{data}");
            Client.Send(new BGBPacket(
                BGBCommand.Sync1,
                data,
                // 0x81 is required. 0x02 is "high speed", 0x04 is "double speed"
                0x81 | 0x02, // | 0x04,
                0,
                Time
            ));
        }

        public void SendSlave(byte data) {
            Client.Send(new BGBPacket(
                BGBCommand.Sync2,
                data,
                0x80,
                0,
                0
            ));
        }

        public void SendTime() {
            Client.Send(new BGBPacket(
                BGBCommand.Sync3,
                0x00,
                0,
                0,
                Time
            ));
        }

    }
}
