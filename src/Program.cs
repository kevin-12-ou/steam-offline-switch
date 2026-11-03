using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace SteamOfflineSwitch
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // 命令行模式：--cli status|offline|online|ensure
                if (HasArg(args, "--cli"))
                {
                    RunCli(args);
                    return;
                }

                string mutexName = "SteamOfflineSwitch_" + Environment.UserName;
            bool created;
            Mutex mutex = new Mutex(true, mutexName, out created);
            bool owns = created;
            try
            {
                if (!created)
                {
                    bool isRelaunch = HasArg(args, "--elevated");
                    if (isRelaunch)
                    {
                        try { owns = mutex.WaitOne(6000); }
                        catch { owns = false; }
                    }
                    if (!owns)
                    {
                        // 已有实例在运行：通知它把窗口显示出来，然后退出。
                        // 这样用户再次双击图标时，即使原窗口被收进了托盘，也能重新打开。
                        try { MainForm.ActivateExisting(); } catch { }
                        return;
                    }
                }

                Settings settings = Store.Load();
                Log.I("启动 v" + AppInfo.Version + " args=" + JoinArgs(args));

                if (!Elevation.IsAdmin())
                {
                    if (Elevation.RelaunchElevated(JoinArgs(args)))
                    {
                        Log.I("已请求以管理员身份重新启动");
                        return;
                    }
                    MessageBox.Show(
                        "本程序需要管理员权限才能管理 Windows 防火墙。\n"
                        + "已尝试自动提权但被取消或失败，请右键选择“以管理员身份运行”。",
                        AppInfo.Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                Application.Run(new MainForm(settings, args));
            }
            finally
            {
                if (owns)
                {
                    try { mutex.ReleaseMutex(); } catch { }
                }
                try { mutex.Dispose(); } catch { }
            }
            }
            catch (Exception ex)
            {
                try { Log.E("启动失败", ex); } catch { }
                try
                {
                    MessageBox.Show(
                        "程序启动失败：\n" + ex.Message,
                        AppInfo.Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch { }
            }
        }

        private static void RunCli(string[] args)
        {
            string action = ArgValue(args, "--cli");
            if (string.IsNullOrEmpty(action)) action = "status";

            if (!Elevation.IsAdmin())
            {
                if (Elevation.RelaunchElevated(JoinArgs(args))) return;
            }

            string result;
            try
            {
                Settings s = Store.Load();
                if (string.IsNullOrEmpty(s.SteamExe) || !File.Exists(s.SteamExe))
                    s.SteamExe = SteamLocator.Find();

                switch (action.ToLowerInvariant())
                {
                    case "status":
                        result = Firewall.IsBlocked() ? "BLOCKED" : "OPEN";
                        break;
                    case "ensure":
                        result = Firewall.EnsureRules(s.SteamExe) ? "OK" : "FAIL";
                        break;
                    case "offline":
                        if (!Firewall.RulesExist()) Firewall.EnsureRules(s.SteamExe);
                        result = Firewall.SetBlocked(true) && Firewall.IsBlocked() ? "BLOCKED" : "FAIL";
                        break;
                    case "online":
                        if (!Firewall.RulesExist()) Firewall.EnsureRules(s.SteamExe);
                        result = Firewall.SetBlocked(false) && !Firewall.IsBlocked() ? "OPEN" : "FAIL";
                        break;
                    default:
                        result = "UNKNOWN_ACTION";
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.E("CLI 执行失败", ex);
                result = "ERROR: " + ex.Message;
            }

            try
            {
                File.WriteAllText(Path.Combine(Store.Dir, "cli-result.txt"),
                    action + " => " + result + Environment.NewLine);
            }
            catch { }
        }

        private static string JoinArgs(string[] args)
        {
            return args == null ? "" : string.Join(" ", args);
        }

        private static bool HasArg(string[] args, string name)
        {
            if (args == null) return false;
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string ArgValue(string[] args, string name)
        {
            if (args == null) return null;
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }
    }
}
