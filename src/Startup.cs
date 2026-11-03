using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SteamOfflineSwitch
{
    internal static class Startup
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "SteamOfflineSwitch";

        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    if (k == null) return false;
                    return k.GetValue(ValueName) != null;
                }
            }
            catch { return false; }
        }

        public static void Set(bool enabled)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k == null) return;
                    if (enabled)
                        k.SetValue(ValueName, "\"" + Application.ExecutablePath + "\" --autostart");
                    else
                    {
                        try { k.DeleteValue(ValueName, false); } catch { }
                    }
                }
            }
            catch (Exception ex) { Log.E("设置开机启动失败", ex); }
        }
    }
}
