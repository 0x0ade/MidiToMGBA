using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MidiToBGB {
    public class BGBLinkClient : IDisposable {

        public TcpClient Client;
        public NetworkStream Stream;

        protected Queue<BGBPacket> TransferQueue = new Queue<BGBPacket>();
        protected Thread ReceiveThread;
        protected Thread TransferThread;

        public event Action<BGBPacket> OnReceive;

        public BGBLinkClient() {
            Client = new TcpClient();
            Client.NoDelay = true;
        }

        public void Connect(string hostname = "127.0.0.1", int port = 8765) {
            if (Client == null || Stream != null)
                return;
            Client.Connect(hostname, port);
            Stream = Client.GetStream();

            ReceiveThread = new Thread(ReceiveLoop);
            ReceiveThread.Name = $"BGBLink {hostname}:{port} Receive";
            ReceiveThread.IsBackground = true;
            ReceiveThread.Start();

            TransferThread = new Thread(TransferLoop);
            TransferThread.Name = $"BGBLink {hostname}:{port} Transfer";
            TransferThread.IsBackground = true;
            TransferThread.Start();
        }

        public void Dispose() {
            Client?.Close();
            Stream?.Dispose();
            Stream = null;
        }

        public void Send(BGBPacket packet) {
            TransferQueue.Enqueue(packet);
        }

        private void ReceiveLoop() {
            byte[] buffer = new byte[BGBPacket.Size];
            while (Client?.Connected ?? false) {
                Thread.Sleep(0);
                if (Client.Available < BGBPacket.Size)
                    continue;

#if !DEBUG
                try {
#endif
                Stream.Read(buffer, 0, BGBPacket.Size);
#if !DEBUG
                } catch (Exception e) {
                    Console.WriteLine("Receiving BGBPacket failed");
                    Console.WriteLine(e);
                    Dispose();
                    return;
                }
#endif

                BGBPacket packet = new BGBPacket(buffer);
#if DEBUG
                // Console.WriteLine($"[BGB] [RX] {packet}");
#endif
                OnReceive?.Invoke(packet);
            }
        }

        private void TransferLoop() {
            while (Client?.Connected ?? false) {
                Thread.Sleep(0);
                if (TransferQueue.Count == 0)
                    continue;

                lock (TransferQueue) {
                    while (TransferQueue.Count > 0) {
                        BGBPacket packet = TransferQueue.Dequeue();
#if DEBUG
                        // Console.WriteLine($"[BGB] [TX] {packet}");
#endif
                        byte[] buffer = packet.Bytes;

#if !DEBUG
                        try {
#endif
                        Stream.Write(buffer, 0, buffer.Length);
                        Stream.Flush();
#if !DEBUG
                        } catch (Exception e) {
                            Console.WriteLine("Transfering BGBPacket failed");
                            Console.WriteLine(e);
                            Dispose();
                            return;
                        }
#endif
                    }
                }
            }
        }

    }
}
