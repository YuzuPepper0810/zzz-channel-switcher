using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace ZzzSwitch
{
    // 统一的配色与绘制工具。全部手绘，不引入外部依赖（只用 System.Drawing）。
    static class Theme
    {
        public static readonly Color Page = Color.FromArgb(243, 245, 249);
        public static readonly Color CardFill = Color.White;
        public static readonly Color CardBorder = Color.FromArgb(228, 232, 238);
        public static readonly Color Divider = Color.FromArgb(236, 239, 244);

        public static readonly Color TextStrong = Color.FromArgb(27, 31, 38);
        public static readonly Color TextBody = Color.FromArgb(58, 65, 75);
        public static readonly Color TextDim = Color.FromArgb(122, 130, 142);
        public static readonly Color TextFaint = Color.FromArgb(166, 173, 183);

        public static readonly Color Official = Color.FromArgb(64, 116, 240);
        public static readonly Color Bilibili = Color.FromArgb(251, 114, 153);
        public static readonly Color Success = Color.FromArgb(24, 138, 78);
        public static readonly Color Warn = Color.FromArgb(183, 121, 31);
        public static readonly Color Danger = Color.FromArgb(198, 59, 59);

        public static readonly Color LogBack = Color.FromArgb(30, 34, 41);
        public static readonly Color LogText = Color.FromArgb(200, 208, 218);
        public static readonly Color LogDim = Color.FromArgb(110, 118, 129);

        public static Font Ui(float size, FontStyle style)
        {
            return new Font("Microsoft YaHei UI", size, style);
        }

        public static GraphicsPath Rounded(Rectangle r, int radius)
        {
            GraphicsPath p = new GraphicsPath();
            if (radius <= 0) { p.AddRectangle(r); return p; }
            int d = radius * 2;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static Color Lighten(Color c, float amount)
        {
            return Color.FromArgb(c.A, Mix(c.R, 255, amount), Mix(c.G, 255, amount), Mix(c.B, 255, amount));
        }

        public static Color Darken(Color c, float amount)
        {
            return Color.FromArgb(c.A, Mix(c.R, 0, amount), Mix(c.G, 0, amount), Mix(c.B, 0, amount));
        }

        public static Color Tint(Color c, float amount)
        {
            return Color.FromArgb(Mix(255, c.R, amount), Mix(255, c.G, amount), Mix(255, c.B, amount));
        }

        static int Mix(int a, int b, float t)
        {
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            int v = (int)Math.Round(a + (b - a) * t);
            if (v < 0) v = 0;
            if (v > 255) v = 255;
            return v;
        }

        public static void DrawText(Graphics g, string text, Font font, Rectangle bounds, Color color, TextFormatFlags flags)
        {
            TextRenderer.DrawText(g, text, font, bounds, color, flags | TextFormatFlags.NoPrefix);
        }
    }

    // 圆角卡片：自带浅边框，可选左侧彩色竖条（用来标出当前渠道）。
    class Card : Panel
    {
        public int Radius = 12;
        public Color AccentColor = Color.Empty;
        public int AccentWidth = 4;

        public Card()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            // 子控件继承这个颜色，必须是白色，否则标签会在卡片上刷出一块块灰底。
            BackColor = Theme.CardFill;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Page);   // 圆角外的部分露出页面底色
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 3);
            // 2px 偏移的浅色投影，让卡片从页面底色上"浮"起来
            using (GraphicsPath sh = Theme.Rounded(new Rectangle(r.X, r.Y + 2, r.Width, r.Height), Radius))
            using (SolidBrush sb = new SolidBrush(Color.FromArgb(233, 236, 242)))
                g.FillPath(sb, sh);
            using (GraphicsPath path = Theme.Rounded(r, Radius))
            {
                using (SolidBrush b = new SolidBrush(Theme.CardFill)) g.FillPath(b, path);
                using (Pen pen = new Pen(Theme.CardBorder)) g.DrawPath(pen, path);
                if (AccentColor != Color.Empty && AccentWidth > 0)
                {
                    Region old = g.Clip;
                    g.SetClip(path);
                    using (SolidBrush ab = new SolidBrush(AccentColor))
                        g.FillRectangle(ab, new Rectangle(r.X, r.Y, AccentWidth, r.Height));
                    g.Clip = old;
                }
            }
        }
    }

    // 手绘按钮：Primary（实色）/ Secondary（描边）。Current 表示"当前渠道"，用描边+彩色文字。
    class UiButton : Button
    {
        bool _hover;
        bool _press;

        public Color Accent = Theme.Official;
        public bool Primary = true;
        public bool Current = false;
        public int Radius = 9;

        public UiButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Theme.CardFill;
            UseVisualStyleBackColor = false;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _press = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _press = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _press = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill, border, text;

            if (!Enabled)
            {
                fill = Color.FromArgb(242, 244, 247);
                border = Theme.CardBorder;
                text = Theme.TextFaint;
            }
            else if (Primary)
            {
                Color baseColor = _press ? Theme.Darken(Accent, 0.10f) : (_hover ? Theme.Lighten(Accent, 0.10f) : Accent);
                fill = baseColor;
                border = baseColor;
                text = Color.White;
            }
            else
            {
                fill = _hover ? Theme.Tint(Accent, 0.07f) : (_press ? Theme.Tint(Accent, 0.12f) : Theme.CardFill);
                border = Current ? Theme.Lighten(Accent, 0.30f) : Color.FromArgb(219, 224, 231);
                text = Current ? Accent : Theme.TextBody;
            }

            using (GraphicsPath path = Theme.Rounded(r, Radius))
            {
                using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, path);
                using (Pen pen = new Pen(border)) g.DrawPath(pen, path);
            }

            Theme.DrawText(g, Text, Font, r, text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            if (Focused && ShowFocusCues && Enabled)
            {
                Rectangle inner = new Rectangle(r.X + 3, r.Y + 3, r.Width - 6, r.Height - 6);
                using (GraphicsPath ip = Theme.Rounded(inner, Math.Max(2, Radius - 3)))
                {
                    using (Pen fp = new Pen(Color.FromArgb(90, text)))
                    {
                        fp.DashStyle = DashStyle.Dot;
                        g.DrawPath(fp, ip);
                    }
                }
            }
        }
    }

    // 手绘复选框：16px 圆角方块 + 对勾，颜色跟随当前渠道。
    class UiCheck : CheckBox
    {
        public Color Accent = Theme.Official;
        bool _hover;

        public UiCheck()
        {
            BackColor = Theme.CardFill;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnCheckedChanged(EventArgs e) { Invalidate(); base.OnCheckedChanged(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            const int box = 16;
            int top = (Height - box) / 2;
            if (top < 0) top = 0;
            Rectangle br = new Rectangle(1, top, box, box);

            Color accent = Enabled ? Accent : Theme.TextFaint;
            using (GraphicsPath bp = Theme.Rounded(br, 4))
            {
                if (Checked)
                {
                    using (SolidBrush b = new SolidBrush(_hover ? Theme.Lighten(accent, 0.10f) : accent)) g.FillPath(b, bp);
                }
                else
                {
                    using (SolidBrush b = new SolidBrush(Enabled ? Theme.CardFill : Color.FromArgb(242, 244, 247))) g.FillPath(b, bp);
                    using (Pen pen = new Pen(_hover ? Theme.Darken(accent, 0.05f) : Color.FromArgb(200, 206, 215))) g.DrawPath(pen, bp);
                }
            }

            if (Checked)
            {
                using (Pen tick = new Pen(Color.White, 2f))
                {
                    tick.StartCap = LineCap.Round;
                    tick.EndCap = LineCap.Round;
                    g.DrawLine(tick, br.X + 4, br.Y + 8, br.X + 7, br.Y + 11);
                    g.DrawLine(tick, br.X + 7, br.Y + 11, br.X + 12, br.Y + 5);
                }
            }

            Rectangle textRect = new Rectangle(br.Right + 7, 0, Width - br.Right - 8, Height);
            Theme.DrawText(g, Text, Font, textRect, Enabled ? Theme.TextBody : Theme.TextFaint,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    // 底部状态栏：左侧彩色圆点 + 消息，右侧时间 + 版本胶囊。
    class StatusBar : Panel
    {
        public string Message = "就绪";
        public Color MessageColor = Theme.TextDim;
        public string TimeText = "";
        public string Version = "v2.0";

        public StatusBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.CardFill;
        }

        public void Set(string message, Color color)
        {
            Message = message;
            MessageColor = color;
            TimeText = DateTime.Now.ToString("HH:mm:ss");
            Invalidate();
        }

        public void InvalidateOnly()
        {
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.CardFill);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (Pen top = new Pen(Theme.CardBorder)) g.DrawLine(top, 0, 0, Width, 0);

            int box = 8;
            int cy = Height / 2 - box / 2;
            using (SolidBrush b = new SolidBrush(MessageColor)) g.FillEllipse(b, 14, cy, box, box);

            Font body = Theme.Ui(9f, FontStyle.Regular);
            Font small = Theme.Ui(8.25f, FontStyle.Regular);

            int usedRight = 96;
            Rectangle textRect = new Rectangle(30, 0, Math.Max(40, Width - usedRight - 30), Height);
            Theme.DrawText(g, Message, body, textRect, MessageColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            // 版本胶囊
            Rectangle pill = new Rectangle(Width - 62, Height / 2 - 9, 48, 18);
            using (GraphicsPath pp = Theme.Rounded(pill, 9))
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(241, 244, 248))) g.FillPath(b, pp);
            }
            Theme.DrawText(g, Version, small, pill, Theme.TextDim,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            if (TimeText.Length > 0)
            {
                Rectangle timeRect = new Rectangle(Width - 62 - 62, 0, 60, Height);
                Theme.DrawText(g, TimeText, small, timeRect, Theme.TextFaint,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            }
        }
    }

    // 自绘路径框：系统 ComboBox 的箭头和边框跟这套扁平风格不搭，这里完全手画。
    // 单击弹出最近目录菜单（菜单由窗体外层提供，控件本身只管外观和命中）。
    class PathBox : Control
    {
        public Color Accent = Theme.Official;
        public int Radius = 7;
        bool _hover;

        public PathBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.CardFill;
            Cursor = Cursors.Hand;
            Font = Theme.Ui(9f, FontStyle.Regular);
            Height = 28;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color border = !Enabled ? Theme.CardBorder
                         : (Focused || _hover) ? Theme.Tint(Accent, 0.45f)
                         : Color.FromArgb(224, 228, 235);
            using (GraphicsPath path = Theme.Rounded(r, Radius))
            {
                using (SolidBrush b = new SolidBrush(Enabled ? Color.FromArgb(250, 251, 253) : Color.FromArgb(247, 248, 250)))
                    g.FillPath(b, path);
                using (Pen p = new Pen(border)) g.DrawPath(p, path);
            }

            Rectangle txt = new Rectangle(11, 0, Width - 11 - 26, Height);
            Theme.DrawText(g, Text ?? "", Font, txt,
                Enabled ? Theme.TextStrong : Theme.TextFaint,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.PathEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);

            Rectangle chev = new Rectangle(Width - 22, 0, 14, Height);
            Theme.DrawText(g, "▾", Theme.Ui(8.5f, FontStyle.Regular), chev,
                Enabled ? Theme.TextDim : Theme.TextFaint,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }

    // 深色滚动条：没有箭头、没有系统边框，只画一条圆角轨道 + 圆角滑块。
    // 由外层用 Update(min,max,large,value) 喂状态，用户拖动/点击时抛 ValueChanged。
    class DarkScrollBar : Control
    {
        public int Radius = 4;
        public Color ThumbColor = Color.FromArgb(84, 95, 112);
        public Color ThumbHoverColor = Color.FromArgb(124, 137, 156);
        public Color TrackColor = Color.FromArgb(41, 46, 55);
        public int MinThumb = 22;
        public int SidePad = 2;

        int _min;
        int _max;
        int _large = 1;
        int _val;
        bool _drag;
        bool _hover;
        int _dragOffset;

        public event EventHandler ValueChanged;

        public DarkScrollBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.LogBack;
            Width = 12;
        }

        public int Value { get { return _val; } }
        public bool Needed { get { return (_max - _min + 1) > _large; } }

        public void Update(int min, int max, int large, int value)
        {
            int nmax = Math.Max(min, max);
            int nlarge = Math.Max(1, large);
            int nval = Math.Min(Math.Max(value, min), nmax);
            if (nmax == _max && nlarge == _large && nval == _val && min == _min) return;
            _min = min;
            _max = nmax;
            _large = nlarge;
            _val = nval;
            Cursor = Needed ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        Rectangle Thumb()
        {
            int h = Height - SidePad * 2;
            if (h <= 0) return Rectangle.Empty;
            int span = Math.Max(1, _max - _min + 1);
            int th = (int)Math.Round((double)h * _large / span);
            if (th < MinThumb) th = MinThumb;
            if (th > h) th = h;
            int scrollable = span - _large;
            double frac = scrollable <= 0 ? 0 : (double)(_val - _min) / scrollable;
            if (frac < 0) frac = 0;
            if (frac > 1) frac = 1;
            int y = SidePad + (int)Math.Round((h - th) * frac);
            return new Rectangle(SidePad, y, Width - SidePad * 2, th);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            if (!Needed) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle track = new Rectangle(SidePad + 1, SidePad, Math.Max(2, Width - SidePad * 2 - 2), Height - SidePad * 2);
            using (GraphicsPath tp = Theme.Rounded(track, Radius))
            using (SolidBrush tb = new SolidBrush(TrackColor))
                g.FillPath(tb, tp);

            Rectangle th = Thumb();
            if (th.Height <= 0) return;
            using (GraphicsPath hp = Theme.Rounded(th, Radius))
            using (SolidBrush hb = new SolidBrush(_drag || _hover ? ThumbHoverColor : ThumbColor))
                g.FillPath(hb, hp);
        }

        void Raise()
        {
            if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
        }

        // 把鼠标 y 换算成 value
        void SetFromMouse(int y)
        {
            int h = Height - SidePad * 2;
            if (h <= 0) return;
            Rectangle th = Thumb();
            if (th.Height <= 0) return;
            int travel = h - th.Height;
            if (travel <= 0) { _val = _min; Invalidate(); Raise(); return; }
            int py = y - _dragOffset - SidePad;
            if (py < 0) py = 0;
            if (py > travel) py = travel;
            int scrollable = Math.Max(1, (_max - _min + 1) - _large);
            int nv = _min + (int)Math.Round((double)py / travel * scrollable);
            if (nv == _val) return;
            _val = nv;
            Invalidate();
            Raise();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!Needed) return;
            Rectangle th = Thumb();
            if (th.Contains(e.Location))
            {
                _drag = true;
                _dragOffset = e.Y - th.Y;
                Capture = true;
            }
            else
            {
                int step = Math.Max(1, _large - 1);
                int nv = e.Y < th.Y ? _val - step : _val + step;
                if (nv < _min) nv = _min;
                if (nv > _max) nv = _max;
                if (nv != _val) { _val = nv; Invalidate(); Raise(); }
            }
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_hover) { _hover = true; Invalidate(); }
            if (_drag) SetFromMouse(e.Y);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _drag = false;
            Capture = false;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_drag) return;
            _hover = false;
            Invalidate();
        }
    }

    // 深色圆角底板，用来放日志文本（白底日志区太刺眼，也不好看）。
    class DarkPanel : Panel
    {
        public int Radius = 10;
        public Color Fill = Theme.LogBack;

        public DarkPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.CardFill;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Theme.Rounded(r, Radius))
            {
                using (SolidBrush b = new SolidBrush(Fill)) g.FillPath(b, path);
                using (Pen pen = new Pen(Color.FromArgb(52, 58, 68))) g.DrawPath(pen, path);
            }
        }
    }

    // 资源管理器风格的浅色菜单渲染器（默认渲染器是蓝色渐变高亮，和整体不搭）。
    class FlatColorTable : ProfessionalColorTable
    {
        public override Color MenuStripGradientBegin { get { return Theme.CardFill; } }
        public override Color MenuStripGradientEnd { get { return Theme.CardFill; } }
        public override Color ToolStripDropDownBackground { get { return Theme.CardFill; } }
        public override Color ImageMarginGradientBegin { get { return Theme.CardFill; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.CardFill; } }
        public override Color ImageMarginGradientEnd { get { return Theme.CardFill; } }
        public override Color MenuBorder { get { return Theme.CardBorder; } }
        public override Color MenuItemBorder { get { return Color.FromArgb(236, 240, 246); } }
        public override Color MenuItemSelected { get { return Color.FromArgb(238, 241, 247); } }
        public override Color MenuItemSelectedGradientBegin { get { return Color.FromArgb(238, 241, 247); } }
        public override Color MenuItemSelectedGradientEnd { get { return Color.FromArgb(238, 241, 247); } }
        public override Color MenuItemPressedGradientBegin { get { return Color.FromArgb(232, 236, 243); } }
        public override Color MenuItemPressedGradientEnd { get { return Color.FromArgb(232, 236, 243); } }
        public override Color SeparatorDark { get { return Theme.Divider; } }
        public override Color SeparatorLight { get { return Color.White; } }
        public override Color CheckBackground { get { return Color.FromArgb(238, 241, 247); } }
        public override Color CheckSelectedBackground { get { return Color.FromArgb(230, 235, 243); } }
        public override Color CheckPressedBackground { get { return Color.FromArgb(230, 235, 243); } }
        public override Color ButtonSelectedHighlight { get { return Color.FromArgb(238, 241, 247); } }
        public override Color ButtonSelectedBorder { get { return Color.FromArgb(238, 241, 247); } }
    }

    class FlatMenuRenderer : ToolStripProfessionalRenderer
    {
        public FlatMenuRenderer() : base(new FlatColorTable()) { }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Theme.TextBody : Theme.TextFaint;
            base.OnRenderItemText(e);
        }
    }
}
