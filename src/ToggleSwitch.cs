using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SteamOfflineSwitch
{
    // 自定义拨动开关（纯绘制）：On=true 表示“联网/在线”。
    internal sealed class ToggleSwitch : Control
    {
        private bool _on;

        public bool On
        {
            get { return _on; }
            set { if (_on != value) { _on = value; Invalidate(); } }
        }

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint
                | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw
                | ControlStyles.SupportsTransparentBackColor, true);
            Size = new Size(96, 46);
            Cursor = Cursors.Hand;
            BackColor = Color.Transparent;
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Invalidate(); }

        private static GraphicsPath Rounded(Rectangle r, int radius)
        {
            int d = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Color.White);

            Color track;
            if (!Enabled) track = Color.FromArgb(205, 205, 205);
            else if (_on) track = Color.FromArgb(45, 150, 90);
            else track = Color.FromArgb(150, 158, 168);

            Rectangle body = new Rectangle(1, 3, Width - 3, Height - 7);
            int rad = body.Height / 2;
            using (GraphicsPath path = Rounded(body, rad))
            {
                using (SolidBrush br = new SolidBrush(track)) g.FillPath(br, path);
                if (Enabled && IsMouseOver())
                {
                    using (Pen pen = new Pen(Color.FromArgb(60, 0, 0, 0), 1)) g.DrawPath(pen, path);
                }
            }

            int d = body.Height - 6;
            int kx = _on ? (body.Right - d - 3) : (body.X + 3);
            int ky = body.Y + 3;
            using (SolidBrush sh = new SolidBrush(Color.FromArgb(40, 0, 0, 0)))
                g.FillEllipse(sh, kx + 1, ky + 2, d, d);
            using (SolidBrush kb = new SolidBrush(Color.White))
                g.FillEllipse(kb, kx, ky, d, d);
        }

        private bool IsMouseOver()
        {
            return ClientRectangle.Contains(PointToClient(Cursor.Position));
        }
    }
}
