using System;
using System.IO;
using Microsoft.Win32;

namespace SteamOfflineSwitch
{
    internal static class SteamLocator
    {
        public static string Find()
        {
            string p = ReadReg(Registry.CurrentUser, @"Software\Valve\Steam", "SteamExe");
            if (Valid(p)) return Normalize(p);

            p = ReadReg(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath");
            if (!string.IsNullOrEmpty(p))
            {
                string cand = Path.Combine(p, "steam.exe");
                if (Valid(cand)) return cand;
            }

            p = ReadReg(Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath");
            if (!string.IsNullOrEmpty(p))
            {
                string cand = Path.Combine(p, "steam.exe");
                if (Valid(cand)) return cand;
            }

            string def = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                @"Steam\steam.exe");
            if (Valid(def)) return def;

            return "";
        }

        private static string ReadReg(RegistryKey root, string sub, string name)
        {
            try
            {
                using (RegistryKey k = root.OpenSubKey(sub, false))
                {
                    if (k == null) return null;
                    object v = k.GetValue(name);
                    return v == null ? null : v.ToString();
                }
            }
            catch { return null; }
        }

        private static bool Valid(string p)
        {
            try { return !string.IsNullOrEmpty(p) && File.Exists(p); }
            catch { return false; }
        }

        private static string Normalize(string p)
        {
            try { return Path.GetFullPath(p); }
            catch { return p; }
        }
    }
}
