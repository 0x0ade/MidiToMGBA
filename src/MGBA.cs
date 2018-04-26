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
        private delegate int d_mSDLMain(int argc, byte** argv);
        public static void mMain(params string[] args) {
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

        #region mTiming

        public struct mTimingEvent {
            public void* context;
            // void (*callback)(struct mTiming*, void* context, uint32_t);
            public delegate void d_callback(mTiming* timing, void* context, uint cyclesLate);
            public IntPtr _callback;
            public d_callback callback {
                get {
                    if (_callback == IntPtr.Zero)
                        return null;
                    return (d_callback) Marshal.GetDelegateForFunctionPointer(_callback, typeof(d_callback));
                }
                set {
                    if (value == null) {
                        _callback = IntPtr.Zero;
                        return;
                    }
                    _callback = Marshal.GetFunctionPointerForDelegate(value);
                }
            }
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

        [DllImport(libmgba, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mTimingSchedule")]
        private extern static void INTERNAL_mTimingSchedule(IntPtr timing, IntPtr @event, int when);
        public static void mTimingSchedule(mTiming* timing, mTimingEvent* @event, int when) => INTERNAL_mTimingSchedule((IntPtr) timing, (IntPtr) @event, when);
        [DllImport(libmgba, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mTimingDeschedule")]
        private extern static void INTERNAL_mTimingDeschedule(IntPtr timing, IntPtr @event);
        public static void mTimingDeschedule(mTiming* timing, mTimingEvent* @event) => INTERNAL_mTimingDeschedule((IntPtr) timing, (IntPtr) @event);
        [DllImport(libmgba, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mTimingIsScheduled")]
        private extern static bool INTERNAL_mTimingIsScheduled(IntPtr timing, IntPtr @event);
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
            public IntPtr _startCallback;
            public ThreadCallback startCallback {
                get {
                    if (_startCallback == IntPtr.Zero)
                        return null;
                    return (ThreadCallback) Marshal.GetDelegateForFunctionPointer(_startCallback, typeof(ThreadCallback));
                }
                set {
                    if (value == null) {
                        _startCallback = IntPtr.Zero;
                        return;
                    }
                    _startCallback = Marshal.GetFunctionPointerForDelegate(value);
                }
            }
            public IntPtr _resetCallback;
            public ThreadCallback resetCallback {
                get {
                    if (_resetCallback == IntPtr.Zero)
                        return null;
                    return (ThreadCallback) Marshal.GetDelegateForFunctionPointer(_resetCallback, typeof(ThreadCallback));
                }
                set {
                    if (value == null) {
                        _resetCallback = IntPtr.Zero;
                        return;
                    }
                    _resetCallback = Marshal.GetFunctionPointerForDelegate(value);
                }
            }
            public IntPtr _cleanCallback;
            public ThreadCallback cleanCallback {
                get {
                    if (_cleanCallback == IntPtr.Zero)
                        return null;
                    return (ThreadCallback) Marshal.GetDelegateForFunctionPointer(_cleanCallback, typeof(ThreadCallback));
                }
                set {
                    if (value == null) {
                        _cleanCallback = IntPtr.Zero;
                        return;
                    }
                    _cleanCallback = Marshal.GetFunctionPointerForDelegate(value);
                }
            }
            public IntPtr _frameCallback;
            public ThreadCallback frameCallback {
                get {
                    if (_frameCallback == IntPtr.Zero)
                        return null;
                    return (ThreadCallback) Marshal.GetDelegateForFunctionPointer(_frameCallback, typeof(ThreadCallback));
                }
                set {
                    if (value == null) {
                        _frameCallback = IntPtr.Zero;
                        return;
                    }
                    _frameCallback = Marshal.GetFunctionPointerForDelegate(value);
                }
            }
            public IntPtr _sleepCallback;
            public ThreadCallback sleepCallback {
                get {
                    if (_sleepCallback == IntPtr.Zero)
                        return null;
                    return (ThreadCallback) Marshal.GetDelegateForFunctionPointer(_sleepCallback, typeof(ThreadCallback));
                }
                set {
                    if (value == null) {
                        _sleepCallback = IntPtr.Zero;
                        return;
                    }
                    _sleepCallback = Marshal.GetFunctionPointerForDelegate(value);
                }
            }
            public IntPtr _pauseCallback;
            public ThreadCallback pauseCallback {
                get {
                    if (_pauseCallback == IntPtr.Zero)
                        return null;
                    return (ThreadCallback) Marshal.GetDelegateForFunctionPointer(_pauseCallback, typeof(ThreadCallback));
                }
                set {
                    if (value == null) {
                        _pauseCallback = IntPtr.Zero;
                        return;
                    }
                    _pauseCallback = Marshal.GetFunctionPointerForDelegate(value);
                }
            }
            public IntPtr _unpauseCallback;
            public ThreadCallback unpauseCallback {
                get {
                    if (_unpauseCallback == IntPtr.Zero)
                        return null;
                    return (ThreadCallback) Marshal.GetDelegateForFunctionPointer(_unpauseCallback, typeof(ThreadCallback));
                }
                set {
                    if (value == null) {
                        _unpauseCallback = IntPtr.Zero;
                        return;
                    }
                    _unpauseCallback = Marshal.GetFunctionPointerForDelegate(value);
                }
            }
            public void* userData;
            public delegate void d_run(mCoreThread* driver);
            public IntPtr _run;
            public d_run run {
                get {
                    if (_run == IntPtr.Zero)
                        return null;
                    return (d_run) Marshal.GetDelegateForFunctionPointer(_run, typeof(d_run));
                }
                set {
                    if (value == null) {
                        _run = IntPtr.Zero;
                        return;
                    }
                    _run = Marshal.GetFunctionPointerForDelegate(value);
                }
            }

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
                    if (_init == IntPtr.Zero)
                        return null;
                    return (d_init) Marshal.GetDelegateForFunctionPointer(_init, typeof(d_init));
                }
                set {
                    if (value == null) {
                        _init = IntPtr.Zero;
                        return;
                    }
                    _init = Marshal.GetFunctionPointerForDelegate(value);
                }
            }
	        // public void (*deinit)(struct GBSIODriver* driver);
            public delegate void d_deinit(GBSIODriver* driver);
            public IntPtr _deinit;
            public d_deinit deinit {
                get {
                    if (_deinit == IntPtr.Zero)
                        return null;
                    return (d_deinit) Marshal.GetDelegateForFunctionPointer(_deinit, typeof(d_deinit));
                }
                set {
                    if (value == null) {
                        _deinit = IntPtr.Zero;
                        return;
                    }
                    _deinit = Marshal.GetFunctionPointerForDelegate(value);
                }
            }
            // public void (*writeSB)(struct GBSIODriver* driver, uint8_t value);
            public delegate void d_writeSB(GBSIODriver* driver, byte value);
            public IntPtr _writeSB;
            public d_writeSB writeSB {
                get {
                    if (_writeSB == IntPtr.Zero)
                        return null;
                    return (d_writeSB) Marshal.GetDelegateForFunctionPointer(_writeSB, typeof(d_writeSB));
                }
                set {
                    if (value == null) {
                        _writeSB = IntPtr.Zero;
                        return;
                    }
                    _writeSB = Marshal.GetFunctionPointerForDelegate(value);
                }
            }

            // public uint8_t(*writeSC)(struct GBSIODriver* driver, uint8_t value);
            public delegate byte d_writeSC(GBSIODriver* driver, byte value);
            public IntPtr _writeSC;
            public d_writeSC writeSC {
                get {
                    if (_writeSC == IntPtr.Zero)
                        return null;
                    return (d_writeSC) Marshal.GetDelegateForFunctionPointer(_writeSC, typeof(d_writeSC));
                }
                set {
                    if (value == null) {
                        _writeSC = IntPtr.Zero;
                        return;
                    }
                    _writeSC = Marshal.GetFunctionPointerForDelegate(value);
                }
            }
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

        [DllImport(libmgba, CallingConvention = CallingConvention.Cdecl, EntryPoint = "mLockstepInit")]
        private static extern void INTERNAL_mLockstepInit(IntPtr lockstep);
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
