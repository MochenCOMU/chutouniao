using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Chutouniao
{
    internal class SettingsForm : Form
    {
        class Preset
        {
            public string Title;
            public string Process;
            public string[] Mouse;
        }

        static readonly Preset[] Presets =
        {
            new Preset { Title = "Photoshop —— Ctrl 拖拽移动 / Ctrl+Alt 拖拽复制并移动",
                         Process = "Photoshop", Mouse = new[] { "Ctrl+Left", "Ctrl+Alt+Left" } },
            new Preset { Title = "文件资源管理器 —— Ctrl 拖拽=复制文件",
                         Process = "explorer", Mouse = new[] { "Ctrl+Left" } },
            new Preset { Title = "Maya —— Alt+左/中/右键=旋转/平移/缩放",
                         Process = "maya", Mouse = new[] { "Alt+Left", "Alt+Middle", "Alt+Right" } },
            new Preset { Title = "3ds Max —— Shift 拖拽=克隆对象",
                         Process = "3dsmax", Mouse = new[] { "Shift+Left" } },
            new Preset { Title = "Figma —— Alt 拖拽=复制",
                         Process = "Figma", Mouse = new[] { "Alt+Left" } }
        };

        readonly App _app;
        readonly List<Profile> _profiles = new List<Profile>();
        readonly Timer _timer;

        ListBox _lstProfiles, _lstMouse, _lstKeys;
        CheckBox _chkC, _chkA, _chkS, _chkW;
        CheckBox _chkReplay, _chkBeep, _chkFlash, _chkStartup;
        ComboBox _cboButton;
        NumericUpDown _numThreshold;
        HotkeyBox _hkRule, _hkTarget, _hkPause;
        CheckBox _chkForward;
        Label _lblCurrent, _lblStats, _lblConfigPath, _lblForeground, _lblLast;
        GroupBox _gbMouse, _gbKeys;
        Button _btnAddM, _btnDelM, _btnAddK, _btnDelK;

        public SettingsForm(App app)
        {
            _app = app;

            Text = "出头鸟 Chutouniao " + Program.Version + " · 设置";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(900, 686);
            BackColor = Color.FromArgb(250, 250, 252);

            try { Font = new Font("Microsoft YaHei UI", 9F); }
            catch { Font = SystemFonts.MessageBoxFont; }
            try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { }

            var tabs = new TabControl();
            tabs.SetBounds(12, 12, 876, 588);
            tabs.Font = Font;
            tabs.TabPages.Add(BuildProgramsTab());
            tabs.TabPages.Add(BuildDetailsTab());
            tabs.TabPages.Add(BuildGeneralTab());
            Controls.Add(tabs);

            var btnReset = new Button();
            btnReset.Text = "恢复默认";
            btnReset.SetBounds(12, 612, 96, 32);
            btnReset.Click += delegate { LoadDefaults(); };
            Controls.Add(btnReset);

            var btnOk = new Button();
            btnOk.Text = "保存";
            btnOk.SetBounds(668, 612, 96, 32);
            btnOk.Click += delegate { SaveAndClose(); };
            Controls.Add(btnOk);

            var btnCancel = new Button();
            btnCancel.Text = "取消";
            btnCancel.SetBounds(772, 612, 94, 32);
            btnCancel.Click += delegate { Close(); };
            Controls.Add(btnCancel);

            // 底部版权条：这一页用户最常打开，署名放这儿
            var copy = new Label();
            copy.Text = Program.Name + " v" + Program.Version + "　·　" + Program.Copyright + "　·　MIT License";
            copy.SetBounds(12, 654, 620, 20);
            copy.ForeColor = Color.FromArgb(140, 140, 150);
            Controls.Add(copy);

            var home = new LinkLabel();
            home.Text = Program.HomeUrl.Replace("https://", "");
            home.SetBounds(636, 654, 252, 20);
            home.TextAlign = ContentAlignment.MiddleRight;
            home.LinkColor = Color.FromArgb(0, 102, 204);
            home.LinkBehavior = LinkBehavior.HoverUnderline;
            home.Click += delegate
            {
                try { System.Diagnostics.Process.Start(Program.HomeUrl); }
                catch { }
            };
            Controls.Add(home);

            AcceptButton = btnOk;
            CancelButton = null; // 捕获按键的控件要能吃到 Esc，交给它自己处理

            LoadFromConfig();

            _timer = new Timer();
            _timer.Interval = 600;
            _timer.Tick += delegate { RefreshStatus(); };
            _timer.Start();
            RefreshStatus();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _timer.Stop();
            _timer.Dispose();
            base.OnFormClosed(e);
        }

        // ================= 界面构建 =================

        TabPage NewPage(string title)
        {
            var p = new TabPage(title);
            p.BackColor = Color.White;
            return p;
        }

        CheckBox NewCheck(string text, int x, int y, int w)
        {
            var c = new CheckBox();
            c.Text = text;
            c.SetBounds(x, y, w, 22);
            return c;
        }

        Button NewButton(string text, int x, int y, int w, int h)
        {
            var b = new Button();
            b.Text = text;
            b.SetBounds(x, y, w, h);
            return b;
        }

        TabPage BuildProgramsTab()
        {
            var p = NewPage("程序与规则");

            var l1 = new Label();
            l1.Text = "目标程序（进程名，不含 .exe）";
            l1.SetBounds(10, 8, 300, 20);
            p.Controls.Add(l1);

            _lstProfiles = new ListBox();
            _lstProfiles.SetBounds(10, 30, 300, 340);
            _lstProfiles.IntegralHeight = false;
            _lstProfiles.SelectedIndexChanged += delegate { OnProfileChanged(); };
            p.Controls.Add(_lstProfiles);

            var btnAddApp = NewButton("添加程序…", 10, 376, 145, 30);
            btnAddApp.Click += delegate { PickRunning(); };
            p.Controls.Add(btnAddApp);

            var btnPreset = NewButton("添加预设 ▾", 159, 376, 151, 30);
            btnPreset.Click += delegate { ShowPresetMenu(btnPreset); };
            p.Controls.Add(btnPreset);

            var btnDel = NewButton("删除选中的程序", 10, 412, 300, 30);
            btnDel.Click += delegate { DeleteProfile(); };
            p.Controls.Add(btnDel);

            var hint = new Label();
            hint.Text = "左边选一个程序，右边为它单独配规则。规则只对这个程序生效，不会影响别的软件。";
            hint.SetBounds(10, 448, 300, 80);
            hint.ForeColor = Color.FromArgb(110, 110, 120);
            p.Controls.Add(hint);

            // ---- 右栏 ----
            _lblCurrent = new Label();
            _lblCurrent.SetBounds(326, 8, 530, 20);
            _lblCurrent.Text = "当前程序：—";
            _lblCurrent.Font = new Font(Font, FontStyle.Bold);
            p.Controls.Add(_lblCurrent);

            _gbMouse = new GroupBox();
            _gbMouse.Text = "鼠标拖拽（按住这些键再拖 → 整个拖拽失效）";
            _gbMouse.SetBounds(326, 30, 530, 206);
            p.Controls.Add(_gbMouse);

            _chkC = NewCheck("Ctrl", 10, 26, 60);
            _chkA = NewCheck("Alt", 74, 26, 52);
            _chkS = NewCheck("Shift", 130, 26, 62);
            _chkW = NewCheck("Win", 196, 26, 56);
            _gbMouse.Controls.Add(_chkC);
            _gbMouse.Controls.Add(_chkA);
            _gbMouse.Controls.Add(_chkS);
            _gbMouse.Controls.Add(_chkW);

            _cboButton = new ComboBox();
            _cboButton.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboButton.SetBounds(262, 24, 96, 24);
            _cboButton.Items.AddRange(new object[] { "左键", "中键", "右键" });
            _cboButton.SelectedIndex = 0;
            _gbMouse.Controls.Add(_cboButton);

            _btnAddM = NewButton("添加拖拽规则", 366, 22, 150, 28);
            _btnAddM.Click += delegate { AddMouseRule(); };
            _gbMouse.Controls.Add(_btnAddM);

            _lstMouse = new ListBox();
            _lstMouse.SetBounds(10, 58, 510, 96);
            _lstMouse.IntegralHeight = false;
            _gbMouse.Controls.Add(_lstMouse);

            _btnDelM = NewButton("删除选中", 10, 162, 120, 26);
            _btnDelM.Click += delegate { RemoveSelected(_lstMouse); };
            _gbMouse.Controls.Add(_btnDelM);

            _lblForeground = new Label();
            _lblForeground.SetBounds(140, 166, 380, 20);
            _lblForeground.ForeColor = Color.FromArgb(110, 110, 120);
            _gbMouse.Controls.Add(_lblForeground);

            _gbKeys = new GroupBox();
            _gbKeys.Text = "键盘快捷键（拦下原组合，可以转发成别的键）";
            _gbKeys.SetBounds(326, 246, 530, 270);
            p.Controls.Add(_gbKeys);

            _hkRule = new HotkeyBox();
            _hkRule.SetBounds(10, 26, 290, 28);
            _gbKeys.Controls.Add(_hkRule);

            _btnAddK = NewButton("添加", 308, 26, 90, 28);
            _btnAddK.Click += delegate { AddKeyRule(); };
            _gbKeys.Controls.Add(_btnAddK);

            var k1 = new Label();
            k1.Text = "先按下要拦的组合";
            k1.SetBounds(406, 30, 118, 20);
            k1.ForeColor = Color.FromArgb(110, 110, 120);
            _gbKeys.Controls.Add(k1);

            _chkForward = new CheckBox();
            _chkForward.Text = "转发为";
            _chkForward.SetBounds(10, 66, 90, 22);
            _chkForward.CheckedChanged += delegate
            {
                _hkTarget.Enabled = _chkForward.Checked && _gbKeys.Enabled;
                if (!_chkForward.Checked) { _hkTarget.Clear(); }
            };
            _gbKeys.Controls.Add(_chkForward);

            _hkTarget = new HotkeyBox();
            _hkTarget.SetBounds(104, 64, 290, 28);
            _hkTarget.Enabled = false;
            _gbKeys.Controls.Add(_hkTarget);

            var k2 = new Label();
            k2.Text = "按下它改成这个组合";
            k2.SetBounds(400, 68, 126, 20);
            k2.ForeColor = Color.FromArgb(110, 110, 120);
            _gbKeys.Controls.Add(k2);

            _lstKeys = new ListBox();
            _lstKeys.SetBounds(10, 104, 510, 86);
            _lstKeys.IntegralHeight = false;
            _gbKeys.Controls.Add(_lstKeys);

            _btnDelK = NewButton("删除选中", 10, 198, 120, 26);
            _btnDelK.Click += delegate { RemoveSelected(_lstKeys); };
            _gbKeys.Controls.Add(_btnDelK);

            var kh = new Label();
            kh.Text = "不勾=禁用；勾了=换成新键，例 Ctrl+Shift+E → Alt+E";
            kh.SetBounds(136, 202, 390, 20);
            kh.ForeColor = Color.FromArgb(110, 110, 120);
            _gbKeys.Controls.Add(kh);

            return p;
        }

        TabPage BuildDetailsTab()
        {
            var p = NewPage("细节");

            var hint = new Label();
            hint.Text = "这些是全局行为，对所有程序生效。";
            hint.SetBounds(10, 8, 700, 20);
            hint.ForeColor = Color.FromArgb(110, 110, 120);
            p.Controls.Add(hint);

            _chkReplay = NewCheck("小位移算作点击：把这次点击补发出去（保住 Ctrl+单击 之类的操作）", 10, 40, 700);
            p.Controls.Add(_chkReplay);

            var l1 = new Label();
            l1.Text = "判定阈值";
            l1.SetBounds(10, 76, 70, 22);
            p.Controls.Add(l1);

            _numThreshold = new NumericUpDown();
            _numThreshold.Minimum = 0;
            _numThreshold.Maximum = 60;
            _numThreshold.SetBounds(78, 74, 60, 24);
            p.Controls.Add(_numThreshold);

            var l2 = new Label();
            l2.Text = "像素（按下到抬起的位移不超过它，就当作点击处理）";
            l2.SetBounds(146, 76, 500, 22);
            l2.ForeColor = Color.FromArgb(110, 110, 120);
            p.Controls.Add(l2);

            _chkBeep = NewCheck("拦截时播放提示音", 10, 112, 220);
            p.Controls.Add(_chkBeep);

            _chkFlash = NewCheck("拦截时闪烁托盘图标", 256, 112, 240);
            p.Controls.Add(_chkFlash);

            var note = new Label();
            note.Text =
                "副作用提醒：勾了 Ctrl+左键拖拽 之后，Photoshop 自由变换里的「Ctrl 拖角=扭曲」也会被挡，\n" +
                "因为它同样是 Ctrl+拖拽。要用的时候走 编辑 > 变换 > 扭曲，或者按住暂停快捷键。\n" +
                "\n" +
                "规则是「精确匹配」：只勾了 Ctrl，那么 Ctrl+Shift+拖拽不会被拦 —— 多按一个键就是临时放行。";
            note.SetBounds(10, 150, 830, 140);
            note.ForeColor = Color.FromArgb(160, 90, 40);
            p.Controls.Add(note);

            var gLast = new GroupBox();
            gLast.Text = "最近一次拦截（用来确认它真的在工作）";
            gLast.SetBounds(10, 300, 830, 84);
            p.Controls.Add(gLast);

            _lblLast = new Label();
            _lblLast.SetBounds(14, 30, 800, 30);
            _lblLast.ForeColor = Color.FromArgb(60, 60, 70);
            gLast.Controls.Add(_lblLast);

            return p;
        }

        TabPage BuildGeneralTab()
        {
            var p = NewPage("通用");

            _chkStartup = NewCheck("开机自动启动（写入 HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run）", 10, 12, 700);
            p.Controls.Add(_chkStartup);

            var l = new Label();
            l.Text = "暂停 / 恢复拦截：";
            l.SetBounds(10, 52, 120, 22);
            p.Controls.Add(l);

            _hkPause = new HotkeyBox();
            _hkPause.SetBounds(130, 48, 300, 28);
            p.Controls.Add(_hkPause);

            var lh = new Label();
            lh.Text = "全局生效。点一下再按组合键；Backspace 清空 = 不要快捷键。";
            lh.SetBounds(440, 52, 420, 22);
            lh.ForeColor = Color.FromArgb(110, 110, 120);
            p.Controls.Add(lh);

            var g = new GroupBox();
            g.Text = "统计";
            g.SetBounds(10, 88, 850, 74);
            p.Controls.Add(g);

            _lblStats = new Label();
            _lblStats.SetBounds(14, 28, 800, 30);
            g.Controls.Add(_lblStats);

            var btnOpen = NewButton("打开配置文件夹", 10, 172, 140, 28);
            btnOpen.Click += delegate { _app.OpenConfigFolder(); };
            p.Controls.Add(btnOpen);

            var btnImport = NewButton("导入配置…", 158, 172, 118, 28);
            btnImport.Click += delegate { ImportConfig(); };
            p.Controls.Add(btnImport);

            var btnExport = NewButton("导出配置…", 284, 172, 118, 28);
            btnExport.Click += delegate { ExportConfig(); };
            p.Controls.Add(btnExport);

            _lblConfigPath = new Label();
            _lblConfigPath.SetBounds(414, 176, 440, 22);
            _lblConfigPath.ForeColor = Color.FromArgb(110, 110, 120);
            p.Controls.Add(_lblConfigPath);

            var about = new TextBox();
            about.Multiline = true;
            about.ReadOnly = true;
            about.ScrollBars = ScrollBars.Vertical;
            about.BackColor = Color.FromArgb(246, 246, 248);
            about.SetBounds(10, 210, 850, 246);
            about.Text =
                "这个工具在做什么\r\n" +
                "  在 Windows 的输入层，把「修饰键 + 拖拽」或「修饰键 + 按键」拦下来，让前台程序根本收不到。\r\n" +
                "  所以它不需要改任何软件本体：对方升级、重装都不影响；卸载就是删掉这个 exe。\r\n" +
                "\r\n" +
                "为什么必须在外面拦\r\n" +
                "  很多软件的快捷键是写死在程序里的（Photoshop 的 Ctrl 移动就是典型），官方没给开关，\r\n" +
                "  键盘快捷键对话框里也改不了。只能在系统层面先把输入吃掉。\r\n" +
                "\r\n" +
                "可以拿来治什么\r\n" +
                "  · 拖一下就挪位置的（PS 的 Ctrl 拖拽、资源管理器的 Ctrl 拖拽复制文件）\r\n" +
                "  · 拖一下就多一份的（Ctrl+Alt 拖拽、Alt 拖拽复制）\r\n" +
                "  · 按错就触发的固定快捷键（在这个程序里配一条键盘规则即可）\r\n" +

                "\r\n" +
                "注意\r\n" +
                "  · 目标程序若以管理员身份运行，本工具受 Windows 权限隔离，拦不住。\r\n" +
                "  · 它只能拦住「输入」这一步：对应的图标可能还会闪一下，但不会真的执行。";
            p.Controls.Add(about);

            // 作者区：这一页最常被打开，让他被记住
            var avatar = new PictureBox();
            avatar.SetBounds(10, 466, 46, 46);
            avatar.SizeMode = PictureBoxSizeMode.Zoom;
            avatar.Image = LoadAvatar();
            avatar.Cursor = Cursors.Hand;
            avatar.Click += delegate { OpenHome(); };
            p.Controls.Add(avatar);

            var who = new Label();
            who.Text = "墨琛COMU（MochenCOMU / 墨琛_COMU）";
            who.Font = new Font(Font, FontStyle.Bold);
            who.SetBounds(66, 468, 500, 22);
            p.Controls.Add(who);

            var bio = new Label();
            bio.Text = "10 后 · ISTP · 绘画 / 摄影 / 音乐 · 梦想有一天成为高技术力大手子";
            bio.ForeColor = Color.FromArgb(120, 120, 130);
            bio.SetBounds(66, 490, 780, 20);
            p.Controls.Add(bio);

            var mail = new Label();
            mail.Text = "QQ / Email：3639339270@qq.com";
            mail.ForeColor = Color.FromArgb(150, 150, 160);
            mail.SetBounds(596, 468, 256, 20);
            mail.TextAlign = ContentAlignment.TopRight;
            p.Controls.Add(mail);

            return p;
        }

        static System.Drawing.Image LoadAvatar()
        {
            try
            {
                using (var s = typeof(SettingsForm).Assembly.GetManifestResourceStream("avatar.png"))
                {
                    if (s == null) return null;
                    var ms = new System.IO.MemoryStream();
                    s.CopyTo(ms);
                    ms.Position = 0;
                    return System.Drawing.Image.FromStream(ms);
                }
            }
            catch { return null; }
        }

        static void OpenHome()
        {
            try { System.Diagnostics.Process.Start(Program.HomeUrl); }
            catch { }
        }

        // ================= 预设 =================

        void ShowPresetMenu(Button anchor)
        {
            var menu = new ContextMenuStrip();
            foreach (Preset pre in Presets)
            {
                Preset local = pre;
                var item = new ToolStripMenuItem(pre.Title);
                item.Click += delegate { AddProfile(local.Process, local.Mouse, null); };
                menu.Items.Add(item);
            }
            menu.Show(anchor, new Point(0, anchor.Height));
        }

        void AddProfile(string name, string[] mouse, string[] keys)
        {
            if (name.Length == 0) return;
            foreach (Profile p in _profiles)
            {
                if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(this, "列表里已经有 " + name + " 了。", "出头鸟 Chutouniao",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    SelectProfile(name);
                    return;
                }
            }

            var prof = new Profile { Name = name };
            if (mouse != null)
                foreach (string s in mouse)
                {
                    MouseRule r;
                    if (MouseRule.TryParse(s, out r)) prof.Mouse.Add(r);
                }
            if (keys != null)
                foreach (string s in keys)
                {
                    KeyRule r;
                    if (KeyRule.TryParse(s, out r)) prof.Keys.Add(r);
                }

            _profiles.Add(prof);
            _lstProfiles.Items.Add(prof);
            SelectProfile(name);
        }

        void SelectProfile(string name)
        {
            for (int i = 0; i < _lstProfiles.Items.Count; i++)
            {
                Profile p = (Profile)_lstProfiles.Items[i];
                if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    _lstProfiles.SelectedIndex = i;
                    return;
                }
            }
        }

        Profile Selected
        {
            get
            {
                int i = _lstProfiles.SelectedIndex;
                if (i < 0 || i >= _profiles.Count) return null;
                return _profiles[i];
            }
        }

        void OnProfileChanged()
        {
            Profile p = Selected;
            _lblCurrent.Text = p == null ? "当前程序：—（先在左边选一个）" : "当前程序：" + p.Name;

            _lstMouse.Items.Clear();
            _lstKeys.Items.Clear();
            if (p != null)
            {
                foreach (MouseRule r in p.Mouse) _lstMouse.Items.Add(r);
                foreach (KeyRule r in p.Keys) _lstKeys.Items.Add(r);
            }

            bool on = p != null;
            foreach (Control c in _gbMouse.Controls) c.Enabled = on;
            foreach (Control c in _gbKeys.Controls) c.Enabled = on;
            if (_hkTarget != null) _hkTarget.Enabled = on && _chkForward.Checked;
            if (_lblForeground != null) _lblForeground.Enabled = true;

            RefreshStatus();
        }

        void DeleteProfile()
        {
            Profile p = Selected;
            if (p == null) return;
            if (_profiles.Count == 1)
            {
                MessageBox.Show(this, "至少留一个程序，否则这个工具什么都不会拦。", "出头鸟 Chutouniao",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            int idx = _lstProfiles.SelectedIndex;
            _profiles.RemoveAt(idx);
            _lstProfiles.Items.RemoveAt(idx);
            if (_lstProfiles.Items.Count > 0) _lstProfiles.SelectedIndex = Math.Min(idx, _lstProfiles.Items.Count - 1);
            else OnProfileChanged();
        }

        void PickRunning()
        {
            using (var dlg = new ProcessPickerForm())
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    foreach (string n in dlg.SelectedNames) AddProfile(n, null, null);
            }
        }

        // ================= 规则编辑 =================

        void AddMouseRule()
        {
            Profile p = Selected;
            if (p == null) return;

            var rule = new MouseRule
            {
                Ctrl = _chkC.Checked,
                Alt = _chkA.Checked,
                Shift = _chkS.Checked,
                Win = _chkW.Checked,
                Button = _cboButton.SelectedIndex
            };

            if (!rule.Ctrl && !rule.Alt && !rule.Shift && !rule.Win)
            {
                if (MessageBox.Show(this,
                        "没有勾任何修饰键，这条规则会变成「裸拖拽」——" + rule.Button + "按下拖动全部失效，\n" +
                        "包括正常拖动画布内容。确定要这样加吗？",
                        "出头鸟 Chutouniao", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;
            }

            foreach (MouseRule r in p.Mouse)
            {
                if (r.SameAs(rule))
                {
                    MessageBox.Show(this, "这条规则已经有了。", "出头鸟 Chutouniao",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }

            p.Mouse.Add(rule);
            _lstMouse.Items.Add(rule);
        }

        void AddKeyRule()
        {
            Profile p = Selected;
            if (p == null) return;

            if (!_hkRule.HasValue)
            {
                MessageBox.Show(this, "先点一下输入框，然后按下要拦的组合键。", "出头鸟 Chutouniao",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_chkForward.Checked && !_hkTarget.HasValue)
            {
                MessageBox.Show(this, "勾了「转发为」，但还没按目标组合键。\n要么按一个目标组合，要么把勾去掉（那就是直接禁用）。",
                    "出头鸟 Chutouniao", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var rule = new KeyRule
            {
                Ctrl = _hkRule.Ctrl,
                Alt = _hkRule.Alt,
                Shift = _hkRule.Shift,
                Win = _hkRule.Win,
                VK = _hkRule.VK,
                Forward = _chkForward.Checked && _hkTarget.HasValue,
                TCtrl = _chkForward.Checked && _hkTarget.HasValue && _hkTarget.Ctrl,
                TAlt = _chkForward.Checked && _hkTarget.HasValue && _hkTarget.Alt,
                TShift = _chkForward.Checked && _hkTarget.HasValue && _hkTarget.Shift,
                TWin = _chkForward.Checked && _hkTarget.HasValue && _hkTarget.Win,
                TVK = _chkForward.Checked && _hkTarget.HasValue ? _hkTarget.VK : 0
            };

            if (rule.HasTarget &&
                rule.Ctrl == rule.TCtrl && rule.Alt == rule.TAlt && rule.Shift == rule.TShift &&
                rule.Win == rule.TWin && rule.VK == rule.TVK)
            {
                MessageBox.Show(this, "原组合和目标组合一样，那就等于没改。", "出头鸟 Chutouniao",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!rule.Ctrl && !rule.Alt && !rule.Shift && !rule.Win)
            {
                if (MessageBox.Show(this,
                        "没有修饰键：单按 " + RuleFormat.KeyName(rule.VK) + " 就会被吃掉，\n" +
                        "这个键在 " + p.Name + " 里将彻底打不出来。确定吗？",
                        "出头鸟 Chutouniao", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;
            }

            foreach (KeyRule r in p.Keys)
            {
                if (r.SameAs(rule))
                {
                    MessageBox.Show(this, "这条规则已经有了。", "出头鸟 Chutouniao",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }

            p.Keys.Add(rule);
            _lstKeys.Items.Add(rule);
            _hkRule.Clear();
            _chkForward.Checked = false;
            _hkTarget.Clear();
        }

        void RemoveSelected(ListBox box)
        {
            Profile p = Selected;
            if (p == null || box.SelectedIndex < 0) return;
            int i = box.SelectedIndex;
            box.Items.RemoveAt(i);

            if (box == _lstMouse) p.Mouse.RemoveAt(i);
            else p.Keys.RemoveAt(i);
        }

        // ================= 数据 <-> 界面 =================

        void LoadFromConfig()
        {
            LoadFrom(_app.Cfg);
        }

        void LoadFrom(Config src)
        {
            _profiles.Clear();
            foreach (Profile p in src.Profiles) _profiles.Add(p.Clone());

            _lstProfiles.Items.Clear();
            foreach (Profile p in _profiles) _lstProfiles.Items.Add(p);
            if (_lstProfiles.Items.Count > 0) _lstProfiles.SelectedIndex = 0;

            _chkReplay.Checked = src.ReplayClick;
            _numThreshold.Value = Math.Max(_numThreshold.Minimum,
                Math.Min(_numThreshold.Maximum, src.ClickThreshold));
            _chkBeep.Checked = src.Beep;
            _chkFlash.Checked = src.Flash;
            _chkStartup.Checked = Config.IsStartupEnabled();
            _hkPause.Value = src.Hotkey;
            _hkPause.Invalidate();

            OnProfileChanged();
        }

        void LoadDefaults()
        {
            if (MessageBox.Show(this, "把程序和规则全部恢复成默认（只有 Photoshop 的两条）？",
                    "出头鸟 Chutouniao", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            _profiles.Clear();
            _lstProfiles.Items.Clear();
            AddProfile("Photoshop", new[] { "Ctrl+Left", "Ctrl+Alt+Left" }, null);

            _chkReplay.Checked = true;
            _numThreshold.Value = 5;
            _chkBeep.Checked = false;
            _chkFlash.Checked = false;
            _hkPause.Value = "Ctrl+Alt+Shift+F12";
            _hkPause.Invalidate();
        }

        void RefreshStatus()
        {
            if (_lblStats != null)
                _lblStats.Text = "本次运行已拦截 " + _app.Engine.SessionBlocked + " 次　·　历史累计 " +
                                 _app.Cfg.TotalBlocked + " 次";

            if (_lblConfigPath != null)
                _lblConfigPath.Text = Config.ConfigPath + (Config.IsPortable ? "（便携模式）" : "");

            if (_lblLast != null)
                _lblLast.Text = _app.Engine.LastBlockedText.Length == 0
                    ? "还没有拦过东西。去目标程序里按住那个组合试一下，这里会显示刚才拦了什么。"
                    : _app.Engine.LastBlockedText;

            if (_lblForeground != null)
            {
                string fg = Interceptor.ForegroundProcessName();
                bool hit = false;
                foreach (Profile p in _profiles)
                    if (string.Equals(p.Name, fg, StringComparison.OrdinalIgnoreCase)) { hit = true; break; }
                _lblForeground.Text = string.IsNullOrEmpty(fg)
                    ? "当前前台：—"
                    : "当前前台：" + fg + (hit ? "  ✔ 在名单里" : "  （不在名单里）");
            }
        }

        Config BuildConfigFromForm(Config into)
        {
            into.Profiles.Clear();
            foreach (Profile p in _profiles) into.Profiles.Add(p.Clone());
            into.ReplayClick = _chkReplay.Checked;
            into.ClickThreshold = (int)_numThreshold.Value;
            into.Beep = _chkBeep.Checked;
            into.Flash = _chkFlash.Checked;
            into.Hotkey = _hkPause.Value;
            into.StartWithWindows = _chkStartup.Checked;
            return into;
        }

        void ImportConfig()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "导入配置";
                dlg.Filter = "出头鸟 设置 (*.ini)|*.ini|所有文件 (*.*)|*.*";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                Config c = Config.Load(dlg.FileName);
                if (c.Profiles.Count == 0)
                {
                    MessageBox.Show(this, "这个文件里没有可用的程序配置。", "出头鸟 Chutouniao",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                LoadFrom(c);
                MessageBox.Show(this, "导入了 " + c.Profiles.Count + " 个程序。点「保存」才会生效。",
                    "出头鸟 Chutouniao", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        void ExportConfig()
        {
            using (var dlg = new SaveFileDialog())
            {
                dlg.Title = "导出配置";
                dlg.Filter = "出头鸟 设置 (*.ini)|*.ini";
                dlg.FileName = "psctrguard-settings.ini";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                Config tmp = BuildConfigFromForm(Config.Load(null));
                tmp.Save(dlg.FileName);
                MessageBox.Show(this, "已导出到：\n" + dlg.FileName, "出头鸟 Chutouniao",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }


        void SaveAndClose()
        {
            if (_profiles.Count == 0)
            {
                MessageBox.Show(this, "至少要有一个程序。", "出头鸟 Chutouniao",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Config c = BuildConfigFromForm(_app.Cfg);

            if (!Config.SetStartup(c.StartWithWindows))
                MessageBox.Show(this, "写入开机启动失败（可能被安全软件拦了）。其它设置已保存。",
                    "出头鸟 Chutouniao", MessageBoxButtons.OK, MessageBoxIcon.Warning);

            _app.ApplyConfig();
            bool hotkeyOk = _app.RegisterHotkey();
            if (!hotkeyOk && c.Hotkey.Length > 0)
                MessageBox.Show(this, "快捷键 " + c.Hotkey + " 被别的程序占用了，先用托盘菜单暂停吧。",
                    "出头鸟 Chutouniao", MessageBoxButtons.OK, MessageBoxIcon.Information);

            c.Save();
            Close();
        }
    }
}
