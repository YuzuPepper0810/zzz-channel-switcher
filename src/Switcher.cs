using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ZzzSwitch
{
    public sealed class SwitchOutcome
    {
        public int Code;
        public string Message = "";
        public bool Wrote;
        public string BackupPath;
        public List<string> Detail = new List<string>();
    }

    public sealed class Status
    {
        public string Dir = "";
        public bool Exists;
        public int Channel = -1;
        public string ChannelName = "unknown";
        public string Cps = "";
        public string GameVersion = "";
        public string VersionInfo = "";
        public bool VersionConsistent;
        public bool SdkPresent;
        public int SdkFiles;
        public long SdkBytes;
        public string Owner = "none";
        public bool GameRunning;
        public string[] Launchers = new string[0];

        public string ToText()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("目录      : " + Dir);
            sb.AppendLine("可用      : " + (Exists ? "是" : "否"));
            sb.AppendLine("当前渠道  : " + (Channel < 0 ? "未知" : (Channel + " (" + ChannelName + ")")));
            sb.AppendLine("cps       : " + (Cps.Length == 0 ? "(缺失)" : Cps));
            sb.AppendLine("版本账本  : version_info=" + (VersionInfo.Length == 0 ? "(缺失)" : VersionInfo)
                          + " / game_version=" + (GameVersion.Length == 0 ? "(缺失)" : GameVersion)
                          + " -> " + (VersionConsistent ? "一致" : "不一致"));
            sb.AppendLine("B站SDK    : " + (SdkPresent ? ("存在 " + SdkFiles + " 文件 / " + (SdkBytes / 1048576) + " MB") : "缺失"));
            sb.AppendLine("启动器归属: " + Owner);
            sb.AppendLine("游戏进程  : " + (GameRunning ? "运行中" : "未运行"));
            for (int i = 0; i < Launchers.Length; i++) sb.AppendLine("启动器    : " + Launchers[i]);
            return sb.ToString().TrimEnd();
        }

        public string ToJson()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{");
            sb.Append("\"dir\":\"").Append(Esc(Dir)).Append("\",");
            sb.Append("\"exists\":").Append(Exists ? "true" : "false").Append(",");
            sb.Append("\"channel\":").Append(Channel).Append(",");
            sb.Append("\"channelName\":\"").Append(Esc(ChannelName)).Append("\",");
            sb.Append("\"cps\":\"").Append(Esc(Cps)).Append("\",");
            sb.Append("\"gameVersion\":\"").Append(Esc(GameVersion)).Append("\",");
            sb.Append("\"versionInfo\":\"").Append(Esc(VersionInfo)).Append("\",");
            sb.Append("\"versionConsistent\":").Append(VersionConsistent ? "true" : "false").Append(",");
            sb.Append("\"sdkPresent\":").Append(SdkPresent ? "true" : "false").Append(",");
            sb.Append("\"sdkFiles\":").Append(SdkFiles).Append(",");
            sb.Append("\"sdkSizeMb\":").Append(SdkBytes / 1048576).Append(",");
            sb.Append("\"owner\":\"").Append(Esc(Owner)).Append("\",");
            sb.Append("\"canSwitchToBilibili\":").Append((SdkPresent && VersionConsistent) ? "true" : "false").Append(",");
            sb.Append("\"canSwitchToOfficial\":").Append(VersionConsistent ? "true" : "false").Append(",");
            sb.Append("\"gameRunning\":").Append(GameRunning ? "true" : "false");
            sb.Append("}");
            return sb.ToString();
        }

        static string Esc(string s)
        {
            if (s == null) return "";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '"') sb.Append("\\\"");
                else if (c == '\\') sb.Append(@"\\");
                else if (c == '\r') sb.Append("\\r");
                else if (c == '\n') sb.Append("\\n");
                else if (c == '\t') sb.Append("\\t");
                else if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                else sb.Append(c);
            }
            return sb.ToString();
        }
    }

    public static class Switcher
    {
        public const int OK = 0;
        public const int VERSION_GATE = 2;
        public const int SDK_MISSING = 3;
        public const int BUSY = 4;
        public const int BAD_DIR = 5;
        public const int WRITE_FAILED = 6;

        public static int ReadChannel(IniFile ini)
        {
            string v = ini.GetFirst("channel");
            if (v == null) return -1;
            int n;
            if (!int.TryParse(v.Trim(), out n)) return -1;
            return n;
        }

        public static string ReadCps(IniFile ini)
        {
            string v = ini.GetFirst("cps");
            return v == null ? "" : v.Trim();
        }

        public static Status Check(string gameDir)
        {
            Status s = new Status();
            s.Dir = gameDir == null ? "" : gameDir;
            s.Exists = GameFolder.IsValid(gameDir);
            s.GameRunning = GameFolder.IsGameRunning();
            try { s.Launchers = LauncherMap.FindLauncherExes().ToArray(); } catch { }
            if (!s.Exists) return s;

            string cfg = Path.Combine(gameDir, GameFolder.ConfigName);
            if (File.Exists(cfg))
            {
                try
                {
                    IniFile ini = IniFile.FromFile(cfg);
                    s.Channel = ReadChannel(ini);
                    ChannelProfile p = ChannelProfile.FromChannel(s.Channel);
                    s.ChannelName = p == null ? "unknown" : p.Key;
                    s.Cps = ReadCps(ini);
                    string sec = ini.SectionHolding("game_version");
                    string gv = ini.Get(sec, "game_version");
                    s.GameVersion = gv == null ? "" : gv;
                }
                catch (Exception ex) { Log.Warn("读取 config.ini 失败：" + ex.Message); }
            }

            s.VersionInfo = GameFolder.ReadVersionInfo(gameDir);
            if (s.VersionInfo == null) s.VersionInfo = "";
            s.VersionConsistent = s.VersionInfo.Length > 0 && s.GameVersion.Length > 0
                && string.Equals(s.VersionInfo.Trim(), s.GameVersion.Trim(), StringComparison.OrdinalIgnoreCase);

            int fc;
            long tb;
            s.SdkPresent = ChannelSdk.IsPresent(gameDir, out fc, out tb);
            s.SdkFiles = fc;
            s.SdkBytes = tb;

            ChannelProfile owner = LauncherMap.ReadOwner(gameDir);
            s.Owner = owner == null ? "none" : owner.Key;
            return s;
        }

        public static SwitchOutcome Run(string gameDir, ChannelProfile target, bool dryRun, string sdkSource)
        {
            SwitchOutcome r = new SwitchOutcome();
            if (target == null) { r.Code = BAD_DIR; r.Message = "目标渠道未识别"; return r; }
            if (!GameFolder.IsValid(gameDir))
            {
                r.Code = BAD_DIR;
                r.Message = "目录无效或缺少 " + GameFolder.GameExeName;
                return r;
            }
            string cfg = Path.Combine(gameDir, GameFolder.ConfigName);
            if (!File.Exists(cfg))
            {
                r.Code = BAD_DIR;
                r.Message = "config.ini 不存在，先用官方/B站启动器完整安装一次";
                return r;
            }
            if (GameFolder.IsGameRunning())
            {
                r.Code = BUSY;
                r.Message = "游戏正在运行，先关闭 ZenlessZoneZero.exe";
                return r;
            }
            if (!GameFolder.CanLockConfig(gameDir))
            {
                r.Code = BUSY;
                r.Message = "config.ini 被占用（启动器可能刚写过），稍后重试";
                return r;
            }

            IniFile ini;
            try { ini = IniFile.FromFile(cfg); }
            catch (Exception ex) { r.Code = BAD_DIR; r.Message = "config.ini 读取失败：" + ex.Message; return r; }

            string sec = ini.SectionHolding("game_version");

            // 闸门 1：版本账本 version_info 必须等于 game_version
            string infoVer = GameFolder.ReadVersionInfo(gameDir);
            string gameVer = ini.Get(sec, "game_version");
            if (infoVer == null || gameVer == null
                || !string.Equals(infoVer.Trim(), gameVer.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                r.Code = VERSION_GATE;
                r.Message = "版本账本不一致：version_info=" + (infoVer == null ? "(缺失)" : infoVer)
                          + " / game_version=" + (gameVer == null ? "(缺失)" : gameVer)
                          + "；先让负责本目录的启动器把游戏更新完再切换";
                return r;
            }

            // 闸门 2：切 B 服必须有渠道 SDK
            if (target.RequiresSdk)
            {
                int fc;
                long tb;
                if (!ChannelSdk.IsPresent(gameDir, out fc, out tb))
                {
                    if (!string.IsNullOrEmpty(sdkSource) && GameFolder.IsValid(sdkSource))
                    {
                        int sfc;
                        long stb;
                        if (ChannelSdk.IsPresent(sdkSource, out sfc, out stb))
                        {
                            List<string> rep = new List<string>();
                            ChannelSdk.CopyFrom(sdkSource, gameDir, rep);
                            r.Detail.AddRange(rep);
                            for (int i = 0; i < rep.Count; i++) Log.Info("sdk: " + rep[i]);
                        }
                    }
                    if (!ChannelSdk.IsPresent(gameDir, out fc, out tb))
                    {
                        r.Code = SDK_MISSING;
                        r.Message = "缺少 B 站渠道 SDK（PCGameSDK.dll + BLPlatform64）；用 --sdk-source 指向已装 B 服的目录，或用 B 站启动器装一次";
                        return r;
                    }
                }
            }

            int cur = ReadChannel(ini);
            if (cur == int.Parse(target.Channel))
            {
                r.Code = OK;
                r.Wrote = false;
                r.Message = "已在 " + target.Display + "（channel=" + cur + "），未改动任何文件";
                Log.Info(r.Message);
                return r;
            }

            // 内存改写
            string gvBefore = ini.Get(sec, "game_version");
            int keysBefore = ini.CountKeys(sec);
            ini.Set(sec, "channel", target.Channel);
            ini.Set(sec, "sub_channel", target.SubChannel);
            ini.Set(sec, "cps", target.Cps);
            if (!string.IsNullOrEmpty(target.SdkPluginKey))
            {
                string pv = ChannelSdk.DetectPluginVersion(gameDir);
                ini.SetOrAdd(sec, target.SdkPluginKey, pv, "downloading_mode");
            }
            else
            {
                if (ini.Remove(sec, ChannelProfile.Bilibili.SdkPluginKey))
                    r.Detail.Add("移除 plugin_sdk_version（官服不需要该键，备份中保留）");
            }

            // 内存复核
            if (ReadChannel(ini) != int.Parse(target.Channel))
            {
                r.Code = WRITE_FAILED;
                r.Message = "改写复核失败：channel 未生效";
                return r;
            }
            string cps = ini.Get(sec, "cps");
            if (cps == null || !string.Equals(cps.Trim(), target.Cps, StringComparison.OrdinalIgnoreCase))
            {
                r.Code = WRITE_FAILED;
                r.Message = "改写复核失败：cps 未生效";
                return r;
            }
            string gvAfter = ini.Get(sec, "game_version");
            if (!string.Equals(gvBefore, gvAfter, StringComparison.Ordinal))
            {
                r.Code = WRITE_FAILED;
                r.Message = "拒绝写入：game_version 被意外改动";
                return r;
            }
            int delta = ini.CountKeys(sec) - keysBefore;
            if (delta < -1 || delta > 1)
            {
                r.Code = WRITE_FAILED;
                r.Message = "拒绝写入：段内键数变化异常 " + delta;
                return r;
            }

            if (dryRun)
            {
                r.Code = OK;
                r.Wrote = false;
                r.Message = "dry-run：" + ChannelProfile.NameOf(cur) + " → " + target.Display + "，未写盘";
                Log.Info(r.Message);
                return r;
            }

            // 备份
            string backupDir = GameFolder.BackupDir(gameDir);
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string fromName = ChannelProfile.FromChannel(cur) == null ? ("ch" + cur) : ChannelProfile.FromChannel(cur).Key;
            string backupPath = Path.Combine(backupDir, GameFolder.ConfigName + "." + stamp + "." + fromName + "-to-" + target.Key);
            try
            {
                Directory.CreateDirectory(backupDir);
                File.Copy(cfg, backupPath, true);
                WriteSha(backupPath);
                r.BackupPath = backupPath;
                Log.Info("备份：" + backupPath);
            }
            catch (Exception ex)
            {
                r.Code = WRITE_FAILED;
                r.Message = "备份失败，未写盘：" + ex.Message;
                return r;
            }

            // 原子落盘
            string tmp = cfg + ".tmp";
            try
            {
                File.WriteAllBytes(tmp, ini.ToBytes());
                if (File.Exists(cfg)) File.Replace(tmp, cfg, null);
                else File.Move(tmp, cfg);
            }
            catch (Exception ex)
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                try { File.Copy(backupPath, cfg, true); } catch { }
                r.Code = WRITE_FAILED;
                r.Message = "写入失败，已用备份回滚：" + ex.Message;
                Log.Error(r.Message);
                return r;
            }

            // 磁盘复核
            try
            {
                IniFile check = IniFile.FromFile(cfg);
                string chkSec = check.SectionHolding("channel");
                string chkCps = check.Get(chkSec, "cps");
                if (ReadChannel(check) != int.Parse(target.Channel)
                    || chkCps == null || !string.Equals(chkCps.Trim(), target.Cps, StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(backupPath, cfg, true);
                    r.Code = WRITE_FAILED;
                    r.Message = "磁盘复核失败，已用备份回滚";
                    Log.Error(r.Message);
                    return r;
                }
            }
            catch (Exception ex)
            {
                try { File.Copy(backupPath, cfg, true); } catch { }
                r.Code = WRITE_FAILED;
                r.Message = "磁盘复核异常，已用备份回滚：" + ex.Message;
                return r;
            }

            r.Code = OK;
            r.Wrote = true;
            r.Message = ChannelProfile.NameOf(cur) + " → " + target.Display + " 完成，备份 " + Path.GetFileName(backupPath);
            Log.Info(r.Message);
            return r;
        }

        public static SwitchOutcome Rollback(string gameDir)
        {
            SwitchOutcome r = new SwitchOutcome();
            if (!GameFolder.IsValid(gameDir)) { r.Code = BAD_DIR; r.Message = "目录无效"; return r; }
            if (GameFolder.IsGameRunning()) { r.Code = BUSY; r.Message = "游戏正在运行，先关闭"; return r; }

            string cfg = Path.Combine(gameDir, GameFolder.ConfigName);
            string backupDir = GameFolder.BackupDir(gameDir);
            if (!Directory.Exists(backupDir)) { r.Code = BAD_DIR; r.Message = "没有 switch-backup 目录，无备份可用"; return r; }

            string[] files;
            try { files = Directory.GetFiles(backupDir, GameFolder.ConfigName + ".*"); }
            catch (Exception ex) { r.Code = BAD_DIR; r.Message = "读取备份目录失败：" + ex.Message; return r; }
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            string latest = null;
            for (int i = files.Length - 1; i >= 0; i--)
            {
                if (files[i].EndsWith(".sha256", StringComparison.OrdinalIgnoreCase)) continue;
                if (files[i].EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) continue;
                latest = files[i];
                break;
            }
            if (latest == null) { r.Code = BAD_DIR; r.Message = "备份目录里没有 config.ini 备份"; return r; }

            string expect = null;
            try { if (File.Exists(latest + ".sha256")) expect = File.ReadAllText(latest + ".sha256").Trim(); }
            catch { }
            if (expect != null)
            {
                string actual = Sha256Of(latest);
                if (!string.Equals(expect, actual, StringComparison.OrdinalIgnoreCase))
                {
                    r.Code = WRITE_FAILED;
                    r.Message = "备份哈希不符，拒绝回滚：" + Path.GetFileName(latest);
                    return r;
                }
            }

            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string pre = Path.Combine(backupDir, GameFolder.ConfigName + "." + stamp + ".before-rollback");
            try
            {
                if (File.Exists(cfg)) { File.Copy(cfg, pre, true); WriteSha(pre); }
                File.Copy(latest, cfg, true);
                IniFile ini = IniFile.FromFile(cfg);
                r.Code = OK;
                r.Wrote = true;
                r.BackupPath = pre;
                r.Message = "已回滚到 " + Path.GetFileName(latest) + "（channel=" + ReadChannel(ini) + "），回滚前状态存 " + Path.GetFileName(pre);
                Log.Info(r.Message);
            }
            catch (Exception ex)
            {
                r.Code = WRITE_FAILED;
                r.Message = "回滚失败：" + ex.Message;
                Log.Error(r.Message);
            }
            return r;
        }

        static void WriteSha(string path)
        {
            try { File.WriteAllText(path + ".sha256", Sha256Of(path), new UTF8Encoding(false)); }
            catch { }
        }

        static string Sha256Of(string path)
        {
            try
            {
                using (SHA256 sha = SHA256.Create())
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    byte[] h = sha.ComputeHash(fs);
                    StringBuilder sb = new StringBuilder(h.Length * 2);
                    for (int i = 0; i < h.Length; i++) sb.Append(h[i].ToString("x2"));
                    return sb.ToString();
                }
            }
            catch { return ""; }
        }
    }
}
