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

namespace MidiToMGBA {
    public unsafe class MGBALink : IDisposable {

        public static HashSet<MGBALink> Links = new HashSet<MGBALink>();

        public static GBSIO* SIO;
        public static GBSIODriver* Driver;

        public MGBALink() {
            if (Links.Count == 0) {
                // First link - setup.

                IntPtr libmgba = PInvokeHelper.OpenLibrary("libmgba.dll");
                // Hook GBSIOInit to grab a reference to GBSIO.
                h_GBSIOInit = new NativeDetour(
                    libmgba.GetFunction("GBSIOInit"),
                    typeof(MGBALink).GetMethod("GBSIOInit", BindingFlags.NonPublic | BindingFlags.Static)
                );
                orig_GBSIOInit = h_GBSIOInit.GenerateTrampoline<d_GBSIOInit>();

                // Hook mTimingInit (called via non-exported GBInit) to set the driver during init.
                h_mTimingInit = new NativeDetour(
                    libmgba.GetFunction("mTimingInit"),
                    typeof(MGBALink).GetMethod("mTimingInit", BindingFlags.NonPublic | BindingFlags.Static)
                );
                orig_mTimingInit = h_mTimingInit.GenerateTrampoline<d_mTimingInit>();

                // Set up the link driver.
                Driver = (GBSIODriver*) Marshal.AllocHGlobal(Marshal.SizeOf(typeof(GBSIODriver)));
                Driver->p = (GBSIO*) IntPtr.Zero;
                Driver->init = Init;
                Driver->deinit = Deinit;
                Driver->writeSB = WriteSB;
                Driver->writeSC = WriteSC;
            }
            Links.Add(this);
        }

        public void Send(byte data) {
            lock (this) {
                if ((IntPtr) SIO == IntPtr.Zero)
                    return;
                while (SIO->remainingBits != 0) {
                    Console.WriteLine($"Waiting - {SIO->remainingBits} bits left.");
                    Thread.Sleep(100);
                }
                Console.WriteLine($"Sending 0x{data.ToString("X2")}");
                SIO->pendingSB = data;
                SIO->remainingBits = 8;
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

            h_mTimingInit.Undo();
            h_mTimingInit.Free();
            h_mTimingInit = null;

            Marshal.FreeHGlobal((IntPtr) Driver);
            Driver = (GBSIODriver*) IntPtr.Zero;

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

        private static NativeDetour h_mTimingInit;
        private delegate void d_mTimingInit(IntPtr timingPtr, IntPtr relativeCyclesPtr, IntPtr nextEventPtr);
        private static d_mTimingInit orig_mTimingInit;
        private static void mTimingInit(IntPtr timingPtr, IntPtr relativeCyclesPtr, IntPtr nextEventPtr) {
            int* relativeCycles = (int*) relativeCyclesPtr;
            int* nextEvent = (int*) nextEventPtr;
            Console.WriteLine($"mTimingInit, timing @ 0x{timingPtr.ToString("X16")}, relativeCycles: {*relativeCycles}, nextEvent: {*nextEvent}");
            orig_mTimingInit(timingPtr, relativeCyclesPtr, nextEventPtr);

            Console.WriteLine("Setting GBSIO driver");
            GBSIOSetDriver(SIO, Driver);
        }

        private static bool Init(GBSIODriver* driver) {
            Console.WriteLine("mGBALink Driver: Init");
            return true;
        }

        private static void Deinit(GBSIODriver* driver) {
            Console.WriteLine("mGBALink Driver: Deinit");
        }

        private static void WriteSB(GBSIODriver* driver, byte value) {
            Console.WriteLine($"mGBALink Driver: WriteSB 0x{value.ToString("X2")}");
        }

        private static byte WriteSC(GBSIODriver* driver, byte value) {
            Console.WriteLine($"mGBALink Driver: WriteSC 0x{value.ToString("X2")}");
            // Note: Value currently unused by GBSIOWriteSC, but python binding returns input.
            return value;
        }

    }
}
