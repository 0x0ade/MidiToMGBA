using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Collections.ObjectModel;
using Sanford.Multimedia.Midi;
using System.IO;

namespace MidiToMGBA {
    class Program {

        static void Main(string[] args) {
            Console.WriteLine($"MidiToMGBA {Assembly.GetExecutingAssembly().GetName().Version}");

            if (InputDevice.DeviceCount == 0) {
                Console.WriteLine("No MIDI input device found.");
                Console.WriteLine("If you want to control mGBA from your DAW, use http://www.tobias-erichsen.de/software/loopmidi.html");
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey();
                return;
            }

            MidiInCaps caps;
            int inputId = -1;

            string rom = "mGB.gb";

            Queue<string> argsQueue = new Queue<string>(args);
            List<string> argsMGBA = new List<string>(args.Length);
            while (argsQueue.Count > 0) {
                string arg = argsQueue.Dequeue();

                if (arg == "--midi") {
                    string inputName = argsQueue.Dequeue().ToLowerInvariant();
                    if (!int.TryParse(inputName, out inputId)) {
                        inputId = -1;
                    }
                    if (inputId == -1) {
                        for (int i = 0; i < InputDevice.DeviceCount; i++) {
                            caps = InputDevice.GetDeviceCapabilities(i);
                            if (caps.name.ToLowerInvariant().Replace(" ", "") == inputName) {
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

                } else if (arg == "--rom") {
                    rom = argsQueue.Dequeue();

                } else if (arg == "--mgba") {
                    DynamicDll.DllMap["libmgba.dll"] = argsQueue.Dequeue();
                } else if (arg == "--mgba-sdl") {
                    DynamicDll.DllMap["libmgba-sdl.dll"] = argsQueue.Dequeue();

                } else if (arg == "--log-data") {
                    MGBALink.LogData = true;
                } else if (arg == "--sync") {
                    if (!uint.TryParse(argsQueue.Dequeue(), out MGBALink.DequeueSync))
                        MGBALink.DequeueSync = MGBALink.DequeueSyncDefault;
                } else if (arg == "--buffersize") {
                    if (!uint.TryParse(argsQueue.Dequeue(), out MGBALink.AudioBuffers))
                        MGBALink.AudioBuffers = MGBALink.AudioBuffersDefault;
                } else if (arg == "--samplerate") {
                    if (!uint.TryParse(argsQueue.Dequeue(), out MGBALink.SampleRate))
                        MGBALink.SampleRate = MGBALink.SampleRateDefault;

                } else {
                    argsMGBA.Add(arg);
                }
            }

            if (inputId == -1) {
                Console.WriteLine("No --midi ID given, connecting to last connected device.");
                for (int i = 0; i < InputDevice.DeviceCount; i++) {
                    caps = InputDevice.GetDeviceCapabilities(i);
                    Console.WriteLine($"#{i}: {caps.name.Replace(" ", "")}");
                }
                inputId = InputDevice.DeviceCount - 1;
            }

            caps = InputDevice.GetDeviceCapabilities(inputId);

#if !DEBUG
            try {
#endif
                Console.WriteLine("Setting up mGBA link");
                using (MGBALink link = new MGBALink()) {
                    Console.WriteLine($"Connecting to MIDI input {inputId} {caps.name}");
                    using (InputDevice input = new InputDevice(inputId, postEventsOnCreationContext: false, postDriverCallbackToDelegateQueue: true)) {
                        using (MidiToMGBABridge bridge = new MidiToMGBABridge(input, link)) {
                            Console.WriteLine($"Starting up mGBA, loading {rom}");
                            argsMGBA.Add(rom);
                            // Run mGBA in a separate thread to keep this thread functional.
                            // Who knows what the .NET runtime is doing behind the scenes...
                            Thread thread = new Thread(() => MGBA.mMain(argsMGBA.ToArray()));
                            thread.Start();
                            while (thread.IsAlive && !input.IsDisposed) {
                                Thread.Sleep(0);
                            }
                        }
                    }
                }
#if !DEBUG
            } catch (Exception e) {
                Console.WriteLine("Fatal Error!");
                Console.WriteLine(e);
                if (File.Exists("error.txt"))
                    File.Delete("error.txt");
                File.WriteAllText("error.txt", $"MidiToMGBA {Assembly.GetExecutingAssembly().GetName().Version}\r\n\r\n{e.ToString().Replace("\n", "\r\n").Replace("\r\r\n", "\r\n")}");
            }
#endif
        }

    }
}
