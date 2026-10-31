using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ZzzSwitch
{
    // UI 偏好：窗口位置、最近目录。与 config.ini 用同一套无损 INI 读写。
    sealed class UiPrefs
    {
        const string Sec = "ui";

        public int Left = -1;
        public int Top = -1;
        public int Width = 0;
        public int Height = 0;
        public string LastDir = "";
        public bool Dx12 = false;
        public readonly List<string> Recent = new List<string>();

        static string SettingsFile()
        {
            string local = Path.Combine(GameFolder.AppDir, "ui.ini");
            try
            {
                using (FileStream fs = new FileStream(local, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite)) { }
                return local;
            }
            catch { }
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ZZZSwitcher");
            try { Directory.CreateDirectory(dir); } catch { }
            return Path.Combine(dir, "ui.ini");
        }

        static int Int(string s, int fallback)
        {
            int n;
            if (!string.IsNullOrEmpty(s) && int.TryParse(s.Trim(), out n)) return n;
            return fallback;
        }

        public static UiPrefs Load()
        {
            UiPrefs p = new UiPrefs();
            try
            {
                string f = SettingsFile();
                if (!File.Exists(f)) return p;
                IniFile ini = IniFile.FromFile(f);
                p.Left = Int(ini.Get(Sec, "left"), -1);
                p.Top = Int(ini.Get(Sec, "top"), -1);
                p.Width = Int(ini.Get(Sec, "width"), 0);
                p.Height = Int(ini.Get(Sec, "height"), 0);
                string last = ini.Get(Sec, "last");
                if (!string.IsNullOrEmpty(last)) p.LastDir = last;
                p.Dx12 = string.Equals((ini.Get(Sec, "dx12") ?? "").Trim(), "1", StringComparison.OrdinalIgnoreCase);
                for (int i = 0; i < 8; i++)
                {
                    string d = ini.Get(Sec, "recent" + i);
                    if (string.IsNullOrEmpty(d)) continue;
                    if (!p.Recent.Contains(d)) p.Recent.Add(d);
                }
            }
            catch { }
            return p;
        }

        public static void Save(UiPrefs p)
        {
            if (p == null) return;
            try
            {
                string f = SettingsFile();
                IniFile ini = File.Exists(f) ? IniFile.FromFile(f) : IniFile.FromBytes(new byte[0]);
                ini.SetOrAdd(Sec, "left", p.Left.ToString(), null);
                ini.SetOrAdd(Sec, "top", p.Top.ToString(), null);
                ini.SetOrAdd(Sec, "width", p.Width.ToString(), null);
                ini.SetOrAdd(Sec, "height", p.Height.ToString(), null);
                ini.SetOrAdd(Sec, "last", p.LastDir, null);
                ini.SetOrAdd(Sec, "dx12", p.Dx12 ? "1" : "0", null);
                for (int i = 0; i < p.Recent.Count && i < 8; i++) ini.SetOrAdd(Sec, "recent" + i, p.Recent[i], null);
                for (int i = p.Recent.Count; i < p.Recent.Count + 8; i++) ini.Remove(Sec, "recent" + i);
                File.WriteAllBytes(f, ini.ToBytes());
            }
            catch { }
        }

        public void AddRecent(string dir)
        {
            if (string.IsNullOrEmpty(dir)) return;
            Recent.RemoveAll(delegate(string s) { return string.Equals(s, dir, StringComparison.OrdinalIgnoreCase); });
            Recent.Insert(0, dir);
            while (Recent.Count > 6) Recent.RemoveAt(Recent.Count - 1);
            LastDir = dir;
        }
    }

    sealed class SwitcherForm : Form
    {
        const int PAD = 16;
        static readonly Color CGreen = Theme.Success;
        static readonly Color CAmber = Theme.Warn;
        static readonly Color CRed = Theme.Danger;
        static readonly Color CDim = Theme.TextDim;
        static readonly Color CBg = Theme.Page;

        string _dir;
        UiPrefs _prefs;

        Card _topCard;
        Card _infoCard;
        PathBox _pathBox;
        readonly List<string> _dirs = new List<string>();
        MenuStrip _menu;
        UiButton _btnBrowse;
        UiButton _btnDetect;
        Label _chDot;
        Label _chBig;
        Label _chSub;
        Label _verVal;
        Label _sdkVal;
        Label _ownerVal;
        Label _stateVal;
        UiButton _btnOfficial;
        UiButton _btnBili;
        UiCheck _onlySwitch;
        UiCheck _dx12;
        ToolStripMenuItem _miOnly;
        ToolStripMenuItem _miDx12;
        RichTextBox _log;
        DarkScrollBar _sbLog;
        Timer _logSync;
        bool _logBusy;
        StatusBar _status;
        ToolTip _tips;

        public SwitcherForm(string dir)
        {
            _dir = dir;
            BuildUi();
        }

        // ---------- 布局 ----------
        void BuildUi()
        {
            SuspendLayout();
            Text = "绝区零 · 渠道切换器";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimumSize = new Size(680, 640);
            ClientSize = new Size(700, 660);
            Font = Theme.Ui(9f, FontStyle.Regular);
            BackColor = CBg;
            AllowDrop = true;
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

            // WinForms：停靠按 z 序逆序处理（最后添加的先占位），所以 Fill 必须最先添加。
            Control cLog = BuildLogPanel();
            Control cAct = BuildActionPanel();
            Control cInfo = BuildInfoPanel();
            Control cTop = BuildTopPanel();
            Control cMenu = BuildMenu();
            Control cStatus = BuildStatusStrip();

            Controls.Add(cLog);
            Controls.Add(cAct);
            Controls.Add(cInfo);
            Controls.Add(cTop);
            Controls.Add(cMenu);
            Controls.Add(cStatus);

            Load += delegate { OnLoadUi(); };
            FormClosing += delegate { OnSaveUi(); };
            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;

            _tips = new ToolTip();
            _tips.SetToolTip(_btnBrowse, "选择包含 ZenlessZoneZero.exe 的游戏目录（Ctrl+O）");
            _tips.SetToolTip(_btnDetect, "从注册表读取已安装的游戏目录（Ctrl+D）");
            _tips.SetToolTip(_btnOfficial, "写入 channel=1 / sub_channel=1 / cps=zzz_gw_pc（Ctrl+1）");
            _tips.SetToolTip(_btnBili, "写入 channel=14 / sub_channel=0 / cps=zzz_bilibili_pc，并补齐 B 站 SDK（Ctrl+2）");
            _tips.SetToolTip(_log, "右键可复制或清空；双击打开 switch.log");
            _tips.SetToolTip(_pathBox, "点这里换目录：最近用过的目录、浏览…、自动检测");
            _tips.SetToolTip(_dx12, "以 -use-d3d12 启动游戏（Unity 渲染后端开关，实测有效）；关掉则用客户端默认后端");
            _tips.SetToolTip(_onlySwitch, "只写 config.ini 与备份，不启动游戏（Ctrl+3 是试运行，完全不写盘）");

            ResumeLayout(false);
            PerformLayout();
        }

        Control BuildMenu()
        {
            _menu = new MenuStrip();
            _menu.Font = Theme.Ui(9f, FontStyle.Regular);
            _menu.BackColor = Theme.CardFill;
            _menu.ForeColor = Theme.TextBody;
            _menu.GripStyle = ToolStripGripStyle.Hidden;
            _menu.Padding = new Padding(6, 2, 0, 2);
            _menu.Renderer = new FlatMenuRenderer();

            ToolStripMenuItem file = new ToolStripMenuItem("文件(&F)");
            file.DropDownItems.Add(Mi("选择游戏目录…(&O)", Keys.Control | Keys.O, delegate { PickFolder(); }));
            file.DropDownItems.Add(Mi("自动检测已安装目录(&D)", Keys.Control | Keys.D, delegate { AutoDetect(true); }));
            file.DropDownItems.Add(new ToolStripSeparator());
            file.DropDownItems.Add(Mi("打开游戏目录(&G)", Keys.None, delegate { OpenPath(_dir); }));
            file.DropDownItems.Add(Mi("打开备份目录(&B)", Keys.None, delegate { OpenBackup(); }));
            file.DropDownItems.Add(Mi("打开日志文件(&L)", Keys.None, delegate { OpenLogFile(); }));
            file.DropDownItems.Add(new ToolStripSeparator());
            file.DropDownItems.Add(Mi("退出(&X)", Keys.Alt | Keys.F4, delegate { Close(); }));

            ToolStripMenuItem act = new ToolStripMenuItem("操作(&A)");
            act.DropDownItems.Add(Mi("切到官服并启动(&1)", Keys.Control | Keys.D1, delegate { DoSwitch(ChannelProfile.Official); }));
            act.DropDownItems.Add(Mi("切到 B 服并启动(&2)", Keys.Control | Keys.D2, delegate { DoSwitch(ChannelProfile.Bilibili); }));
            act.DropDownItems.Add(Mi("试运行，不写入文件(&3)", Keys.Control | Keys.D3, delegate { DryRun(); }));
            act.DropDownItems.Add(new ToolStripSeparator());
            _miOnly = Mi("只切换，不启动游戏", Keys.None, delegate { ToggleOnly(); });
            act.DropDownItems.Add(_miOnly);
            _miDx12 = Mi("以 DX12 启动游戏（-use-d3d12）", Keys.Control | Keys.D0, delegate { ToggleDx12(); });
            act.DropDownItems.Add(_miDx12);
            act.DropDownItems.Add(Mi("启动游戏（不改配置）(&G)", Keys.Control | Keys.Enter, delegate { StartGame(); }));
            act.DropDownItems.Add(Mi("重新检测(&R)", Keys.F5, delegate { RefreshStatus(true); }));
            act.DropDownItems.Add(Mi("结束游戏进程(&K)", Keys.None, delegate { KillGame(true); }));

            ToolStripMenuItem help = new ToolStripMenuItem("帮助(&H)");
            help.DropDownItems.Add(Mi("复制诊断信息(&C)", Keys.Control | Keys.Shift | Keys.C, delegate { CopyDiagnostics(); }));
            help.DropDownItems.Add(Mi("打开 README(&M)", Keys.None, delegate { OpenReadme(); }));
            help.DropDownItems.Add(new ToolStripSeparator());
            help.DropDownItems.Add(Mi("关于(&A)", Keys.None, delegate { About(); }));

            _menu.Items.Add(file);
            _menu.Items.Add(act);
            _menu.Items.Add(help);
            MainMenuStrip = _menu;
            return _menu;
        }

        ToolStripMenuItem Mi(string text, Keys key, EventHandler handler)
        {
            ToolStripMenuItem mi = new ToolStripMenuItem(text);
            if (key != Keys.None) mi.ShortcutKeys = key;
            mi.Click += handler;
            return mi;
        }

        // 每个区块都是「外层留白容器 + 圆角卡片」，卡片宽度在加入父容器前先算好，
        // 否则 L|R 锚点会按 Panel 默认宽度 200 计算增量，控件会被拉飞。
        int CardWidth() { return ClientSize.Width - PAD * 2; }

        Control BuildTopPanel()
        {
            int cardW = CardWidth();

            Panel host = new Panel();
            host.Dock = DockStyle.Top;
            host.Height = 152;
            host.Width = ClientSize.Width;
            host.BackColor = CBg;
            host.Padding = new Padding(PAD, PAD, PAD, 0);

            _topCard = new Card();
            _topCard.Dock = DockStyle.Fill;
            _topCard.Width = cardW;
            _topCard.AccentColor = Theme.Official;
            host.Controls.Add(_topCard);

            Label cap = new Label();
            cap.Text = "游戏目录";
            cap.SetBounds(20, 14, 64, 20);
            cap.ForeColor = Theme.TextDim;
            _topCard.Controls.Add(cap);

            int detectW = 92;
            int browseW = 70;
            int detectLeft = cardW - 18 - detectW;
            int browseLeft = detectLeft - 8 - browseW;
            int boxLeft = 90;
            int boxW = Math.Max(160, browseLeft - 10 - boxLeft);

            _pathBox = new PathBox();
            _pathBox.SetBounds(boxLeft, 10, boxW, 28);
            _pathBox.Accent = Theme.Official;
            _pathBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _pathBox.AllowDrop = true;
            _pathBox.Click += delegate { ShowDirMenu(); };
            _pathBox.DragEnter += OnDragEnter;
            _pathBox.DragDrop += OnDragDrop;
            _topCard.Controls.Add(_pathBox);

            _btnBrowse = new UiButton();
            _btnBrowse.Text = "浏览…";
            _btnBrowse.Primary = false;
            _btnBrowse.Accent = Theme.TextDim;
            _btnBrowse.Radius = 7;
            _btnBrowse.Font = Theme.Ui(9f, FontStyle.Regular);
            _btnBrowse.SetBounds(browseLeft, 9, browseW, 28);
            _btnBrowse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnBrowse.Click += delegate { PickFolder(); };
            _topCard.Controls.Add(_btnBrowse);

            _btnDetect = new UiButton();
            _btnDetect.Text = "自动检测";
            _btnDetect.Primary = false;
            _btnDetect.Accent = Theme.TextDim;
            _btnDetect.Radius = 7;
            _btnDetect.Font = Theme.Ui(9f, FontStyle.Regular);
            _btnDetect.SetBounds(detectLeft, 9, detectW, 28);
            _btnDetect.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnDetect.Click += delegate { AutoDetect(true); };
            _topCard.Controls.Add(_btnDetect);

            _chDot = new Label();
            _chDot.Text = "●";
            _chDot.SetBounds(18, 46, 16, 30);
            _chDot.Font = Theme.Ui(11f, FontStyle.Regular);
            _chDot.ForeColor = Theme.TextFaint;
            _topCard.Controls.Add(_chDot);

            _chBig = new Label();
            _chBig.SetBounds(36, 46, cardW - 36 - 20, 32);
            _chBig.Font = Theme.Ui(15.5f, FontStyle.Bold);
            _chBig.ForeColor = Theme.TextStrong;
            _chBig.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _chBig.AutoEllipsis = true;
            _chBig.Text = "尚未检测";
            _topCard.Controls.Add(_chBig);

            _chSub = new Label();
            _chSub.SetBounds(38, 84, cardW - 38 - 20, 20);
            _chSub.Font = new Font("Consolas", 9f);
            _chSub.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _chSub.ForeColor = Theme.TextDim;
            _chSub.AutoEllipsis = true;
            _chSub.Text = "cps=-";
            _topCard.Controls.Add(_chSub);

            return host;
        }

        Control BuildInfoPanel()
        {
            int cardW = CardWidth();

            Panel host = new Panel();
            host.Dock = DockStyle.Top;
            host.Height = 148;
            host.Width = ClientSize.Width;
            host.BackColor = CBg;
            host.Padding = new Padding(PAD, PAD, PAD, 0);

            _infoCard = new Card();
            _infoCard.Dock = DockStyle.Fill;
            _infoCard.Width = cardW;
            host.Controls.Add(_infoCard);

            _verVal = AddRow(_infoCard, "版本账本", 12, cardW);
            _sdkVal = AddRow(_infoCard, "B 站 SDK", 36, cardW);
            _ownerVal = AddRow(_infoCard, "启动器归属", 60, cardW);
            _stateVal = AddRow(_infoCard, "游戏 / 占用", 84, cardW);

            return host;
        }

        Label AddRow(Card host, string key, int y, int cardW)
        {
            Label k = new Label();
            k.Text = key;
            k.SetBounds(20, y + 2, 90, 20);
            k.ForeColor = Theme.TextDim;
            k.Font = Theme.Ui(8.75f, FontStyle.Regular);
            host.Controls.Add(k);

            Label v = new Label();
            v.SetBounds(112, y + 1, cardW - 112 - 22, 22);
            v.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            v.AutoEllipsis = true;
            v.Font = Theme.Ui(9.25f, FontStyle.Regular);
            v.ForeColor = Theme.TextBody;
            v.Text = "-";
            host.Controls.Add(v);

            if (y > 12)
            {
                Panel line = new Panel();
                line.BackColor = Theme.Divider;
                line.SetBounds(20, y - 4, cardW - 40, 1);
                line.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                host.Controls.Add(line);
            }
            return v;
        }

        Control BuildActionPanel()
        {
            int cardW = CardWidth();

            Panel host = new Panel();
            host.Dock = DockStyle.Top;
            host.Height = 124;
            host.Width = ClientSize.Width;
            host.BackColor = CBg;
            host.Padding = new Padding(PAD, PAD, PAD, 0);

            Card card = new Card();
            card.Dock = DockStyle.Fill;
            card.Width = cardW;
            host.Controls.Add(card);

            int btnW = (cardW - 18 * 2 - 12) / 2;

            _btnOfficial = new UiButton();
            _btnOfficial.Text = "切到官服并启动";
            _btnOfficial.Accent = Theme.Official;
            _btnOfficial.Primary = true;
            _btnOfficial.SetBounds(18, 16, btnW, 46);
            _btnOfficial.Font = Theme.Ui(10.5f, FontStyle.Bold);
            _btnOfficial.Click += delegate { DoSwitch(ChannelProfile.Official); };
            card.Controls.Add(_btnOfficial);

            _btnBili = new UiButton();
            _btnBili.Text = "切到 B 服并启动";
            _btnBili.Accent = Theme.Bilibili;
            _btnBili.Primary = true;
            _btnBili.SetBounds(18 + btnW + 12, 16, btnW, 46);
            _btnBili.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _btnBili.Font = Theme.Ui(10.5f, FontStyle.Bold);
            _btnBili.Click += delegate { DoSwitch(ChannelProfile.Bilibili); };
            card.Controls.Add(_btnBili);

            _onlySwitch = new UiCheck();
            _onlySwitch.Text = "只切换，不启动游戏";
            _onlySwitch.Accent = Theme.Official;
            _onlySwitch.SetBounds(18, 74, 160, 22);
            _onlySwitch.Font = Theme.Ui(9f, FontStyle.Regular);
            _onlySwitch.CheckedChanged += delegate
            {
                if (_miOnly != null) _miOnly.Checked = _onlySwitch.Checked;
            };
            card.Controls.Add(_onlySwitch);

            _dx12 = new UiCheck();
            _dx12.Text = "DX12 启动";
            _dx12.Accent = Theme.Official;
            _dx12.SetBounds(190, 74, 110, 22);
            _dx12.Font = Theme.Ui(9f, FontStyle.Regular);
            _dx12.CheckedChanged += delegate
            {
                if (_miDx12 != null) _miDx12.Checked = _dx12.Checked;
            };
            if (_tips != null) _tips.SetToolTip(_dx12, "以 -use-d3d12 启动游戏（Unity 渲染后端开关，实测有效）；关掉则用客户端默认后端");
            card.Controls.Add(_dx12);

            Label hint = new Label();
            hint.Text = "F5 刷新 · Ctrl+1/2 切换 · Ctrl+3 试运行 · 可拖入目录";
            hint.SetBounds(308, 77, cardW - 308 - 18, 18);
            hint.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            hint.ForeColor = Theme.TextFaint;
            hint.Font = Theme.Ui(8.5f, FontStyle.Regular);
            hint.AutoEllipsis = true;
            card.Controls.Add(hint);

            return host;
        }

        Control BuildLogPanel()
        {
            int cardW = CardWidth();

            Panel host = new Panel();
            host.Dock = DockStyle.Fill;
            host.Width = ClientSize.Width;
            host.BackColor = CBg;
            host.Padding = new Padding(PAD, PAD, PAD, PAD);

            Card card = new Card();
            card.Dock = DockStyle.Fill;
            card.Width = cardW;
            host.Controls.Add(card);

            Panel capRow = new Panel();
            capRow.Dock = DockStyle.Top;
            capRow.Height = 26;
            capRow.Width = cardW;
            capRow.BackColor = Theme.CardFill;

            Label cap = new Label();
            cap.Text = "日志";
            cap.SetBounds(18, 2, 60, 20);
            cap.Font = Theme.Ui(9f, FontStyle.Bold);
            cap.ForeColor = Theme.TextStrong;
            capRow.Controls.Add(cap);

            LinkLabel open = new LinkLabel();
            open.Text = "打开 switch.log";
            open.SetBounds(cardW - 18 - 120, 3, 120, 18);
            open.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            open.TextAlign = ContentAlignment.MiddleRight;
            open.Font = Theme.Ui(8.5f, FontStyle.Regular);
            open.LinkColor = Theme.TextDim;
            open.ActiveLinkColor = Theme.Official;
            open.LinkBehavior = LinkBehavior.HoverUnderline;
            open.LinkClicked += delegate { OpenLogFile(); };
            capRow.Controls.Add(open);

            DarkPanel dark = new DarkPanel();
            dark.Dock = DockStyle.Fill;
            dark.Width = cardW - 32;
            dark.Padding = new Padding(12, 10, 10, 10);
            card.Controls.Add(dark);
            card.Controls.Add(capRow);   // 后添加者先停靠，标题栏必须先占掉顶部 26px

            _log = new RichTextBox();
            _log.Dock = DockStyle.Fill;
            _log.ReadOnly = true;
            _log.ScrollBars = RichTextBoxScrollBars.None;   // 原生滚动条太丑，改用右侧自绘滑条
            _log.WordWrap = true;                          // 长路径折行，省掉横向滚动条
            _log.DetectUrls = false;
            _log.Font = new Font("Consolas", 8.75f);
            _log.BackColor = Theme.LogBack;
            _log.ForeColor = Theme.LogText;
            _log.BorderStyle = BorderStyle.None;
            _log.DoubleClick += delegate { OpenLogFile(); };
            _log.ContextMenuStrip = BuildLogMenu();
            _log.MouseWheel += OnLogWheel;
            _log.KeyUp += delegate { SyncLogScroll(); };
            _log.MouseUp += delegate { SyncLogScroll(); };
            _log.TextChanged += delegate { SyncLogScroll(); };
            dark.Controls.Add(_log);
            // ScrollBars=None 的 RichTextBox 不抢焦点，滚轮会落到窗体上；鼠标进入日志区时主动给焦点。
            dark.MouseEnter += delegate { if (_log != null && !_log.Focused) _log.Focus(); };

            _sbLog = new DarkScrollBar();
            _sbLog.Dock = DockStyle.Right;
            _sbLog.Width = 12;
            _sbLog.BackColor = Theme.LogBack;
            _sbLog.ValueChanged += delegate { ScrollLogTo(_sbLog.Value); };
            dark.Controls.Add(_sbLog);

            return host;
        }

        ContextMenuStrip BuildLogMenu()
        {
            ContextMenuStrip m = new ContextMenuStrip();
            m.Items.Add("复制所选(&C)", null, delegate
            {
                if (!string.IsNullOrEmpty(_log.SelectedText)) Clipboard.SetText(_log.SelectedText);
            });
            m.Items.Add("复制全部(&A)", null, delegate
            {
                if (!string.IsNullOrEmpty(_log.Text)) Clipboard.SetText(_log.Text);
            });
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("清空显示(&L)", null, delegate { _log.Clear(); });
            m.Items.Add("打开 switch.log(&O)", null, delegate { OpenLogFile(); });
            return m;
        }

        StatusBar BuildStatusStrip()
        {
            _status = new StatusBar();
            _status.Dock = DockStyle.Bottom;
            _status.Height = 30;
            _status.Width = ClientSize.Width;
            return _status;
        }

        // ---------- 生命周期 ----------
        void OnLoadUi()
        {
            _prefs = UiPrefs.Load();
            if (_prefs.Width >= MinimumSize.Width && _prefs.Height >= MinimumSize.Height)
            {
                Rectangle r = new Rectangle(_prefs.Left, _prefs.Top, _prefs.Width, _prefs.Height);
                bool visible = false;
                foreach (Screen sc in Screen.AllScreens)
                    if (sc.WorkingArea.IntersectsWith(r)) visible = true;
                if (visible && r.Left > -1000)
                {
                    StartPosition = FormStartPosition.Manual;
                    Bounds = r;
                }
            }

            if (string.IsNullOrEmpty(_dir)) _dir = _prefs.LastDir;
            if (string.IsNullOrEmpty(_dir)) _dir = GameFolder.ReadSavedDir();

            // 记住上次的 DX12 选择
            if (_dx12 != null) _dx12.Checked = _prefs.Dx12;
            if (_miDx12 != null) _miDx12.Checked = _prefs.Dx12;

            _dirs.Clear();
            foreach (string d in _prefs.Recent)
                if (!string.IsNullOrEmpty(d)) AddToBox(d);
            if (!string.IsNullOrEmpty(_dir)) AddToBox(GameFolder.Normalize(_dir));

            RenderLogTail(40);
            AppendLog("日志文件：" + Log.FilePath);

            if (!GameFolder.IsValid(_dir)) AutoDetect(false);
            if (GameFolder.IsValid(_dir)) SetDir(_dir, false);
            else
            {
                AppendLog("没有找到可用的游戏目录，请点“浏览…”选择包含 ZenlessZoneZero.exe 的目录");
                RefreshStatus(false);
            }

            // 定时对齐自绘滚动条：光标移动、滚轮、拖动选中都会改变首行可见行号。
            if (_logSync == null)
            {
                _logSync = new Timer();
                _logSync.Interval = 200;
                _logSync.Tick += delegate { SyncLogScroll(); };
            }
            _logSync.Start();
            SyncLogScroll();
        }

        void OnSaveUi()
        {
            if (_logSync != null) _logSync.Stop();
            if (_prefs == null) return;
            _prefs.Width = Width;
            _prefs.Height = Height;
            _prefs.Left = Left;
            _prefs.Top = Top;
            if (!string.IsNullOrEmpty(_dir)) _prefs.AddRecent(GameFolder.Normalize(_dir));
            if (_dx12 != null) _prefs.Dx12 = _dx12.Checked;
            UiPrefs.Save(_prefs);
        }

        // ---------- 目录 ----------
        void AddToBox(string dir)
        {
            if (string.IsNullOrEmpty(dir)) return;
            bool found = false;
            for (int i = 0; i < _dirs.Count; i++)
            {
                if (string.Equals(_dirs[i], dir, StringComparison.OrdinalIgnoreCase)) { found = true; break; }
            }
            if (!found) _dirs.Insert(0, dir);
            while (_dirs.Count > 8) _dirs.RemoveAt(_dirs.Count - 1);
        }

        // 点路径框弹出「最近目录 + 浏览 + 自动检测」菜单。
        void ShowDirMenu()
        {
            if (_pathBox == null || !_pathBox.Enabled) return;
            ContextMenuStrip m = new ContextMenuStrip();
            m.Font = Theme.Ui(9f, FontStyle.Regular);
            m.Renderer = new FlatMenuRenderer();
            m.ShowImageMargin = false;

            bool any = false;
            foreach (string d in _dirs)
            {
                if (string.IsNullOrEmpty(d)) continue;
                any = true;
                string cur = d;
                ToolStripMenuItem it = new ToolStripMenuItem(d);
                it.Checked = string.Equals(d, _dir, StringComparison.OrdinalIgnoreCase);
                it.ToolTipText = GameFolder.IsValid(d) ? "可用" : "目录失效";
                it.Click += delegate { SetDir(cur, true); };
                m.Items.Add(it);
            }
            if (any) m.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem br = new ToolStripMenuItem("浏览…");
            br.ShortcutKeyDisplayString = "Ctrl+O";
            br.Click += delegate { PickFolder(); };
            m.Items.Add(br);

            ToolStripMenuItem au = new ToolStripMenuItem("自动检测已安装目录");
            au.ShortcutKeyDisplayString = "Ctrl+D";
            au.Click += delegate { AutoDetect(true); };
            m.Items.Add(au);

            m.Show(_pathBox, new Point(0, _pathBox.Height + 2));
        }

        void SetDir(string dir, bool save)
        {
            if (string.IsNullOrEmpty(dir)) return;
            _dir = GameFolder.Normalize(dir);
            AddToBox(_dir);
            if (_pathBox != null) { _pathBox.Text = _dir; _pathBox.Invalidate(); }
            if (save)
            {
                GameFolder.SaveDir(_dir);
                if (_prefs != null) _prefs.AddRecent(_dir);
            }
            RefreshStatus(true);
        }

        void PickFolder()
        {
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.Description = "选择绝区零游戏目录（必须包含 ZenlessZoneZero.exe）";
                dlg.ShowNewFolderButton = false;
                if (!string.IsNullOrEmpty(_dir) && Directory.Exists(_dir)) dlg.SelectedPath = _dir;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                string picked = dlg.SelectedPath;
                if (!GameFolder.IsValid(picked))
                {
                    MessageBox.Show(this, "该目录里没有 ZenlessZoneZero.exe，换一个再试。", "目录无效",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                SetDir(picked, true);
                AppendLog("游戏目录设为 " + _dir);
            }
        }

        // 返回找到的目录数量
        int AutoDetect(bool interactive)
        {
            List<string> found = new List<string>();
            ChannelProfile[] all = ChannelProfile.All();
            for (int i = 0; i < all.Length; i++)
            {
                string d = null;
                try { d = LauncherMap.ReadInstallPath(all[i].LauncherSchema); }
                catch { }
                if (!GameFolder.IsValid(d)) continue;
                d = GameFolder.Normalize(d);
                if (!found.Contains(d)) found.Add(d);
            }

            for (int i = 0; i < found.Count; i++) AddToBox(found[i]);

            if (found.Count == 0)
            {
                if (interactive)
                    MessageBox.Show(this, "注册表里没有登记可用的绝区零目录，请手动选择。",
                        "未检测到", MessageBoxButtons.OK, MessageBoxIcon.Information);
                else AppendLog("自动检测：注册表里没有登记可用目录");
                return 0;
            }

            string pick = null;
            if (GameFolder.IsValid(_dir))
            {
                for (int i = 0; i < found.Count; i++)
                    if (string.Equals(found[i], GameFolder.Normalize(_dir), StringComparison.OrdinalIgnoreCase)) pick = found[i];
            }
            if (pick == null) pick = found[0];

            SetDir(pick, true);
            if (interactive)
            {
                AppendLog("自动检测到 " + found.Count + " 个目录");
                for (int i = 0; i < found.Count; i++) AppendLog("  " + found[i]);
            }
            return found.Count;
        }

        // ---------- 状态 ----------
        void RefreshStatus(bool logIt)
        {
            if (_pathBox == null) return;
            Status st = Switcher.Check(_dir);

            bool valid = st.Exists;
            if (!valid)
            {
                _chBig.Text = "目录无效";
                _chBig.ForeColor = CRed;
                _chSub.Text = string.IsNullOrEmpty(_dir) ? "未选择游戏目录" : "该目录缺少 ZenlessZoneZero.exe 或 config.ini";
                _verVal.Text = _sdkVal.Text = _ownerVal.Text = _stateVal.Text = "-";
                _verVal.ForeColor = _sdkVal.ForeColor = _ownerVal.ForeColor = _stateVal.ForeColor = CDim;
                _btnOfficial.Enabled = false;
                _btnBili.Enabled = false;
                ApplyChannelTheme(Theme.Warn, false, false);
                SetStatus("请选择包含 ZenlessZoneZero.exe 的游戏目录", CRed);
                return;
            }

            if (st.Channel < 0)
            {
                _chBig.Text = "渠道未知";
                _chBig.ForeColor = CRed;
                _chSub.Text = "config.ini 里没有可解析的 channel 键";
            }
            else
            {
                ChannelProfile cur = ChannelProfile.FromChannel(st.Channel);
                _chBig.Text = "当前：" + (cur == null ? "未知渠道 (" + st.Channel + ")" : cur.Display);
                _chBig.ForeColor = cur == null
                    ? CAmber
                    : (cur == ChannelProfile.Official ? Theme.Official : Theme.Bilibili);
                _chSub.Text = "channel=" + st.Channel
                            + "   sub_channel=" + (cur == null ? "?" : cur.SubChannel.ToString())
                            + "   cps=" + (st.Cps.Length == 0 ? "?" : st.Cps);
            }

            _verVal.Text = (st.GameVersion.Length == 0 ? "?" : st.GameVersion)
                         + "   version_info " + (st.VersionInfo.Length == 0 ? "(缺失)" : st.VersionInfo)
                         + (st.VersionConsistent ? "   ✅ 一致" : "   ⚠️ 不一致（切换会被拒绝）");
            _verVal.ForeColor = st.VersionConsistent ? Theme.TextBody : CAmber;

            if (st.SdkPresent)
            {
                _sdkVal.Text = "✅ " + st.SdkFiles + " 文件 / " + (st.SdkBytes / 1048576) + " MB   PCGameSDK.dll + BLPlatform64 齐备";
                _sdkVal.ForeColor = Theme.TextBody;
            }
            else
            {
                _sdkVal.Text = "⚠️ 缺失 —— 官服目录的正常状态；要切 B 服需先补齐（或用 --sdk-source）";
                _sdkVal.ForeColor = CAmber;
            }

            if (st.Owner == "none")
            {
                _ownerVal.Text = "未在注册表登记 —— 启动器可能不会自动更新这个目录";
                _ownerVal.ForeColor = CAmber;
            }
            else
            {
                ChannelProfile op = ChannelProfile.FromKey(st.Owner);
                _ownerVal.Text = (op == null ? st.Owner : op.Display) + " 负责更新此目录（注册表 HYP 项）";
                _ownerVal.ForeColor = Theme.TextBody;
            }

            bool canLock = GameFolder.CanLockConfig(_dir);
            if (st.GameRunning)
            {
                _stateVal.Text = "⚠️ 游戏正在运行 —— 切换前需要先退出";
                _stateVal.ForeColor = CAmber;
            }
            else if (!canLock)
            {
                _stateVal.Text = "⚠️ config.ini 被其他程序占用";
                _stateVal.ForeColor = CAmber;
            }
            else
            {
                _stateVal.Text = "✅ 未运行，config.ini 可写";
                _stateVal.ForeColor = Theme.TextBody;
            }

            // 按钮可用性
            bool isOfficial = ChannelProfile.FromChannel(st.Channel) == ChannelProfile.Official;
            bool isBili = ChannelProfile.FromChannel(st.Channel) == ChannelProfile.Bilibili;
            Color chan = isOfficial ? Theme.Official : (isBili ? Theme.Bilibili : Theme.Warn);
            ApplyChannelTheme(chan, isOfficial, isBili);

            _btnOfficial.Enabled = !st.GameRunning;
            _btnBili.Enabled = !st.GameRunning && (st.SdkPresent || isBili);
            _btnOfficial.Text = isOfficial ? "✓ 当前是官服" : "切到官服并启动";
            _btnBili.Text = isBili ? "✓ 当前是 B 服" : "切到 B 服并启动";

            string msg;
            Color col;
            if (st.GameRunning) { msg = "游戏正在运行：先退出游戏（或按 Ctrl+K 结束进程）再切换"; col = CAmber; }
            else if (!st.VersionConsistent) { msg = "版本账本不一致：请先用启动器更新到同一版本再切换"; col = CAmber; }
            else if (!st.SdkPresent && !isBili) { msg = "当前目录没有 B 站 SDK：只能切到官服；要切 B 服请补 SDK"; col = CAmber; }
            else { msg = "就绪 —— 渠道三元组与版本账本均已读取"; col = CGreen; }
            SetStatus(msg, col);

            if (logIt && st.GameRunning) AppendLog("检测到游戏进程正在运行");
        }

        void SetStatus(string msg, Color col)
        {
            if (_status != null) _status.Set(msg, col);
        }

        // 卡片左侧竖条、大标题圆点、复选框与按钮配色跟随当前渠道。
        void ApplyChannelTheme(Color chan, bool isOfficial, bool isBili)
        {
            if (_chDot != null) { _chDot.ForeColor = chan; _chDot.Invalidate(); }
            if (_topCard != null) { _topCard.AccentColor = chan; _topCard.Invalidate(); }
            if (_btnOfficial != null)
            {
                _btnOfficial.Accent = Theme.Official;
                _btnOfficial.Primary = !isOfficial;
                _btnOfficial.Current = isOfficial;
                _btnOfficial.Invalidate();
            }
            if (_btnBili != null)
            {
                _btnBili.Accent = Theme.Bilibili;
                _btnBili.Primary = !isBili;
                _btnBili.Current = isBili;
                _btnBili.Invalidate();
            }
            if (_onlySwitch != null) { _onlySwitch.Accent = chan; _onlySwitch.Invalidate(); }
            if (_dx12 != null) { _dx12.Accent = chan; _dx12.Invalidate(); }
        }

        // ---------- 动作 ----------
        void ToggleOnly()
        {
            _onlySwitch.Checked = !_onlySwitch.Checked;
        }

        void DoSwitch(ChannelProfile p)
        {
            if (p == null) return;
            if (!GameFolder.IsValid(_dir))
            {
                MessageBox.Show(this, "先选择一个有效的游戏目录。", "目录无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                PickFolder();
                return;
            }
            Status st = Switcher.Check(_dir);
            if (ChannelProfile.FromChannel(st.Channel) == p && _onlySwitch.Checked)
            {
                AppendLog("当前已是" + p.Display + "，无需切换");
                return;
            }
            if (st.GameRunning)
            {
                DialogResult dr = MessageBox.Show(this,
                    "检测到游戏正在运行，切换必须先退出游戏。\r\n\r\n现在结束游戏进程并继续？",
                    "游戏正在运行", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                if (dr != DialogResult.Yes) return;
                KillGame(false);
                if (GameFolder.IsGameRunning())
                {
                    AppendLog("游戏进程仍未退出，已中止切换");
                    SetStatus("游戏进程仍在运行，已中止", CRed);
                    return;
                }
            }

            SetBusy(true, p.Display);
            SwitchOutcome r = null;
            try { r = Switcher.Run(_dir, p, false, null); }
            catch (Exception ex) { Log.Error("切换异常：" + ex.ToString()); AppendLog("切换异常：" + ex.Message); }
            finally { SetBusy(false, null); }

            if (r == null) { RefreshStatus(false); return; }

            for (int i = 0; i < r.Detail.Count; i++) AppendLog("  " + r.Detail[i]);
            AppendLog(r.Message);

            RefreshStatus(false);
            if (r.Code != Switcher.OK)
            {
                MessageBox.Show(this, r.Message, "切换中止（退出码 " + r.Code + "）",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            SetStatus("已切到" + p.Display + (string.IsNullOrEmpty(r.BackupPath) ? "" : "，备份 " + Path.GetFileName(r.BackupPath)), CGreen);
            if (!_onlySwitch.Checked) StartGame();
        }

        void DryRun()
        {
            if (!GameFolder.IsValid(_dir)) { PickFolder(); return; }
            Status st = Switcher.Check(_dir);
            ChannelProfile p = ChannelProfile.FromChannel(st.Channel) == ChannelProfile.Bilibili
                ? ChannelProfile.Official : ChannelProfile.Bilibili;
            SetBusy(true, "试运行");
            SwitchOutcome r = null;
            try { r = Switcher.Run(_dir, p, true, null); }
            catch (Exception ex) { AppendLog("试运行异常：" + ex.Message); }
            finally { SetBusy(false, null); }
            if (r == null) return;
            AppendLog("试运行（不写入）：目标 " + p.Display + " → " + (r.Code == Switcher.OK ? "可切换" : "会被中止，退出码 " + r.Code));
            for (int i = 0; i < r.Detail.Count; i++) AppendLog("  " + r.Detail[i]);
            AppendLog("  " + r.Message);
        }

        void StartGame()
        {
            if (!GameFolder.IsValid(_dir)) { PickFolder(); return; }
            if (GameFolder.IsGameRunning())
            {
                DialogResult dr = MessageBox.Show(this,
                    "游戏已经在运行了。\r\n\r\n要结束它再按当前设置重新启动吗？（重启才会带上新的渲染参数）",
                    "游戏正在运行", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                if (dr != DialogResult.Yes) return;
                KillGame(false);
                if (GameFolder.IsGameRunning())
                {
                    AppendLog("游戏进程仍在运行，已中止启动");
                    SetStatus("游戏进程仍在运行，已中止启动", CRed);
                    return;
                }
            }
            bool dx12 = _dx12 != null && _dx12.Checked;
            try
            {
                GameFolder.StartGame(_dir, dx12);
                string mode = dx12 ? "（DX12 · -use-d3d12）" : "（客户端默认渲染后端）";
                AppendLog("已启动游戏" + mode);
                Log.Info("启动游戏 dir=" + _dir + " args=" + (dx12 ? GameFolder.Dx12Argument : "(none)"));
                SetStatus("已启动游戏" + mode, CGreen);
            }
            catch (Exception ex)
            {
                AppendLog("启动失败：" + ex.Message);
                Log.Error("启动游戏失败：" + ex);
                MessageBox.Show(this, "启动游戏失败：" + ex.Message, "启动失败",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void ToggleDx12()
        {
            if (_dx12 == null) return;
            _dx12.Checked = !_dx12.Checked;
            AppendLog(_dx12.Checked
                ? "已启用 DX12 启动：" + GameFolder.Dx12Argument
                : "已关闭 DX12 启动，使用客户端默认渲染后端");
        }

        void KillGame(bool interactive)
        {
            Process[] ps;
            try { ps = Process.GetProcessesByName(GameFolder.ProcessName); }
            catch { ps = new Process[0]; }
            if (ps.Length == 0)
            {
                if (interactive) AppendLog("没有正在运行的游戏进程");
                return;
            }
            for (int i = 0; i < ps.Length; i++)
            {
                try { ps[i].CloseMainWindow(); } catch { }
            }
            System.Threading.Thread.Sleep(1200);
            for (int i = 0; i < ps.Length; i++)
            {
                try { if (!ps[i].HasExited) ps[i].Kill(); } catch { }
            }
            System.Threading.Thread.Sleep(600);
            for (int i = 0; i < ps.Length; i++) { try { ps[i].Dispose(); } catch { } }
            AppendLog("已结束游戏进程");
            RefreshStatus(false);
        }

        void SetBusy(bool busy, string what)
        {
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
            _btnOfficial.Enabled = !busy;
            _btnBili.Enabled = !busy;
            _btnBrowse.Enabled = !busy;
            _btnDetect.Enabled = !busy;
            if (_pathBox != null) _pathBox.Enabled = !busy;
            _menu.Enabled = !busy;
            if (busy) SetStatus("正在处理：" + (what == null ? "" : what), CAmber);
            Application.DoEvents();
        }

        // ---------- 杂项 ----------
        void OpenPath(string dir)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                MessageBox.Show(this, "目录不存在。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try { Process.Start(new ProcessStartInfo("explorer.exe", "\"" + dir + "\"")); }
            catch (Exception ex) { AppendLog("打开目录失败：" + ex.Message); }
        }

        void OpenBackup()
        {
            if (!GameFolder.IsValid(_dir)) { PickFolder(); return; }
            string dir = GameFolder.BackupDir(_dir);
            try
            {
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                Process.Start(new ProcessStartInfo("explorer.exe", "\"" + dir + "\""));
            }
            catch (Exception ex) { AppendLog("打开备份目录失败：" + ex.Message); }
        }

        void OpenLogFile()
        {
            try
            {
                if (!File.Exists(Log.FilePath)) { AppendLog("日志文件还不存在"); return; }
                Process.Start(new ProcessStartInfo("notepad.exe", "\"" + Log.FilePath + "\""));
            }
            catch (Exception ex) { AppendLog("打开日志失败：" + ex.Message); }
        }

        void OpenReadme()
        {
            string p = Path.Combine(GameFolder.AppDir, "README.md");
            if (!File.Exists(p)) { AppendLog("README.md 不在 exe 同目录"); return; }
            try { Process.Start(new ProcessStartInfo(p) { UseShellExecute = true }); }
            catch (Exception ex) { AppendLog("打开 README 失败：" + ex.Message); }
        }

        void CopyDiagnostics()
        {
            try
            {
                string text = "ZZZSwitcher 诊断信息\r\n" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n"
                            + Switcher.Check(_dir).ToText()
                            + "\r\n启动器:";
                string[] ls = LauncherMap.FindLauncherExes().ToArray();
                for (int i = 0; i < ls.Length; i++) text += "\r\n  " + ls[i];
                Clipboard.SetText(text);
                SetStatus("诊断信息已复制到剪贴板", CGreen);
                AppendLog("诊断信息已复制到剪贴板");
            }
            catch (Exception ex) { AppendLog("复制失败：" + ex.Message); }
        }

        void About()
        {
            MessageBox.Show(this,
                "绝区零 · 渠道切换器 v2.0\r\n\r\n"
                + "只改 config.ini 的 channel / sub_channel / cps / plugin_sdk_version，\r\n"
                + "不动 version_info、pkg_version 与任何游戏资源；每次切换前自动备份并记录 SHA-256。\r\n\r\n"
                + "日志：" + Log.FilePath + "\r\n目录：" + GameFolder.AppDir,
                "关于", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        void AppendLog(string msg)
        {
            if (_log == null) return;
            string line = DateTime.Now.ToString("HH:mm:ss  ") + msg;
            if (!_log.IsHandleCreated) { _log.AppendText(line + "\r\n"); return; }
            int vis = Math.Max(1, (_log.ClientSize.Height - 2) / LogLineHeight());
            int lines0 = LineCount();
            int first0 = FirstVisible();
            bool stick = (first0 + vis >= lines0 - 1);   // 已经贴底才跟着滚，用户往上翻时不打断
            _log.AppendText(line + "\r\n");
            if (stick)
            {
                try { _log.SelectionStart = _log.TextLength; _log.ScrollToCaret(); } catch { }
            }
            else
            {
                int delta = first0 - FirstVisible();
                if (delta != 0) SendMessage(_log.Handle, EM_LINESCROLL, IntPtr.Zero, (IntPtr)delta);
            }
            SyncLogScroll();
        }

        void RenderLogTail(int count)
        {
            if (_log == null) return;
            string[] tail = Log.GetTail(count);
            _log.Clear();
            for (int i = 0; i < tail.Length; i++) _log.AppendText(tail[i] + "\r\n");
            try { _log.SelectionStart = _log.TextLength; _log.ScrollToCaret(); } catch { }
            SyncLogScroll();
        }

        // ---------- 日志滚动条（自绘） ----------
        const int EM_GETLINECOUNT = 0x00BA;
        const int EM_GETFIRSTVISIBLELINE = 0x00CE;
        const int EM_LINESCROLL = 0x00B6;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        int LogLineHeight()
        {
            if (_log == null) return 16;
            Size sz = TextRenderer.MeasureText("国Ag|", _log.Font, new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            return Math.Max(8, sz.Height);
        }

        // 把 TextBox/RichTextBox 的当前滚动位置与自绘滑条对齐（含"内容不满一屏就隐藏滑条"）。
        void SyncLogScroll()
        {
            if (_log == null || _sbLog == null || _logBusy || !_log.IsHandleCreated) return;
            _logBusy = true;
            try
            {
                int lines = SendMessage(_log.Handle, EM_GETLINECOUNT, IntPtr.Zero, IntPtr.Zero).ToInt32();
                int first = SendMessage(_log.Handle, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero).ToInt32();
                int vis = Math.Max(1, (_log.ClientSize.Height - 2) / LogLineHeight());
                _sbLog.Update(0, Math.Max(0, lines - 1), vis, first);
                if (_sbLog.Visible != _sbLog.Needed) _sbLog.Visible = _sbLog.Needed;
            }
            catch { }
            finally { _logBusy = false; }
        }

        void ScrollLogTo(int target)
        {
            if (_log == null || !_log.IsHandleCreated) return;
            int vis = Math.Max(1, (_log.ClientSize.Height - 2) / LogLineHeight());
            int maxFirst = Math.Max(0, LineCount() - vis);
            if (target < 0) target = 0;
            if (target > maxFirst) target = maxFirst;   // 别让滑条拖到"首行=最后一行"这种不可达位置
            ScrollLogBy(target - FirstVisible());
        }

        // 相对滚动；RichTextBox 在 ScrollBars=None 下不再自行处理滚轮，这里统包。
        void ScrollLogBy(int delta)
        {
            if (delta == 0 || _log == null || !_log.IsHandleCreated) return;
            _logBusy = true;
            try
            {
                while (delta != 0)
                {
                    int step = Math.Max(-500, Math.Min(500, delta));
                    SendMessage(_log.Handle, EM_LINESCROLL, IntPtr.Zero, (IntPtr)step);
                    delta -= step;
                }
            }
            finally { _logBusy = false; }
            SyncLogScroll();
        }

        int LineCount()
        {
            if (_log == null || !_log.IsHandleCreated) return 0;
            return SendMessage(_log.Handle, EM_GETLINECOUNT, IntPtr.Zero, IntPtr.Zero).ToInt32();
        }

        int FirstVisible()
        {
            if (_log == null || !_log.IsHandleCreated) return 0;
            return SendMessage(_log.Handle, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero).ToInt32();
        }

        void OnLogWheel(object sender, MouseEventArgs e)
        {
            int lines = SystemInformation.MouseWheelScrollLines;
            if (lines < 0) lines = 3;
            int step = (e.Delta == 0 ? 0 : (e.Delta > 0 ? 1 : -1)) * Math.Max(1, lines);
            if (e.Delta == 0) return;
            ScrollLogBy(-step);
        }

        void OnDragEnter(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.None;
            try
            {
                if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
                string[] items = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (items == null || items.Length == 0) return;
                string d = items[0];
                if (File.Exists(d)) d = Path.GetDirectoryName(d);
                if (Directory.Exists(d)) e.Effect = DragDropEffects.Copy;
            }
            catch { }
        }

        void OnDragDrop(object sender, DragEventArgs e)
        {
            try
            {
                if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
                string[] items = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (items == null || items.Length == 0) return;
                string d = items[0];
                if (File.Exists(d)) d = Path.GetDirectoryName(d);
                if (!Directory.Exists(d)) return;
                if (!GameFolder.IsValid(d))
                {
                    // 允许拖到上一级，例如拖 games 目录
                    string child = Path.Combine(d, "games", "ZenlessZoneZero Game");
                    if (GameFolder.IsValid(child)) d = child;
                }
                if (!GameFolder.IsValid(d))
                {
                    SetStatus("拖入的目录里没有 ZenlessZoneZero.exe", CRed);
                    AppendLog("拖入的目录无效：" + d);
                    return;
                }
                SetDir(d, true);
                AppendLog("游戏目录设为 " + _dir);
            }
            catch (Exception ex) { AppendLog("拖放处理失败：" + ex.Message); }
        }
    }
}
