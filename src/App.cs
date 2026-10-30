using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Chutouniao
{
    /// <summary>隐藏的消息窗口：接收全局热键，以及第二个实例发来的“打开设置”请求</summary>
    internal class HiddenWindow : NativeWindow
    {
        public const int WM_HOTKEY = 0x0312;
        public const int WM_SHOW_SETTINGS = 0x8001;

        public event EventHandler HotkeyPressed;
        public event EventHandler ShowSettingsRequested;

        public HiddenWindow()
        {
            CreateParams cp = new CreateParams();
            cp.Caption = "ChutouniaoMessageWindow";
            cp.Parent = new IntPtr(-3); // HWND_MESSAGE：只收消息，不出现在任务栏/Alt-Tab
            CreateHandle(cp);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                EventHandler h = HotkeyPressed;
                if (h != null) h(this, EventArgs.Empty);
            }
            else if (m.Msg == WM_SHOW_SETTINGS)
            {
                EventHandler h = ShowSettingsRequested;
                if (h != null) h(this, EventArgs.Empty);
            }
            base.WndProc(ref m);
        }
    }

    internal class App : ApplicationContext
    {
        [DllImport("user32.dll")]
        static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")]
        static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("kernel32.dll")]
        static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")]
        static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
        [DllImport("user32.dll")]
        static extern bool BringWindowToTop(IntPtr hWnd);

        const int HotkeyId = 0x5053;
        const uint MOD_ALT = 0x0001, MOD_CONTROL = 0x0002, MOD_SHIFT = 0x0004, MOD_WIN = 0x0008;

        public const string ShowSettingsEventName = @"Local\Chutouniao.ShowSettings";

        public Config Cfg;
        public Interceptor Engine;

        NotifyIcon _tray;
        HiddenWindow _hw;
        SettingsForm _form;
        ToolStripMenuItem _pauseItem;
        ToolStripMenuItem _countItem;
        System.Windows.Forms.Timer _flashTimer;
        bool _hotkeyOk;
        bool _exiting;

        public App()
        {
            Config.MigrateLegacyInstall(); // 先把旧名字留下的配置/启动项搬过来
            bool firstRun = !System.IO.File.Exists(Config.ConfigPath);
            Cfg = Config.Load();
            if (firstRun) Cfg.Save(); // 首次运行就把默认配置写出来，方便用户找
            if (Config.IsStartupEnabled() != Cfg.StartWithWindows) Config.SetStartup(Cfg.StartWithWindows);

            Engine = new Interceptor();
            Engine.Blocked += OnBlocked;
            ApplyConfig();
            Engine.Install();

            _hw = new HiddenWindow();
            _hw.HotkeyPressed += delegate { TogglePause(); };
            _hw.ShowSettingsRequested += delegate { ShowSettings(); };
            RegisterHotkey();

            BuildTray();
            StartIpcListener();

            if (firstRun) _tray.ShowBalloonTip(5000, "出头鸟已在后台运行",
                "Photoshop 里的 Ctrl 拖拽、Ctrl+Alt 拖拽已被拦掉。双击托盘图标可以改设置。",
                ToolTipIcon.Info);
        }

        // ---------------- 配置应用 ----------------

        public void ApplyConfig()
        {
            Engine.Profiles.Clear();
            foreach (Profile p in Cfg.Profiles) Engine.Profiles[p.Name] = p;
            Engine.ReplayClick = Cfg.ReplayClick;
            Engine.ClickThreshold = Cfg.ClickThreshold;
            UpdateTrayText();
        }

        public bool RegisterHotkey()
        {
            if (_hw == null) return false;
            UnregisterHotKey(_hw.Handle, HotkeyId);
            _hotkeyOk = false;

            bool c, a, s, w;
            string last;
            if (RuleFormat.TryParseCombo(Cfg.Hotkey, out c, out a, out s, out w, out last))
            {
                uint vk;
                if (RuleFormat.TryKeyCode(last, out vk))
                {
                    uint mods = 0;
                    if (c) mods |= MOD_CONTROL;
                    if (a) mods |= MOD_ALT;
                    if (s) mods |= MOD_SHIFT;
                    if (w) mods |= MOD_WIN;
                    _hotkeyOk = RegisterHotKey(_hw.Handle, HotkeyId, mods, vk);
                }
            }

            if (_pauseItem != null)
                _pauseItem.ShortcutKeyDisplayString = _hotkeyOk ? Cfg.Hotkey : "";
            UpdateTrayText();
            return _hotkeyOk;
        }

        // ---------------- 托盘 ----------------

        void BuildTray()
        {
            var menu = new ContextMenuStrip();

            var settings = new ToolStripMenuItem("设置…");
            settings.Font = new Font(settings.Font, FontStyle.Bold);
            settings.Click += delegate { ShowSettings(); };

            _pauseItem = new ToolStripMenuItem("暂停拦截");
            _pauseItem.Click += delegate { TogglePause(); };

            _countItem = new ToolStripMenuItem("本次已拦截 0 次");
            _countItem.Enabled = false;

            var openCfg = new ToolStripMenuItem("打开配置文件夹");
            openCfg.Click += delegate { OpenConfigFolder(); };

            var about = new ToolStripMenuItem("关于");
            about.Click += delegate { ShowAbout(); };

            var exit = new ToolStripMenuItem("退出");
            exit.Click += delegate { ExitApp(); };

            menu.Items.Add(settings);
            menu.Items.Add(_pauseItem);
            menu.Items.Add(_countItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(openCfg);
            menu.Items.Add(about);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exit);

            _tray = new NotifyIcon();
            _tray.Icon = Icon;
            _tray.Text = "出头鸟 Chutouniao";
            _tray.ContextMenuStrip = menu;
            _tray.Visible = true;
            _tray.DoubleClick += delegate { ShowSettings(); };

            _flashTimer = new System.Windows.Forms.Timer();
            _flashTimer.Interval = 220;
            _flashTimer.Tick += delegate
            {
                _flashTimer.Stop();
                _tray.Icon = Icon;
            };
        }

        static Icon Icon
        {
            get { return SystemIcons.Shield; }
        }

        void OnBlocked(object sender, EventArgs e)
        {
            Cfg.TotalBlocked++;
            _countItem.Text = "本次已拦截 " + Engine.SessionBlocked + " 次 · 累计 " + Cfg.TotalBlocked;
            UpdateTrayText();

            if (Cfg.Flash)
            {
                _tray.Icon = SystemIcons.Warning;
                _flashTimer.Stop();
                _flashTimer.Start();
            }
            if (Cfg.Beep)
            {
                try { System.Media.SystemSounds.Asterisk.Play(); } catch { }
            }
        }

        void UpdateTrayText()
        {
            if (_tray == null) return;
            string state = Engine != null && Engine.Paused ? "已暂停" : "拦截中";
            _tray.Text = "出头鸟 · " + state + " · 本次 " +
                         (Engine == null ? 0 : Engine.SessionBlocked) + " 次";
        }

        public void TogglePause()
        {
            Engine.Paused = !Engine.Paused;
            _pauseItem.Checked = Engine.Paused;
            _pauseItem.Text = Engine.Paused ? "恢复拦截" : "暂停拦截";
            UpdateTrayText();
        }

        public void ShowSettings()
        {
            if (_form != null && !_form.IsDisposed)
            {
                _form.Activate();
                _form.BringToFront();
                return;
            }
            _form = new SettingsForm(this);
            _form.FormClosed += delegate
            {
                Cfg.Save();
                _form = null;
            };
            _form.Show();
            BringToFrontForcefully(_form.Handle);
        }

        /// <summary>后台进程直接 Activate() 常被 Windows 忽略，这里用 AttachThreadInput + TopMost 组合把它提到最前</summary>
        void BringToFrontForcefully(IntPtr hWnd)
        {
            try
            {
                _form.TopMost = true;
                _form.Activate();
                _form.TopMost = false;

                IntPtr fg = GetForegroundWindow();
                uint pid;
                uint fgThread = GetWindowThreadProcessId(fg, out pid);
                uint myThread = GetCurrentThreadId();
                bool attached = AttachThreadInput(myThread, fgThread, true);
                SetForegroundWindow(hWnd);
                BringWindowToTop(hWnd);
                if (attached) AttachThreadInput(myThread, fgThread, false);
            }
            catch { }
        }

        void ShowAbout()
        {
            MessageBox.Show(
                "出头鸟 Chutouniao " + Program.Version + "\n\n" +
                "「世界若无出头鸟，人人皆为待宰鸡」\n\n" +
                "把「修饰键 + 拖拽 / 修饰键 + 按键」这类改不掉的输入\n" +
                "在系统层面拦下来，或者换绑成你按得顺手的键。\n" +
                "可以按程序分别配置，互不干扰。\n\n" +
                "托盘图标：双击打开设置，右键看菜单。\n" +
                "配置文件：" + Config.ConfigPath + "\n\n" +
                "© 2026 墨琛COMU（墨琛_COMU / MochenCOMU）\n" +
                Program.HomeUrl + "\n" +
                "MIT License · 随便用、随便改，记得署名",
                "关于 出头鸟 Chutouniao", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public void OpenConfigFolder()
        {
            try
            {
                string dir = System.IO.Path.GetDirectoryName(Config.ConfigPath);
                if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
                System.Diagnostics.Process.Start("explorer.exe", "\"" + dir + "\"");
            }
            catch { }
        }

        // ---------------- 单实例：第二次启动时唤起设置 ----------------

        void StartIpcListener()
        {
            var th = new Thread(delegate()
            {
                try
                {
                    using (EventWaitHandle ev = EventWaitHandle.OpenExisting(ShowSettingsEventName))
                    {
                        while (true)
                        {
                            ev.WaitOne();
                            if (_exiting) break;
                            PostMessage(_hw.Handle, HiddenWindow.WM_SHOW_SETTINGS, IntPtr.Zero, IntPtr.Zero);
                        }
                    }
                }
                catch { }
            });
            th.IsBackground = true;
            th.Start();
        }

        // ---------------- 退出 ----------------

        public void ExitApp()
        {
            _exiting = true;
            try { Cfg.Save(); } catch { }
            ExitThread();
        }

        protected override void ExitThreadCore()
        {
            try { if (_tray != null) { _tray.Visible = false; _tray.Dispose(); } } catch { }
            try { if (_hw != null) { UnregisterHotKey(_hw.Handle, HotkeyId); _hw.DestroyHandle(); } } catch { }
            try { if (Engine != null) Engine.Uninstall(); } catch { }
            base.ExitThreadCore();
        }
    }
}
