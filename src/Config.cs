using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace Chutouniao
{
    /// <summary>配置模型 + 读写（纯文本 INI，方便用户直接改）</summary>
    internal class Config
    {
        public List<Profile> Profiles = new List<Profile>();

        public bool ReplayClick = true;
        public int ClickThreshold = 5;
        public bool Beep = false;
        public bool Flash = false;
        public bool StartWithWindows = true;
        public string Hotkey = "Ctrl+Alt+Shift+F12";
        public long TotalBlocked = 0;

        public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public const string RunValueName = "Chutouniao";


        public static string ExePath
        {
            get { return System.Windows.Forms.Application.ExecutablePath; }
        }

        public static string ExeDir
        {
            get { return Path.GetDirectoryName(ExePath); }
        }

        /// <summary>便携模式：exe 旁边有 settings.ini 就用它；否则用 %APPDATA%\Chutouniao\settings.ini</summary>
        public static string ConfigPath
        {
            get
            {
                string portable = Path.Combine(ExeDir, "settings.ini");
                if (File.Exists(portable)) return portable;
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Chutouniao");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                return Path.Combine(dir, "settings.ini");
            }
        }

        public static bool IsPortable
        {
            get { return File.Exists(Path.Combine(ExeDir, "settings.ini")); }
        }

        public Profile FindProfile(string processName)
        {
            if (string.IsNullOrEmpty(processName)) return null;
            foreach (Profile p in Profiles)
                if (string.Equals(p.Name, processName, StringComparison.OrdinalIgnoreCase)) return p;
            return null;
        }

        // ---------------- 读 ----------------

        public static Config Load()
        {
            return Load(ConfigPath);
        }

        public static Config Load(string path)
        {
            var c = new Config();

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                ApplyDefault(c);
                return c;
            }

            var legacyTargets = new List<string>();
            bool legacyCtrl = false, legacyCtrlAlt = false, legacyCtrlShift = false,
                 legacyAlt = false, legacyShift = false;
            bool hasLegacy = false;
            Profile current = null;

            try
            {
                foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.Length == 0) continue;
                    if (line[0] == ';' || line[0] == '#') continue;

                    if (line.StartsWith("["))
                    {
                        string sec = line.Trim('[', ']', ' ').ToLowerInvariant();
                        if (sec == "profile")
                        {
                            current = new Profile();
                            c.Profiles.Add(current);
                        }
                        else current = null;
                        continue;
                    }

                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim().ToLowerInvariant();
                    string v = line.Substring(eq + 1).Trim();

                    if (current != null)
                    {
                        switch (k)
                        {
                            case "name":
                                current.Name = v;
                                break;
                            case "mouse":
                                foreach (string item in SplitRules(v))
                                {
                                    MouseRule mr;
                                    if (MouseRule.TryParse(item, out mr)) current.Mouse.Add(mr);
                                }
                                break;
                            case "keys":
                                foreach (string item in SplitRules(v))
                                {
                                    KeyRule kr;
                                    if (KeyRule.TryParse(item, out kr)) current.Keys.Add(kr);
                                }
                                break;
                        }
                        continue;
                    }

                    switch (k)
                    {
                        // —— 2.x 的旧字段，读出来做迁移 ——
                        case "targets":
                            foreach (string t in v.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                string name = StripExe(t);
                                if (name.Length > 0) legacyTargets.Add(name);
                            }
                            hasLegacy = true;
                            break;
                        case "blockctrl": legacyCtrl = ToBool(v); hasLegacy = true; break;
                        case "blockctrlalt": legacyCtrlAlt = ToBool(v); hasLegacy = true; break;
                        case "blockctrlshift": legacyCtrlShift = ToBool(v); hasLegacy = true; break;
                        case "blockalt": legacyAlt = ToBool(v); hasLegacy = true; break;
                        case "blockshift": legacyShift = ToBool(v); hasLegacy = true; break;

                        case "replayclick": c.ReplayClick = ToBool(v); break;
                        case "clickthreshold":
                            int t2;
                            if (int.TryParse(v, out t2)) c.ClickThreshold = Math.Max(0, Math.Min(60, t2));
                            break;
                        case "beep": c.Beep = ToBool(v); break;
                        case "flash": c.Flash = ToBool(v); break;
                        case "startwithwindows": c.StartWithWindows = ToBool(v); break;
                        case "hotkey": c.Hotkey = v; break;
                        case "totalblocked":
                            long n;
                            if (long.TryParse(v, out n)) c.TotalBlocked = n;
                            break;
                    }
                }
            }
            catch { }

            // 2.x 配置迁移：把当时的全局规则套到每个目标程序上
            if (c.Profiles.Count == 0 && hasLegacy && legacyTargets.Count > 0)
            {
                foreach (string t in legacyTargets)
                {
                    var p = new Profile { Name = t };
                    if (legacyCtrl) p.Mouse.Add(new MouseRule { Ctrl = true, Button = 0 });
                    if (legacyCtrlAlt) p.Mouse.Add(new MouseRule { Ctrl = true, Alt = true, Button = 0 });
                    if (legacyCtrlShift) p.Mouse.Add(new MouseRule { Ctrl = true, Shift = true, Button = 0 });
                    if (legacyAlt) p.Mouse.Add(new MouseRule { Alt = true, Button = 0 });
                    if (legacyShift) p.Mouse.Add(new MouseRule { Shift = true, Button = 0 });
                    c.Profiles.Add(p);
                }
            }

            c.Profiles.RemoveAll(delegate(Profile p) { return string.IsNullOrEmpty(p.Name); });
            if (c.Profiles.Count == 0) ApplyDefault(c);
            return c;
        }

        static void ApplyDefault(Config c)
        {
            c.Profiles.Clear();
            var ps = new Profile { Name = "Photoshop" };
            ps.Mouse.Add(new MouseRule { Ctrl = true, Button = 0 });             // Ctrl + 左键拖拽
            ps.Mouse.Add(new MouseRule { Ctrl = true, Alt = true, Button = 0 }); // Ctrl + Alt + 左键拖拽
            c.Profiles.Add(ps);
        }

        // ---------------- 写 ----------------

        public void Save()
        {
            Save(ConfigPath);
        }

        public void Save(string path)
        {
            var sb = new StringBuilder();
            sb.AppendLine("; 出头鸟 Chutouniao 配置文件");
            sb.AppendLine("; 可以直接编辑，程序下次启动生效；也可以在设置界面里改");
            sb.AppendLine();
            sb.AppendLine("[General]");
            sb.AppendLine("StartWithWindows=" + S(StartWithWindows));
            sb.AppendLine("Hotkey=" + Hotkey);
            sb.AppendLine("ReplayClick=" + S(ReplayClick));
            sb.AppendLine("ClickThreshold=" + ClickThreshold);
            sb.AppendLine("Beep=" + S(Beep));
            sb.AppendLine("Flash=" + S(Flash));
            sb.AppendLine("TotalBlocked=" + TotalBlocked);

            foreach (Profile p in Profiles)
            {
                sb.AppendLine();
                sb.AppendLine("[Profile]");
                sb.AppendLine("Name=" + p.Name);

                var mouse = new List<string>();
                foreach (MouseRule m in p.Mouse) mouse.Add(m.Serialize());
                sb.AppendLine("Mouse=" + string.Join(";", mouse.ToArray()));

                var keys = new List<string>();
                foreach (KeyRule k in p.Keys) keys.Add(k.Serialize());
                sb.AppendLine("Keys=" + string.Join(";", keys.ToArray()));
            }

            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            }
            catch { }
        }

        static string S(bool v) { return v ? "1" : "0"; }

        static bool ToBool(string v)
        {
            string s = v.Trim().ToLowerInvariant();
            return s == "1" || s == "true" || s == "yes" || s == "on";
        }

        static string StripExe(string s)
        {
            s = s.Trim();
            if (s.ToLowerInvariant().EndsWith(".exe")) s = s.Substring(0, s.Length - 4);
            return s;
        }

        static List<string> SplitRules(string v)
        {
            var list = new List<string>();
            if (v == null) return list;
            foreach (string part in v.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string s = part.Trim();
                if (s.Length > 0) list.Add(s);
            }
            return list;
        }

        // ---------------- 开机自启 ----------------

        public static bool IsStartupEnabled()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                    return k != null && k.GetValue(RunValueName) != null;
            }
            catch { return false; }
        }

        public static bool SetStartup(bool on)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (k == null) return false;
                    if (on) k.SetValue(RunValueName, "\"" + ExePath + "\"");
                    else if (k.GetValue(RunValueName) != null) k.DeleteValue(RunValueName);
                }
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// 旧名字留下的东西搬过来：配置目录、启动项、快捷方式。
        /// 经历过 PsCtrlGuard（1.x~3.x）和 SongBang（4.0）两次改名。
        /// 必须在第一次读取 ConfigPath 之前调用。
        /// </summary>
        public static void MigrateLegacyInstall()
        {
            // 1) 配置目录：先找最近的一个旧名字
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string newDir = Path.Combine(appData, "Chutouniao");
                if (!Directory.Exists(newDir))
                {
                    string[] oldNames = { "SongBang", "PsCtrlGuard" };
                    foreach (string old in oldNames)
                    {
                        string oldDir = Path.Combine(appData, old);
                        if (!Directory.Exists(oldDir)) continue;
                        try { Directory.Move(oldDir, newDir); }
                        catch
                        {
                            Directory.CreateDirectory(newDir);
                            string f = Path.Combine(oldDir, "settings.ini");
                            if (File.Exists(f)) File.Copy(f, Path.Combine(newDir, "settings.ini"), true);
                        }
                        break;
                    }
                }
            }
            catch { }

            // 2) 旧的开机启动项
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (k != null)
                        foreach (string old in new[] { "SongBang", "PsCtrlGuard" })
                            if (k.GetValue(old) != null) k.DeleteValue(old);
                }
            }
            catch { }

            // 3) 旧快捷方式
            string[] dirs =
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
            };
            foreach (string d in dirs) DeleteOldShortcuts(d);
        }

        static void DeleteOldShortcuts(string dir)
        {
            if (string.IsNullOrEmpty(dir)) return;
            string[] names = { "PsCtrlGuard.lnk", "SongBang.lnk", "松绑 SongBang.lnk" };
            foreach (string n in names)
            {
                try
                {
                    string p = Path.Combine(dir, n);
                    if (File.Exists(p)) File.Delete(p);
                }
                catch { }
            }
        }
    }
}
