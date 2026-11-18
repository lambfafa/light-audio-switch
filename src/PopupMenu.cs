using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LightAudioSwitch
{
    // Keep WinForms menu navigation, scrolling, and outside-click dismissal.
    // Draw the surface and rows ourselves so no system menu chrome is visible.
    internal sealed class PopupMenu : ContextMenuStrip, IMessageFilter
    {
        private const int MouseWheelMessage = 0x020A;
        private const int WheelDetent = 120;
        private const int RowHeight = 39;
        private const int ScrollButtonSpace = 20;
        private float scale = 1;
        private bool openingFromAnchor;
        private int wheelRemainder;
        private static readonly PopupRenderer SharedRenderer = new PopupRenderer();

        public PopupMenu(string accessibleName)
        {
            Font = Theme.Body;
            BackColor = Color.White;
            AccessibleName = accessibleName;
            ShowCheckMargin = ShowImageMargin = false;
            ShowItemToolTips = true;
            Renderer = SharedRenderer;
            AutoSize = false;
        }

        public MenuOption AddOption(string text, EventHandler onClick = null, string note = null)
        {
            var item = new MenuOption(text, note);
            if (onClick != null) item.Click += onClick;
            Items.Add(item);
            return item;
        }

        public void ShowFor(Control anchor, bool matchWidth = false, bool alignRight = false)
        {
            if (Visible) { Close(); return; }
            SetScale(anchor);
            int gap = Pixels(7);
            Rectangle card = anchor.RectangleToScreen(anchor.ClientRectangle);
            Rectangle area = Screen.FromRectangle(card).WorkingArea;
            int width = matchWidth ? card.Width : MeasureWidth();
            SizeRows(Math.Max(1, Math.Min(width, area.Width - gap * 2)));
            int belowSpace = area.Bottom - card.Bottom - gap, aboveSpace = card.Top - area.Top - gap;
            int height = PreferredPopupHeight();
            bool above = height > belowSpace && aboveSpace > belowSpace;
            Height = Math.Min(height, Math.Max(Pixels(RowHeight + ScrollButtonSpace), above ? aboveSpace : belowSpace));
            ToolStripDropDownDirection direction = alignRight
                ? (above ? ToolStripDropDownDirection.AboveLeft : ToolStripDropDownDirection.BelowLeft)
                : (above ? ToolStripDropDownDirection.AboveRight : ToolStripDropDownDirection.BelowRight);
            openingFromAnchor = true;
            try { Show(anchor, new Point(alignRight ? anchor.Width : 0, above ? -gap : anchor.Height + gap), direction); }
            finally { openingFromAnchor = false; }
        }

        protected override void OnOpening(CancelEventArgs e)
        {
            // Context and notification-area menus open through WinForms rather
            // than ShowFor, so size their rows before the window becomes visible.
            if (!openingFromAnchor)
            {
                SetScale(SourceControl ?? this);
                Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
                SizeRows(Math.Max(1, Math.Min(MeasureWidth(), area.Width - Pixels(14))));
                Height = Math.Min(PreferredPopupHeight(), area.Height);
            }
            base.OnOpening(e);
        }

        private void SetScale(Control control)
        {
            using (var graphics = control.CreateGraphics()) scale = graphics.DpiX / 96;
        }

        private int Pixels(float logical) { return (int)Math.Round(logical * scale); }

        private int PreferredPopupHeight()
        {
            int compactHeight = Pixels(RowHeight) * 7 + Pixels(ScrollButtonSpace);
            return Math.Min(GetPreferredSize(Size.Empty).Height, compactHeight);
        }

        private int MeasureWidth()
        {
            int width = Pixels(192);
            foreach (MenuOption option in Options())
            {
                var measured = TextRenderer.MeasureText(option.Text, Font, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                width = Math.Max(width, measured.Width + Pixels(48));
            }
            return Math.Min(width, Pixels(384));
        }

        private void SizeRows(int width)
        {
            SuspendLayout();
            foreach (MenuOption option in Options()) option.RowSize = new Size(Math.Max(1, width - 2), Pixels(RowHeight));
            Width = width;
            ResumeLayout(true);
            PerformLayout();
        }

        private IEnumerable<MenuOption> Options()
        {
            foreach (ToolStripItem item in Items)
                if (item is MenuOption) yield return (MenuOption)item;
        }

        protected override void OnOpened(EventArgs e)
        {
            wheelRemainder = 0;
            // WinForms keeps keyboard focus on the window behind a popup. Catch
            // wheel messages aimed at the popup even if that window receives them.
            Application.AddMessageFilter(this);
            base.OnOpened(e);
        }

        protected override void OnClosed(ToolStripDropDownClosedEventArgs e)
        {
            Application.RemoveMessageFilter(this);
            base.OnClosed(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Application.RemoveMessageFilter(this);
            base.Dispose(disposing);
        }

        bool IMessageFilter.PreFilterMessage(ref Message message)
        {
            if (!Visible || message.Msg != MouseWheelMessage) return false;
            long position = message.LParam.ToInt64();
            var point = new Point(unchecked((short)position), unchecked((short)(position >> 16)));
            if (!Bounds.Contains(point)) return false;

            int delta = unchecked((short)(message.WParam.ToInt64() >> 16));
            wheelRemainder += delta;
            int detents = wheelRemainder / WheelDetent;
            wheelRemainder %= WheelDetent;
            if (detents == 0) return true;
            int rows = SystemInformation.MouseWheelScrollLines;
            if (rows < 0) rows = Math.Max(1, DisplayRectangle.Height / Pixels(RowHeight));
            int steps = Math.Min(Items.Count, Math.Abs(detents) * Math.Min(rows, Items.Count));
            for (int i = 0; i < steps && ScrollOneRow(detents > 0); i++) { }
            return true;
        }

        private bool ScrollOneRow(bool up)
        {
            Rectangle viewport = DisplayRectangle;
            ToolStripItem edge = null;
            foreach (ToolStripItem item in Items)
            {
                if (!item.Available || !item.Enabled || !item.CanSelect) continue;
                if (item.Bounds.Top >= viewport.Top && item.Bounds.Bottom <= viewport.Bottom)
                {
                    edge = item;
                    if (up) break;
                }
            }
            if (edge == null) return false;

            ToolStripItem next = GetNextItem(edge, up ? ArrowDirection.Up : ArrowDirection.Down);
            if (next == null || (up ? next.Bounds.Top >= edge.Bounds.Top : next.Bounds.Top <= edge.Bounds.Top))
                return false;

            // Move from the edge so the standard menu navigation immediately
            // brings the next row on screen. Do not wrap at the ends.
            edge.Select();
            base.ProcessDialogKey(up ? Keys.Up : Keys.Down);
            return true;
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (Width <= 0 || Height <= 0) return;
            Region previous = Region;
            using (var path = Theme.Round(new RectangleF(0, 0, Width, Height), 13 * scale)) Region = new Region(path);
            if (previous != null) previous.Dispose();
        }

        private sealed class PopupRenderer : ToolStripRenderer
        {
            private static readonly Color SelectedFill = Color.FromArgb(232, 242, 255);
            private static readonly Color SelectedHover = Color.FromArgb(219, 235, 255);

            protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
            { e.Graphics.FillRectangle(Brushes.White, e.AffectedBounds); }

            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                float s = e.Graphics.DpiX / 96;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Theme.StrokeRound(e.Graphics, new RectangleF(.5f, .5f, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1), 13 * s, Theme.Line, s);
            }

            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                var option = e.Item as MenuOption;
                if (option == null) return;
                if (!option.Selected && !option.Checked) return;
                float s = e.Graphics.DpiX / 96;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Color fill = option.Checked ? (option.Selected ? SelectedHover : SelectedFill) : Theme.Canvas;
                Theme.FillRound(e.Graphics, new RectangleF(5 * s, 2 * s, option.Width - 10 * s, option.Height - 4 * s), 8 * s, fill);
            }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                var option = e.Item as MenuOption;
                if (option == null) { base.OnRenderItemText(e); return; }
                var g = e.Graphics;
                float s = g.DpiX / 96;
                int left = (int)(16 * s), right = option.Width - (int)((option.Checked ? 39 : 16) * s);
                Color ink = option.Checked ? Theme.Blue : option.Enabled && option.Note == null ? Theme.Ink : Theme.Muted;
                const TextFormatFlags flags = TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.PreserveGraphicsClipping;
                if (option.Note != null)
                {
                    int badgeWidth = (int)Math.Round(32 * s);
                    Theme.Text(g, option.Note, Theme.Small, Theme.Muted, new Rectangle(right - badgeWidth, 0, badgeWidth, option.Height), flags | TextFormatFlags.Right);
                    right -= badgeWidth + (int)(8 * s);
                }
                if (right > left)
                    Theme.Text(g, option.Text, Theme.Body, ink, new Rectangle(left, 0, right - left, option.Height), flags);
                if (option.Checked)
                {
                    float x = option.Width - 24 * s, y = option.Height / 2f;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var pen = new Pen(Theme.Blue, 1.7f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                        g.DrawLines(pen, new[] { new PointF(x - 5 * s, y), new PointF(x - 1 * s, y + 4 * s), new PointF(x + 6 * s, y - 4 * s) });
                }
            }
            protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e) { }
            protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
            {
                int inset = (int)(12 * e.Graphics.DpiX / 96);
                int y = e.Item.Height / 2;
                using (var pen = new Pen(Theme.Line)) e.Graphics.DrawLine(pen, inset, y, e.Item.Width - inset, y);
            }
        }
    }

    internal sealed class MenuOption : ToolStripMenuItem
    {
        public Size RowSize;
        public readonly string Note;

        public MenuOption(string text, string note) : base(text)
        {
            Note = note;
            if (note != null) AccessibleName = text + "，" + note;
            Margin = Padding.Empty;
        }

        public override Size GetPreferredSize(Size constrainingSize)
        { return RowSize.IsEmpty ? base.GetPreferredSize(constrainingSize) : RowSize; }
    }
}
