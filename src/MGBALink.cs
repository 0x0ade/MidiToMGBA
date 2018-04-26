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

        private static object SIOLock = new object();

        public static HashSet<MGBALink> Links = new HashSet<MGBALink>();

        public static mTiming* Timing;
        public static GBSIO* SIO;

        public static Queue<byte> Queue = new Queue<byte>();

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

                // Hook mTimingInit (called via non-exported GBInit) to grab a reference to mTiming.
                h_mTimingInit = new NativeDetour(
                    libmgba.GetFunction("mTimingInit"),
                    typeof(MGBALink).GetMethod("mTimingInit", BindingFlags.NonPublic | BindingFlags.Static)
                );
                orig_mTimingInit = h_mTimingInit.GenerateTrampoline<d_mTimingInit>();

                // Hook mCoreThreadStart to inject the link feeder.
                h_mCoreThreadStart = new NativeDetour(
                    libmgba.GetFunction("mCoreThreadStart"),
                    typeof(MGBALink).GetMethod("mCoreThreadStart", BindingFlags.NonPublic | BindingFlags.Static)
                );
                orig_mCoreThreadStart = h_mCoreThreadStart.GenerateTrampoline<d_mCoreThreadStart>();

                // Hook mSDLAttachPlayer to hook the renderer's runloop.
                // This is required to fix any managed runtime <-> unmanaged state bugs.
                h_mSDLAttachPlayer = new NativeDetour(
                    libmgbasdl.GetFunction("mSDLAttachPlayer"),
                    typeof(MGBALink).GetMethod("mSDLAttachPlayer", BindingFlags.NonPublic | BindingFlags.Static)
                );
                orig_mSDLAttachPlayer = h_mSDLAttachPlayer.GenerateTrampoline<d_mSDLAttachPlayer>();
            }

            Links.Add(this);
        }

        public void Send(byte data) {
            lock (SIOLock) {
                if ((IntPtr) SIO == IntPtr.Zero)
                    return;

                Console.WriteLine($"Queuing 0x{data.ToString("X2")}");
                Queue.Enqueue(data);
            }
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

            h_mCoreThreadStart.Undo();
            h_mCoreThreadStart.Free();
            h_mCoreThreadStart = null;

            h_mSDLAttachPlayer.Undo();
            h_mSDLAttachPlayer.Free();
            h_mSDLAttachPlayer = null;

            Timing = (mTiming*) IntPtr.Zero;
            SIO = (GBSIO*) IntPtr.Zero;
        }

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

        private static NativeDetour h_mCoreThreadStart;
        private delegate bool d_mCoreThreadStart(IntPtr threadContextPtr);
        private static d_mCoreThreadStart orig_mCoreThreadStart;
        private static bool mCoreThreadStart(IntPtr threadContextPtr) {
            Console.WriteLine($"mCoreThreadStart, threadContext @ 0x{threadContextPtr.ToString("X16")}");
            bool rv = orig_mCoreThreadStart(threadContextPtr);

            mCoreThread* threadContext = (mCoreThread*) threadContextPtr;
            orig_frameCallback = threadContext->frameCallback;
            threadContext->frameCallback = frameCallback;

            return rv;
        }

        private static ThreadCallback orig_frameCallback;
        private static void frameCallback(mCoreThread* threadContext) {
            // Console.WriteLine($"frameCallback, threadContext @ 0x{((IntPtr) threadContext).ToString("X16")}");
            orig_frameCallback?.Invoke(threadContext);

            if (Queue.Count > 0) {
                byte data = Queue.Dequeue();
                SIO->pendingSB = data;
                // Telling mGBA to write to SC makes it read the pending SB.
                GBSIOWriteSC(SIO, 0x83);
            }
        }

        private static NativeDetour h_mSDLAttachPlayer;
        private delegate bool d_mSDLAttachPlayer(IntPtr eventsPtr, IntPtr playerPtr);
        private static d_mSDLAttachPlayer orig_mSDLAttachPlayer;
        private static bool mSDLAttachPlayer(IntPtr eventsPtr, IntPtr playerPtr) {
            Console.WriteLine($"mSDLAttachPlayer, events @ 0x{eventsPtr.ToString("X16")}, player @ 0x{playerPtr.ToString("X16")}");
            bool rv = orig_mSDLAttachPlayer(eventsPtr, playerPtr);

            // mSDLPlayer is part of the mSDLRenderer struct. The init and runloop function pointers are after the player.
            mSDLPlayer* player = (mSDLPlayer*) playerPtr;
            IntPtr init = playerPtr + Marshal.SizeOf(typeof(mSDLPlayer));
            IntPtr runloop = init + IntPtr.Size;
            // Set our own RunLoop using GetFunctionPointerForDelegate and GetDelegateForFunctionPointer,
            // so that the .NET runtime can deal with the context switch properly.
            // This fixes all other managed threads hanging after a while.
            if (orig_mSDLRunLoop == null) {
                orig_mSDLRunLoop = (d_mSDLRunLoop) Marshal.GetDelegateForFunctionPointer(*((IntPtr*) runloop), typeof(d_mSDLRunLoop));
                *((IntPtr*) runloop) = Marshal.GetFunctionPointerForDelegate(inst_mSDLRunLoop = mSDLRunLoop);
            }

            return rv;
        }

        private delegate void d_mSDLRunLoop(IntPtr rendererPtr, IntPtr userPtr);
        private static d_mSDLRunLoop orig_mSDLRunLoop;
        private static d_mSDLRunLoop inst_mSDLRunLoop;
        private static void mSDLRunLoop(IntPtr rendererPtr, IntPtr userPtr) {
            Console.WriteLine($"mSDLRunLoop, renderer @ 0x{rendererPtr.ToString("X16")}, user @ 0x{userPtr.ToString("X16")}");
            orig_mSDLRunLoop(rendererPtr, userPtr);
        }

    }
}
