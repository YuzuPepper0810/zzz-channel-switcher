using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace ZzzSwitch
{
    // 启动器归属：HKCU\Software\miHoYo\HYP\<schema>\GameInstallPath 决定哪个启动器负责这个目录的更新。
    // 只读注册表，从不写。
    public static class LauncherMap
    {
        const string HypRoot = @"Software\miHoYo\HYP\";
        const string ValueName = "GameInstallPath";
        const string UninstallRoot = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
        const string UninstallRoot32 = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";

        public static string ReadInstallPath(string schema)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(HypRoot + schema, false))
                {
                    if (k == null) return null;
                    object v = k.GetValue(ValueName);
                    return v == null ? null : v.ToString();
                }
            }
            catch { return null; }
        }

        public static ChannelProfile ReadOwner(string gameDir)
        {
            if (string.IsNullOrEmpty(gameDir)) return null;
            string target = GameFolder.Normalize(gameDir);
            ChannelProfile[] all = ChannelProfile.All();
            for (int i = 0; i < all.Length; i++)
            {
                string p = ReadInstallPath(all[i].LauncherSchema);
                if (string.IsNullOrEmpty(p)) continue;
                if (string.Equals(GameFolder.Normalize(p), target, StringComparison.OrdinalIgnoreCase)) return all[i];
            }
            return null;
        }

        // 扫描卸载表，找 launcher.exe 实际存在的启动器目录（仅用于展示，不启动、不修改）
        public static List<string> FindLauncherExes()
        {
            List<string> found = new List<string>();
            RegistryKey[] roots = new RegistryKey[3];
            try { roots[0] = Registry.LocalMachine.OpenSubKey(UninstallRoot32, false); } catch { }
            try { roots[1] = Registry.LocalMachine.OpenSubKey(UninstallRoot, false); } catch { }
            try { roots[2] = Registry.CurrentUser.OpenSubKey(UninstallRoot, false); } catch { }

            for (int r = 0; r < roots.Length; r++)
            {
                RegistryKey root = roots[r];
                if (root == null) continue;
                try
                {
                    string[] subs = root.GetSubKeyNames();
                    for (int i = 0; i < subs.Length; i++)
                    {
                        RegistryKey sk = null;
                        try
                        {
                            sk = root.OpenSubKey(subs[i], false);
                            if (sk == null) continue;
                            object nameObj = sk.GetValue("DisplayName");
                            if (nameObj == null) continue;
                            string name = nameObj.ToString();
                            if (name.IndexOf("绝区零", StringComparison.Ordinal) < 0) continue;
                            object locObj = sk.GetValue("InstallLocation");
                            if (locObj == null) continue;
                            string loc = locObj.ToString().Trim().TrimEnd('\\');
                            if (loc.Length == 0) continue;
                            string exe = Path.Combine(loc, "launcher.exe");
                            if (File.Exists(exe) && !found.Contains(exe)) found.Add(exe);
                        }
                        catch { }
                        finally { if (sk != null) sk.Close(); }
                    }
                }
                catch { }
                finally { try { root.Close(); } catch { } }
            }
            return found;
        }
    }
}
