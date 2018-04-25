using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Reflection;
using static MidiToMGBA.MGBA;
using MonoMod.RuntimeDetour;
using System.Reflection.Emit;

namespace MidiToMGBA {
    public unsafe class MGBALink : IDisposable {

        public static List<MGBALink> Links = new List<MGBALink>();
        public static GBSIOLockstep* Lockstep;

        public GBSIOLockstepNode* Node;

        public MGBALink() {
            if (Links.Count == 0) {
                // First link - setup.

                IntPtr libmgba = PInvokeHelper.OpenLibrary("libmgba.dll");
                h_GBSIOInit = new NativeDetour(
                    libmgba.GetFunction("GBSIOInit"),
                    typeof(MGBALink).GetMethod("GBSIOInit", BindingFlags.NonPublic | BindingFlags.Static)
                );
                orig_GBSIOInit = h_GBSIOInit.GenerateTrampoline<d_GBSIOInit>();

                Lockstep = (GBSIOLockstep*) Marshal.AllocHGlobal(Marshal.SizeOf(typeof(GBSIOLockstep)));
                GBSIOLockstepInit(Lockstep);
            }
            Links.Add(this);

            Node = (GBSIOLockstepNode*) Marshal.AllocHGlobal(Marshal.SizeOf(typeof(GBSIOLockstepNode)));
            GBSIOLockstepNodeCreate(Node);
            GBSIOLockstepAttachNode(Lockstep, Node);
        }

        public void Send(byte data) {
        }

        public void Dispose() {
            if (!Links.Contains(this))
                return;

            GBSIOLockstepDetachNode(Lockstep, Node);
            Marshal.FreeHGlobal((IntPtr) Node);

            Links.Remove(this);
            if (Links.Count > 0)
                return;

            // Last link - dispose.
            h_GBSIOInit.Undo();
            h_GBSIOInit.Free();
            h_GBSIOInit = null;

            Marshal.FreeHGlobal((IntPtr) Lockstep);
        }

        private static NativeDetour h_GBSIOInit;
        private delegate void d_GBSIOInit(IntPtr sioPtr);
        private static d_GBSIOInit orig_GBSIOInit;
        private static void GBSIOInit(IntPtr sioPtr) {
            GBSIO* sio = (GBSIO*) sioPtr;
            Console.WriteLine($"GBSIOInit, sio @ 0x{sioPtr.ToString("X16")}");
            orig_GBSIOInit(sioPtr);

            Console.WriteLine("Setting GBSIO driver");
            GBSIOSetDriver(sio, (GBSIODriver*) Links[0].Node);
        }

    }
}
