using System;
using System.Threading;
using System.Windows.Forms;

namespace Chutouniao
{
    internal static class Program
    {
        public const string Version = "4.1.0";
        public const string Name = "出头鸟 Chutouniao";
        public const string HomeUrl = "https://github.com/MochenCOMU/chutouniao";
        public const string Copyright = "© 2026 墨琛COMU（墨琛_COMU / MochenCOMU）";

        static EventWaitHandle _showSignal;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool AllowSetForegroundWindow(int dwProcessId);

        [STAThread]
        static void Main()
        {
            bool createdNew;
            using (Mutex mutex = new Mutex(true, @"Local\Chutouniao.SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    // 已经有一个在跑：让它把设置窗口调出来，然后自己退出
                    try
                    {
                        AllowSetForegroundWindow(-1); // ASFW_ANY：把置前权限交给已经在跑的那个实例
                        using (EventWaitHandle ev = EventWaitHandle.OpenExisting(App.ShowSettingsEventName))
                            ev.Set();
                    }
                    catch { }
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // 先装一个 WinForms 的同步上下文：拦截器要靠它把「转发按键」排到消息循环里执行，
                // 而不是在低层钩子回调里直接 SendInput。
                System.Threading.SynchronizationContext.SetSynchronizationContext(
                    new System.Windows.Forms.WindowsFormsSynchronizationContext());
                Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e)
                {
                    MessageBox.Show("出错了：" + e.Exception.Message, "出头鸟 Chutouniao",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                };

                _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, App.ShowSettingsEventName);

                Application.Run(new App());

                GC.KeepAlive(_showSignal);
            }
        }
    }
}
