using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace SteamOfflineSwitch
{
    internal static class UiIcons
    {
        private static Icon _online;
        private static Icon _offline;

        public static Icon Get(bool offline)
        {
            if (offline)
            {
                if (_offline == null) _offline = Build(true);
                return _offline;
            }
            if (_online == null) _online = Build(false);
            return _online;
        }

        private static Icon Build(bool offline)
        {
            IntPtr h;
            using (Bitmap bmp = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    Color bg = offline ? Color.FromArgb(226, 128, 44) : Color.FromArgb(46, 110, 220);
                    using (SolidBrush b = new SolidBrush(bg)) g.FillEllipse(b, 1, 1, 30, 30);
                    using (Pen pen = new Pen(Color.White, 3f))
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;
                        g.DrawEllipse(pen, 9, 9, 14, 14);
                        if (offline) g.DrawLine(pen, 8, 8, 24, 24);
                    }
                }
                h = bmp.GetHicon();
            }
            return Icon.FromHandle(h);
        }
    }
}
