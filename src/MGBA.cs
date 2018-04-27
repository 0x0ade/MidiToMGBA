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

        static MGBA() {
            typeof(MGBA).ResolveDynamicDllImports();
        }

        private delegate int d_mMain(int argc, byte** argv);
        [DynamicDllImport(libmgbasdl, "mSDLMain", "mMain", "main")]
        private readonly static d_mMain INTERNAL_mMain;
        public static void mMain(params string[] args) {
            IntPtr* argv = (IntPtr*) Marshal.AllocHGlobal(IntPtr.Size * args.Length + 1);
            argv[0] = Marshal.StringToHGlobalAnsi(Assembly.GetEntryAssembly().Location);
            for (int i = 0; i < args.Length; i++)
                argv[i + 1] = Marshal.StringToHGlobalAnsi(args[i]);

            int code;
            try {
                code = INTERNAL_mMain(args.Length + 1, (byte**) argv);
            } finally {
                for (int i = 0; i < args.Length + 1; i++)
                    Marshal.FreeHGlobal(argv[i]);
                Marshal.FreeHGlobal((IntPtr) argv);
            }

            if (code != 0)
                throw new Exception($"mGBA exit code {code}");
        }

        #region mTiming

        public struct mTimingEvent {
            public void* context;
            // void (*callback)(struct mTiming*, void* context, uint32_t);
            public delegate void d_callback(mTiming* timing, void* context, uint cyclesLate);
            public IntPtr callback;
            public byte* name;
            public uint when;
            public uint priority;

            public mTimingEvent* next;
        }

        public struct mTiming {
            public mTimingEvent* root;
            public mTimingEvent* reroot;

            public uint masterCycles;
            public int* relativeCycles;
            public int* nextEvent;
        }

        private delegate void d_mTimingSchedule(IntPtr timing, IntPtr @event, int when);
        [DynamicDllImport(libmgba, "mTimingSchedule")]
        private readonly static d_mTimingSchedule INTERNAL_mTimingSchedule;
        public static void mTimingSchedule(mTiming* timing, mTimingEvent* @event, int when) => INTERNAL_mTimingSchedule((IntPtr) timing, (IntPtr) @event, when);
        private delegate void d_mTimingDeschedule(IntPtr timing, IntPtr @event);
        [DynamicDllImport(libmgba, "mTimingDeschedule")]
        private readonly static d_mTimingDeschedule INTERNAL_mTimingDeschedule;
        public static void mTimingDeschedule(mTiming* timing, mTimingEvent* @event) => INTERNAL_mTimingDeschedule((IntPtr) timing, (IntPtr) @event);
        private delegate bool d_mTimingIsScheduled(IntPtr timing, IntPtr @event);
        [DynamicDllImport(libmgba, "mTimingIsScheduled")]
        private readonly static d_mTimingIsScheduled INTERNAL_mTimingIsScheduled;
        public static bool mTimingIsScheduled(mTiming* timing, mTimingEvent* @event) => INTERNAL_mTimingIsScheduled((IntPtr) timing, (IntPtr) @event);

        #endregion

        #region mCoreThread

        // typedef void (*ThreadCallback)(struct mCoreThread* threadContext);
        public delegate void ThreadCallback(mCoreThread* threadContext);

        public struct mLogger {
            // void (*log)(struct mLogger*, int category, enum mLogLevel level, const char* format, va_list args);
            public IntPtr log;
	        public IntPtr filter; // struct mLogFilter*
        }

        public struct mThreadLogger {
	        public mLogger d;
	        public mCoreThread* p;
        }

        public struct mCoreThread {
	        // Input
	        public IntPtr core; // struct mCore*

	        public mThreadLogger logger;
            public IntPtr startCallback;
            public IntPtr resetCallback;
            public IntPtr cleanCallback;
            public IntPtr frameCallback;
            public IntPtr sleepCallback;
            public IntPtr pauseCallback;
            public IntPtr unpauseCallback;
            public void* userData;
            public delegate void d_run(mCoreThread* driver);
            public IntPtr run;

            public IntPtr impl; // struct mCoreThreadInternal*
        }

        #endregion


        #region mSDLPlayer

        public struct CircleBuffer {
            public void* data;
            public IntPtr capacity;
            public IntPtr size;
            public void* readPtr;
            public void* writePtr;
        }

        public struct mRumble {
            // void (*setRumble)(struct mRumble*, int enable);
            public IntPtr setRumble;
        }

        public struct mRotationSource {
            // void (*sample)(struct mRotationSource*);
            public IntPtr sample;

            // int32_t (*readTiltX)(struct mRotationSource*);
            public IntPtr readTiltX;
            // int32_t (*readTiltY)(struct mRotationSource*);
            public IntPtr readTiltY;

            // int32_t(*readGyroZ)(struct mRotationSource*);
            public IntPtr readGyroZ;
        }

        public struct mSDLPlayer {
            public IntPtr playerId;
            public IntPtr bindings;
            public IntPtr joystick;
            public IntPtr window;
            public int fullscreen;
            public int windowUpdated;

            public struct mSDLRumble {
                public mRumble d;
                public IntPtr p;

                public int level;
                public float activeLevel;
                public CircleBuffer history;
            }
            public mSDLRumble rumble;

            public struct mSDLRotation {
                public mRotationSource d;
                public IntPtr p;

                // Tilt
                public int axisX;
                public int axisY;

                // Gyro
                public int gyroX;
                public int gyroY;
                public float gyroSensitivity;
                public CircleBuffer zHistory;
                public int oldX;
                public int oldY;
                public float zDelta;
            }
            public mSDLRotation rotation;
        }

        #endregion

        #region GBSIO

        public const int MAX_GBS = 2;

        private delegate void d_GBSIOInit(IntPtr sio);
        [DynamicDllImport(libmgba, "GBSIOInit")]
        private readonly static d_GBSIOInit INTERNAL_GBSIOInit;
        public static void GBSIOInit(GBSIO* sio) => INTERNAL_GBSIOInit((IntPtr) sio);
        private delegate void d_GBSIOReset(IntPtr sio);
        [DynamicDllImport(libmgba, "GBSIOReset")]
        private readonly static d_GBSIOReset INTERNAL_GBSIOReset;
        public static void GBSIOReset(GBSIO* sio) => INTERNAL_GBSIOReset((IntPtr) sio);
        private delegate void d_GBSIODeinit(IntPtr sio);
        [DynamicDllImport(libmgba, "GBSIODeinit")]
        private readonly static d_GBSIODeinit INTERNAL_GBSIODeinit;
        public static void GBSIODeinit(GBSIO* sio) => INTERNAL_GBSIODeinit((IntPtr) sio);
        private delegate void d_GBSIOSetDriver(IntPtr sio, IntPtr driver);
        [DynamicDllImport(libmgba, "GBSIOSetDriver")]
        private readonly static d_GBSIOSetDriver INTERNAL_GBSIOSetDriver;
        public static void GBSIOSetDriver(GBSIO* sio, GBSIODriver* driver) => INTERNAL_GBSIOSetDriver((IntPtr) sio, (IntPtr) driver);
        private delegate void d_GBSIOWriteSC(IntPtr sio, byte sc);
        [DynamicDllImport(libmgba, "GBSIOWriteSC")]
        private readonly static d_GBSIOWriteSC INTERNAL_GBSIOWriteSC;
        public static void GBSIOWriteSC(GBSIO* sio, byte sc) => INTERNAL_GBSIOWriteSC((IntPtr) sio, sc);
        private delegate void d_GBSIOWriteSB(IntPtr sio, byte sb);
        [DynamicDllImport(libmgba, "GBSIOWriteSB")]
        private readonly static d_GBSIOWriteSB INTERNAL_GBSIOWriteSB;
        public static void GBSIOWriteSB(GBSIO* sio, byte sb) => INTERNAL_GBSIOWriteSB((IntPtr) sio, sb);
        
        public struct GBSIODriver {
            public GBSIO* p;

            // public bool (*init)(struct GBSIODriver* driver);
            public delegate bool d_init(GBSIODriver* driver);
            public IntPtr init;
	        // public void (*deinit)(struct GBSIODriver* driver);
            public delegate void d_deinit(GBSIODriver* driver);
            public IntPtr deinit;
            // public void (*writeSB)(struct GBSIODriver* driver, uint8_t value);
            public delegate void d_writeSB(GBSIODriver* driver, byte value);
            public IntPtr writeSB;

            // public uint8_t(*writeSC)(struct GBSIODriver* driver, uint8_t value);
            public delegate byte d_writeSC(GBSIODriver* driver, byte value);
            public IntPtr writeSC;
        }

        public struct GBSIO {
            public IntPtr p;

            public mTimingEvent @event;

            public GBSIODriver* driver;

            public int nextEvent;
            public int period;
            public int remainingBits;

            public byte pendingSB;
        }

        #endregion

        #region Lockstep

        public enum mLockstepPhase {
	        TRANSFER_IDLE = 0,
	        TRANSFER_STARTING,
	        TRANSFER_STARTED,
	        TRANSFER_FINISHING,
	        TRANSFER_FINISHED
        }

        public struct mLockstep {
            public int attached;
            public mLockstepPhase transferActive;
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

        private delegate void d_mLockstepInit(IntPtr lockstep);
        [DynamicDllImport(libmgba, "mLockstepInit")]
        private readonly static d_mLockstepInit INTERNAL_mLockstepInit;
        public static void mLockstepInit(mLockstep* lockstep) => INTERNAL_mLockstepInit((IntPtr) lockstep);

        #endregion

        #region GBSIOLockstep

        public struct GBSIOLockstep {
            public mLockstep d;
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
	        public mTimingEvent @event;

            public volatile int nextEvent;
            public int eventDiff;
            public int id;
            public bool transferFinished;
            // #ifndef NDEBUG
            public int transferId;
            public mLockstepPhase phase;
            // #endif
        }

        private delegate void d_GBSIOLockstepInit(IntPtr lockstep);
        [DynamicDllImport(libmgba, "GBSIOLockstepInit")]
        private readonly static d_GBSIOLockstepInit INTERNAL_GBSIOLockstepInit;
        public static void GBSIOLockstepInit(GBSIOLockstep* lockstep) => INTERNAL_GBSIOLockstepInit((IntPtr) lockstep);
        private delegate void d_GBSIOLockstepNodeCreate(IntPtr node);
        [DynamicDllImport(libmgba, "GBSIOLockstepNodeCreate")]
        private readonly static d_GBSIOLockstepNodeCreate INTERNAL_GBSIOLockstepNodeCreate;
        public static void GBSIOLockstepNodeCreate(GBSIOLockstepNode* node) => INTERNAL_GBSIOLockstepNodeCreate((IntPtr) node);
        private delegate bool d_GBSIOLockstepAttachNode(IntPtr lockstep, IntPtr node);
        [DynamicDllImport(libmgba, "GBSIOLockstepAttachNode")]
        private readonly static d_GBSIOLockstepAttachNode INTERNAL_GBSIOLockstepAttachNode;
        public static bool GBSIOLockstepAttachNode(GBSIOLockstep* lockstep, GBSIOLockstepNode* node) => INTERNAL_GBSIOLockstepAttachNode((IntPtr) lockstep, (IntPtr) node);
        private delegate void d_GBSIOLockstepDetachNode(IntPtr lockstep, IntPtr node);
        [DynamicDllImport(libmgba, "GBSIOLockstepDetachNode")]
        private readonly static d_GBSIOLockstepDetachNode INTERNAL_GBSIOLockstepDetachNode;
        public static void GBSIOLockstepDetachNode(GBSIOLockstep* lockstep, GBSIOLockstepNode* node) => INTERNAL_GBSIOLockstepDetachNode((IntPtr) lockstep, (IntPtr) node);

        #endregion

    }
}
