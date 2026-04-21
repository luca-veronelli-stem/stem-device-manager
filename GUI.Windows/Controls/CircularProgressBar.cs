using System.ComponentModel;

namespace GUI.Windows
{
    public class CircularProgressBar : Control
    {
        private int _value = 0;
        private int _maximum = 100;
        private int _lineWidth = 10;

        [Category("Behavior")]
        [Description("Current progress value.")]
        [DefaultValue(0)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public int Value
        {
            get => _value;
            set
            {
                if (value < 0) _value = 0;
                else if (value > Maximum) _value = Maximum;
                else _value = value;
                Invalidate();
            }
        }

        [Category("Behavior")]
        [Description("Maximum progress value.")]
        [DefaultValue(100)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public int Maximum
        {
            get => _maximum;
            set
            {
                if (value <= 0) throw new ArgumentOutOfRangeException("Maximum must be > 0");
                _maximum = value;
                if (_value > _maximum) _value = _maximum;
                Invalidate();
            }
        }

        [Category("Appearance")]
        [Description("Width of the progress arc.")]
        [DefaultValue(10)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public int LineWidth
        {
            get => _lineWidth;
            set { _lineWidth = Math.Max(1, value); Invalidate(); }
        }

        public CircularProgressBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint, true);
            Font = new System.Drawing.Font("Segoe UI", 14, FontStyle.Bold);
            ForeColor = Color.SeaGreen;
            Size = new Size(150, 150);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Calculate bounds for arc
            Rectangle rect = new Rectangle(
                LineWidth / 2,
                LineWidth / 2,
                Width - LineWidth,
                Height - LineWidth);

            // Draw base circle (background)
            using (Pen backPen = new Pen(Color.LightGray, LineWidth))
            {
                e.Graphics.DrawArc(backPen, rect, 0, 360);
            }

            // Draw progress arc
            float sweepAngle = 360f * _value / _maximum;
            using (Pen progressPen = new Pen(ForeColor, LineWidth))
            {
                e.Graphics.DrawArc(progressPen, rect, -90, sweepAngle);
            }

            // Draw percentage text
            string percentText = $"{(int)((float)_value / _maximum * 100)}%";
            SizeF textSize = e.Graphics.MeasureString(percentText, Font);
            PointF textPos = new PointF(
                (Width - textSize.Width) / 2,
                (Height - textSize.Height) / 2);

            using (Brush textBrush = new SolidBrush(ForeColor))
            {
                e.Graphics.DrawString(percentText, Font, textBrush, textPos);
            }
        }
    }
}

