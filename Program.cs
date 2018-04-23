using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Sanford.Multimedia;
using Sanford.Multimedia.Midi;

namespace MidiToBGB {
    class Program {

        // TODO: Documentation.
        // TODO: Link to http://www.tobias-erichsen.de/software/loopmidi.html

        static void Main(string[] args) {
            Console.WriteLine($"MidiToBGB {Assembly.GetExecutingAssembly().GetName().Version}");

            MidiInCaps caps;
            int inputId = -1;
            string host = "127.0.0.1";
            int port = 8765;

            Queue<string> argQueue = new Queue<string>(args);
            while (argQueue.Count > 0) {
                string arg = argQueue.Dequeue();
                if (arg == "--midi") {
                    string inputName = argQueue.Dequeue().ToLowerInvariant();
                    if (!int.TryParse(inputName, out inputId)) {
                        inputId = -1;
                    }
                    if (inputId == -1) {
                        for (int i = 0; i < inputId; i++) {
                            caps = InputDevice.GetDeviceCapabilities(i);
                            if (caps.name.ToLowerInvariant() == inputName) {
                                inputId = i;
                                break;
                            }
                        }
                    }
                    if (inputId == -1) {
                        Console.WriteLine($"MIDI input device not found: {inputName}");
                        Console.WriteLine("Press any key to exit.");
                        Console.ReadKey();
                    }

                } else if (arg == "--bgb") {
                    host = argQueue.Dequeue();
                    port = int.Parse(argQueue.Dequeue());
                }
            }

            if (inputId == -1) {
                Console.WriteLine("No --midi ID given, connecting to last connected device.");
                inputId = InputDevice.DeviceCount - 1;
            }

            if (inputId == -1) {
                Console.WriteLine("No MIDI input device found.");
                Console.WriteLine("If you want to control BGB from your DAW, use http://www.tobias-erichsen.de/software/loopmidi.html");
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey();
                return;
            }

            caps = InputDevice.GetDeviceCapabilities(inputId);
            Console.WriteLine($"Connecting to MIDI input {inputId} {caps.name}");
            using (InputDevice input = new InputDevice(inputId)) {

                Console.WriteLine($"Connecting to BGB {host} {port}");
                using (BGBLink link = new BGBLink(host, port)) {

                    Console.WriteLine("Starting bridge.");
                    using (MidiToBGBBridge bridge = new MidiToBGBBridge(input, link)) {
                        while (link.Client.Client.Connected && !input.IsDisposed)
                            Thread.Sleep(0);
                    }
                }
            }

        }

    }
}
