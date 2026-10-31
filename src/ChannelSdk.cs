using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace ZzzSwitch
{
    // B 站渠道 SDK：PCGameSDK.dll + BLPlatform64\ + sdk_pkg_version + license.txt
    // 官服目录里一套都没有，切到 B 服前必须补齐（否则登录界面拿不到 B 站 SDK）。
    public static class ChannelSdk
    {
        public const string PluginDirRel = @"ZenlessZoneZero_Data\Plugins\x86_64";
        public const string SdkDllName = "PCGameSDK.dll";
        public const string SdkPlatformDirName = "BLPlatform64";
        public const string SdkManifestName = "sdk_pkg_version";
        public const string SdkLicenseName = "license.txt";
        public const string FallbackPluginVersion = "5.0.4";

        public static string PluginDir(string gameDir) { return Path.Combine(gameDir, PluginDirRel); }
        public static string SdkDllPath(string gameDir) { return Path.Combine(gameDir, Path.Combine(PluginDirRel, SdkDllName)); }
        public static string SdkPlatformPath(string gameDir) { return Path.Combine(gameDir, Path.Combine(PluginDirRel, SdkPlatformDirName)); }

        public static bool IsPresent(string gameDir, out int fileCount, out long totalBytes)
        {
            fileCount = 0;
            totalBytes = 0;
            if (string.IsNullOrEmpty(gameDir)) return false;
            string dll = SdkDllPath(gameDir);
            string plat = SdkPlatformPath(gameDir);
            if (!File.Exists(dll) || !Directory.Exists(plat)) return false;
            try
            {
                fileCount = 1;
                totalBytes = new FileInfo(dll).Length;
                string[] files = Directory.GetFiles(plat, "*", SearchOption.AllDirectories);
                fileCount += files.Length;
                for (int i = 0; i < files.Length; i++)
                {
                    try { totalBytes += new FileInfo(files[i]).Length; } catch { }
                }
            }
            catch { }
            return true;
        }

        // PCGameSDK.dll 的 FileVersion 形如 5.0.4.0 -> "5.0.4"
        public static string DetectPluginVersion(string gameDir)
        {
            try
            {
                string dll = SdkDllPath(gameDir);
                if (File.Exists(dll))
                {
                    FileVersionInfo vi = FileVersionInfo.GetVersionInfo(dll);
                    string v = vi.FileVersion;
                    if (!string.IsNullOrEmpty(v))
                    {
                        string[] parts = v.Trim().Split('.');
                        if (parts.Length >= 3) return parts[0] + "." + parts[1] + "." + parts[2];
                        if (v.Trim().Length > 0) return v.Trim();
                    }
                }
            }
            catch { }
            return FallbackPluginVersion;
        }

        // 从已装 B 服的目录补齐渠道 SDK；返回是否补齐成功
        public static bool CopyFrom(string sourceGameDir, string targetGameDir, List<string> report)
        {
            report.Add("SDK 来源：" + sourceGameDir);
            CopyFile(SdkDllPath(sourceGameDir), SdkDllPath(targetGameDir), report);

            string srcPlat = SdkPlatformPath(sourceGameDir);
            string dstPlat = SdkPlatformPath(targetGameDir);
            try
            {
                if (Directory.Exists(srcPlat))
                {
                    string[] files = Directory.GetFiles(srcPlat, "*", SearchOption.AllDirectories);
                    for (int i = 0; i < files.Length; i++)
                    {
                        string rel = files[i].Substring(srcPlat.Length).TrimStart('\\');
                        CopyFile(files[i], Path.Combine(dstPlat, rel), report);
                    }
                }
            }
            catch (Exception ex) { report.Add("FAIL 遍历 BLPlatform64：" + ex.Message); }

            CopyFile(Path.Combine(sourceGameDir, SdkManifestName), Path.Combine(targetGameDir, SdkManifestName), report);
            CopyFile(Path.Combine(PluginDir(sourceGameDir), SdkLicenseName), Path.Combine(PluginDir(targetGameDir), SdkLicenseName), report);

            int fc;
            long tb;
            bool ok = IsPresent(targetGameDir, out fc, out tb);
            report.Add("SDK 补齐结果：" + (ok ? ("成功 " + fc + " 文件 / " + (tb / 1048576) + " MB") : "失败"));
            return ok;
        }

        static void CopyFile(string src, string dst, List<string> report)
        {
            try
            {
                if (!File.Exists(src)) { return; }
                FileInfo si = new FileInfo(src);
                if (File.Exists(dst))
                {
                    FileInfo di = new FileInfo(dst);
                    if (di.Length == si.Length) { report.Add("skip(同尺寸) " + RelName(dst)); return; }
                }
                string d = Path.GetDirectoryName(dst);
                if (!string.IsNullOrEmpty(d)) Directory.CreateDirectory(d);
                File.Copy(src, dst, true);
                report.Add("copy " + RelName(dst) + " (" + si.Length + " B)");
            }
            catch (Exception ex) { report.Add("FAIL " + dst + " :: " + ex.Message); }
        }

        static string RelName(string path)
        {
            int i = path.IndexOf(@"Plugins\x86_64\", StringComparison.OrdinalIgnoreCase);
            return i >= 0 ? path.Substring(i) : Path.GetFileName(path);
        }
    }
}
