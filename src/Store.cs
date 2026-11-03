using System;
using System.IO;
using System.Text;

namespace SteamOfflineSwitch
{
    internal sealed class Settings
    {
        public string SteamExe = "";
        public bool ShowTray = true;
        public bool AutoRestoreOnStart = true;
        public bool MinimizeToTray = true;
        public bool CloseToTray = true;
        public bool StartWithWindows = false;
    }

    internal static class Store
    {
        private static string _dir;

        public static string Dir
        {
            get
            {
                if (_dir == null)
                {
                    _dir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "SteamOfflineSwitch");
                }
                try { Directory.CreateDirectory(_dir); } catch { }
                return _dir;
            }
        }

        public static string SettingsPath { get { return Path.Combine(Dir, "settings.ini"); } }
        public static string LogPath { get { return Path.Combine(Dir, "app.log"); } }

        public static Settings Load()
        {
            Settings s = new Settings();
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string[] lines = File.ReadAllLines(SettingsPath, Encoding.UTF8);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string line = lines[i].Trim();
                        if (line.Length == 0 || line[0] == '#' || line[0] == ';') continue;
                        int eq = line.IndexOf('=');
                        if (eq < 0) continue;
                        string k = line.Substring(0, eq).Trim();
                        string v = line.Substring(eq + 1).Trim();
                        if (k == "SteamExe") s.SteamExe = v;
                        else if (k == "ShowTray") s.ShowTray = (v == "1");
                        else if (k == "AutoRestoreOnStart") s.AutoRestoreOnStart = (v == "1");
                        else if (k == "MinimizeToTray") s.MinimizeToTray = (v == "1");
                        else if (k == "CloseToTray") s.CloseToTray = (v == "1");
                        else if (k == "StartWithWindows") s.StartWithWindows = (v == "1");
                    }
                }
            }
            catch (Exception ex) { Log.E("读取设置失败", ex); }
            return s;
        }

        public static void Save(Settings s)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# Steam 离线开关 配置文件");
            sb.AppendLine("SteamExe=" + (s.SteamExe == null ? "" : s.SteamExe));
            sb.AppendLine("ShowTray=" + (s.ShowTray ? "1" : "0"));
            sb.AppendLine("AutoRestoreOnStart=" + (s.AutoRestoreOnStart ? "1" : "0"));
            sb.AppendLine("MinimizeToTray=" + (s.MinimizeToTray ? "1" : "0"));
            sb.AppendLine("CloseToTray=" + (s.CloseToTray ? "1" : "0"));
            sb.AppendLine("StartWithWindows=" + (s.StartWithWindows ? "1" : "0"));
            try { File.WriteAllText(SettingsPath, sb.ToString(), new UTF8Encoding(false)); }
            catch (Exception ex) { Log.E("保存设置失败", ex); }
        }
    }

    internal static class Log
    {
        private static readonly object _lock = new object();

        public static void I(string m) { Write("INFO", m); }

        public static void E(string ctx, Exception ex)
        {
            Write("ERROR", ctx + (ex == null ? "" : " :: " + ex.GetType().Name + ": " + ex.Message));
        }

        public static void Write(string level, string message)
        {
            try
            {
                lock (_lock)
                {
                    string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                        + " [" + level + "] " + message + Environment.NewLine;
                    File.AppendAllText(Store.LogPath, line, new UTF8Encoding(false));
                }
            }
            catch { }
        }
    }
}
