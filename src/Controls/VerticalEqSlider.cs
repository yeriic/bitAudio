using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace bitAudio.Controls
{
    internal sealed class VerticalEqSlider : Control
    {
        private int minimum;
        private int maximum;
        private int value;
        private int smallChange;
        private int largeChange;
        private bool dragging;
        private bool mouseOver;

        public VerticalEqSlider()
        {
            minimum = -120;
            maximum = 120;
            value = 0;
            smallChange = 5;
            largeChange = 20;

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint |
                ControlStyles.Selectable,
                true);

            Width = 36;
            Height = 88;
            TabStop = true;
            Cursor = Cursors.Hand;
            AccentColor = Color.FromArgb(90, 209, 200);
            CutColor = Color.FromArgb(242, 185, 96);
            TrackColor = Color.FromArgb(65, 65, 80);
            ThumbColor = Color.FromArgb(242, 243, 248);
            DisabledColor = Color.FromArgb(95, 95, 108);
            BackColor = Color.FromArgb(35, 35, 46);
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
        public Color CutColor { get; set; }
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

            int padding = 10;
            int trackHeight = Height - padding * 2;
            if (trackHeight < 2)
            {
                return;
            }

            float ratio = maximum == minimum ? 0.5f : (float)(value - minimum) / (maximum - minimum);
            int centerX = Width / 2;
            int thumbY = Height - padding - (int)Math.Round(ratio * trackHeight);
            int zeroRatioY = Height - padding - (int)Math.Round(((float)(0 - minimum) / (maximum - minimum)) * trackHeight);

            Color track = Enabled ? TrackColor : DisabledColor;
            Color accent = Enabled ? (value >= 0 ? AccentColor : CutColor) : DisabledColor;
            Color thumb = Enabled ? ThumbColor : DisabledColor;

            using (Pen trackPen = new Pen(track, 6f))
            {
                trackPen.StartCap = LineCap.Round;
                trackPen.EndCap = LineCap.Round;
                graphics.DrawLine(trackPen, centerX, padding, centerX, Height - padding);
            }

            using (Pen centerPen = new Pen(Color.FromArgb(95, 95, 112), 1f))
            {
                graphics.DrawLine(centerPen, centerX - 9, zeroRatioY, centerX + 9, zeroRatioY);
            }

            int fillTop = Math.Min(thumbY, zeroRatioY);
            int fillHeight = Math.Abs(thumbY - zeroRatioY);
            if (fillHeight > 1)
            {
                using (Pen fillPen = new Pen(accent, 6f))
                {
                    fillPen.StartCap = LineCap.Round;
                    fillPen.EndCap = LineCap.Round;
                    graphics.DrawLine(fillPen, centerX, fillTop, centerX, fillTop + fillHeight);
                }
            }

            int radius = mouseOver || Focused ? 8 : 7;
            using (SolidBrush thumbBrush = new SolidBrush(thumb))
            {
                graphics.FillEllipse(thumbBrush, centerX - radius, thumbY - radius, radius * 2, radius * 2);
            }

            if (Focused && Enabled)
            {
                using (Pen focusPen = new Pen(accent, 1.5f))
                {
                    graphics.DrawEllipse(focusPen, centerX - radius - 2, thumbY - radius - 2, (radius + 2) * 2, (radius + 2) * 2);
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
            SetValueFromPosition(e.Y);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (dragging)
            {
                SetValueFromPosition(e.Y);
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
                Value += e.Delta > 0 ? smallChange : -smallChange;
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (Enabled && e.Button == MouseButtons.Left)
            {
                Value = 0;
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
                    Value = 0;
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

        private void SetValueFromPosition(int y)
        {
            int padding = 10;
            int trackHeight = Height - padding * 2;
            if (trackHeight <= 0)
            {
                return;
            }

            float ratio = (float)(Height - padding - y) / trackHeight;
            if (ratio < 0f)
            {
                ratio = 0f;
            }
            else if (ratio > 1f)
            {
                ratio = 1f;
            }

            int newValue = minimum + (int)Math.Round(ratio * (maximum - minimum));
            if (smallChange > 1)
            {
                newValue = minimum + (int)Math.Round((double)(newValue - minimum) / smallChange) * smallChange;
            }
            Value = newValue;
        }
    }
}
