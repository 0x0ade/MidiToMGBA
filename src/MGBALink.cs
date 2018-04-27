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
        public static GBSIODriver* Driver;
        public static mTimingEvent* QueueCheckEvent;

        public static Queue<byte> Queue = new Queue<byte>();

        public static bool LogData = false;
        public readonly static int SyncSIODefault = 512;
        public static int SyncSIO = SyncSIODefault;
        public readonly static int SyncWaitDefault = 64;
        public static int SyncWait = SyncSIODefault;
        public readonly static uint AudioBuffersDefault = 1024;
        public static uint AudioBuffers = AudioBuffersDefault;
        public readonly static uint SampleRateDefault = 48000; // mGBA defaults to 44100
        public static uint SampleRate = SampleRateDefault;

        public MGBALink() {
            if (Links.Count == 0) {
                // First link - setup.

                IntPtr libmgba = DynamicDll.OpenLibrary("libmgba.dll");
                IntPtr libmgbasdl = DynamicDll.OpenLibrary("libmgba-sdl.dll");

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

                // Hook mTimingInit (called via non-exported GBInit) to set up any timing-related stuff and the driver.
                h_mTimingInit = new NativeDetour(
                    libmgba.GetFunction("mTimingInit"),
                    typeof(MGBALink).GetMethod("mTimingInit", BindingFlags.NonPublic | BindingFlags.Static)
                );
                orig_mTimingInit = h_mTimingInit.GenerateTrampoline<d_mTimingInit>();

                // Hook mCoreConfigLoadDefaults to change the configs before loading them.
                h_mCoreConfigLoadDefaults = new NativeDetour(
                    libmgba.GetFunction("mCoreConfigLoadDefaults"),
                    typeof(MGBALink).GetMethod("mCoreConfigLoadDefaults", BindingFlags.NonPublic | BindingFlags.Static)
                );
                orig_mCoreConfigLoadDefaults = h_mCoreConfigLoadDefaults.GenerateTrampoline<d_mCoreConfigLoadDefaults>();

                // Hook mSDLAttachPlayer to hook the renderer's runloop.
                // This is required to fix any managed runtime <-> unmanaged state bugs.
                h_mSDLAttachPlayer = new NativeDetour(
                    libmgbasdl.GetFunction("mSDLAttachPlayer"),
                    typeof(MGBALink).GetMethod("mSDLAttachPlayer", BindingFlags.NonPublic | BindingFlags.Static)
                );
                orig_mSDLAttachPlayer = h_mSDLAttachPlayer.GenerateTrampoline<d_mSDLAttachPlayer>();

                // Hook mSDLInitAudio to force our own sample rate.
                h_mSDLInitAudio = new NativeDetour(
                    libmgbasdl.GetFunction("mSDLInitAudio"),
                    typeof(MGBALink).GetMethod("mSDLInitAudio", BindingFlags.NonPublic | BindingFlags.Static)
                );
                orig_mSDLInitAudio = h_mSDLInitAudio.GenerateTrampoline<d_mSDLInitAudio>();

                // Setup the custom GBSIODriver, responsible for syncing dequeues.
                Driver = (GBSIODriver*) Marshal.AllocHGlobal(Marshal.SizeOf(typeof(GBSIODriver)));
                // Slow but functional zeroing.
                for (int i = 0; i < sizeof(mTimingEvent); i++)
                    Driver->p = (GBSIO*) IntPtr.Zero;
                Driver->init = PinnedPtr<GBSIODriver.d_init>(DriverInit);
                Driver->deinit = PinnedPtr<GBSIODriver.d_deinit>(DriverDeinit);
                Driver->writeSB = PinnedPtr<GBSIODriver.d_writeSB>(DriverWriteSB);
                Driver->writeSC = PinnedPtr<GBSIODriver.d_writeSC>(DriverWriteSC);

                // Setup the queue check event.
                QueueCheckEvent = (mTimingEvent*) Marshal.AllocHGlobal(sizeof(mTimingEvent));
                // Slow but functional zeroing.
                for (int i = 0; i < sizeof(mTimingEvent); i++)
                    *((byte*) ((IntPtr) QueueCheckEvent + i)) = 0x00;
                QueueCheckEvent->context = (void*) IntPtr.Zero;
                QueueCheckEvent->name = (byte*) Marshal.StringToHGlobalAnsi("MidiToGBG Queue Check");
                QueueCheckEvent->callback = PinnedPtr<mTimingEvent.d_callback>(QueueCheck);
                QueueCheckEvent->priority = 0x30;
            }

            Links.Add(this);
        }

        public void Send(byte data) {
            if (LogData)
                Console.WriteLine($"->O   0x{data.ToString("X2")} #{Queue.Count}");
            Queue.Enqueue(data);
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

            h_mCoreConfigLoadDefaults.Undo();
            h_mCoreConfigLoadDefaults.Free();
            h_mCoreConfigLoadDefaults = null;

            h_mSDLAttachPlayer.Undo();
            h_mSDLAttachPlayer.Free();
            h_mSDLAttachPlayer = null;

            Timing = (mTiming*) IntPtr.Zero;
            SIO = (GBSIO*) IntPtr.Zero;

            Marshal.FreeHGlobal((IntPtr) Driver);
            Driver = (GBSIODriver*) IntPtr.Zero;

            Marshal.FreeHGlobal((IntPtr) QueueCheckEvent->name);
            Marshal.FreeHGlobal((IntPtr) QueueCheckEvent);
            QueueCheckEvent = (mTimingEvent*) IntPtr.Zero;
        }

        private static bool Dequeue() {
            if (Queue.Count == 0)
                return false;

            byte data = Queue.Dequeue();
            if (LogData)
                Console.WriteLine($"  O-> 0x{data.ToString("X2")}, {Queue.Count} left");
            SIO->pendingSB = data;
            SIO->remainingBits = 8;

            // We've successfully supplied a byte - force mGBA to read it.

            // Execute _GBSIOProcessEvents to force-update SB

            // Note: One should probably write to SIO->p->memory.io and execute the interrupt directly.
            // _GBSIOProcessEvents is rather meant to run scheduedly on the "master" GB.
            // Meanwhile, mGB makes the GB (and thus mGBA) act like a "slave".
            // Unfortunately, AFAIK, mGBA doesn't offer any way to supply a link clock signal for the slave.
            // This means that the link is currently limited to blindly feeding data into mGBA.

            while (SIO->remainingBits > 1) {
                _GBSIOProcessEvents(Timing, SIO, 0);
                // _GBSIOProcessEvents reschedules itself - deschedule, or risk hanging mGBA.
                mTimingDeschedule(Timing, &SIO->@event);
            }
            mTimingSchedule(Timing, &SIO->@event, SyncSIO);

            return true;
        }

        private static void QueueCheck(mTiming* timing, void* context, uint cyclesLate) {
            if (Dequeue())
                return;

            mTimingSchedule(Timing, QueueCheckEvent, SyncWait);
        }

        #region Delegate Hooks

        private delegate void d_mSDLRunLoop(IntPtr rendererPtr, IntPtr userPtr);
        private static d_mSDLRunLoop orig_mSDLRunLoop;
        private static void mSDLRunLoop(IntPtr rendererPtr, IntPtr userPtr) {
            Console.WriteLine($"mSDLRunLoop, renderer @ 0x{rendererPtr.ToString("X16")}, user @ 0x{userPtr.ToString("X16")}");

            orig_mSDLRunLoop(rendererPtr, userPtr);
        }

        #endregion

        #region Driver

        private static bool DriverInit(GBSIODriver* driver) {
            Console.WriteLine("DRIVR +");
            return true;
        }

        private static void DriverDeinit(GBSIODriver* driver) {
            Console.WriteLine("DRIVR -");
        }

        private static void DriverWriteSB(GBSIODriver* driver, byte value) {
            Console.WriteLine($"DRIVR SB 0x{value.ToString("X2")}");
        }

        private static byte DriverWriteSC(GBSIODriver* driver, byte value) {
            // 0x80: Transfer enabled
            // 0x01: Shift Clock, must be "external" (0)
            if ((value & 0x80) == 0x80 && (value & 0x01) != 0x01) {
                // Game waiting for transfer from outside - dequeue or kick off QueueCheck loop.
                if (!Dequeue()) {
                    // We haven't dequeued anything - schedule QueueCheck.
                    mTimingSchedule(Timing, QueueCheckEvent, 0);
                }
            } else {
                // Unexpected SC
                Console.WriteLine($"DRIVR SC 0x{value.ToString("X2")}");
            }

            // Note: Value currently unused by GBSIOWriteSC, but python binding returns input.
            return value;
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

            GBSIOSetDriver(SIO, Driver);
        }

        private static NativeDetour h_mCoreConfigLoadDefaults;
        private delegate void d_mCoreConfigLoadDefaults(IntPtr configPtr, IntPtr optsPtr);
        private static d_mCoreConfigLoadDefaults orig_mCoreConfigLoadDefaults;
        private static void mCoreConfigLoadDefaults(IntPtr configPtr, IntPtr optsPtr) {
            Console.WriteLine($"mCoreConfigLoadDefaults, config @ 0x{configPtr.ToString("X16")}, opts @ 0x{optsPtr.ToString("X16")}");

            mCoreOptions* opts = (mCoreOptions*) optsPtr;
            opts->volume = 0x200; // Defaults to 0x100, which is quieter than BGB.
            opts->audioSync = true;
            opts->videoSync = false;
            // opts->fpsTarget = float(GBA_ARM7TDMI_FREQUENCY) / float(VIDEO_TOTAL_LENGTH);
            opts->fpsTarget = 0x1000000U / 280896F; // Defaults to 60F
            opts->rewindEnable = false; // Defaults to true
            opts->rewindSave = false; // Defaults to true
            opts->audioBuffers = (IntPtr) AudioBuffers; // Defaults to 1024
            opts->sampleRate = SampleRate; // Ignored by the SDL2 platform.

            orig_mCoreConfigLoadDefaults(configPtr, optsPtr);
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
                *((IntPtr*) runloop) = PinnedPtr<d_mSDLRunLoop>(mSDLRunLoop);
            }

            return rv;
        }

        private static NativeDetour h_mSDLInitAudio;
        private delegate bool d_mSDLInitAudio(IntPtr eventsPtr, IntPtr playerPtr);
        private static d_mSDLInitAudio orig_mSDLInitAudio;
        private static bool mSDLInitAudio(IntPtr contextPtr, IntPtr threadContextPtr) {
            Console.WriteLine($"mSDLInitAudio, context @ 0x{contextPtr.ToString("X16")}, threadContext @ 0x{threadContextPtr.ToString("X16")}");

            // mSDLAudio starts with size_t samples, then unsigned sampleRate
            *((uint*) (contextPtr + IntPtr.Size)) = SampleRate;

            return orig_mSDLInitAudio(contextPtr, threadContextPtr);
        }

        #endregion

        #region Delegate pinning helper

        private static HashSet<Delegate> Delegates = new HashSet<Delegate>();
        private static HashSet<GCHandle> DelegateHandles = new HashSet<GCHandle>();

        private static IntPtr PinnedPtr<T>(T del) where T : class {
            if (!typeof(Delegate).IsAssignableFrom(typeof(T)))
                throw new ArgumentException("Generic parameter must be delegate type");
            Delegate d = del as Delegate;
            Delegates.Add(d);
            DelegateHandles.Add(GCHandle.Alloc(d));
            return Marshal.GetFunctionPointerForDelegate(d);
        }

        #endregion

    }
}
