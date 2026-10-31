using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace ZzzSwitch
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            Log.FilePath = Path.Combine(GameFolder.AppDir, "switch.log");
            try
            {
                return Run(args);
            }
            catch (Exception ex)
            {
                Log.Error("未处理异常：" + ex.ToString());
                Write(Console.Error, "严重错误：" + ex.Message);
                return 1;
            }
        }

        static int Run(string[] args)
        {
            string mode = "ui";
            string target = null;
            string dir = null;
            string sdkSource = null;
            bool json = false;
            bool dryRun = false;
            bool start = false;
            bool dx12 = false;

            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (Eq(a, "--check")) mode = "check";
                else if (Eq(a, "--switch"))
                {
                    mode = "switch";
                    if (i + 1 < args.Length) target = args[++i];
                }
                else if (Eq(a, "--rollback")) mode = "rollback";
                else if (Eq(a, "--launch") || Eq(a, "--play")) mode = "launch";
                else if (Eq(a, "--dx12") || Eq(a, "--d3d12")) dx12 = true;
                else if (Eq(a, "--json")) json = true;
                else if (Eq(a, "--dry-run")) dryRun = true;
                else if (Eq(a, "--start")) start = true;
                else if (Eq(a, "--dir")) { if (i + 1 < args.Length) dir = args[++i]; }
                else if (Eq(a, "--sdk-source")) { if (i + 1 < args.Length) sdkSource = args[++i]; }
                else if (a == "-h" || a == "/?" || Eq(a, "--help"))
                {
                    Write(Console.Out, HelpText());
                    return 0;
                }
            }

            if (string.IsNullOrEmpty(dir))
            {
                string saved = GameFolder.ReadSavedDir();
                if (!string.IsNullOrEmpty(saved)) dir = saved;
            }

            if (mode == "ui")
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate(object s, System.Threading.ThreadExceptionEventArgs e)
                {
                    Log.Error("界面异常：" + e.Exception);
                    MessageBox.Show(e.Exception.Message, "出错了", MessageBoxButtons.OK, MessageBoxIcon.Error);
                };
                Application.Run(new SwitcherForm(dir));
                return 0;
            }

            if (mode == "check")
            {
                Status st = Switcher.Check(dir);
                Write(Console.Out, json ? st.ToJson() : st.ToText());
                Log.Info("check dir=" + (dir == null ? "(none)" : dir) + " channel=" + st.Channel);
                return st.Exists ? Switcher.OK : Switcher.BAD_DIR;
            }

            if (mode == "switch")
            {
                ChannelProfile p = ChannelProfile.FromKey(target);
                if (p == null)
                {
                    Write(Console.Error, "未知渠道：" + (target == null ? "(空)" : target) + "，可选 official | bilibili");
                    return Switcher.BAD_DIR;
                }
                SwitchOutcome r = Switcher.Run(dir, p, dryRun, sdkSource);
                for (int i = 0; i < r.Detail.Count; i++) Write(Console.Out, "  " + r.Detail[i]);
                Write(Console.Out, (r.Code == Switcher.OK ? "OK" : "FAIL(" + r.Code + ")") + " " + r.Message);
                if (r.Code == Switcher.OK && start && !dryRun) StartGame(dir, dx12);
                return r.Code;
            }

            if (mode == "launch")
            {
                if (!GameFolder.IsValid(dir))
                {
                    Write(Console.Error, "游戏目录无效：" + (dir == null ? "(空)" : dir));
                    return Switcher.BAD_DIR;
                }
                if (GameFolder.IsGameRunning())
                {
                    Write(Console.Out, "游戏已在运行，未重复启动");
                    return Switcher.BUSY;
                }
                StartGame(dir, dx12);
                Write(Console.Out, "OK 已启动游戏" + (dx12 ? "（DX12 · " + GameFolder.Dx12Argument + "）" : "（默认渲染后端）"));
                return Switcher.OK;
            }

            if (mode == "rollback")
            {
                SwitchOutcome r = Switcher.Rollback(dir);
                Write(Console.Out, (r.Code == Switcher.OK ? "OK" : "FAIL(" + r.Code + ")") + " " + r.Message);
                return r.Code;
            }

            Write(Console.Error, "参数无法识别，用 --help 看用法");
            return Switcher.BAD_DIR;
        }

        static bool Eq(string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        static void Write(TextWriter w, string text)
        {
            try
            {
                if (w != null) w.WriteLine(text);
            }
            catch { }
        }

        static void StartGame(string dir, bool dx12)
        {
            try
            {
                if (!GameFolder.IsValid(dir)) return;
                GameFolder.StartGame(dir, dx12);
                Log.Info("已启动游戏：" + dir + " args=" + (dx12 ? GameFolder.Dx12Argument : "(none)"));
            }
            catch (Exception ex) { Log.Error("启动游戏失败：" + ex.Message); }
        }

        static string HelpText()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("ZZZSwitcher — 绝区零 官服/B服 通道切换器");
            sb.AppendLine();
            sb.AppendLine("  ZZZSwitcher.exe                                   打开界面（默认）");
            sb.AppendLine("  ZZZSwitcher.exe --check [--json] [--dir PATH]     只读状态");
            sb.AppendLine("  ZZZSwitcher.exe --switch official|bilibili [--start] [--dry-run] [--dir PATH] [--sdk-source PATH]");
            sb.AppendLine("  ZZZSwitcher.exe --rollback [--dir PATH]           回滚到最近一次备份");
            sb.AppendLine("  ZZZSwitcher.exe --launch [--dx12] [--dir PATH]     只启动游戏，不改配置");
            sb.AppendLine();
            sb.AppendLine("渲染参数:");
            sb.AppendLine("  --dx12 (--d3d12)   以 " + GameFolder.Dx12Argument + " 启动，走 Direct3D 12 后端；不写配置、不改 config.ini");
            sb.AppendLine("                     实际参数由 UnityPlayer 解析，等价于在命令行手打该开关");
            sb.AppendLine();
            sb.AppendLine("退出码: 0 成功 / 2 版本账本不一致 / 3 SDK 缺失 / 4 游戏或文件占用 / 5 目录无效 / 6 写入失败已回滚");
            return sb.ToString();
        }
    }
}
