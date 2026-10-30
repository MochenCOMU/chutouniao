// 只用来把安装界面渲染成 PNG（不参与发布）
// 编译： csc /target:exe /main:InstallerShot /win32icon:src\icon.ico /out:InstallerShot.exe installer\Setup.cs _test\InstallerShot.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class InstallerShot
{
    const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Chutouniao";

    [STAThread]
    static void Main(string[] args)
    {
        string outPath = args.Length > 0 ? args[0] : "installer.png";

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        Setup.InstallDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "Chutouniao");

        // 临时摘掉卸载登记，让截图是「首次安装」的样子（截完再放回去）
        Dictionary<string, object> backup = BackupUninstallKey();

        try
        {
            var f = new InstallerForm();
            f.StartPosition = FormStartPosition.Manual;
            f.Location = new Point(-4000, 0);   // 摆到屏幕外，别打扰人
            f.Show();
            Application.DoEvents();
            System.Threading.Thread.Sleep(250);
            Application.DoEvents();

            using (var bmp = new Bitmap(f.Width, f.Height))
            {
                f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height));
                bmp.Save(outPath, ImageFormat.Png);
                Console.WriteLine("saved " + outPath + "  " + bmp.Width + "x" + bmp.Height);
            }
        }
        finally
        {
            RestoreUninstallKey(backup);
        }

        Environment.Exit(0);
    }

    static Dictionary<string, object> BackupUninstallKey()
    {
        var d = new Dictionary<string, object>();
        try
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(UninstallKey, false))
            {
                if (k == null) return d;
                foreach (string name in k.GetValueNames()) d[name] = k.GetValue(name);
            }
            Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false);
        }
        catch { }
        return d;
    }

    static void RestoreUninstallKey(Dictionary<string, object> d)
    {
        if (d.Count == 0) return;
        try
        {
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(UninstallKey))
            {
                foreach (var kv in d)
                {
                    if (kv.Value is int) k.SetValue(kv.Key, kv.Value, RegistryValueKind.DWord);
                    else k.SetValue(kv.Key, kv.Value);
                }
            }
        }
        catch { }
    }
}
