using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MidiToBGB {
    [StructLayout(LayoutKind.Explicit)]
    public struct BGBPacket {

        public const int Size = 8;

        [FieldOffset(0)]
        public ulong Data;

        [FieldOffset(0)]
        public BGBCommand Command;
        [FieldOffset(1)]
        public byte B2;
        [FieldOffset(2)]
        public byte B3;
        [FieldOffset(3)]
        public byte B4;

        [FieldOffset(4)]
        public int I1;

        public byte[] Bytes {
            get {
                return BitConverter.GetBytes(Data);
            }
            set {
                if (value.Length != Size)
                    throw new ArgumentException($"Array length incorrect. BGBPacket is {Size} bytes long.");
                Data = BitConverter.ToUInt64(value, 0);
            }
        }

        public BGBPacket(byte[] bytes) {
            Data = 0;
            Command = 0;
            B2 = B3 = B4 = 0;
            I1 = 0;
            Bytes = bytes;
        }

        public BGBPacket(BGBCommand b1, byte b2, byte b3, byte b4, int i1) {
            Data = 0;
            Command = b1;
            B2 = b2;
            B3 = b3;
            B4 = b4;
            I1 = i1;
        }

        public override string ToString() {
            return $"[{Command} {B2} {B3} {B4} {I1}]";
        }

    }
}
