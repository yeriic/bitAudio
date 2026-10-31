using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace bitAudio.Controls
{
    internal sealed class NoiseGateBar : Control
    {
        private int minimumDb;
        private int maximumDb;
        private int valueDb;
        private float levelDb;
        private bool dragging;
        private bool mouseOver;

        public NoiseGateBar()
        {
            minimumDb = -80;
            maximumDb = 0;
            valueDb = -80;
            levelDb = -90f;

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint |
                ControlStyles.Selectable,
                true);

            Height = 42;
            TabStop = true;
            Cursor = Cursors.Hand;
            AccentColor = Color.FromArgb(90, 209, 200);
            WarningColor = Color.FromArgb(242, 185, 96);
            TrackColor = Color.FromArgb(65, 65, 80);
            ThumbColor = Color.FromArgb(242, 243, 248);
            DisabledColor = Color.FromArgb(95, 95, 108);
            BackColor = Color.FromArgb(31, 31, 40);
            AccessibleRole = AccessibleRole.Slider;
        }

        public event EventHandler ValueChanged;

        public int MinimumDb
        {
            get { return minimumDb; }
            set
            {
                minimumDb = value;
                if (maximumDb < minimumDb)
                {
                    maximumDb = minimumDb;
                }
                if (valueDb < minimumDb)
                {
                    valueDb = minimumDb;
                }
                Invalidate();
            }
        }

        public int MaximumDb
        {
            get { return maximumDb; }
            set
            {
                maximumDb = value;
                if (minimumDb > maximumDb)
                {
                    minimumDb = maximumDb;
                }
                if (valueDb > maximumDb)
                {
                    valueDb = maximumDb;
                }
                Invalidate();
            }
        }

        public int ValueDb
        {
            get { return valueDb; }
            set
            {
                int clamped = value;
                if (clamped < minimumDb)
                {
                    clamped = minimumDb;
                }
                if (clamped > maximumDb)
                {
                    clamped = maximumDb;
                }

                if (valueDb != clamped)
                {
                    valueDb = clamped;
                    Invalidate();
                    RaiseValueChanged();
                }
            }
        }

        public float LevelDb
        {
            get { return levelDb; }
            set
            {
                float clamped = value;
                if (clamped < -90f)
                {
                    clamped = -90f;
                }
                if (clamped > 0f)
                {
                    clamped = 0f;
                }

                if (Math.Abs(levelDb - clamped) > 0.1f)
                {
                    levelDb = clamped;
                    Invalidate();
                }
            }
        }

        public bool IsDragging
        {
            get { return dragging; }
        }

        public Color AccentColor { get; set; }
        public Color WarningColor { get; set; }
        public Color TrackColor { get; set; }
        public Color ThumbColor { get; set; }
        public Color DisabledColor { get; set; }

        private void RaiseValueChanged()
        {
            EventHandler handler = ValueChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(BackColor);

            int padding = 9;
            int trackWidth = Width - padding * 2;
            if (trackWidth < 2)
            {
                return;
            }

            int centerY = Height / 2;
            float valueRatio = maximumDb == minimumDb ? 0f : (float)(valueDb - minimumDb) / (maximumDb - minimumDb);
            float levelRatio = maximumDb == minimumDb ? 0f : (levelDb - minimumDb) / (maximumDb - minimumDb);
            if (levelRatio < 0f)
            {
                levelRatio = 0f;
            }
            else if (levelRatio > 1f)
            {
                levelRatio = 1f;
            }

            int thresholdX = padding + (int)Math.Round(valueRatio * trackWidth);
            int levelX = padding + (int)Math.Round(levelRatio * trackWidth);

            Color track = Enabled ? TrackColor : DisabledColor;
            Color meter = Enabled
                ? (levelDb >= valueDb ? AccentColor : WarningColor)
                : DisabledColor;
            Color thumb = Enabled ? ThumbColor : DisabledColor;

            using (Pen trackPen = new Pen(track, 8f))
            {
                trackPen.StartCap = LineCap.Round;
                trackPen.EndCap = LineCap.Round;
                graphics.DrawLine(trackPen, padding, centerY, Width - padding, centerY);
            }

            if (levelX > padding)
            {
                using (Pen meterPen = new Pen(meter, 8f))
                {
                    meterPen.StartCap = LineCap.Round;
                    meterPen.EndCap = LineCap.Round;
                    graphics.DrawLine(meterPen, padding, centerY, levelX, centerY);
                }
            }

            using (Pen thresholdPen = new Pen(thumb, 2f))
            {
                graphics.DrawLine(thresholdPen, thresholdX, 7, thresholdX, Height - 7);
            }

            int radius = mouseOver || Focused ? 8 : 7;
            using (SolidBrush thumbBrush = new SolidBrush(thumb))
            {
                graphics.FillEllipse(thumbBrush, thresholdX - radius, centerY - radius, radius * 2, radius * 2);
            }

            if (Focused && Enabled)
            {
                using (Pen focusPen = new Pen(AccentColor, 1.5f))
                {
                    graphics.DrawEllipse(focusPen, thresholdX - radius - 2, centerY - radius - 2, (radius + 2) * 2, (radius + 2) * 2);
                }
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (!Enabled || e.Button != MouseButtons.Left)
            {
                return;
            }

            Focus();
            dragging = true;
            Capture = true;
            SetValueFromPosition(e.X);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (dragging)
            {
                SetValueFromPosition(e.X);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (dragging)
            {
                dragging = false;
                Capture = false;
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            mouseOver = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            mouseOver = false;
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (!Enabled)
            {
                return;
            }

            Focus();
            ValueDb += e.Delta > 0 ? 1 : -1;
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Left:
                case Keys.Right:
                case Keys.Up:
                case Keys.Down:
                case Keys.Home:
                case Keys.End:
                case Keys.PageUp:
                case Keys.PageDown:
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (!Enabled)
            {
                return;
            }

            switch (e.KeyCode)
            {
                case Keys.Left:
                case Keys.Down:
                    ValueDb -= 1;
                    e.Handled = true;
                    break;
                case Keys.Right:
                case Keys.Up:
                    ValueDb += 1;
                    e.Handled = true;
                    break;
                case Keys.PageDown:
                    ValueDb -= 5;
                    e.Handled = true;
                    break;
                case Keys.PageUp:
                    ValueDb += 5;
                    e.Handled = true;
                    break;
                case Keys.Home:
                    ValueDb = minimumDb;
                    e.Handled = true;
                    break;
                case Keys.End:
                    ValueDb = maximumDb;
                    e.Handled = true;
                    break;
            }
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        private void SetValueFromPosition(int x)
        {
            int padding = 9;
            int trackWidth = Width - padding * 2;
            if (trackWidth <= 0)
            {
                return;
            }

            float ratio = (float)(x - padding) / trackWidth;
            if (ratio < 0f)
            {
                ratio = 0f;
            }
            else if (ratio > 1f)
            {
                ratio = 1f;
            }

            ValueDb = minimumDb + (int)Math.Round(ratio * (maximumDb - minimumDb));
        }
    }
}
