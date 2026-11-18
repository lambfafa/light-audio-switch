using System;
using System.Drawing;
using System.Windows.Forms;

namespace LightAudioSwitch
{
    internal sealed class HelpPage : Panel
    {
        private static readonly Color AnswerInk = Color.FromArgb(85, 85, 91);
        private readonly float scale;
        private readonly QuietButton back = new QuietButton { Text = "‹  返回设置", AccessibleName = "返回设置" };
        private readonly Panel scroll = new Panel { AutoScroll = true, TabStop = false };
        public event EventHandler BackRequested;

        public HelpPage(float scale)
        {
            this.scale = scale;
            Dock = DockStyle.Fill;
            BackColor = Theme.Canvas;
            AccessibleName = "使用帮助";

            back.SetBounds(D(24), D(20), D(94), D(33));
            back.Click += delegate { if (BackRequested != null) BackRequested(this, EventArgs.Empty); };
            Controls.Add(back);
            var heading = TextLabel("使用帮助", Theme.Display, Theme.Ink);
            heading.SetBounds(D(32), D(60), D(680), D(46));
            Controls.Add(heading);

            scroll.SetBounds(0, D(121), D(744), 0);
            Controls.Add(scroll);
            int y = 0;
            var setup = Section("首次设置", ref y);
            int row = D(11);
            row = Step(setup, row, "1", "在「游戏耳机」和「日常音箱」卡片中，分别选好耳机和音箱。");
            row = Step(setup, row, "2", "《三角洲行动》已预置。其他游戏点「添加」，选择运行中的游戏或游戏 EXE。");
            row = Step(setup, row, "3", "点「保存」，再点窗口顶部的「自动」。");
            Finish(setup, row + D(7), ref y);

            var modes = Section("切换规则", ref y);
            row = D(13);
            row = Definition(modes, row, "自动", "只要列表里的游戏还在运行，就会使用耳机。全部退出后，默认等 3 秒切回音箱。仅打开 WeGame 或 Steam 不会触发切换。");
            row = Definition(modes, row + D(9), "耳机 / 音箱", "固定使用对应设备。点「自动」恢复按游戏切换。");
            Finish(modes, row + D(14), ref y);

            var questions = Section("常见问题", ref y);
            row = D(13);
            row = Answer(questions, row, "音箱显示离线？", "先在 Windows 里确认音箱已连接，再点「刷新设备」。蓝牙重新配对后，请重新选择音箱并保存。");
            row = Answer(questions, row + D(11), "切换了设备，应用的声音没变？",
                "把应用里的输出设为「默认」，也可以在 Windows 音量混合器里修改。\n如果游戏在启动时固定了输出，先选「耳机」再重启游戏。");
            row = Answer(questions, row + D(11), "关掉窗口后还会切换吗？", "会。程序留在系统托盘。双击托盘图标可打开设置，在托盘菜单点「退出」可停止切换。");
            Finish(questions, row + D(14), ref y);
            scroll.AutoScrollMinSize = new Size(0, y);
        }

        public void FocusBack() { back.Focus(); }

        public bool ScrollByKey(Keys key)
        {
            int top = -scroll.AutoScrollPosition.Y;
            switch (key)
            {
                case Keys.Up: top -= D(34); break;
                case Keys.Down: top += D(34); break;
                case Keys.PageUp: top -= scroll.Height; break;
                case Keys.PageDown: top += scroll.Height; break;
                case Keys.Home: top = 0; break;
                case Keys.End: top = scroll.AutoScrollMinSize.Height; break;
                default: return false;
            }
            scroll.AutoScrollPosition = new Point(0, Math.Max(0, top));
            return true;
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (scroll != null) scroll.Height = Math.Max(0, ClientSize.Height - scroll.Top - D(12));
        }

        private int D(float value) { return (int)Math.Round(value * scale); }

        private Surface Section(string name, ref int y)
        {
            var label = TextLabel(name, Theme.Medium, Theme.Ink);
            label.SetBounds(D(34), y, D(678), D(25));
            scroll.Controls.Add(label);
            y += D(31);
            var surface = new Surface { AccessibleName = name };
            surface.SetBounds(D(32), y, D(680), 1);
            scroll.Controls.Add(surface);
            return surface;
        }

        private void Finish(Surface surface, int row, ref int y)
        {
            surface.Height = row;
            y += row + D(15);
        }

        private int Step(Surface surface, int y, string number, string text)
        {
            var index = TextLabel(number, Theme.Small, Theme.Blue);
            index.TextAlign = ContentAlignment.MiddleCenter;
            index.SetBounds(D(14), y, D(24), D(20));
            surface.Controls.Add(index);
            return AddText(surface, text, Theme.Body, Theme.Ink, 48, y, 614) + D(5);
        }

        private int Definition(Surface surface, int y, string name, string text)
        {
            AddText(surface, name, Theme.Body, Theme.Blue, 20, y, 92);
            return AddText(surface, text, Theme.Body, Theme.Ink, 114, y, 546);
        }

        private int Answer(Surface surface, int y, string question, string text)
        {
            int body = AddText(surface, question, Theme.Medium, Theme.Ink, 20, y, 640) + D(3);
            return AddText(surface, text, Theme.Body, AnswerInk, 20, body, 640);
        }

        private int AddText(Surface surface, string text, Font font, Color color, int x, int y, int width)
        {
            var label = TextLabel(text, font, color);
            int w = D(width);
            int h = TextRenderer.MeasureText(text, font, new Size(w, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + D(4);
            label.SetBounds(D(x), y, w, h);
            surface.Controls.Add(label);
            return y + h;
        }

        private static Label TextLabel(string text, Font font, Color color)
        {
            return new Label { Text = text, Font = font, ForeColor = color, BackColor = Color.Transparent, UseMnemonic = false };
        }
    }
}
