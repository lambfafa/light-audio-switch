using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LightAudioSwitch
{
    internal static class Theme
    {
        public static readonly Icon AppIcon = Icon.ExtractAssociatedIcon(typeof(Theme).Assembly.Location);
        public static readonly Color Blue = ColorTranslator.FromHtml("#0066cc");
        public static readonly Color Ink = ColorTranslator.FromHtml("#1d1d1f");
        public static readonly Color Muted = ColorTranslator.FromHtml("#77777d");
        public static readonly Color Canvas = ColorTranslator.FromHtml("#f5f5f7");
        public static readonly Color Line = ColorTranslator.FromHtml("#e6e6eb");
        public static readonly Color Track = ColorTranslator.FromHtml("#e8e8ed");
        public static readonly Color Error = ColorTranslator.FromHtml("#b34135");
        public static readonly Font Body = new Font("Microsoft YaHei UI", 9.5f);
        public static readonly Font Small = new Font("Microsoft YaHei UI", 8.5f);
        public static readonly Font Medium = new Font("Microsoft YaHei UI", 10.5f);
        public static readonly Font Title = new Font("Microsoft YaHei UI", 17f, FontStyle.Bold);
        public static readonly Font Display = new Font("Microsoft YaHei UI", 23f, FontStyle.Bold);
        public static GraphicsPath Round(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0) { path.AddRectangle(r); return path; }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure(); return path;
        }
        public static void FillRound(Graphics g, RectangleF r, float radius, Color color)
        { using (var p = Round(r, radius)) using (var b = new SolidBrush(color)) g.FillPath(b, p); }
        public static void StrokeRound(Graphics g, RectangleF r, float radius, Color color, float width)
        { using (var p = Round(r, radius)) using (var pen = new Pen(color, width)) g.DrawPath(pen, p); }
        public static void Text(Graphics g, string text, Font font, Color color, Rectangle bounds, TextFormatFlags extra)
        { TextRenderer.DrawText(g, text, font, bounds, color, TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | extra); }
        public static string DeviceName(string text)
        {
            if (String.IsNullOrEmpty(text)) return "选择设备";
            const string prefix = "扬声器 (";
            return text.StartsWith(prefix, StringComparison.Ordinal) && text.EndsWith(")", StringComparison.Ordinal)
                ? text.Substring(prefix.Length, text.Length - prefix.Length - 1) : text;
        }
        public static void Symbol(Graphics g, string kind, RectangleF box, Color color)
        {
            var state = g.Save();
            g.TranslateTransform(box.X, box.Y); g.ScaleTransform(box.Width / 24, box.Height / 24);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(color, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                if (kind == "headset")
                {
                    g.DrawArc(pen, 4, 3, 16, 18, 180, 180);
                    g.DrawLine(pen, 4, 12, 4, 17); g.DrawLine(pen, 20, 12, 20, 17);
                    using (var l = Round(new RectangleF(3.5f, 11, 4, 9), 1.6f)) g.DrawPath(pen, l);
                    using (var r = Round(new RectangleF(16.5f, 11, 4, 9), 1.6f)) g.DrawPath(pen, r);
                }
                else if (kind == "speaker")
                {
                    using (var path = Round(new RectangleF(5, 2.5f, 14, 19), 3)) g.DrawPath(pen, path);
                    g.DrawEllipse(pen, 9, 6, 6, 6); g.DrawEllipse(pen, 10.5f, 15.5f, 3, 3);
                }
                else if (kind == "game")
                {
                    using (var path = Round(new RectangleF(2, 6, 20, 13), 4.5f)) g.DrawPath(pen, path);
                    g.DrawLine(pen, 6, 12, 11, 12); g.DrawLine(pen, 8.5f, 9.5f, 8.5f, 14.5f);
                    g.DrawEllipse(pen, 16, 10, 0.8f, 0.8f); g.DrawEllipse(pen, 18, 13, 0.8f, 0.8f);
                }
                else if (kind == "refresh")
                {
                    g.DrawArc(pen, 5, 5, 14, 14, 45, 290);
                    g.DrawLines(pen, new[] { new PointF(19, 3), new PointF(19, 8), new PointF(14, 8) });
                }
                else if (kind == "close")
                { g.DrawLine(pen, 8, 8, 16, 16); g.DrawLine(pen, 16, 8, 8, 16); }
                else if (kind == "chevron")
                { g.DrawLines(pen, new[] { new PointF(8, 10), new PointF(12, 14), new PointF(16, 10) }); }
                else if (kind == "chevron-up")
                { g.DrawLines(pen, new[] { new PointF(8, 14), new PointF(12, 10), new PointF(16, 14) }); }
            }
            g.Restore(state);
        }
    }

    internal class Surface : Panel
    {
        public float Radius = 16;
        public Surface()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.White;
        }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? Theme.Canvas : Parent.BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Theme.FillRound(e.Graphics, new RectangleF(0, 0, Width, Height), Radius * e.Graphics.DpiX / 96, BackColor);
        }
    }

    // Use a plain managed control: native Button/CheckBox windows can repaint
    // their flat borders over rounded owner drawing during focus/default changes.
    internal class PaintedAction : Control
    {
        protected bool Hovered, Pressed;
        private bool keyboardPressed;
        public PaintedAction()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor | ControlStyles.Selectable, true);
            SetStyle(ControlStyles.StandardDoubleClick, false);
            BackColor = Color.Transparent; Font = Theme.Body; Cursor = Cursors.Hand; TabStop = true;
        }
        protected override void OnMouseEnter(EventArgs e) { Hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { Hovered = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { Focus(); Pressed = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { Pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnMouseCaptureChanged(EventArgs e) { Pressed = false; Invalidate(); base.OnMouseCaptureChanged(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { keyboardPressed = Pressed = false; Invalidate(); base.OnLostFocus(e); }
        protected override void OnEnabledChanged(EventArgs e) { if (!Enabled) keyboardPressed = Pressed = false; Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space && e.Modifiers == Keys.None) { keyboardPressed = Pressed = true; Invalidate(); e.Handled = e.SuppressKeyPress = true; }
            base.OnKeyDown(e);
        }
        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space && keyboardPressed) {
                keyboardPressed = Pressed = false; Invalidate(); if (Enabled && Focused) OnClick(EventArgs.Empty);
                e.Handled = e.SuppressKeyPress = true;
            }
            base.OnKeyUp(e);
        }
        protected bool IsPressed { get { return Pressed && (Hovered || keyboardPressed); } }
    }

    internal class QuietButton : PaintedAction, IButtonControl
    {
        public bool Primary, Selected, Segment;
        public DialogResult DialogResult { get; set; }
        public QuietButton() { AccessibleRole = AccessibleRole.PushButton; }
        public void NotifyDefault(bool value) { /* The primary fill already identifies the default action. */ }
        public void PerformClick()
        {
            if (!Enabled || !CanSelect) return;
            OnClick(EventArgs.Empty);
        }
        protected override bool ProcessDialogKey(Keys keyData)
        { if (keyData == Keys.Enter) { PerformClick(); return true; } return base.ProcessDialogKey(keyData); }
        protected override void OnClick(EventArgs e)
        { if (!Enabled) return; base.OnClick(e); var form = FindForm(); if (DialogResult != DialogResult.None && form != null) form.DialogResult = DialogResult; }
        protected override AccessibleObject CreateAccessibilityInstance() { return new ActionAccessibleObject(this); }
        private sealed class ActionAccessibleObject : ControlAccessibleObject
        {
            private readonly QuietButton owner;
            public ActionAccessibleObject(QuietButton owner) : base(owner) { this.owner = owner; }
            public override string DefaultAction { get { return "按下"; } }
            public override AccessibleStates State { get { return base.State | (owner.Selected ? AccessibleStates.Checked : AccessibleStates.None); } }
            public override void DoDefaultAction() { owner.PerformClick(); }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            float s = e.Graphics.DpiX / 96;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(1, 1, Width - 2, Height - 2);
            Color bg = Primary ? (!Enabled ? Theme.Track : IsPressed ? Color.FromArgb(0, 80, 165) : Theme.Blue)
                : Selected ? Color.White : IsPressed ? Color.FromArgb(220, 220, 227) : Hovered && Enabled ? Color.FromArgb(235, 235, 240) : Color.Transparent;
            if (bg != Color.Transparent) Theme.FillRound(e.Graphics, r, (Segment ? 7 : 18) * s, bg);
            Color ink = !Enabled ? Theme.Muted : Primary ? Color.White : Segment ? Theme.Ink : Theme.Blue;
            Theme.Text(e.Graphics, Text, Font, ink, ClientRectangle, TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine);
            if (Focused && ShowFocusCues) Theme.StrokeRound(e.Graphics, new RectangleF(2, 2, Width - 4, Height - 4), (Segment ? 7 : 18) * s, Primary ? Color.White : Theme.Blue, s);
        }
    }

    internal sealed class ModeControl : Surface
    {
        private readonly QuietButton[] buttons = new QuietButton[3];
        private OutputMode mode;
        public event Action<OutputMode> ModeRequested;
        public ModeControl()
        {
            BackColor = Theme.Track; Radius = 10;
            string[] labels = { "自动", "耳机", "音箱" };
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                var b = new QuietButton { Text = labels[i], Segment = true,
                    Font = Theme.Small, AccessibleName = labels[i] + "模式", AccessibleRole = AccessibleRole.RadioButton };
                b.Click += delegate { if (ModeRequested != null) ModeRequested((OutputMode)index); };
                buttons[i] = b; Controls.Add(b);
            }
            SetMode(OutputMode.Auto);
        }
        public void SetMode(OutputMode value)
        {
            mode = value;
            for (int i = 0; i < 3; i++) { buttons[i].Selected = i == (int)mode; buttons[i].Invalidate(); }
        }
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (buttons[2] == null) return;
            int pad = Math.Max(3, Height / 12), w = (Width - pad * 2) / 3;
            for (int i = 0; i < 3; i++) buttons[i].SetBounds(pad + i * w, pad, i == 2 ? Width - pad - (pad + i * w) : w, Height - pad * 2);
        }
    }

    internal sealed class Toggle : PaintedAction
    {
        private bool isChecked;
        public event EventHandler CheckedChanged;
        public bool Checked
        {
            get { return isChecked; }
            set { if (isChecked == value) return; isChecked = value; Invalidate();
                AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
                if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty); }
        }
        public Toggle() { AccessibleRole = AccessibleRole.CheckButton; }
        protected override void OnClick(EventArgs e) { if (Enabled) { Checked = !Checked; base.OnClick(e); } }
        protected override AccessibleObject CreateAccessibilityInstance() { return new ToggleAccessibleObject(this); }
        private sealed class ToggleAccessibleObject : ControlAccessibleObject
        {
            private readonly Toggle owner;
            public ToggleAccessibleObject(Toggle owner) : base(owner) { this.owner = owner; }
            public override string DefaultAction { get { return "切换"; } }
            public override AccessibleStates State { get { return base.State | (owner.Checked ? AccessibleStates.Checked : AccessibleStates.None); } }
            public override void DoDefaultAction() { if (owner.Enabled) owner.OnClick(EventArgs.Empty); }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; float s = g.DpiX / 96;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(2 * s, (Height - 24 * s) / 2, Width - 4 * s, 24 * s);
            Theme.FillRound(g, r, 12 * s, Enabled && Checked ? Theme.Blue : Theme.Track);
            float size = 18 * s;
            using (var brush = new SolidBrush(Color.White)) g.FillEllipse(brush, Checked ? r.Right - size - 3 * s : r.Left + 3 * s, r.Top + 3 * s, size, size);
            if (Focused && ShowFocusCues) Theme.StrokeRound(g, new RectangleF(0.5f, r.Y - 2 * s, Width - 1, r.Height + 4 * s), 14 * s, Theme.Blue, s);
        }
    }

    internal sealed class DeviceCard : Control
    {
        public readonly bool IsHeadset;
        public List<AudioDevice> Devices = new List<AudioDevice>();
        private AudioDevice selectedDevice;
        public AudioDevice SelectedDevice
        {
            get { return selectedDevice; }
            set { selectedDevice = value; Text = value == null ? "选择设备" : Theme.DeviceName(value.Name);
                AccessibleDescription = value == null ? "尚未选择" : value.Name + (value.Active ? "" : "，离线"); Invalidate(); }
        }
        public bool IsCurrent;
        public event Action SelectionChanged;
        private bool hovered;
        private readonly PopupMenu deviceMenu = new PopupMenu("输出设备");
        public DeviceCard(bool headset)
        {
            IsHeadset = headset;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Canvas; Font = Theme.Body; Cursor = Cursors.Hand; TabStop = true;
            AccessibleName = headset ? "游戏输出设备" : "日常输出设备";
            AccessibleRole = AccessibleRole.ComboBox;
            deviceMenu.Opened += delegate { Invalidate(); };
            deviceMenu.Closed += delegate { Invalidate(); };
        }
        public void Choose(string id, string savedName)
        {
            SelectedDevice = Devices.Find(delegate(AudioDevice d) { return d.Id == id; });
            if (SelectedDevice == null && !String.IsNullOrEmpty(id)) SelectedDevice = new AudioDevice { Id = id, Name = savedName ?? "原设备", State = 0 };
            Invalidate();
        }
        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnClick(EventArgs e) { base.OnClick(e); Focus(); OpenMenu(); }
        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (keyData == Keys.Enter || keyData == (Keys.Alt | Keys.Down)) { OpenMenu(); return true; }
            return base.ProcessDialogKey(keyData);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space || e.KeyCode == Keys.F4 || (e.Alt && e.KeyCode == Keys.Down))
            { e.Handled = e.SuppressKeyPress = true; OpenMenu(); }
            base.OnKeyDown(e);
        }
        private void OpenMenu()
        {
            if (deviceMenu.Visible) { deviceMenu.Close(); return; }
            // Rebuild from the latest device list while keeping the menu itself alive.
            while (deviceMenu.Items.Count > 0) deviceMenu.Items[0].Dispose();
            foreach (AudioDevice device in Devices)
            {
                var item = deviceMenu.AddOption(Theme.DeviceName(device.Name), delegate {
                    if (SelectedDevice != null && SelectedDevice.Id == device.Id) return;
                    SelectedDevice = device;
                    if (SelectionChanged != null) SelectionChanged();
                }, device.Active ? null : "离线");
                item.Checked = SelectedDevice != null && SelectedDevice.Id == device.Id;
                item.ToolTipText = device.Name;
            }
            if (deviceMenu.Items.Count == 0) deviceMenu.AddOption("没有可用设备").Enabled = false;
            deviceMenu.ShowFor(this, matchWidth: true);
        }
        protected override void Dispose(bool disposing)
        { if (disposing) deviceMenu.Dispose(); base.Dispose(disposing); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; float s = g.DpiX / 96;
            g.Clear(Theme.Canvas); g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(s, s, Width - 2 * s, Height - 2 * s);
            Theme.FillRound(g, r, 17 * s, Color.White);
            bool expanded = deviceMenu.Visible;
            if (IsCurrent || expanded || hovered || (Focused && ShowFocusCues))
                Theme.StrokeRound(g, r, 17 * s, IsCurrent || expanded || (Focused && ShowFocusCues) ? Theme.Blue : Theme.Line, IsCurrent ? 1.5f * s : s);
            Theme.Symbol(g, IsHeadset ? "headset" : "speaker", new RectangleF(20 * s, 21 * s, 23 * s, 23 * s), IsCurrent ? Theme.Blue : Theme.Ink);
            Theme.Text(g, IsHeadset ? "游戏耳机" : "日常音箱", Theme.Small, Theme.Muted,
                new Rectangle((int)(53 * s), (int)(19 * s), Width - (int)(98 * s), (int)(27 * s)), TextFormatFlags.SingleLine);
            string name = SelectedDevice == null ? "选择设备" : Theme.DeviceName(SelectedDevice.Name);
            Theme.Text(g, name, Theme.Medium, SelectedDevice == null ? Theme.Blue : Theme.Ink,
                new Rectangle((int)(20 * s), (int)(62 * s), Width - (int)(60 * s), (int)(34 * s)), TextFormatFlags.SingleLine);
            Theme.Symbol(g, expanded ? "chevron-up" : "chevron", new RectangleF(Width - 37 * s, 70 * s, 18 * s, 18 * s), expanded ? Theme.Blue : Theme.Muted);
            if (SelectedDevice != null && !SelectedDevice.Active)
                Theme.Text(g, "离线", Theme.Small, Theme.Error, new Rectangle(Width - (int)(55 * s), (int)(21 * s), (int)(38 * s), (int)(23 * s)), TextFormatFlags.Right | TextFormatFlags.SingleLine);
            else if (IsCurrent) using (var b = new SolidBrush(Theme.Blue)) g.FillEllipse(b, Width - 28 * s, 30 * s, 5 * s, 5 * s);
        }
    }

    internal sealed class GameList : ListBox
    {
        public event Action<int> RemoveRequested;
        private readonly ToolTip tip = new ToolTip();
        private int tipIndex = -1;
        public GameList()
        {
            BorderStyle = BorderStyle.None; DrawMode = DrawMode.OwnerDrawFixed;
            IntegralHeight = false; BackColor = Color.White; Font = Theme.Body;
            AccessibleName = "自动切换的游戏列表";
            using (var g = CreateGraphics()) ItemHeight = (int)Math.Round(54 * g.DpiX / 96);
        }
        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= Items.Count) return;
            var rule = (GameRule)Items[e.Index]; var g = e.Graphics; float s = g.DpiX / 96;
            var b = e.Bounds;
            g.FillRectangle(Brushes.White, b); g.SmoothingMode = SmoothingMode.AntiAlias;
            if ((e.State & DrawItemState.Selected) != 0) Theme.FillRound(g, new RectangleF(b.X, b.Y + 2 * s, b.Width, b.Height - 4 * s), 9 * s, Theme.Canvas);
            Theme.FillRound(g, new RectangleF(b.X + 10 * s, b.Y + 11 * s, 32 * s, 32 * s), 8 * s, Theme.Canvas);
            Theme.Symbol(g, "game", new RectangleF(b.X + 16 * s, b.Y + 17 * s, 20 * s, 20 * s), Theme.Ink);
            Theme.Text(g, rule.Name, Theme.Body, Theme.Ink,
                new Rectangle(b.X + (int)(55 * s), b.Y, b.Width - (int)(100 * s), b.Height), TextFormatFlags.SingleLine);
            Theme.Symbol(g, "close", new RectangleF(b.Right - 37 * s, b.Y + 17 * s, 20 * s, 20 * s), Theme.Muted);
            if (Focused && (e.State & DrawItemState.Focus) != 0)
                Theme.StrokeRound(g, new RectangleF(b.X + 1, b.Y + 2 * s, b.Width - 2, b.Height - 4 * s), 9 * s, Theme.Blue, s);
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            int index = IndexFromPoint(e.Location);
            using (var g = CreateGraphics())
                if (index >= 0 && e.X > ClientSize.Width - 44 * g.DpiX / 96)
                { if (RemoveRequested != null) RemoveRequested(index); return; }
            base.OnMouseDown(e);
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            int index = IndexFromPoint(e.Location);
            if (index != tipIndex)
            {
                tipIndex = index;
                var g = index >= 0 ? Items[index] as GameRule : null;
                tip.SetToolTip(this, g == null ? "" : (g.FullPath ?? g.Executable));
            }
            base.OnMouseMove(e);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete && SelectedIndex >= 0)
            { if (RemoveRequested != null) RemoveRequested(SelectedIndex); e.Handled = true; }
            base.OnKeyDown(e);
        }
        protected override void Dispose(bool disposing) { if (disposing) tip.Dispose(); base.Dispose(disposing); }
    }
}
