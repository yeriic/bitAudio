using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace bitAudio.Controls
{
    internal sealed class SlimSlider : Control
    {
        private int minimum;
        private int maximum;
        private int value;
        private int smallChange;
        private int largeChange;
        private bool dragging;
        private bool mouseOver;

        public SlimSlider()
        {
            minimum = 0;
            maximum = 100;
            value = 0;
            smallChange = 1;
            largeChange = 10;

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint |
                ControlStyles.Selectable,
                true);

            Height = 32;
            TabStop = true;
            Cursor = Cursors.Hand;
            AccentColor = Color.FromArgb(90, 209, 200);
            TrackColor = Color.FromArgb(62, 62, 76);
            ThumbColor = Color.FromArgb(240, 241, 246);
            DisabledColor = Color.FromArgb(95, 95, 108);
            BackColor = Color.FromArgb(32, 32, 41);
            AccessibleRole = AccessibleRole.Slider;
        }

        public event EventHandler ValueChanged;

        public int Minimum
        {
            get { return minimum; }
            set
            {
                minimum = value;
                if (maximum < minimum)
                {
                    maximum = minimum;
                }
                if (this.value < minimum)
                {
                    this.value = minimum;
                }
                Invalidate();
            }
        }

        public int Maximum
        {
            get { return maximum; }
            set
            {
                maximum = value;
                if (minimum > maximum)
                {
                    minimum = maximum;
                }
                if (this.value > maximum)
                {
                    this.value = maximum;
                }
                Invalidate();
            }
        }

        public int Value
        {
            get { return value; }
            set
            {
                int clamped = value;
                if (clamped < minimum)
                {
                    clamped = minimum;
                }
                if (clamped > maximum)
                {
                    clamped = maximum;
                }

                if (this.value != clamped)
                {
                    this.value = clamped;
                    Invalidate();
                    RaiseValueChanged();
                }
            }
        }

        public int SmallChange
        {
            get { return smallChange; }
            set { smallChange = value < 1 ? 1 : value; }
        }

        public int LargeChange
        {
            get { return largeChange; }
            set { largeChange = value < 1 ? 1 : value; }
        }

        public bool IsDragging
        {
            get { return dragging; }
        }

        public Color AccentColor { get; set; }
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

            float ratio = maximum == minimum ? 0f : (float)(value - minimum) / (maximum - minimum);
            int centerY = Height / 2;
            int thumbX = padding + (int)Math.Round(ratio * trackWidth);

            Color track = Enabled ? TrackColor : DisabledColor;
            Color accent = Enabled ? AccentColor : DisabledColor;
            Color thumb = Enabled ? ThumbColor : DisabledColor;

            using (Pen trackPen = new Pen(track, 6f))
            {
                trackPen.StartCap = LineCap.Round;
                trackPen.EndCap = LineCap.Round;
                graphics.DrawLine(trackPen, padding, centerY, Width - padding, centerY);
            }

            if (thumbX > padding)
            {
                using (Pen fillPen = new Pen(accent, 6f))
                {
                    fillPen.StartCap = LineCap.Round;
                    fillPen.EndCap = LineCap.Round;
                    graphics.DrawLine(fillPen, padding, centerY, thumbX, centerY);
                }
            }

            int radius = mouseOver || Focused ? 8 : 7;
            using (SolidBrush thumbBrush = new SolidBrush(thumb))
            {
                graphics.FillEllipse(thumbBrush, thumbX - radius, centerY - radius, radius * 2, radius * 2);
            }

            if (Focused && Enabled)
            {
                using (Pen focusPen = new Pen(accent, 1.5f))
                {
                    graphics.DrawEllipse(focusPen, thumbX - radius - 2, centerY - radius - 2, (radius + 2) * 2, (radius + 2) * 2);
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
            if (Enabled)
            {
                Focus();
                int delta = e.Delta > 0 ? smallChange : -smallChange;
                Value += delta;
            }
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
                    Value -= smallChange;
                    e.Handled = true;
                    break;
                case Keys.Right:
                case Keys.Up:
                    Value += smallChange;
                    e.Handled = true;
                    break;
                case Keys.PageDown:
                    Value -= largeChange;
                    e.Handled = true;
                    break;
                case Keys.PageUp:
                    Value += largeChange;
                    e.Handled = true;
                    break;
                case Keys.Home:
                    Value = minimum;
                    e.Handled = true;
                    break;
                case Keys.End:
                    Value = maximum;
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

            int newValue = minimum + (int)Math.Round(ratio * (maximum - minimum));
            int rounded = newValue;
            if (smallChange > 1)
            {
                rounded = minimum + (int)Math.Round((double)(newValue - minimum) / smallChange) * smallChange;
            }
            Value = rounded;
        }
    }
}
