using Sanford.Multimedia.Midi;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Sanford.Multimedia;

namespace MidiToMGBA {
    public class MidiToMGBABridge : IDisposable {
        
        public InputDevice Input;
        public MGBALink Link;

        public MidiToMGBABridge(InputDevice input, MGBALink link) {
            Input = input;
            Link = link;

            input.MessageReceived += HandleMIDI;
            input.Error += HandleMIDIError;

            input.StartRecording();
        }

        private void HandleMIDI(IMidiMessage msg) {
            lock (Link) {
                foreach (byte data in msg.GetBytes()) {
                    Link.Send(data);
                }
            }
        }

        private void HandleMIDIError(object sender, ErrorEventArgs e) {
            Console.WriteLine("MIDI error!");
            Console.WriteLine(e.Error);
            Dispose();
        }

        public void Dispose() {
            Input.StopRecording();
            Input.Dispose();
            Link.Dispose();
        }

    }
}
