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

        public int Time => 0x7FFFFFFF & ((int) (Clock.ElapsedTicks * 0.75) + TimeBGB);

        private bool Handshaked = false;

        private Stopwatch Clock;
        private int TimeBGB;

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
                Console.WriteLine($"[BGB] [HANDSHAKE] {packet}");
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
                    break;

                case BGBCommand.Sync1:
                    break;

                case BGBCommand.Sync2:
                    break;

                case BGBCommand.Sync3:
                    if (packet.B2 == 0x00) {
                        int time = Time;
                        // Console.WriteLine($"[BGB] [TIME] BGB: {packet.I1}; self: {time}; self - BGB: {time - packet.I1}");
                        Clock.Stop();
                        Clock.Reset();
                        TimeBGB = packet.I1;
                        Clock.Start();
                        SendTime();
                    }
                    break;

                case BGBCommand.Status:
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
            Client.Send(new BGBPacket(
                BGBCommand.Sync1,
                data,
                0x81,
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
