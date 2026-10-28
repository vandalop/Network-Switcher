using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NetworkSwitcher
{
    public class ToggleSwitch : Control
    {
        private bool _checked = false;
        private Color _onColor = Color.FromArgb(0, 120, 215);
        private Color _offColor = Color.FromArgb(65, 65, 65);
        private Color _knobColor = Color.White;
        private bool _isHovered = false;

        public event EventHandler CheckedChanged;

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked != value)
                {
                    _checked = value;
                    Invalidate();
                    if (CheckedChanged != null)
                    {
                        CheckedChanged(this, EventArgs.Empty);
                    }
                }
            }
        }

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.UserPaint | 
                     ControlStyles.AllPaintingInWmPaint | 
                     ControlStyles.OptimizedDoubleBuffer | 
                     ControlStyles.ResizeRedraw | 
                     ControlStyles.Selectable |
                     ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(46, 24);
            Cursor = Cursors.Hand;
            BackColor = Color.Transparent;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            Invalidate();
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Checked = !Checked;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                Checked = !Checked;
                e.Handled = true;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int w = Width - 1;
            int h = Height - 1;

            // Track Path
            using (GraphicsPath trackPath = new GraphicsPath())
            {
                trackPath.AddArc(0, 0, h, h, 90, 180);
                trackPath.AddArc(w - h, 0, h, h, 270, 180);
                trackPath.CloseFigure();

                Color currentColor = _checked ? _onColor : _offColor;
                if (_isHovered)
                {
                    currentColor = _checked ? Color.FromArgb(0, 140, 240) : Color.FromArgb(80, 80, 80);
                }

                using (SolidBrush brush = new SolidBrush(currentColor))
                {
                    g.FillPath(brush, trackPath);
                }

                using (Pen borderPen = new Pen(_checked ? currentColor : Color.FromArgb(90, 90, 90), 1f))
                {
                    g.DrawPath(borderPen, trackPath);
                }
            }

            // Knob
            int knobDiameter = h - 6;
            int knobX = _checked ? (w - knobDiameter - 3) : 3;
            int knobY = 3;

            using (SolidBrush knobBrush = new SolidBrush(_knobColor))
            {
                g.FillEllipse(knobBrush, knobX, knobY, knobDiameter, knobDiameter);
            }
        }
    }
}
