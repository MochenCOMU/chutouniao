// 仅用于开发期截图检查：把设置窗口的每个标签页渲染成 PNG，不参与发布。
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

internal static class FormShot
{
    static string _dir;

    [STAThread]
    static void Main(string[] args)
    {
        _dir = args.Length > 0 ? args[0] : ".";
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var app = new Chutouniao.App();
        var f = new Chutouniao.SettingsForm(app);
        f.StartPosition = FormStartPosition.Manual;
        f.Location = new Point(-3000, 0); // 摆到屏幕外，不打扰正在用电脑的人
        f.Show();
        Application.DoEvents();

        TabControl tabs = FindTab(f);
        if (tabs == null) { Console.WriteLine("no tabcontrol"); return; }

        string[] names = { "tab-programs", "tab-details", "tab-general" };
        for (int i = 0; i < tabs.TabPages.Count && i < names.Length; i++)
        {
            tabs.SelectedIndex = i;
            Application.DoEvents();
            f.Refresh();
            Application.DoEvents();
            System.Threading.Thread.Sleep(120);

            using (var bmp = new Bitmap(f.Width, f.Height))
            {
                f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height));
                bmp.Save(Path.Combine(_dir, names[i] + ".png"), ImageFormat.Png);
                Console.WriteLine("saved " + names[i] + ".png");
            }
        }

        // 顺手把“选择程序”对话框也渲染一张
        using (var picker = new Chutouniao.ProcessPickerForm())
        {
            picker.Show();
            Application.DoEvents();
            System.Threading.Thread.Sleep(200);
            using (var bmp = new Bitmap(picker.Width, picker.Height))
            {
                picker.DrawToBitmap(bmp, new Rectangle(0, 0, picker.Width, picker.Height));
                bmp.Save(Path.Combine(_dir, "dlg-picker.png"), ImageFormat.Png);
                Console.WriteLine("saved dlg-picker.png");
            }
        }

        f.Close();
        Environment.Exit(0);
    }

    static TabControl FindTab(Control parent)
    {
        foreach (Control c in parent.Controls)
        {
            if (c is TabControl) return (TabControl)c;
            TabControl t = FindTab(c);
            if (t != null) return t;
        }
        return null;
    }
}
