using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Reflection;
using static MidiToMGBA.MGBA;
using MonoMod.RuntimeDetour;
using System.Reflection.Emit;
using System.Threading;
using System.Diagnostics;

namespace MidiToMGBA {
    public unsafe class MGBALink : IDisposable {

        public static HashSet<MGBALink> Links = new HashSet<MGBALink>();

        public static mTiming* Timing;
        public static GBSIO* SIO;

        public static mTimingEvent* DequeueEvent;

        public static Queue<byte> Queue = new Queue<byte>();

        public static bool LogData = false;

        public MGBALink() {
            if (Links.Count == 0) {
                // First link - setup.

                IntPtr libmgba = PInvokeHelper.OpenLibrary("libmgba.dll");
                IntPtr libmgbasdl = PInvokeHelper.OpenLibrary("libmgba-sdl.dll");

                // Hook GBSIOInit to grab a reference to GBSIO.
                h_GBSIOInit = new NativeDetour(
                    libmgba.GetFunction("GBSIOInit"),
                    typeof(MGBALink).GetMethod("GBSIOInit", BindingFlags.NonPublic | BindingFlags.Static)
                );
                orig_GBSIOInit = h_GBSIOInit.GenerateTrampoline<d_GBSIOInit>();

                // Hook GBSIODeinit to properly dispose everything.
                h_GBSIODeinit = new NativeDetour(
                    libmgba.GetFunction("GBSIODeinit"),
                    typeof(MGBALink).GetMethod("GBSIODeinit", BindingFlags.NonPublic | BindingFlags.Static)
                );
                orig_GBSIODeinit = h_GBSIODeinit.GenerateTrampoline<d_GBSIODeinit>();

                // Hook mTimingInit (called via non-exported GBInit) to set up any timing-related stuff.
                h_mTimingInit = new NativeDetour(
                    libmgba.GetFunction("mTimingInit"),
                    typeof(MGBALink).GetMethod("mTimingInit", BindingFlags.NonPublic | BindingFlags.Static)
                );
                orig_mTimingInit = h_mTimingInit.GenerateTrampoline<d_mTimingInit>();

                // Hook _GBSIOProcessEvents to... do nothing, for now.
                h__GBSIOProcessEvents = new NativeDetour(
                    libmgba.GetFunction("_GBSIOProcessEvents"),
                    typeof(MGBALink).GetMethod("_GBSIOProcessEvents", BindingFlags.NonPublic | BindingFlags.Static)
                );
                orig__GBSIOProcessEvents = h__GBSIOProcessEvents.GenerateTrampoline<d__GBSIOProcessEvents>();

                // Hook mSDLAttachPlayer to hook the renderer's runloop.
                // This is required to fix any managed runtime <-> unmanaged state bugs.
                h_mSDLAttachPlayer = new NativeDetour(
                    libmgbasdl.GetFunction("mSDLAttachPlayer"),
                    typeof(MGBALink).GetMethod("mSDLAttachPlayer", BindingFlags.NonPublic | BindingFlags.Static)
                );
                orig_mSDLAttachPlayer = h_mSDLAttachPlayer.GenerateTrampoline<d_mSDLAttachPlayer>();

                // Setup the dequeueing event.
                DequeueEvent = (mTimingEvent*) Marshal.AllocHGlobal(sizeof(mTimingEvent));
                // Slow but functional zeroing.
                for (int i = 0; i < sizeof(mTimingEvent); i++)
                    *((byte*) ((IntPtr) DequeueEvent + i)) = 0x00;
                DequeueEvent->context = (void*) IntPtr.Zero;
                DequeueEvent->name = (byte*) Marshal.StringToHGlobalAnsi("MidiToGBG Data Dequeue");
                DequeueEvent->callback = inst_Dequeue = Dequeue;
                handle_Dequeue = GCHandle.Alloc(inst_Dequeue);
                DequeueEvent->priority = 0x0ade;
            }

            Links.Add(this);
        }

        public void Send(byte data) {
            if (LogData)
                Console.WriteLine($"->O   0x{data.ToString("X2")} #{Queue.Count}");
            Queue.Enqueue(data);
        }

        private static mTimingEvent.d_callback inst_Dequeue;
        private static GCHandle handle_Dequeue;
        private static void Dequeue(mTiming* timing, void* context, uint cyclesLate) {
            if (!mTimingIsScheduled(Timing, &SIO->@event) && SIO->remainingBits != 0) {
                Console.WriteLine($"XXXXX 0x{SIO->pendingSB.ToString("X2")}, {SIO->remainingBits} bits remaining");
                mTimingSchedule(Timing, &SIO->@event, SIO->period);
                mTimingSchedule(Timing, DequeueEvent, SIO->period);
                return;
            }

            if (Queue.Count > 0 && SIO->remainingBits == 0) {
                byte data = Queue.Dequeue();
                if (LogData)
                    Console.WriteLine($"  O-> 0x{data.ToString("X2")}, {Queue.Count} left");
                SIO->pendingSB = data;
                GBSIOWriteSC(SIO, 0x83); // ShiftClock, ClockSpeed, -, -, -, -, -, Enable
            }

            mTimingSchedule(Timing, DequeueEvent, Math.Max(1, SIO->period * 32));
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

            h_GBSIODeinit.Undo();
            h_GBSIODeinit.Free();
            h_GBSIODeinit = null;

            h_mTimingInit.Undo();
            h_mTimingInit.Free();
            h_mTimingInit = null;

            h__GBSIOProcessEvents.Undo();
            h__GBSIOProcessEvents.Free();
            h__GBSIOProcessEvents = null;

            h_mSDLAttachPlayer.Undo();
            h_mSDLAttachPlayer.Free();
            h_mSDLAttachPlayer = null;

            Timing = (mTiming*) IntPtr.Zero;
            SIO = (GBSIO*) IntPtr.Zero;

            Marshal.FreeHGlobal((IntPtr) DequeueEvent->name);
            Marshal.FreeHGlobal((IntPtr) DequeueEvent);
            DequeueEvent = (mTimingEvent*) IntPtr.Zero;
        }

        #region Delegate Hooks

        private delegate void d_mSDLRunLoop(IntPtr rendererPtr, IntPtr userPtr);
        private static d_mSDLRunLoop orig_mSDLRunLoop;
        private static d_mSDLRunLoop inst_mSDLRunLoop;
        private static GCHandle handle_mSDLRunLoop;
        private static void mSDLRunLoop(IntPtr rendererPtr, IntPtr userPtr) {
            Console.WriteLine($"mSDLRunLoop, renderer @ 0x{rendererPtr.ToString("X16")}, user @ 0x{userPtr.ToString("X16")}");

            mTimingSchedule(Timing, DequeueEvent, 0);

            orig_mSDLRunLoop(rendererPtr, userPtr);
        }

        #endregion

        #region NativeDetours

        private static NativeDetour h_GBSIOInit;
        private delegate void d_GBSIOInit(IntPtr sioPtr);
        private static d_GBSIOInit orig_GBSIOInit;
        private static void GBSIOInit(IntPtr sioPtr) {
            Console.WriteLine($"GBSIOInit, sio @ 0x{sioPtr.ToString("X16")}");
            orig_GBSIOInit(sioPtr);

            SIO = (GBSIO*) sioPtr;
        }

        private static NativeDetour h_GBSIODeinit;
        private delegate void d_GBSIODeinit(IntPtr sioPtr);
        private static d_GBSIODeinit orig_GBSIODeinit;
        private static void GBSIODeinit(IntPtr sioPtr) {
            Console.WriteLine($"GBSIODeinit, sio @ 0x{sioPtr.ToString("X16")}");
            orig_GBSIODeinit(sioPtr);

            SIO = (GBSIO*) IntPtr.Zero;
            // Don't foreach, as disposing the link removes it from the set.
            while (Links.Count > 0)
                Links.First().Dispose();
        }

        private static NativeDetour h_mTimingInit;
        private delegate void d_mTimingInit(IntPtr timingPtr, IntPtr relativeCyclesPtr, IntPtr nextEventPtr);
        private static d_mTimingInit orig_mTimingInit;
        private static void mTimingInit(IntPtr timingPtr, IntPtr relativeCyclesPtr, IntPtr nextEventPtr) {
            int* relativeCycles = (int*) relativeCyclesPtr;
            int* nextEvent = (int*) nextEventPtr;
            Console.WriteLine($"mTimingInit, timing @ 0x{timingPtr.ToString("X16")}, relativeCycles: {*relativeCycles}, nextEvent: {*nextEvent}");
            orig_mTimingInit(timingPtr, relativeCyclesPtr, nextEventPtr);

            Timing = (mTiming*) timingPtr;

        }

        private static NativeDetour h__GBSIOProcessEvents;
        private delegate void d__GBSIOProcessEvents(IntPtr timingPtr, IntPtr contextPtr, uint cyclesLate);
        private static d__GBSIOProcessEvents orig__GBSIOProcessEvents;
        private static void _GBSIOProcessEvents(IntPtr timingPtr, IntPtr contextPtr, uint cyclesLate) {
            // Console.WriteLine($"_GBSIOProcessEvents, timing @ 0x{timingPtr.ToString("X16")}, context @ 0x{contextPtr.ToString("X16")}, cyclesLate: {cyclesLate}");
            orig__GBSIOProcessEvents(timingPtr, contextPtr, cyclesLate);
        }

        private static NativeDetour h_mSDLAttachPlayer;
        private delegate bool d_mSDLAttachPlayer(IntPtr eventsPtr, IntPtr playerPtr);
        private static d_mSDLAttachPlayer orig_mSDLAttachPlayer;
        private static bool mSDLAttachPlayer(IntPtr eventsPtr, IntPtr playerPtr) {
            Console.WriteLine($"mSDLAttachPlayer, events @ 0x{eventsPtr.ToString("X16")}, player @ 0x{playerPtr.ToString("X16")}");
            bool rv = orig_mSDLAttachPlayer(eventsPtr, playerPtr);

            // mSDLPlayer is part of the mSDLRenderer struct. The init and runloop function pointers are after the player.
            mSDLPlayer* player = (mSDLPlayer*) playerPtr;
            IntPtr init = playerPtr + sizeof(mSDLPlayer);
            IntPtr runloop = init + IntPtr.Size;
            // Set our own RunLoop using GetFunctionPointerForDelegate and GetDelegateForFunctionPointer,
            // so that the .NET runtime can deal with the context switch properly.
            // This fixes all other managed threads hanging after a while.
            if (orig_mSDLRunLoop == null) {
                orig_mSDLRunLoop = (d_mSDLRunLoop) Marshal.GetDelegateForFunctionPointer(*((IntPtr*) runloop), typeof(d_mSDLRunLoop));
                *((IntPtr*) runloop) = Marshal.GetFunctionPointerForDelegate(inst_mSDLRunLoop = mSDLRunLoop);
                handle_mSDLRunLoop = GCHandle.Alloc(inst_mSDLRunLoop);
            }

            return rv;
        }

    #endregion

    }
}
