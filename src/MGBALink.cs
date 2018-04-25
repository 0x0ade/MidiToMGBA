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

        public static GBSIOLockstepNode* Node;

        public MGBALink() {
            if (Links.Count == 0) {
                // First link - setup.

                IntPtr libmgba = PInvokeHelper.OpenLibrary("libmgba.dll");
                // Hook GBSIOInit to set the link driver.
                h_GBSIOInit = new NativeDetour(
                    libmgba.GetFunction("GBSIOInit"),
                    typeof(MGBALink).GetMethod("GBSIOInit", BindingFlags.NonPublic | BindingFlags.Static)
                );
                orig_GBSIOInit = h_GBSIOInit.GenerateTrampoline<d_GBSIOInit>();

                // Hook mTimingInit and mTimingSchedule to delay schedulings after init.
                h_mTimingInit = new NativeDetour(
                    libmgba.GetFunction("mTimingInit"),
                    typeof(MGBALink).GetMethod("mTimingInit", BindingFlags.NonPublic | BindingFlags.Static)
                );
                orig_mTimingInit = h_mTimingInit.GenerateTrampoline<d_mTimingInit>();

                h_mTimingSchedule = new NativeDetour(
                    libmgba.GetFunction("mTimingSchedule"),
                    typeof(MGBALink).GetMethod("mTimingSchedule", BindingFlags.NonPublic | BindingFlags.Static)
                );
                orig_mTimingSchedule = h_mTimingSchedule.GenerateTrampoline<d_mTimingSchedule>();

                // Set up the link driver.
                Lockstep = (GBSIOLockstep*) Marshal.AllocHGlobal(Marshal.SizeOf(typeof(GBSIOLockstep)));
                GBSIOLockstepInit(Lockstep);

                Node = (GBSIOLockstepNode*) Marshal.AllocHGlobal(Marshal.SizeOf(typeof(GBSIOLockstepNode)));
                GBSIOLockstepNodeCreate(Node);
                GBSIOLockstepAttachNode(Lockstep, Node);
            }
            Links.Add(this);
        }

        public void Send(byte data) {
        }

        public void Dispose() {
            if (!Links.Contains(this))
                return;

            Links.Remove(this);
            if (Links.Count > 0)
                return;

            // Last link - dispose.
            h_GBSIOInit.Undo();
            h_GBSIOInit.Free();
            h_GBSIOInit = null;

            GBSIOLockstepDetachNode(Lockstep, Node);
            Marshal.FreeHGlobal((IntPtr) Node);
            Node = (GBSIOLockstepNode*) IntPtr.Zero;

            Marshal.FreeHGlobal((IntPtr) Lockstep);
            Lockstep = (GBSIOLockstep*) IntPtr.Zero;
        }

        private static NativeDetour h_GBSIOInit;
        private delegate void d_GBSIOInit(IntPtr sioPtr);
        private static d_GBSIOInit orig_GBSIOInit;
        private static void GBSIOInit(IntPtr sioPtr) {
            GBSIO* sio = (GBSIO*) sioPtr;
            Console.WriteLine($"GBSIOInit, sio @ 0x{sioPtr.ToString("X16")}");
            orig_GBSIOInit(sioPtr);

            Console.WriteLine("Setting GBSIO driver");
            GBSIOSetDriver(sio, (GBSIODriver*) Node);
        }

        private static bool mTimingInitialized = false;
        private static Queue<Tuple<IntPtr, IntPtr, int>> mTimingScheduledPremature = new Queue<Tuple<IntPtr, IntPtr, int>>();

        private static NativeDetour h_mTimingInit;
        private delegate void d_mTimingInit(IntPtr timingPtr, IntPtr relativeCyclesPtr, IntPtr nextEventPtr);
        private static d_mTimingInit orig_mTimingInit;
        private static void mTimingInit(IntPtr timingPtr, IntPtr relativeCyclesPtr, IntPtr nextEventPtr) {
            Timing* timing = (Timing*) timingPtr;
            int* relativeCycles = (int*) relativeCyclesPtr;
            int* nextEvent = (int*) nextEventPtr;

            Console.WriteLine($"mTimingInit, timing @ 0x{timingPtr.ToString("X16")}, relativeCycles: {*relativeCycles}, nextEvent: {*nextEvent}");

            orig_mTimingInit(timingPtr, relativeCyclesPtr, nextEventPtr);

            mTimingInitialized = true;
            while (mTimingScheduledPremature.Count > 0) {
                Tuple<IntPtr, IntPtr, int> delayed = mTimingScheduledPremature.Dequeue();
                orig_mTimingSchedule(delayed.Item1, delayed.Item2, delayed.Item3);
            }
        }

        private static NativeDetour h_mTimingSchedule;
        private delegate void d_mTimingSchedule(IntPtr timingPtr, IntPtr eventPtr, int when);
        private static d_mTimingSchedule orig_mTimingSchedule;
        private static void mTimingSchedule(IntPtr timingPtr, IntPtr eventPtr, int when) {
            Timing* timing = (Timing*) timingPtr;
            TimingEvent* @event = (TimingEvent*) eventPtr;

            if (!mTimingInitialized) {
                Console.WriteLine($"premature mTimingSchedule, timing @ 0x{timingPtr.ToString("X16")}, event @ 0x{eventPtr.ToString("X16")}, when: {when}");
                mTimingScheduledPremature.Enqueue(Tuple.Create(timingPtr, eventPtr, when));
                return;
            }

            orig_mTimingSchedule(timingPtr, eventPtr, when);
        }

    }
}
