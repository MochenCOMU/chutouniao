using System;
using System.Collections.Generic;
using System.Text;

namespace Chutouniao
{
    /// <summary>虚拟键名与名字之间的互转（覆盖常用键，其它用 VK_0xNN 兜底）</summary>
    internal static class RuleFormat
    {
        static readonly Dictionary<string, uint> NameToVk = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<uint, string> VkToName = new Dictionary<uint, string>();

        static RuleFormat()
        {
            for (uint vk = 0x30; vk <= 0x39; vk++) Add(((char)vk).ToString(), vk); // 0-9
            for (uint vk = 0x41; vk <= 0x5A; vk++) Add(((char)vk).ToString(), vk); // A-Z
            for (uint i = 1; i <= 24; i++) Add("F" + i, 0x6F + i);             // F1-F24
            for (uint i = 0; i <= 9; i++) Add("Num" + i, 0x60 + i);            // 小键盘

            Add("Tab", 0x09); Add("Space", 0x20); Add("Enter", 0x0D); Add("Esc", 0x1B);
            Add("Backspace", 0x08); Add("Delete", 0x2E); Add("Insert", 0x2D);
            Add("Home", 0x24); Add("End", 0x23); Add("PageUp", 0x21); Add("PageDown", 0x22);
            Add("Left", 0x25); Add("Up", 0x26); Add("Right", 0x27); Add("Down", 0x28);
            Add("PrtSc", 0x2C); Add("ScrollLock", 0x91); Add("Pause", 0x13);
            Add("CapsLock", 0x14); Add("NumLock", 0x90); Add("Menu", 0x5D);
            Add("-", 0xBD); Add("=", 0xBB); Add(",", 0xBC); Add(".", 0xBE);
            Add("/", 0xBF); Add(";", 0xBA); Add("'", 0xDE); Add("[", 0xDB);
            Add("]", 0xDD); Add("\\", 0xDC); Add("`", 0xC0);
            Add("Num*", 0x6A); Add("Num+", 0x6B); Add("Num-", 0x6D);
            Add("Num.", 0x6E); Add("Num/", 0x6F);
        }

        static void Add(string name, uint vk)
        {
            if (!NameToVk.ContainsKey(name)) NameToVk[name] = vk;
            if (!VkToName.ContainsKey(vk)) VkToName[vk] = name;
        }

        public static string KeyName(uint vk)
        {
            string n;
            if (VkToName.TryGetValue(vk, out n)) return n;
            return "VK_0x" + vk.ToString("X2");
        }

        public static bool TryKeyCode(string name, out uint vk)
        {
            vk = 0;
            if (string.IsNullOrEmpty(name)) return false;
            name = name.Trim();
            if (NameToVk.TryGetValue(name, out vk)) return true;
            if (name.StartsWith("VK_", StringComparison.OrdinalIgnoreCase))
            {
                string hex = name.Substring(3);
                if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) hex = hex.Substring(2);
                uint v;
                if (uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out v))
                {
                    vk = v;
                    return true;
                }
            }
            return false;
        }

        public static string Combo(bool ctrl, bool alt, bool shift, bool win)
        {
            var sb = new StringBuilder();
            if (ctrl) sb.Append("Ctrl+");
            if (alt) sb.Append("Alt+");
            if (shift) sb.Append("Shift+");
            if (win) sb.Append("Win+");
            return sb.ToString();
        }

        public static string KeyCombo(bool ctrl, bool alt, bool shift, bool win, uint vk)
        {
            if (vk == 0) return "";
            return Combo(ctrl, alt, shift, win) + KeyName(vk);
        }

        /// <summary>解析 "Ctrl+Alt+Shift+F12" / "Alt+左键" 这类文本</summary>
        public static bool TryParseCombo(string text, out bool ctrl, out bool alt,
                                         out bool shift, out bool win, out string last)
        {
            ctrl = alt = shift = win = false;
            last = "";
            if (string.IsNullOrEmpty(text)) return false;

            string[] parts = text.Split('+');
            // "Alt++" 这种键名本身带加号的，拼接回来
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                if (p.Length == 0)
                {
                    if (i > 0 && i < parts.Length - 1) { parts[i - 1] = parts[i - 1] + "+"; }
                    continue;
                }
                string up = p.ToUpperInvariant();
                if (up == "CTRL" || up == "CONTROL") { ctrl = true; continue; }
                if (up == "ALT") { alt = true; continue; }
                if (up == "SHIFT") { shift = true; continue; }
                if (up == "WIN" || up == "WINDOWS") { win = true; continue; }
                last = p;
            }
            return last.Length > 0;
        }
    }

    /// <summary>一条「修饰键 + 鼠标键拖拽」规则</summary>
    internal class MouseRule
    {
        public bool Ctrl, Alt, Shift, Win;
        public int Button; // 0=左键 1=中键 2=右键

        public static string ButtonName(int b)
        {
            if (b == 1) return "中键";
            if (b == 2) return "右键";
            return "左键";
        }

        public override string ToString()
        {
            return RuleFormat.Combo(Ctrl, Alt, Shift, Win) + ButtonName(Button) + "拖拽";
        }

        public string Serialize()
        {
            string mods = RuleFormat.Combo(Ctrl, Alt, Shift, Win).TrimEnd('+');
            string btn = Button == 1 ? "Middle" : (Button == 2 ? "Right" : "Left");
            return mods.Length == 0 ? btn : mods + "+" + btn;
        }

        public static bool TryParse(string s, out MouseRule rule)
        {
            rule = null;
            bool c, a, sh, w;
            string last;
            if (!RuleFormat.TryParseCombo(s, out c, out a, out sh, out w, out last)) return false;

            int btn;
            if (string.Equals(last, "Left", StringComparison.OrdinalIgnoreCase)) btn = 0;
            else if (string.Equals(last, "Middle", StringComparison.OrdinalIgnoreCase)) btn = 1;
            else if (string.Equals(last, "Right", StringComparison.OrdinalIgnoreCase)) btn = 2;
            else return false;

            rule = new MouseRule { Ctrl = c, Alt = a, Shift = sh, Win = w, Button = btn };
            return true;
        }

        public bool SameAs(MouseRule other)
        {
            return other != null && Ctrl == other.Ctrl && Alt == other.Alt &&
                   Shift == other.Shift && Win == other.Win && Button == other.Button;
        }
    }

    /// <summary>
    /// 一条「修饰键 + 键盘按键」规则。
    /// Forward=false 时：这个组合在目标程序里被吃掉，什么都没发生。
    /// Forward=true 时：吃掉原组合，再替用户重新按一遍 TVK（快捷键转发/改绑）。
    /// </summary>
    internal class KeyRule
    {
        public bool Ctrl, Alt, Shift, Win;
        public uint VK;

        public bool Forward;
        public bool TCtrl, TAlt, TShift, TWin;
        public uint TVK;

        public bool HasTarget { get { return Forward && TVK != 0; } }

        public override string ToString()
        {
            string src = RuleFormat.KeyCombo(Ctrl, Alt, Shift, Win, VK);
            if (!HasTarget) return src + "　→　禁用";
            return src + "　→　" + RuleFormat.KeyCombo(TCtrl, TAlt, TShift, TWin, TVK);
        }

        public string Serialize()
        {
            string src = RuleFormat.KeyCombo(Ctrl, Alt, Shift, Win, VK);
            if (!HasTarget) return src;
            return src + ">" + RuleFormat.KeyCombo(TCtrl, TAlt, TShift, TWin, TVK);
        }

        public static bool TryParse(string s, out KeyRule rule)
        {
            rule = null;
            if (string.IsNullOrEmpty(s)) return false;

            string left = s, right = null;
            int gt = s.IndexOf('>');
            if (gt >= 0)
            {
                left = s.Substring(0, gt);
                right = s.Substring(gt + 1);
            }

            bool c, a, sh, w;
            string last;
            if (!RuleFormat.TryParseCombo(left, out c, out a, out sh, out w, out last)) return false;
            uint vk;
            if (!RuleFormat.TryKeyCode(last, out vk)) return false;

            var r = new KeyRule { Ctrl = c, Alt = a, Shift = sh, Win = w, VK = vk };

            if (!string.IsNullOrEmpty(right))
            {
                bool c2, a2, s2, w2;
                string last2;
                if (RuleFormat.TryParseCombo(right, out c2, out a2, out s2, out w2, out last2))
                {
                    uint vk2;
                    if (RuleFormat.TryKeyCode(last2, out vk2))
                    {
                        r.Forward = true;
                        r.TCtrl = c2; r.TAlt = a2; r.TShift = s2; r.TWin = w2;
                        r.TVK = vk2;
                    }
                }
            }

            rule = r;
            return true;
        }

        public bool SameAs(KeyRule other)
        {
            if (other == null) return false;
            if (Ctrl != other.Ctrl || Alt != other.Alt || Shift != other.Shift ||
                Win != other.Win || VK != other.VK) return false;
            if (HasTarget != other.HasTarget) return false;
            if (!HasTarget) return true;
            return TCtrl == other.TCtrl && TAlt == other.TAlt && TShift == other.TShift &&
                   TWin == other.TWin && TVK == other.TVK;
        }
    }

    /// <summary>一个目标程序 + 它的规则集合</summary>
    internal class Profile
    {
        public string Name = "";
        public List<MouseRule> Mouse = new List<MouseRule>();
        public List<KeyRule> Keys = new List<KeyRule>();

        public override string ToString() { return Name; }

        public Profile Clone()
        {
            var p = new Profile { Name = Name };
            foreach (MouseRule m in Mouse) p.Mouse.Add(new MouseRule { Ctrl = m.Ctrl, Alt = m.Alt, Shift = m.Shift, Win = m.Win, Button = m.Button });
            foreach (KeyRule k in Keys)
                p.Keys.Add(new KeyRule
                {
                    Ctrl = k.Ctrl, Alt = k.Alt, Shift = k.Shift, Win = k.Win, VK = k.VK,
                    Forward = k.Forward, TCtrl = k.TCtrl, TAlt = k.TAlt,
                    TShift = k.TShift, TWin = k.TWin, TVK = k.TVK
                });
            return p;
        }
    }
}
