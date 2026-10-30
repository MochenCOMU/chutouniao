using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Chutouniao
{
    /// <summary>
    /// 底层钩子：鼠标（拦截「修饰键 + 拖拽」）和键盘（拦截「修饰键 + 按键」）。
    /// 只在目标程序处于前台时生效。
    /// </summary>
    internal class Interceptor
    {
        const int WH_MOUSE_LL = 14;
        const int WH_KEYBOARD_LL = 13;

        const int WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202;
        const int WM_RBUTTONDOWN = 0x0204, WM_RBUTTONUP = 0x0205;
        const int WM_MBUTTONDOWN = 0x0207, WM_MBUTTONUP = 0x0208;
        const int WM_KEYDOWN = 0x0100, WM_SYSKEYDOWN = 0x0104;
        const int WM_KEYUP = 0x0101, WM_SYSKEYUP = 0x0105;

        const uint MAGIC = 0x50534346; // 'PSCF'：标记自己合成的鼠标事件

        const int VK_CONTROL = 0x11, VK_MENU = 0x12, VK_SHIFT = 0x10, VK_LWIN = 0x5B, VK_RWIN = 0x5C;

        [StructLayout(LayoutKind.Sequential)]
        struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public int mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SetWindowsHookEx(int idHook, LowLevelProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")]
        static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll")]
        static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);
        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        const uint KEYEVENTF_KEYUP = 0x0002;
        const byte VK_LSHIFT = 0xA0, VK_RSHIFT = 0xA1, VK_LCONTROL = 0xA2, VK_RCONTROL = 0xA3,
                   VK_LMENU = 0xA4, VK_RMENU = 0xA5;

        // ---- 运行参数 ----
        public Dictionary<string, Profile> Profiles =
            new Dictionary<string, Profile>(StringComparer.OrdinalIgnoreCase);
        public bool ReplayClick = true;
        public int ClickThreshold = 5;
        public bool Paused;

        public long SessionBlocked;

        /// <summary>最近一次拦截的说明，设置界面会显示，方便确认真的生效了</summary>
        public string LastBlockedText = "";

        /// <summary>每拦下一次操作触发一次（在 UI 线程上）</summary>
        public event EventHandler Blocked;

        // 排查用：环境变量 PSCG_DEBUG=1 时，把每次拦截写进 %TEMP%\psctrlguard.log
        static readonly bool DebugOn = Environment.GetEnvironmentVariable("PSCG_DEBUG") == "1";
        static readonly string DebugPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "psctrlguard.log");

        internal static void Dbg(string msg)
        {
            if (!DebugOn) return;
            try { System.IO.File.AppendAllText(DebugPath, DateTime.Now.ToString("HH:mm:ss.fff") + " " + msg + Environment.NewLine); }
            catch { }
        }

        IntPtr _mouseHook, _keyHook;
        LowLevelProc _mouseProc, _keyProc;

        bool _suppressing;
        int _suppressButton;
        int _sx, _sy;

        readonly HashSet<uint> _swallowedKeys = new HashSet<uint>();

        // 转发队列：钩子里只负责入队，真正的按键注入放到消息循环里做（避免在钩子里 SendInput）
        struct ForwardJob { public bool C, A, S, W; public uint VK; }
        readonly Queue<ForwardJob> _jobs = new Queue<ForwardJob>();
        System.Threading.SynchronizationContext _sync;

        IntPtr _cacheHwnd = IntPtr.Zero;
        Profile _cacheProfile;

        public bool IsInstalled { get { return _mouseHook != IntPtr.Zero; } }

        public bool Install()
        {
            if (_mouseHook != IntPtr.Zero) return true;
            _mouseProc = MouseProc;
            _keyProc = KeyProc;
            IntPtr hMod = GetModuleHandle(null);
            _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, hMod, 0);
            _keyHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyProc, hMod, 0);
            _sync = System.Threading.SynchronizationContext.Current;
            Dbg("sync context: " + (_sync == null ? "none（转发会同步执行）" : _sync.GetType().Name));
            return _mouseHook != IntPtr.Zero;
        }

        public void Uninstall()
        {
            try { if (_mouseHook != IntPtr.Zero) UnhookWindowsHookEx(_mouseHook); } catch { }
            try { if (_keyHook != IntPtr.Zero) UnhookWindowsHookEx(_keyHook); } catch { }
            _mouseHook = IntPtr.Zero;
            _keyHook = IntPtr.Zero;
        }

        public int LastError { get { return Marshal.GetLastWin32Error(); } }

        public static string ForegroundProcessName()
        {
            try
            {
                IntPtr h = GetForegroundWindow();
                uint pid;
                GetWindowThreadProcessId(h, out pid);
                if (pid == 0) return "";
                using (Process p = Process.GetProcessById((int)pid)) return p.ProcessName;
            }
            catch { return ""; }
        }

        static bool IsDown(int vk)
        {
            return (GetAsyncKeyState(vk) & 0x8000) != 0;
        }

        Profile ForegroundProfile()
        {
            IntPtr h = GetForegroundWindow();
            if (h == _cacheHwnd && h != IntPtr.Zero) return _cacheProfile;

            Profile found = null;
            try
            {
                uint pid;
                GetWindowThreadProcessId(h, out pid);
                if (pid != 0)
                {
                    using (Process p = Process.GetProcessById((int)pid))
                    {
                        Profile prof;
                        if (Profiles.TryGetValue(p.ProcessName, out prof)) found = prof;
                    }
                }
            }
            catch { found = null; }

            _cacheHwnd = h;
            _cacheProfile = found;
            return found;
        }

        void RaiseBlocked()
        {
            SessionBlocked++;
            EventHandler h = Blocked;
            if (h != null) h(this, EventArgs.Empty);
        }

        // ---------------- 鼠标 ----------------

        static int ButtonIndexOf(int msg)
        {
            if (msg == WM_LBUTTONDOWN || msg == WM_LBUTTONUP) return 0;
            if (msg == WM_MBUTTONDOWN || msg == WM_MBUTTONUP) return 1;
            return 2;
        }

        static bool IsDownMsg(int msg)
        {
            return msg == WM_LBUTTONDOWN || msg == WM_MBUTTONDOWN || msg == WM_RBUTTONDOWN;
        }

        IntPtr MouseProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0)
                {
                    MSLLHOOKSTRUCT info =
                        (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));

                    if ((uint)info.dwExtraInfo.ToInt64() != MAGIC)
                    {
                        int msg = wParam.ToInt32();

                        if (IsDownMsg(msg))
                        {
                            if (!Paused && !_suppressing)
                            {
                                Profile p = ForegroundProfile();
                                if (p != null)
                                {
                                    bool ctrl = IsDown(VK_CONTROL);
                                    bool alt = IsDown(VK_MENU);
                                    bool shift = IsDown(VK_SHIFT);
                                    bool win = IsDown(VK_LWIN) || IsDown(VK_RWIN);
                                    int btn = ButtonIndexOf(msg);

                                    for (int i = 0; i < p.Mouse.Count; i++)
                                    {
                                        MouseRule r = p.Mouse[i];
                                        if (r.Button != btn) continue;
                                        if (r.Ctrl != ctrl || r.Alt != alt || r.Shift != shift || r.Win != win) continue;

                                        _suppressing = true;
                                        _suppressButton = btn;
                                        _sx = info.pt.X;
                                        _sy = info.pt.Y;
                                        LastBlockedText = p.Name + " · 拦住 " + r.ToString();
                                        RaiseBlocked();
                                        Dbg("block mouse btn=" + btn + " ctrl=" + ctrl + " alt=" + alt + " shift=" + shift + " win=" + win);
                                        return (IntPtr)1;
                                    }
                                }
                            }
                        }
                        else if (_suppressing)
                        {
                            _suppressing = false;
                            int dx = Math.Abs(info.pt.X - _sx);
                            int dy = Math.Abs(info.pt.Y - _sy);
                            if (ReplayClick && dx <= ClickThreshold && dy <= ClickThreshold)
                                SendClick(_suppressButton);
                            return (IntPtr)1;
                        }
                    }
                }
            }
            catch { }
            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        void SendClick(int button)
        {
            uint downFlag = 0x0002, upFlag = 0x0004;
            if (button == 1) { downFlag = 0x0020; upFlag = 0x0040; }
            else if (button == 2) { downFlag = 0x0008; upFlag = 0x0010; }

            mouse_event(downFlag, 0, 0, 0, new UIntPtr(MAGIC));
            mouse_event(upFlag, 0, 0, 0, new UIntPtr(MAGIC));
        }

        // ---------------- 键盘 ----------------

        IntPtr KeyProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0)
                {
                    KBDLLHOOKSTRUCT k =
                        (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                    int msg = wParam.ToInt32();

                    // 自己注入的按键不再参与匹配，否则会自己拦自己
                    if ((uint)k.dwExtraInfo.ToInt64() == MAGIC)
                        return CallNextHookEx(_keyHook, nCode, wParam, lParam);

                    if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                    {
                        if (!Paused)
                        {
                            Profile p = ForegroundProfile();
                            if (p != null && p.Keys.Count > 0)
                            {
                                bool ctrl = IsDown(VK_CONTROL);
                                bool alt = IsDown(VK_MENU);
                                bool shift = IsDown(VK_SHIFT);
                                bool win = IsDown(VK_LWIN) || IsDown(VK_RWIN);

                                for (int i = 0; i < p.Keys.Count; i++)
                                {
                                    KeyRule r = p.Keys[i];
                                    if (r.VK != k.vkCode) continue;
                                    if (r.Ctrl != ctrl || r.Alt != alt || r.Shift != shift || r.Win != win) continue;

                                    LastBlockedText = p.Name + " · " + r.ToString();
                                    bool first = _swallowedKeys.Add(k.vkCode);
                                    if (first)
                                    {
                                        RaiseBlocked();
                                        if (r.HasTarget) QueueForward(r);
                                    }
                                    Dbg("block key vk=0x" + k.vkCode.ToString("X2") + " ctrl=" + ctrl + " alt=" + alt + " shift=" + shift + " win=" + win);
                                    return (IntPtr)1;
                                }
                            }
                        }
                    }
                    else if (msg == WM_KEYUP || msg == WM_SYSKEYUP)
                    {
                        if (_swallowedKeys.Remove(k.vkCode))
                            return (IntPtr)1;
                    }
                }
            }
            catch { }
            return CallNextHookEx(_keyHook, nCode, wParam, lParam);
        }

        // ---------------- 快捷键转发 ----------------
        //
        // 把「按下的原组合」换成「用户指定的新组合」重新注入一次。
        // 关键点：物理按住但目标组合不需要的修饰键，要先替用户「松开」，注入完再「按回去」；
        // 目标组合需要而手上没有的修饰键，临时按下再松开。
        // 所有注入事件都带 MAGIC 标记，本钩子会跳过，不会递归。

        void QueueForward(KeyRule r)
        {
            var job = new ForwardJob { C = r.TCtrl, A = r.TAlt, S = r.TShift, W = r.TWin, VK = r.TVK };
            lock (_jobs) _jobs.Enqueue(job);

            if (_sync != null)
                _sync.Post(delegate { PumpForward(); }, null);
            else
                PumpForward();
        }

        void PumpForward()
        {
            ForwardJob job;
            lock (_jobs)
            {
                if (_jobs.Count == 0) return;
                job = _jobs.Dequeue();
            }
            try { SendCombo(job); }
            catch { }
        }

        static void KeyDown(byte vk) { keybd_event(vk, 0, 0, new UIntPtr(MAGIC)); }
        static void KeyUp(byte vk) { keybd_event(vk, 0, KEYEVENTF_KEYUP, new UIntPtr(MAGIC)); }

        static byte HeldVariant(byte generic, byte left, byte right)
        {
            if ((GetAsyncKeyState(left) & 0x8000) != 0) return left;
            if ((GetAsyncKeyState(right) & 0x8000) != 0) return right;
            return left;
        }

        void SendCombo(ForwardJob job)
        {
            bool hC = IsDown(VK_CONTROL), hA = IsDown(VK_MENU),
                 hS = IsDown(VK_SHIFT), hW = IsDown(VK_LWIN) || IsDown(VK_RWIN);

            var toRelease = new List<byte>(); // 物理按着、但目标不需要
            var toPress = new List<byte>();   // 目标需要、但手上没有

            if (hC && !job.C) toRelease.Add(HeldVariant(VK_CONTROL, VK_LCONTROL, VK_RCONTROL));
            if (hA && !job.A) toRelease.Add(HeldVariant(VK_MENU, VK_LMENU, VK_RMENU));
            if (hS && !job.S) toRelease.Add(HeldVariant(VK_SHIFT, VK_LSHIFT, VK_RSHIFT));
            if (hW && !job.W) toRelease.Add(HeldVariant(VK_LWIN, VK_LWIN, VK_RWIN));

            if (!hC && job.C) toPress.Add(VK_LCONTROL);
            if (!hA && job.A) toPress.Add(VK_LMENU);
            if (!hS && job.S) toPress.Add(VK_LSHIFT);
            if (!hW && job.W) toPress.Add(VK_LWIN);

            foreach (byte v in toRelease) KeyUp(v);
            foreach (byte v in toPress) KeyDown(v);

            byte target = (byte)job.VK;
            KeyDown(target);
            KeyUp(target);

            foreach (byte v in toPress) KeyUp(v);
            foreach (byte v in toRelease) KeyDown(v);

            Dbg("forward -> " + RuleFormat.KeyCombo(job.C, job.A, job.S, job.W, job.VK) +
                " (released " + toRelease.Count + ", pressed " + toPress.Count + ")");
        }
    }
}
