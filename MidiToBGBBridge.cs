using Sanford.Multimedia;
using Sanford.Multimedia.Midi;
using System;
using System.Collections.Generic;
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

            /*
            input.ChannelMessageReceived += HandleChannelMessageReceived;
            input.SysCommonMessageReceived += HandleSysCommonMessageReceived;
            input.SysExMessageReceived += HandleSysExMessageReceived;
            input.SysRealtimeMessageReceived += HandleSysRealtimeMessageReceived;
            input.Error += new EventHandler<ErrorEventArgs>(inDevice_Error);
            */

            input.Reset();

            input.ChannelMessageReceived += HandleChannelMessageReceived;
            input.SysCommonMessageReceived += HandleSysCommonMessageReceived;
            input.SysExMessageReceived += HandleSysExMessageReceived;
            input.Error += HandleError;
        }

        private void HandleChannelMessageReceived(object sender, ChannelMessageEventArgs e) {
        }

        private void HandleSysCommonMessageReceived(object sender, SysCommonMessageEventArgs e) {
        }

        private void HandleSysExMessageReceived(object sender, SysExMessageEventArgs e) {
        }

        private void HandleError(object sender, ErrorEventArgs e) {
            Console.WriteLine("MIDI error:");
            Console.WriteLine(e.Error);
        }

        public void Dispose() {
            Input?.Dispose();
            BGB?.Dispose();
        }
        
        

    }
}
