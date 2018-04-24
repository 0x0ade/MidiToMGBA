using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Collections.ObjectModel;
using Sanford.Multimedia.Midi;

namespace MidiToBGB {
    class Program {

        // TODO: Documentation.

        static void Main(string[] args) {
            Console.WriteLine($"MidiToBGB {Assembly.GetExecutingAssembly().GetName().Version}");

            if (InputDevice.DeviceCount == 0) {
                Console.WriteLine("No MIDI input device found.");
                Console.WriteLine("If you want to control BGB from your DAW, use http://www.tobias-erichsen.de/software/loopmidi.html");
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey();
                return;
            }

            MidiInCaps caps;
            int inputId = -1;
            string bgbHost = "127.0.0.1";
            int bgbPort = 8765;

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
                    bgbHost = argQueue.Dequeue();
                    bgbPort = int.Parse(argQueue.Dequeue());
                }
            }

            if (inputId == -1) {
                Console.WriteLine("No --midi ID given, connecting to last connected device.");
                inputId = InputDevice.DeviceCount - 1;
            }

            caps = InputDevice.GetDeviceCapabilities(inputId);
            while (true) {
                try {
                    Console.WriteLine($"Connecting to MIDI input {inputId} {caps.name}");
                    using (InputDevice input = new InputDevice(inputId)) {
                        Console.WriteLine($"Connecting to BGB {bgbHost} {bgbPort}");
                        using (BGBLink link = new BGBLink(bgbHost, bgbPort)) {
                            Console.WriteLine("Starting bridge.");
                            using (MidiToBGBBridge bridge = new MidiToBGBBridge(input, link)) {
                                while (link.Client.Client.Connected && !input.IsDisposed)
                                    Thread.Sleep(0);
                                return;
                            }
                        }
                    }
                } catch (Exception e) {
                    Console.WriteLine("Error!");
                    Console.WriteLine(e);
                    Console.WriteLine("Retrying...");
                }
            }
        }

    }
}
