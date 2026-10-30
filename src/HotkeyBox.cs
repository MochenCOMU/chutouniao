using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Chutouniao
{
    /// <summary>按键捕获控件：点一下，然后直接按组合键就记下来</summary>
    internal class HotkeyBox : Control
    {
        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);

        bool _capturing;
        public bool Ctrl, Alt, Shift, Win;
        public uint VK;

        public event EventHandler Changed;

        public HotkeyBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            TabStop = true;
            BackColor = Color.White;
            Cursor = Cursors.Hand;
            Size = new Size(300, 28);
        }

        public bool HasValue { get { return VK != 0; } }

        public string Value
        {
            get { return VK == 0 ? "" : RuleFormat.KeyCombo(Ctrl, Alt, Shift, Win, VK); }
            set
            {
                Ctrl = Alt = Shift = Win = false;
                VK = 0;
                if (!string.IsNullOrEmpty(value))
                {
                    bool c, a, s, w;
                    string last;
                    if (RuleFormat.TryParseCombo(value, out c, out a, out s, out w, out last))
                    {
                        uint vk;
                        if (RuleFormat.TryKeyCode(last, out vk))
                        {
                            Ctrl = c; Alt = a; Shift = s; Win = w; VK = vk;
                        }
                    }
                }
                Invalidate();
            }
        }

        public void Clear()
        {
            Ctrl = Alt = Shift = Win = false;
            VK = 0;
            Invalidate();
            RaiseChanged();
        }

        void RaiseChanged()
        {
            EventHandler h = Changed;
            if (h != null) h(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);

            using (var back = new SolidBrush(Enabled ? Color.White : Color.FromArgb(240, 240, 242)))
                g.FillRectangle(back, r);
            using (var pen = new Pen(_capturing ? Color.FromArgb(0, 120, 215) : Color.FromArgb(190, 190, 195)))
                g.DrawRectangle(pen, r);

            string text;
            Color color;
            if (_capturing)
            {
                text = "请按下组合键…（Esc 取消，Backspace 清除）";
                color = Color.FromArgb(0, 100, 190);
            }
            else if (VK == 0)
            {
                text = "点击这里，然后按下组合键";
                color = Color.FromArgb(140, 140, 150);
            }
            else
            {
                text = Value;
                color = Color.FromArgb(20, 20, 25);
            }

            TextRenderer.DrawText(g, text, Font, new Rectangle(8, 0, Width - 16, Height),
                Enabled ? color : Color.FromArgb(160, 160, 165),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && Enabled) BeginCapture();
        }

        protected override void OnEnter(EventArgs e)
        {
            base.OnEnter(e);
            Invalidate();
        }

        protected override void OnLeave(EventArgs e)
        {
            base.OnLeave(e);
            if (_capturing)
            {
                _capturing = false;
                Invalidate();
            }
        }

        public void BeginCapture()
        {
            _capturing = true;
            Focus();
            Invalidate();
        }

        protected override bool IsInputKey(Keys keyData)
        {
            return true; // Tab / 方向键 / 功能键都要能收到
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (_capturing)
            {
                CaptureKeys(keyData);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (_capturing)
            {
                CaptureKeys(e.KeyData);
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }
            base.OnKeyDown(e);
        }

        void CaptureKeys(Keys keyData)
        {
            Keys mods = keyData & Keys.Modifiers;
            Keys code = keyData & Keys.KeyCode;

            if (code == Keys.Back || code == Keys.Delete)
            {
                _capturing = false;
                Ctrl = Alt = Shift = Win = false;
                VK = 0;
                Invalidate();
                RaiseChanged();
                return;
            }
            if (code == Keys.Escape)
            {
                _capturing = false;
                Invalidate();
                return;
            }

            // 只按了修饰键：继续等
            if (code == Keys.ControlKey || code == Keys.ShiftKey || code == Keys.Menu ||
                code == Keys.LWin || code == Keys.RWin || code == Keys.None)
                return;

            bool c = (mods & Keys.Control) == Keys.Control;
            bool a = (mods & Keys.Alt) == Keys.Alt;
            bool s = (mods & Keys.Shift) == Keys.Shift;
            bool w = ((GetAsyncKeyState(0x5B) & 0x8000) != 0) || ((GetAsyncKeyState(0x5C) & 0x8000) != 0);

            Ctrl = c; Alt = a; Shift = s; Win = w;
            VK = (uint)code;
            _capturing = false;
            Invalidate();
            RaiseChanged();
        }
    }
}
