using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MidiToBGB {
    public unsafe struct BGBPacket {

        public const int Size = 8;

        public fixed byte Data[Size];
        public byte[] Bytes {
            get {
                byte[] array = new byte[Size];
                fixed (byte* data = Data)
                    Marshal.Copy((IntPtr) data, array, 0, Size);
                return array;
            }
            set {
                if (value.Length != Size)
                    throw new InvalidOperationException($"BGBPacket is {Size} bytes long.");
                fixed (byte* data = Data)
                    Marshal.Copy(value, 0, (IntPtr) data, Size);
            }
        }

        public byte B1 {
            get {
                fixed (byte* data = Data)
                    return data[0];
            }
            set {
                fixed (byte* data = Data)
                    data[0] = value;
            }
        }

        public byte B2 {
            get {
                fixed (byte* data = Data)
                    return data[1];
            }
            set {
                fixed (byte* data = Data)
                    data[1] = value;
            }
        }

        public byte B3 {
            get {
                fixed (byte* data = Data)
                    return data[2];
            }
            set {
                fixed (byte* data = Data)
                    data[2] = value;
            }
        }

        public byte B4 {
            get {
                fixed (byte* data = Data)
                    return data[3];
            }
            set {
                fixed (byte* data = Data)
                    data[3] = value;
            }
        }

        public int I1 {
            get {
                fixed (byte* data = Data)
                    return *((int*) ((long) data + 4));
            }
            set {
                fixed (byte* data = Data)
                    *((int*) ((long) data + 4)) = value;
            }
        }

        public BGBPacket(byte[] bytes) {
            Bytes = bytes;
        }

        public BGBPacket(byte b1, byte b2, byte b3, byte b4, int i1) {
            B1 = b1;
            B2 = b2;
            B3 = b3;
            B4 = b4;
            I1 = i1;
        }

        public override string ToString() {
            return $"[{B1} {B2} {B3} {B4} {I1}]";
        }

    }
}
