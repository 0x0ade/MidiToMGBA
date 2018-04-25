using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Reflection;

namespace MidiToMGBA {
    public static unsafe class MGBA {

        const string libmgba = "libmgba.dll";
        const string libmgbasdl = "libmgba-sdl.dll";

        [DllImport(libmgbasdl, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mSDLMain")]
        private static extern int INTERNAL_mSDLMain(int argc, byte** argv);

        public static void mSDLMain(params string[] args) {
            IntPtr* argv = (IntPtr*) Marshal.AllocHGlobal(IntPtr.Size * args.Length + 1);
            argv[0] = Marshal.StringToHGlobalAnsi(Assembly.GetEntryAssembly().Location);
            for (int i = 0; i < args.Length; i++)
                argv[i + 1] = Marshal.StringToHGlobalAnsi(args[i]);

            int code;
            try {
                code = INTERNAL_mSDLMain(args.Length + 1, (byte**) argv);
            } finally {
                for (int i = 0; i < args.Length + 1; i++)
                    Marshal.FreeHGlobal(argv[i]);
                Marshal.FreeHGlobal((IntPtr) argv);
            }

            if (code != 0)
                throw new Exception($"mGBA exit code {code}");
        }

        public struct TimingEvent {
            public void* context;
            // void (*callback)(struct mTiming*, void* context, uint32_t);
            public IntPtr callback;
            public char* name;
            public uint when;
            public uint priority;

            public TimingEvent* next;
        }

        public struct Timing {
            public TimingEvent* root;
            public TimingEvent* reroot;

            public uint masterCycles;
            public int* relativeCycles;
            public int* nextEvent;
        }

        #region GBSIO

        public const int MAX_GBS = 2;

        [DllImport(libmgba, CallingConvention = CallingConvention.Cdecl, EntryPoint = "GBSIOInit")]
        private static extern void INTERNAL_GBSIOInit(IntPtr sio);
        public static void GBSIOInit(GBSIO* sio) => INTERNAL_GBSIOInit((IntPtr) sio);
        [DllImport(libmgba, CallingConvention = CallingConvention.Cdecl, EntryPoint = "GBSIOReset")]
        private static extern void INTERNAL_GBSIOReset(IntPtr sio);
        public static void GBSIOReset(GBSIO* sio) => INTERNAL_GBSIOReset((IntPtr) sio);
        [DllImport(libmgba, CallingConvention = CallingConvention.Cdecl, EntryPoint = "GBSIODeinit")]
        private static extern void INTERNAL_GBSIODeinit(IntPtr sio);
        public static void GBSIODeinit(GBSIO* sio) => INTERNAL_GBSIODeinit((IntPtr) sio);
        [DllImport(libmgba, CallingConvention = CallingConvention.Cdecl, EntryPoint = "GBSIOSetDriver")]
        private static extern void INTERNAL_GBSIOSetDriver(IntPtr sio, IntPtr driver);
        public static void GBSIOSetDriver(GBSIO* sio, GBSIODriver* driver) => INTERNAL_GBSIOSetDriver((IntPtr) sio, (IntPtr) driver);
        [DllImport(libmgba, CallingConvention = CallingConvention.Cdecl, EntryPoint = "GBSIOWriteSC")]
        private static extern void INTERNAL_GBSIOWriteSC(IntPtr sio, byte sc);
        public static void GBSIOWriteSC(GBSIO* sio, byte sc) => INTERNAL_GBSIOWriteSC((IntPtr) sio, sc);
        [DllImport(libmgba, CallingConvention = CallingConvention.Cdecl, EntryPoint = "GBSIOWriteSB")]
        private static extern void INTERNAL_GBSIOWriteSB(IntPtr sio, byte sb);
        public static void GBSIOWriteSB(GBSIO* sio, byte sb) => INTERNAL_GBSIOWriteSB((IntPtr) sio, sb);
        
        public struct GBSIODriver {
            public GBSIO* p;

            // public bool (*init)(struct GBSIODriver* driver);
            public delegate bool d_init(GBSIODriver* driver);
            public IntPtr _init;
            public d_init init {
                get {
                    return (d_init) Marshal.GetDelegateForFunctionPointer(_init, typeof(d_init));
                }
                set {
                    _init = Marshal.GetFunctionPointerForDelegate(value);
                }
            }
	        // public void (*deinit)(struct GBSIODriver* driver);
            public delegate void d_deinit(GBSIODriver* driver);
            public IntPtr _deinit;
            public d_deinit deinit {
                get {
                    return (d_deinit) Marshal.GetDelegateForFunctionPointer(_deinit, typeof(d_deinit));
                }
                set {
                    _deinit = Marshal.GetFunctionPointerForDelegate(value);
                }
            }
            // public void (*writeSB)(struct GBSIODriver* driver, uint8_t value);
            public delegate void d_writeSB(GBSIODriver* driver, byte value);
            public IntPtr _writeSB;
            public d_writeSB writeSB {
                get {
                    return (d_writeSB) Marshal.GetDelegateForFunctionPointer(_writeSB, typeof(d_writeSB));
                }
                set {
                    _writeSB = Marshal.GetFunctionPointerForDelegate(value);
                }
            }

            // public uint8_t(*writeSC)(struct GBSIODriver* driver, uint8_t value);
            public delegate byte d_writeSC(GBSIODriver* driver, byte value);
            public IntPtr _writeSC;
            public d_writeSC writeSC {
                get {
                    return (d_writeSC) Marshal.GetDelegateForFunctionPointer(_writeSC, typeof(d_writeSC));
                }
                set {
                    _writeSC = Marshal.GetFunctionPointerForDelegate(value);
                }
            }
        }

        public struct GBSIO {
            public IntPtr p;

            public TimingEvent @event;

            public GBSIODriver* driver;

            public int nextEvent;
            public int period;
            public int remainingBits;

            public byte pendingSB;
        }

        #endregion

        #region Lockstep

        public enum LockstepPhase {
	        TRANSFER_IDLE = 0,
	        TRANSFER_STARTING,
	        TRANSFER_STARTED,
	        TRANSFER_FINISHING,
	        TRANSFER_FINISHED
        }

        public struct Lockstep {
            public int attached;
            public LockstepPhase transferActive;
	        public int transferCycles;

            // bool (*signal)(struct mLockstep*, unsigned mask);
            public IntPtr signal;
	        // bool (*wait)(struct mLockstep*, unsigned mask);
            public IntPtr wait;
            // void (*addCycles)(struct mLockstep*, int id, int32_t cycles);
            public IntPtr addCycles;
	        // int32_t(*useCycles)(struct mLockstep*, int id, int32_t cycles);
            public IntPtr useCycles;
	        // void (*unload)(struct mLockstep*, int id);
            public IntPtr unload;
            public void* context;
            // #ifndef NDEBUG
            public int transferId;
            // #endif
        }

        [DllImport(libmgba, CallingConvention = CallingConvention.Cdecl, EntryPoint = "LockstepInit")]
        private static extern void INTERNAL_LockstepInit(IntPtr lockstep);
        public static void LockstepInit(Lockstep* lockstep) => INTERNAL_LockstepInit((IntPtr) lockstep);

        #endregion

        #region GBSIOLockstep

        public struct GBSIOLockstep {
            public Lockstep d;
            // GBSIOLockstepNode* players[MAX_GBS];
            // Blame C#, not me. -ade
            public GBSIOLockstepNode* player1;
            public GBSIOLockstepNode* player2;

            public fixed byte pendingSB[MAX_GBS];
            public bool masterClaimed;
        }

        public struct GBSIOLockstepNode {
            public GBSIODriver d;
	        public GBSIOLockstep* p;
	        public TimingEvent @event;

            public volatile int nextEvent;
            public int eventDiff;
            public int id;
            public bool transferFinished;
            // #ifndef NDEBUG
            public int transferId;
            public LockstepPhase phase;
            // #endif
        }

        [DllImport(libmgba, CallingConvention = CallingConvention.Cdecl, EntryPoint = "GBSIOLockstepInit")]
        private static extern void INTERNAL_GBSIOLockstepInit(IntPtr lockstep);
        public static void GBSIOLockstepInit(GBSIOLockstep* lockstep) => INTERNAL_GBSIOLockstepInit((IntPtr) lockstep);
        [DllImport(libmgba, CallingConvention = CallingConvention.Cdecl, EntryPoint = "GBSIOLockstepNodeCreate")]
        private static extern void INTERNAL_GBSIOLockstepNodeCreate(IntPtr node);
        public static void GBSIOLockstepNodeCreate(GBSIOLockstepNode* node) => INTERNAL_GBSIOLockstepNodeCreate((IntPtr) node);
        [DllImport(libmgba, CallingConvention = CallingConvention.Cdecl, EntryPoint = "GBSIOLockstepAttachNode")]
        private static extern bool INTERNAL_GBSIOLockstepAttachNode(IntPtr lockstep, IntPtr node);
        public static bool GBSIOLockstepAttachNode(GBSIOLockstep* lockstep, GBSIOLockstepNode* node) => INTERNAL_GBSIOLockstepAttachNode((IntPtr) lockstep, (IntPtr) node);
        [DllImport(libmgba, CallingConvention = CallingConvention.Cdecl, EntryPoint = "GBSIOLockstepDetachNode")]
        private static extern void INTERNAL_GBSIOLockstepDetachNode(IntPtr lockstep, IntPtr node);
        public static void GBSIOLockstepDetachNode(GBSIOLockstep* lockstep, GBSIOLockstepNode* node) => INTERNAL_GBSIOLockstepDetachNode((IntPtr) lockstep, (IntPtr) node);

        #endregion

    }
}
