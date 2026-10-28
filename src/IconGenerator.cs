using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace NetworkSwitcher
{
    public static class IconGenerator
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);

        public static Icon CreateTrayIcon(AdapterCategory primaryActive, bool isConnected, bool anyEnabled)
        {
            int size = 32;
            using (Bitmap bmp = new Bitmap(size, size))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);

                Color strokeColor = anyEnabled ? Color.White : Color.FromArgb(160, 160, 160);

                if (!anyEnabled)
                {
                    // Draw muted network icon with a subtle slash
                    DrawEthernetShape(g, strokeColor, 2f);
                    using (Pen slashPen = new Pen(Color.FromArgb(232, 17, 35), 2.5f))
                    {
                        slashPen.StartCap = LineCap.Round;
                        slashPen.EndCap = LineCap.Round;
                        g.DrawLine(slashPen, 6, 26, 26, 6);
                    }
                }
                else if (primaryActive == AdapterCategory.WiFi)
                {
                    // Draw Wi-Fi Waves
                    DrawWiFiShape(g, strokeColor);
                }
                else if (primaryActive == AdapterCategory.UsbTethering)
                {
                    // Draw USB / Phone Shape
                    DrawUsbTetherShape(g, strokeColor);
                }
                else
                {
                    // Default to Ethernet
                    DrawEthernetShape(g, strokeColor, 2f);
                }

                // Draw Status Indicator Dot in bottom-right corner
                if (anyEnabled)
                {
                    Color dotColor = isConnected ? Color.FromArgb(16, 180, 50) : Color.FromArgb(240, 160, 20);
                    using (SolidBrush dotBrush = new SolidBrush(dotColor))
                    using (Pen ringPen = new Pen(Color.FromArgb(30, 30, 30), 1.5f))
                    {
                        Rectangle dotRect = new Rectangle(21, 21, 9, 9);
                        g.FillEllipse(dotBrush, dotRect);
                        g.DrawEllipse(ringPen, dotRect);
                    }
                }

                IntPtr hIcon = bmp.GetHicon();
                Icon icon = Icon.FromHandle(hIcon);
                Icon clone = (Icon)icon.Clone();
                DestroyIcon(hIcon);
                return clone;
            }
        }

        private static void DrawEthernetShape(Graphics g, Color color, float width)
        {
            using (Pen pen = new Pen(color, width))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;

                Rectangle screen = new Rectangle(5, 5, 22, 16);
                using (GraphicsPath path = new GraphicsPath())
                {
                    int r = 3;
                    path.AddArc(screen.X, screen.Y, r * 2, r * 2, 180, 90);
                    path.AddArc(screen.Right - r * 2, screen.Y, r * 2, r * 2, 270, 90);
                    path.AddArc(screen.Right - r * 2, screen.Bottom - r * 2, r * 2, r * 2, 0, 90);
                    path.AddArc(screen.X, screen.Bottom - r * 2, r * 2, r * 2, 90, 90);
                    path.CloseFigure();
                    g.DrawPath(pen, path);
                }

                g.DrawLine(pen, 16, 21, 16, 25);
                g.DrawLine(pen, 11, 25, 21, 25);
                g.DrawLine(pen, 10, 13, 22, 13);
            }
        }

        private static void DrawWiFiShape(Graphics g, Color color)
        {
            using (Pen pen = new Pen(color, 2.2f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;

                g.DrawArc(pen, 4, 6, 24, 24, 225, 90);
                g.DrawArc(pen, 8, 11, 16, 16, 225, 90);
                g.DrawArc(pen, 12, 16, 8, 8, 225, 90);

                using (SolidBrush brush = new SolidBrush(color))
                {
                    g.FillEllipse(brush, 14.5f, 22.5f, 3.5f, 3.5f);
                }
            }
        }

        private static void DrawUsbTetherShape(Graphics g, Color color)
        {
            using (Pen pen = new Pen(color, 2f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;

                Rectangle body = new Rectangle(8, 4, 16, 24);
                using (GraphicsPath path = new GraphicsPath())
                {
                    int r = 3;
                    path.AddArc(body.X, body.Y, r * 2, r * 2, 180, 90);
                    path.AddArc(body.Right - r * 2, body.Y, r * 2, r * 2, 270, 90);
                    path.AddArc(body.Right - r * 2, body.Bottom - r * 2, r * 2, r * 2, 0, 90);
                    path.AddArc(body.X, body.Bottom - r * 2, r * 2, r * 2, 90, 90);
                    path.CloseFigure();
                    g.DrawPath(pen, path);
                }

                g.DrawLine(pen, 11, 8, 21, 8);
                using (SolidBrush brush = new SolidBrush(color))
                {
                    g.FillEllipse(brush, 14.5f, 23.5f, 3f, 3f);
                }
            }
        }
    }
}
