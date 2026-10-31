using System;
using NAudio.Wave;

namespace bitAudio
{
    internal sealed class EqualizerSampleProvider : ISampleProvider
    {
        public static readonly float[] Frequencies = { 31f, 62f, 125f, 250f, 500f, 1000f, 2000f, 4000f, 8000f, 16000f };
        public const float MinimumGainDb = -12f;
        public const float MaximumGainDb = 12f;
        public const int BandCount = 10;

        private const double FilterQ = 1.4;

        private sealed class Biquad
        {
            public double B0;
            public double B1;
            public double B2;
            public double A1;
            public double A2;
        }

        private readonly ISampleProvider source;
        private readonly int channels;
        private readonly object settingsLock = new object();
        private readonly float[] pendingGains;
        private readonly float[] activeGains;
        private readonly Biquad[] filters;
        private readonly double[,] z1;
        private readonly double[,] z2;
        private bool dirty = true;
        private bool bypass = true;

        public EqualizerSampleProvider(ISampleProvider source, float[] initialGains)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            this.source = source;
            channels = source.WaveFormat.Channels;
            pendingGains = new float[BandCount];
            activeGains = new float[BandCount];
            filters = new Biquad[BandCount];
            z1 = new double[BandCount, channels];
            z2 = new double[BandCount, channels];

            for (int i = 0; i < BandCount; i++)
            {
                filters[i] = new Biquad();
            }

            if (initialGains != null)
            {
                int count = Math.Min(initialGains.Length, BandCount);
                for (int i = 0; i < count; i++)
                {
                    pendingGains[i] = ClampGain(initialGains[i]);
                }
            }
        }

        public WaveFormat WaveFormat
        {
            get { return source.WaveFormat; }
        }

        public void SetGains(float[] gains)
        {
            if (gains == null)
            {
                return;
            }

            lock (settingsLock)
            {
                int count = Math.Min(gains.Length, BandCount);
                for (int i = 0; i < count; i++)
                {
                    pendingGains[i] = ClampGain(gains[i]);
                }
                dirty = true;
            }
        }

        public int Read(float[] buffer, int offset, int count)
        {
            bool process;

            lock (settingsLock)
            {
                if (dirty)
                {
                    Array.Copy(pendingGains, activeGains, BandCount);
                    RecomputeFilters();
                    bypass = IsFlat(activeGains);
                    dirty = false;
                }
                process = !bypass;
            }

            int read = source.Read(buffer, offset, count);
            if (!process || read <= 0 || channels <= 0)
            {
                return read;
            }

            int frames = read / channels;
            for (int frame = 0; frame < frames; frame++)
            {
                int baseIndex = offset + frame * channels;
                for (int band = 0; band < BandCount; band++)
                {
                    Biquad filter = filters[band];
                    for (int channel = 0; channel < channels; channel++)
                    {
                        int index = baseIndex + channel;
                        double x = buffer[index];
                        double y = filter.B0 * x + z1[band, channel];
                        z1[band, channel] = filter.B1 * x - filter.A1 * y + z2[band, channel];
                        z2[band, channel] = filter.B2 * x - filter.A2 * y;
                        buffer[index] = (float)y;
                    }
                }
            }

            return read;
        }

        private void RecomputeFilters()
        {
            double sampleRate = source.WaveFormat.SampleRate;

            for (int band = 0; band < BandCount; band++)
            {
                Biquad filter = filters[band];
                double gainDb = activeGains[band];
                double frequency = Frequencies[band];

                if (Math.Abs(gainDb) < 0.001 || frequency >= sampleRate * 0.49)
                {
                    filter.B0 = 1.0;
                    filter.B1 = 0.0;
                    filter.B2 = 0.0;
                    filter.A1 = 0.0;
                    filter.A2 = 0.0;
                    continue;
                }

                double amplitude = Math.Pow(10.0, gainDb / 40.0);
                double omega = 2.0 * Math.PI * frequency / sampleRate;
                double sine = Math.Sin(omega);
                double cosine = Math.Cos(omega);
                double alpha = sine / (2.0 * FilterQ);
                double denominator = 1.0 + alpha / amplitude;

                filter.B0 = (1.0 + alpha * amplitude) / denominator;
                filter.B1 = (-2.0 * cosine) / denominator;
                filter.B2 = (1.0 - alpha * amplitude) / denominator;
                filter.A1 = (-2.0 * cosine) / denominator;
                filter.A2 = (1.0 - alpha / amplitude) / denominator;
            }
        }

        private static bool IsFlat(float[] gains)
        {
            for (int i = 0; i < gains.Length; i++)
            {
                if (Math.Abs(gains[i]) >= 0.001f)
                {
                    return false;
                }
            }
            return true;
        }

        private static float ClampGain(float gain)
        {
            if (gain < MinimumGainDb)
            {
                return MinimumGainDb;
            }
            if (gain > MaximumGainDb)
            {
                return MaximumGainDb;
            }
            return gain;
        }
    }
}
