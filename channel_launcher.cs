using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

// ZZZ Channel Switcher - switch ZenlessZoneZero between official and bilibili channel
// Compile official : csc /target:winexe /win32manifest:launcher.manifest /out:LaunchOfficial.exe /r:System.Windows.Forms.dll channel_launcher.cs
// Compile bilibili: csc /target:winexe /win32manifest:launcher.manifest /define:BILIBILI /out:LaunchBilibili.exe /r:System.Windows.Forms.dll channel_launcher.cs
class ChannelLauncher
{
#if BILIBILI
    const bool BILIBILI_MODE = true;
#else
    const bool BILIBILI_MODE = false;
#endif

    const string SETTINGS_FILE = "launcher.ini";
    const string LOG_FILE = "switch.log";
    const string GAME_EXE = "ZenlessZoneZero.exe";
    const string CFG = "config.ini";
    const string CFG_OFFICIAL = "config.ini.g";
    const string CFG_BILIBILI = "config.ini.b";

    // Bilibili channel config template (channel=14 = bilibili, public community info).
    // Written automatically when config.ini.b is missing, so the repo stays self-contained.
    const string BILIBILI_TEMPLATE =
        "[general]\r\n" +
        "channel=14\r\n" +
        "sub_channel=0\r\n" +
        "cps=zzz_bilibili_pc\r\n" +
        "uapc={\"hyp\":{\"uapc\":\"\"},\"nap_cn\":{\"uapc\":\"\"}}\r\n";

    static string AppDir { get { return Path.GetDirectoryName(Application.ExecutablePath); } }

    static void Log(string msg)
    {
        try
        {
            File.AppendAllText(Path.Combine(AppDir, LOG_FILE),
                DateTime.Now.ToString("HH:mm:ss") + " [" + (BILIBILI_MODE ? "BILI" : "OFFI") + "] " + msg + "\r\n");
        }
        catch { }
    }

    static string LoadOrPickGameDir()
    {
        string ini = Path.Combine(AppDir, SETTINGS_FILE);
        if (File.Exists(ini))
        {
            string dir = File.ReadAllText(ini).Trim();
            if (Directory.Exists(dir) && File.Exists(Path.Combine(dir, GAME_EXE)))
                return dir;
        }
        using (FolderBrowserDialog fbd = new FolderBrowserDialog())
        {
            fbd.Description = "Select the ZenlessZoneZero game folder (the one containing ZenlessZoneZero.exe)";
            fbd.ShowNewFolderButton = false;
            if (fbd.ShowDialog() == DialogResult.OK)
            {
                string dir = fbd.SelectedPath;
                if (File.Exists(Path.Combine(dir, GAME_EXE)))
                {
                    try { File.WriteAllText(ini, dir); } catch { }
                    return dir;
                }
                MessageBox.Show("The selected folder does not contain ZenlessZoneZero.exe.",
                    "ZZZ Channel Switcher", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
        }
        return null;
    }

    // Overwrite-move: safe because we only overwrite a backup whose content
    // matches the file being moved (decided by ReadChannel before calling).
    static void MoveOverwrite(string src, string dst)
    {
        if (!File.Exists(src)) return;
        if (File.Exists(dst)) File.Delete(dst);
        File.Move(src, dst);
    }

    // Read the channel field of config.ini: 1 = official, 14 = bilibili, 0 = unknown/missing.
    static int ReadChannel(string cfg)
    {
        try
        {
            foreach (string line in File.ReadAllLines(cfg))
            {
                string t = line.Trim();
                if (t.StartsWith("channel=", StringComparison.OrdinalIgnoreCase))
                {
                    int v;
                    if (int.TryParse(t.Substring(8).Trim(), out v)) return v;
                }
            }
        }
        catch { }
        return 0;
    }

    [STAThread]
    static void Main(string[] args)
    {
        Log("start, args=" + string.Join(" ", args));
        string gameDir = LoadOrPickGameDir();
        if (gameDir == null)
        {
            Log("no game dir");
            return;
        }
        Log("game dir: " + gameDir);

        string exe = Path.Combine(gameDir, GAME_EXE);
        string cfg = Path.Combine(gameDir, CFG);
        string cfgG = Path.Combine(gameDir, CFG_OFFICIAL);
        string cfgB = Path.Combine(gameDir, CFG_BILIBILI);

        if (Process.GetProcessesByName("ZenlessZoneZero").Length > 0)
        {
            Log("BLOCKED: game running");
            MessageBox.Show("Game is running. Please close it first before switching channel.",
                "ZZZ Channel Switcher", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        Log("game not running, proceed");

        try
        {
            int cur = ReadChannel(cfg);
            Log("current channel=" + cur + " (target " + (BILIBILI_MODE ? "14" : "1") + ")");

            if (BILIBILI_MODE)
            {
                if (cur != 14)
                {
                    // current(official) -> .g, then .b -> current
                    MoveOverwrite(cfg, cfgG);
                    if (!File.Exists(cfgB))
                    {
                        File.WriteAllText(cfgB, BILIBILI_TEMPLATE);
                        Log("created config.ini.b from template");
                    }
                    File.Move(cfgB, cfg);
                }
                else
                {
                    Log("already bilibili, skip switch");
                }
            }
            else
            {
                if (cur != 1)
                {
                    // current(bilibili) -> .b, then .g -> current
                    MoveOverwrite(cfg, cfgB);
                    if (File.Exists(cfgG))
                        File.Move(cfgG, cfg);
                    else
                        Log("WARN: official backup config.ini.g missing");
                }
                else
                {
                    Log("already official, skip switch");
                }
            }
            Log("after switch: cfg=" + File.Exists(cfg) + " cfgG=" + File.Exists(cfgG) + " cfgB=" + File.Exists(cfgB));
        }
        catch (Exception ex)
        {
            Log("SWITCH ERROR: " + ex.Message);
            MessageBox.Show("Failed to switch config file:\n" + ex.Message,
                "ZZZ Channel Switcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = exe,
            WorkingDirectory = gameDir,
            UseShellExecute = false
        };
        if (args.Length > 0)
            psi.Arguments = string.Join(" ", args);

        try
        {
            Process p = Process.Start(psi);
            p.WaitForExit(); // keep this process alive together with the game
        }
        catch (Exception ex)
        {
            MessageBox.Show("Failed to launch game:\n" + ex.Message,
                "ZZZ Channel Switcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
