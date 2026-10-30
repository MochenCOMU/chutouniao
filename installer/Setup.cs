// 出头鸟 Chutouniao 安装程序
// 编译时把 Chutouniao.exe 作为资源嵌进来，产出一个「双击就能装」的单文件安装包。
// 安装到 %LOCALAPPDATA%\Programs\Chutouniao，不需要管理员权限。
// 自己复制一份成 Uninstall.exe，带 /uninstall 参数运行就是卸载。
//
// 编译（见 build.ps1）：
//   csc /target:winexe /resource:Chutouniao.exe,Chutouniao.exe /out:Chutouniao-Setup-4.1.0.exe Setup.cs

using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class Setup
{
    public const string AppName = "出头鸟 Chutouniao";
    public const string AppVersion = "4.1.0";      // 改版本时同步 src\Program.cs
    public const string AppExe = "Chutouniao.exe";
    public const string UninstExe = "Uninstall.exe";
    public const string Publisher = "墨琛COMU";
    public const string HomeUrl = "https://github.com/MochenCOMU/chutouniao";
    public const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Chutouniao";
    public const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string RunValue = "Chutouniao";
    const string Payload = "Chutouniao.exe";
    const string ReadmePayload = "Readme.txt";

    public static string InstallDir;

    [STAThread]
    static void Main(string[] args)
    {
        bool uninstall = false, silent = false;
        foreach (string a in args)
        {
            if (a == "/uninstall" || a == "-uninstall" || a == "--uninstall") uninstall = true;
            if (a == "/silent" || a == "--silent" || a == "/S") silent = true;
        }

        if (uninstall) { Uninstall(silent); return; }

        InstallDir = DefaultInstallDir();

        if (silent)
        {
            DoInstall(InstallDir, true, false, true);
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new InstallerForm());
    }

    // ---------------- 路径 / 状态 ----------------

    public static string DefaultInstallDir()
    {
        string existing = ReadInstallLocation();
        if (!string.IsNullOrEmpty(existing) && Directory.Exists(existing)) return existing;

        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(Path.Combine(local, "Programs"), "Chutouniao");
    }

    public static string ReadInstallLocation()
    {
        try
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(UninstallKey, false))
                if (k != null) return k.GetValue("InstallLocation") as string;
        }
        catch { }
        return null;
    }

    public static bool IsInstalledAlready()
    {
        string dir = ReadInstallLocation();
        return !string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, AppExe));
    }

    // ---------------- 安装 ----------------

    public static bool DoInstall(string dir, bool startup, bool desktop, bool quiet)
    {
        try
        {
            InstallDir = dir;
            Directory.CreateDirectory(dir);
            CleanupOldVersions();

            foreach (Process p in Process.GetProcessesByName("Chutouniao"))
            {
                try { p.Kill(); p.WaitForExit(3000); } catch { }
            }
            System.Threading.Thread.Sleep(300);

            string target = Path.Combine(dir, AppExe);
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(Payload))
            {
                if (s == null) throw new Exception("安装包不完整：找不到内置程序。");
                using (FileStream fs = new FileStream(target, FileMode.Create, FileAccess.Write))
                    s.CopyTo(fs);
            }

            // 顺手放一份说明到安装目录，拿了 exe 就跑的人也能找到怎么用
            try
            {
                using (Stream rs = Assembly.GetExecutingAssembly().GetManifestResourceStream(ReadmePayload))
                {
                    if (rs != null)
                        using (FileStream rf = new FileStream(Path.Combine(dir, "使用说明.txt"), FileMode.Create, FileAccess.Write))
                            rs.CopyTo(rf);
                }
            }
            catch { }

            string self = Application.ExecutablePath;
            string uninst = Path.Combine(dir, UninstExe);
            if (!string.Equals(self, uninst, StringComparison.OrdinalIgnoreCase))
                File.Copy(self, uninst, true);

            CreateShortcut(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName + ".lnk"),
                target, dir, "世界若无出头鸟，人人皆为待宰鸡");

            string desktopLnk = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), AppName + ".lnk");
            if (desktop) CreateShortcut(desktopLnk, target, dir, "世界若无出头鸟，人人皆为待宰鸡");
            else DeleteFile(desktopLnk);

            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (k != null)
                {
                    if (startup) k.SetValue(RunValue, "\"" + target + "\"");
                    else if (k.GetValue(RunValue) != null) k.DeleteValue(RunValue);
                }
            }

            WriteUninstallEntry(dir, target);

            if (quiet) return true;
            return true;
        }
        catch (Exception ex)
        {
            if (!quiet)
                MessageBox.Show("安装失败：\n" + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    /// <summary>
    /// 装新版前把之前叫法的版本清掉（PsCtrlGuard / SongBang），免得两个版本共存。
    /// 注意：不碰 %APPDATA% 里的配置 —— 配置由程序自己启动时迁移过来。
    /// </summary>
    static void CleanupOldVersions()
    {
        foreach (string old in new[] { "SongBang", "PsCtrlGuard" })
        {
            foreach (Process p in Process.GetProcessesByName(old))
            {
                try { p.Kill(); p.WaitForExit(3000); } catch { }
            }

            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                    if (k != null && k.GetValue(old) != null) k.DeleteValue(old);
            }
            catch { }

            string key = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + old;
            try
            {
                string dir = null;
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(key, false))
                    if (k != null) dir = k.GetValue("InstallLocation") as string;
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) Directory.Delete(dir, true);
            }
            catch { }
            try { Registry.CurrentUser.DeleteSubKeyTree(key, false); } catch { }
        }

        string[] dirs =
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            Environment.GetFolderPath(Environment.SpecialFolder.Programs),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
        };
        foreach (string d in dirs)
        {
            if (string.IsNullOrEmpty(d)) continue;
            foreach (string n in new[] { "PsCtrlGuard.lnk", "SongBang.lnk", "松绑 SongBang.lnk" })
            {
                try
                {
                    string p = Path.Combine(d, n);
                    if (File.Exists(p)) File.Delete(p);
                }
                catch { }
            }
        }
    }

    static void WriteUninstallEntry(string dir, string exe)
    {
        using (RegistryKey k = Registry.CurrentUser.CreateSubKey(UninstallKey))
        {
            k.SetValue("DisplayName", AppName);
            k.SetValue("DisplayVersion", AppVersion);
            k.SetValue("Publisher", Publisher);
            k.SetValue("DisplayIcon", exe + ",0");
            k.SetValue("InstallLocation", dir);
            k.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
            k.SetValue("URLInfoAbout", HomeUrl);
            k.SetValue("UninstallString", "\"" + Path.Combine(dir, UninstExe) + "\" /uninstall");
            k.SetValue("QuietUninstallString", "\"" + Path.Combine(dir, UninstExe) + "\" /uninstall /silent");
            k.SetValue("NoModify", 1, RegistryValueKind.DWord);
            k.SetValue("NoRepair", 1, RegistryValueKind.DWord);

            long size = 0;
            try
            {
                foreach (string f in Directory.GetFiles(dir)) size += new FileInfo(f).Length;
            }
            catch { }
            k.SetValue("EstimatedSize", (int)(size / 1024), RegistryValueKind.DWord);
        }
    }

    static void CreateShortcut(string lnkPath, string target, string workDir, string desc)
    {
        try
        {
            object shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            object lnk = shell.GetType().InvokeMember("CreateShortcut",
                BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });
            Type t = lnk.GetType();
            t.InvokeMember("TargetPath", BindingFlags.SetProperty, null, lnk, new object[] { target });
            t.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, lnk, new object[] { workDir });
            t.InvokeMember("Description", BindingFlags.SetProperty, null, lnk, new object[] { desc });
            t.InvokeMember("IconLocation", BindingFlags.SetProperty, null, lnk, new object[] { target + ",0" });
            t.InvokeMember("Save", BindingFlags.InvokeMethod, null, lnk, null);
        }
        catch { }
    }

    static void DeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    // ---------------- 卸载 ----------------

    static void Uninstall(bool silent)
    {
        string dir = ReadInstallLocation();
        if (string.IsNullOrEmpty(dir))
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            dir = Path.Combine(Path.Combine(local, "Programs"), "Chutouniao");
        }

        if (!silent)
        {
            DialogResult r = MessageBox.Show(
                "要卸载 " + AppName + " " + AppVersion + " 吗？\n\n安装目录：" + dir +
                "\n\n（配置文件和规则会一起删除）",
                "卸载 " + AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);
            if (r != DialogResult.Yes) return;
        }

        foreach (Process p in Process.GetProcessesByName("Chutouniao"))
        {
            try { p.Kill(); p.WaitForExit(3000); } catch { }
        }

        DeleteFile(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName + ".lnk"));
        DeleteFile(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), AppName + ".lnk"));

        try { Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false); } catch { }

        try
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                if (k != null && k.GetValue(RunValue) != null) k.DeleteValue(RunValue);
        }
        catch { }

        try
        {
            string appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Chutouniao");
            if (Directory.Exists(appData)) Directory.Delete(appData, true);
        }
        catch { }

        // 自己删自己：交给一个延迟执行的批处理
        try
        {
            string bat = Path.Combine(Path.GetTempPath(), "chutouniao_del_" + Guid.NewGuid().ToString("N") + ".bat");
            var sb = new StringBuilder();
            sb.AppendLine("@echo off");
            sb.AppendLine("timeout /t 2 /nobreak >nul");
            sb.AppendLine("rmdir /s /q \"" + dir + "\" 2>nul");
            sb.AppendLine("del \"%~f0\"");
            File.WriteAllText(bat, sb.ToString(), Encoding.Default);

            var psi = new ProcessStartInfo("cmd.exe", "/c \"" + bat + "\"");
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            Process.Start(psi);
        }
        catch { }

        if (!silent)
            MessageBox.Show("已卸载。", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);

        Application.Exit();
    }
}

/// <summary>安装界面</summary>
internal class InstallerForm : Form
{
    readonly Panel _bodyPanel = new Panel();
    readonly Panel _donePanel = new Panel();
    Button _btnInstall, _btnCancel;
    TextBox _txtDir;
    CheckBox _chkStartup, _chkDesktop, _chkRun;
    ProgressBar _bar;
    Label _lblStatus;
    bool _busy, _installed;
    string _finalDir;

    public InstallerForm()
    {
        Text = "安装 " + Setup.AppName + " " + Setup.AppVersion;
        ClientSize = new Size(600, 424);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.White;
        try { Font = new Font("Microsoft YaHei UI", 9F); }
        catch { Font = SystemFonts.MessageBoxFont; }
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
        catch { }

        BuildHeader();
        BuildBody();
        BuildDonePanel();
        BuildFooter();
    }

    void BuildHeader()
    {
        var header = new Panel();
        header.SetBounds(0, 0, 600, 96);
        header.BackColor = Color.FromArgb(31, 34, 48);
        Controls.Add(header);

        var pic = new PictureBox();
        pic.SetBounds(22, 24, 48, 48);
        pic.SizeMode = PictureBoxSizeMode.Zoom;
        try { pic.Image = Icon.ExtractAssociatedIcon(Application.ExecutablePath).ToBitmap(); }
        catch { }
        header.Controls.Add(pic);

        var t = new Label();
        t.Text = Setup.AppName + " " + Setup.AppVersion;
        t.ForeColor = Color.White;
        t.Font = new Font(Font.FontFamily, 15F, FontStyle.Bold);
        t.SetBounds(86, 22, 480, 30);
        header.Controls.Add(t);

        var s = new Label();
        s.Text = "世界若无出头鸟，人人皆为待宰鸡 —— 把改不掉的快捷键改掉";
        s.ForeColor = Color.FromArgb(186, 194, 214);
        s.SetBounds(88, 54, 490, 22);
        header.Controls.Add(s);
    }

    void BuildBody()
    {
        _bodyPanel.SetBounds(0, 96, 600, 268);
        _bodyPanel.BackColor = Color.White;
        Controls.Add(_bodyPanel);

        var l = new Label();
        l.Text = "安装位置";
        l.SetBounds(24, 18, 200, 20);
        _bodyPanel.Controls.Add(l);

        _txtDir = new TextBox();
        _txtDir.SetBounds(24, 42, 440, 25);
        _txtDir.Text = Setup.InstallDir;
        _bodyPanel.Controls.Add(_txtDir);

        var browse = new Button();
        browse.Text = "浏览…";
        browse.SetBounds(472, 41, 100, 27);
        browse.Click += delegate { BrowseDir(); };
        _bodyPanel.Controls.Add(browse);

        _chkStartup = new CheckBox();
        _chkStartup.Text = "开机自动启动";
        _chkStartup.SetBounds(24, 84, 140, 22);
        _chkStartup.Checked = true;
        _bodyPanel.Controls.Add(_chkStartup);

        _chkDesktop = new CheckBox();
        _chkDesktop.Text = "创建桌面快捷方式";
        _chkDesktop.SetBounds(180, 84, 170, 22);
        _bodyPanel.Controls.Add(_chkDesktop);

        if (Setup.IsInstalledAlready())
        {
            var up = new Label();
            up.Text = "检测到已经装过，这次会直接覆盖升级（规则和设置都保留）。";
            up.SetBounds(24, 114, 552, 22);
            up.ForeColor = Color.FromArgb(160, 110, 30);
            _bodyPanel.Controls.Add(up);
        }

        var note = new Label();
        note.Text =
            "· 装在当前用户目录下，不需要管理员权限。\n" +
            "· 装完会在开始菜单创建「" + Setup.AppName + "」，并在「设置 > 应用」里登记，可随时卸载。\n" +
            "· 作者：墨琛COMU　·　MIT 许可　·　" + Setup.HomeUrl;
        note.SetBounds(24, 146, 552, 72);
        note.ForeColor = Color.FromArgb(110, 110, 120);
        _bodyPanel.Controls.Add(note);

        _bar = new ProgressBar();
        _bar.SetBounds(24, 220, 552, 6);
        _bar.Style = ProgressBarStyle.Marquee;
        _bar.MarqueeAnimationSpeed = 30;
        _bar.Visible = false;
        _bodyPanel.Controls.Add(_bar);

        _lblStatus = new Label();
        _lblStatus.SetBounds(24, 232, 552, 22);
        _lblStatus.ForeColor = Color.FromArgb(110, 110, 120);
        _bodyPanel.Controls.Add(_lblStatus);
    }

    void BuildDonePanel()
    {
        _donePanel.SetBounds(0, 96, 600, 268);
        _donePanel.BackColor = Color.White;
        _donePanel.Visible = false;
        Controls.Add(_donePanel);

        var big = new Label();
        big.Text = "✓ 安装完成";
        big.Font = new Font(Font.FontFamily, 14F, FontStyle.Bold);
        big.ForeColor = Color.FromArgb(30, 140, 80);
        big.SetBounds(24, 40, 300, 34);
        _donePanel.Controls.Add(big);

        var txt = new Label();
        txt.Text = "桌面右下角（托盘）会出现一个小盾牌，那就是它在工作。\n双击托盘图标可以打开设置。";
        txt.SetBounds(24, 82, 552, 50);
        txt.ForeColor = Color.FromArgb(70, 70, 80);
        _donePanel.Controls.Add(txt);

        _chkRun = new CheckBox();
        _chkRun.Text = "立即运行 " + Setup.AppName;
        _chkRun.SetBounds(24, 140, 260, 22);
        _chkRun.Checked = true;
        _donePanel.Controls.Add(_chkRun);
    }

    void BuildFooter()
    {
        var footer = new Panel();
        footer.SetBounds(0, 364, 600, 60);
        footer.BackColor = Color.FromArgb(246, 246, 248);
        Controls.Add(footer);

        _btnInstall = new Button();
        _btnInstall.Text = "安装";
        _btnInstall.SetBounds(384, 14, 96, 32);
        _btnInstall.Click += delegate { OnPrimary(); };
        footer.Controls.Add(_btnInstall);

        _btnCancel = new Button();
        _btnCancel.Text = "取消";
        _btnCancel.SetBounds(488, 14, 88, 32);
        _btnCancel.Click += delegate { Close(); };
        footer.Controls.Add(_btnCancel);

        AcceptButton = _btnInstall;
    }

    void OnPrimary()
    {
        if (_busy) return;
        if (_installed) { FinishAndClose(); return; }
        StartInstall();
    }

    void BrowseDir()
    {
        using (var dlg = new FolderBrowserDialog())
        {
            dlg.Description = "选择安装位置";
            dlg.SelectedPath = _txtDir.Text;
            if (dlg.ShowDialog(this) == DialogResult.OK) _txtDir.Text = dlg.SelectedPath;
        }
    }

    void StartInstall()
    {
        string dir = _txtDir.Text.Trim();
        if (dir.Length == 0) { MessageBox.Show(this, "请填一个安装位置。", Setup.AppName); return; }

        _busy = true;
        _btnInstall.Enabled = false;
        _btnCancel.Enabled = false;
        _txtDir.Enabled = false;
        _bar.Visible = true;
        _lblStatus.Text = "正在安装…";
        Application.DoEvents();

        bool ok = Setup.DoInstall(dir, _chkStartup.Checked, _chkDesktop.Checked, false);
        _bar.Visible = false;
        _busy = false;

        if (!ok)
        {
            _btnInstall.Enabled = true;
            _btnCancel.Enabled = true;
            _txtDir.Enabled = true;
            _lblStatus.Text = "安装失败，详见弹窗。";
            return;
        }

        _finalDir = dir;
        _installed = true;
        _bodyPanel.Visible = false;
        _donePanel.Visible = true;
        _btnInstall.Text = "完成";
        _btnInstall.Enabled = true;
        _btnCancel.Visible = false;
    }

    void FinishAndClose()
    {
        if (_chkRun != null && _chkRun.Checked)
        {
            try
            {
                Process.Start(new ProcessStartInfo(Path.Combine(_finalDir, Setup.AppExe))
                { WorkingDirectory = _finalDir });
            }
            catch { }
        }
        Close();
    }
}
