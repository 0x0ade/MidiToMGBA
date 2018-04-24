﻿using Sanford.Multimedia.Midi;
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
    public class MidiToBGBBridge : IDisposable {
        
        public InputDevice Input;
        public BGBLink BGB;

        public MidiToBGBBridge(InputDevice input, BGBLink bgb) {
            Input = input;
            BGB = bgb;

            input.MessageReceived += HandleMIDI;
            bgb.OnReceive += HandleBGB;

            Input?.StartRecording();
        }

        private void HandleMIDI(IMidiMessage msg) {
            lock (BGB) {
                foreach (byte data in msg.GetBytes()) {
                    BGB.SendMaster(data);
                }
            }
        }

        private void HandleBGB(byte data) {
        }

        public void Dispose() {
            Input?.StopRecording();
            Input?.Dispose();
            BGB?.Dispose();
        }
        
        

    }
}
