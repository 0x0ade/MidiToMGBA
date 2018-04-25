using Sanford.Multimedia.Midi;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace MidiToMGBA {
    public class MidiToMGBABridge : IDisposable {
        
        public InputDevice Input;
        public MGBALink Link;

        public MidiToMGBABridge(InputDevice input, MGBALink link) {
            Input = input;
            Link = link;

            input.MessageReceived += HandleMIDI;

            Input?.StartRecording();
        }

        private void HandleMIDI(IMidiMessage msg) {
            lock (Link) {
                foreach (byte data in msg.GetBytes()) {
                    Link.Send(data);
                }
            }
        }

        public void Dispose() {
            Input?.StopRecording();
            Input?.Dispose();
            Link?.Dispose();
        }

    }
}
