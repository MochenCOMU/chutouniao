// 开发期工具：打印当前配置解析结果，确认迁移/解析没问题
using System;
using PsCtrlGuard;

internal static class ConfigDump
{
    static void Main(string[] args)
    {
        string path = args.Length > 0 ? args[0] : null;
        Config c = path == null ? Config.Load() : Config.Load(path);

        Console.WriteLine("config path : " + (path ?? Config.ConfigPath));
        Console.WriteLine("profiles    : " + c.Profiles.Count);
        Console.WriteLine("hotkey      : " + (c.Hotkey.Length == 0 ? "(none)" : c.Hotkey));
        Console.WriteLine("replayClick : " + c.ReplayClick + "  threshold=" + c.ClickThreshold);
        Console.WriteLine("startup     : " + c.StartWithWindows);

        foreach (Profile p in c.Profiles)
        {
            Console.WriteLine();
            Console.WriteLine("[" + p.Name + "]");
            foreach (MouseRule m in p.Mouse) Console.WriteLine("  mouse: " + m.ToString() + "   (" + m.Serialize() + ")");
            foreach (KeyRule k in p.Keys) Console.WriteLine("  key  : " + k.ToString() + "   (" + k.Serialize() + ")");
        }
    }
}
