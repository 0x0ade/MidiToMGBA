using Sanford.Multimedia;
using Sanford.Multimedia.Midi;
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

        Stopwatch FWatch = Stopwatch.StartNew();
        double FLastMillis;
        double FDiff;
        double FDiffTimeStamp;
        int FCounter;
        int FLastTimestamp;

        public MidiToBGBBridge(InputDevice input, BGBLink bgb) {
            Input = input;
            BGB = bgb;

            input.ChannelMessageReceived += HandleChannelMessageReceived;
            input.SysCommonMessageReceived += HandleSysCommonMessageReceived;
            input.SysExMessageReceived += HandleSysExMessageReceived;
            input.SysRealtimeMessageReceived += HandleSysRealtimeMessageReceived;
            input.Error += HandleError;

            input.StartRecording();
        }

        private void HandleChannelMessageReceived(object sender, ChannelMessageEventArgs e) {
            foreach (byte b in e.Message.GetBytes()) {
                BGB.SendMaster(b);
            }
        }

        private void HandleSysCommonMessageReceived(object sender, SysCommonMessageEventArgs e) {

        }

        private void HandleSysExMessageReceived(object sender, SysExMessageEventArgs e) {

        }

        private void HandleSysRealtimeMessageReceived(object sender, SysRealtimeMessageEventArgs e) {
            FCounter++;
            if (FCounter % 24 == 0) {
                double millis = FWatch.Elapsed.TotalMilliseconds;
                FDiff = 60000 / (millis - FLastMillis);
                FLastMillis = millis;

                int timestamp = e.Message.Timestamp;
                FDiffTimeStamp = 60000.0 / (timestamp - FLastTimestamp);
                FLastTimestamp = timestamp;
            }
        }

        private void HandleError(object sender, ErrorEventArgs e) {
            Console.WriteLine("[MIDI] error");
            Console.WriteLine(e.Error);
        }

        public void Dispose() {
            Input?.StopRecording();
            Input?.Reset();
            Input?.Close();
            Input?.Dispose();
            BGB?.Dispose();
        }
        
        

    }
}
