using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows.Forms;

namespace SteamOfflineSwitch
{
    internal static class Elevation
    {
        public static bool IsAdmin()
        {
            try
            {
                using (WindowsIdentity id = WindowsIdentity.GetCurrent())
                {
                    return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch { return false; }
        }

        // 以管理员身份重新启动自己。成功则调用方应立即退出。
        public static bool RelaunchElevated(string originalArgs)
        {
            try
            {
                string a = (originalArgs == null ? "" : originalArgs);
                a = a.Replace("--elevated", "").Trim();
                if (a.Length > 0) a = a + " --elevated";
                else a = "--elevated";

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = Application.ExecutablePath;
                psi.Arguments = a;
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                psi.WindowStyle = ProcessWindowStyle.Normal;
                Process.Start(psi);
                return true;
            }
            catch (Exception ex) { Log.E("请求管理员权限失败", ex); return false; }
        }
    }
}
