using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace Chutouniao
{
    /// <summary>从正在运行的程序里挑一个进程名</summary>
    internal class ProcessPickerForm : Form
    {
        class Item
        {
            public string Name;
            public string Title;
            public override string ToString()
            {
                return Title.Length > 0 ? Name + "　—　" + Title : Name;
            }
        }

        readonly ListBox _list = new ListBox();
        readonly List<Item> _items = new List<Item>();

        public readonly List<string> SelectedNames = new List<string>();

        public ProcessPickerForm()
        {
            Text = "选择程序";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(560, 420);
            try { Font = new Font("Microsoft YaHei UI", 9F); }
            catch { Font = SystemFonts.MessageBoxFont; }
            try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { }

            var l = new Label();
            l.Text = "下面是当前有窗口的程序（可多选）：";
            l.SetBounds(12, 10, 400, 22);
            Controls.Add(l);

            _list.SetBounds(12, 36, 536, 310);
            _list.SelectionMode = SelectionMode.MultiExtended;
            _list.IntegralHeight = false;
            _list.DoubleClick += delegate { Accept(); };
            Controls.Add(_list);

            var btnRefresh = new Button();
            btnRefresh.Text = "刷新";
            btnRefresh.SetBounds(12, 356, 90, 30);
            btnRefresh.Click += delegate { Reload(); };
            Controls.Add(btnRefresh);

            var btnOk = new Button();
            btnOk.Text = "确定";
            btnOk.SetBounds(358, 356, 90, 30);
            btnOk.Click += delegate { Accept(); };
            Controls.Add(btnOk);

            var btnCancel = new Button();
            btnCancel.Text = "取消";
            btnCancel.SetBounds(458, 356, 90, 30);
            btnCancel.DialogResult = DialogResult.Cancel;
            Controls.Add(btnCancel);

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            Reload();
        }

        void Reload()
        {
            _list.Items.Clear();
            _items.Clear();

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var found = new List<Item>();

            try
            {
                foreach (Process p in Process.GetProcesses())
                {
                    try
                    {
                        if (p.Id == Process.GetCurrentProcess().Id) continue;
                        string name = p.ProcessName;
                        string title = "";
                        try { title = p.MainWindowTitle; } catch { }

                        if (string.IsNullOrEmpty(title)) continue;
                        if (!seen.Add(name)) continue;

                        found.Add(new Item { Name = name, Title = title });
                    }
                    catch { }
                    finally { p.Dispose(); }
                }
            }
            catch { }

            found.Sort(delegate(Item a, Item b) { return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase); });

            foreach (Item it in found)
            {
                _items.Add(it);
                _list.Items.Add(it);
            }
        }

        void Accept()
        {
            SelectedNames.Clear();
            foreach (object o in _list.SelectedItems)
                SelectedNames.Add(((Item)o).Name);

            if (SelectedNames.Count == 0)
            {
                MessageBox.Show(this, "先选一个程序。", "选择程序", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
