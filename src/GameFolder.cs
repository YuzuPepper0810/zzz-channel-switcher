using System;
using System.Diagnostics;
using System.IO;

namespace ZzzSwitch
{
    // 游戏目录、占用、版本账本相关的基础查询。全部只读，除了 launcher.ini 设置文件。
    public static class GameFolder
    {
        public const string GameExeName = "ZenlessZoneZero.exe";
        public const string ConfigName = "config.ini";
        public const string VersionInfoName = "version_info";
        public const string SettingsName = "launcher.ini";
        public const string BackupDirName = "switch-backup";
        public const string ProcessName = "ZenlessZoneZero";
        const string VersionPrefix = "CNPRODWin";

        public static string AppDir
        {
            get
            {
                string d = AppDomain.CurrentDomain.BaseDirectory;
                if (string.IsNullOrEmpty(d)) d = Environment.CurrentDirectory;
                return d.TrimEnd('\\');
            }
        }

        // exe 同目录优先（便携），不可写则落 %APPDATA%\ZZZSwitcher
        public static string SettingsPath()
        {
            string local = Path.Combine(AppDir, SettingsName);
            try
            {
                using (FileStream fs = new FileStream(local, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite)) { }
                return local;
            }
            catch { }
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ZZZSwitcher");
            try { Directory.CreateDirectory(dir); } catch { }
            return Path.Combine(dir, SettingsName);
        }

        public static string ReadSavedDir()
        {
            try
            {
                string p = SettingsPath();
                if (!File.Exists(p)) return null;
                string d = File.ReadAllText(p).Trim();
                if (d.Length > 0 && d[0] == '"' && d[d.Length - 1] == '"') d = d.Substring(1, d.Length - 2);
                return d.Trim();
            }
            catch { return null; }
        }

        public static bool SaveDir(string dir)
        {
            try
            {
                File.WriteAllText(SettingsPath(), dir, new System.Text.UTF8Encoding(false));
                return true;
            }
            catch { return false; }
        }

        public static bool IsValid(string dir)
        {
            if (string.IsNullOrEmpty(dir)) return false;
            try
            {
                return Directory.Exists(dir) && File.Exists(Path.Combine(dir, GameExeName));
            }
            catch { return false; }
        }

        public static string Normalize(string dir)
        {
            if (string.IsNullOrEmpty(dir)) return "";
            try { return Path.GetFullPath(dir).TrimEnd('\\'); }
            catch { return dir.TrimEnd('\\'); }
        }

        public static bool IsGameRunning()
        {
            try { return Process.GetProcessesByName(ProcessName).Length > 0; }
            catch { return false; }
        }

        // config.ini 独占打开测试：启动器/游戏正占用时返回 false
        public static bool CanLockConfig(string dir)
        {
            string cfg = Path.Combine(dir, ConfigName);
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    using (FileStream fs = new FileStream(cfg, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                    return true;
                }
                catch (IOException) { System.Threading.Thread.Sleep(120); }
                catch (UnauthorizedAccessException) { return false; }
            }
            return false;
        }

        // version_info 内容形如 CNPRODWin3.3.0，返回 "3.3.0"
        public static string ReadVersionInfo(string dir)
        {
            try
            {
                string p = Path.Combine(dir, VersionInfoName);
                if (!File.Exists(p)) return null;
                string t = File.ReadAllText(p).Trim();
                if (t.StartsWith(VersionPrefix, StringComparison.OrdinalIgnoreCase))
                    t = t.Substring(VersionPrefix.Length).Trim();
                return t.Length == 0 ? null : t;
            }
            catch { return null; }
        }

        public static string BackupDir(string dir) { return Path.Combine(dir, BackupDirName); }

        public static string GameExePath(string dir) { return Path.Combine(dir, GameExeName); }

        // Unity 播放器的渲染后端开关。2026-10-31 23:10 五组对照实测（每组 -logFile 单独落盘）：
        //   -force-d3d12  → Direct3D 11.0 [level 11.1]（无 Forcing 行，被忽略）
        //   --force-d3d12 → Direct3D 11.0（同上）
        //   -d3d12        → Direct3D 11.0（同上）
        //   -force-d3d11  → Forcing GfxDevice: Direct3D 11（对照组，证明参数解析正常）
        //   -use-d3d12    → Forcing GfxDevice: Direct3D 12 + d3d12: loaded! + Direct3D 12 [level 12.1] ✅
        // 结论：这个客户端（Unity 2019.4.40f1）认的是 -use-d3d12，force- 前缀对 D3D12 无效。
        public const string Dx12Argument = "-use-d3d12";
        public const string Dx11Argument = "-force-d3d11";

        public static string LaunchArguments(bool dx12)
        {
            return dx12 ? Dx12Argument : null;
        }

        // 统一启动入口：UI、CLI、--start 都走这里，避免参数拼装分散。
        public static Process StartGame(string dir, bool dx12)
        {
            if (!IsValid(dir)) throw new DirectoryNotFoundException("游戏目录无效：" + dir);
            ProcessStartInfo psi = new ProcessStartInfo(GameExePath(dir));
            psi.WorkingDirectory = dir;
            psi.UseShellExecute = true;
            string args = LaunchArguments(dx12);
            if (!string.IsNullOrEmpty(args)) psi.Arguments = args;
            return Process.Start(psi);
        }
    }
}
