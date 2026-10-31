using System;
using NAudio.Wave;

namespace bitAudio
{
    internal sealed class GainLimiterSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider source;
        private volatile float gain;
        private volatile bool limiterEnabled;

        public GainLimiterSampleProvider(ISampleProvider source, float initialGain, bool limiter)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            this.source = source;
            gain = initialGain < 0f ? 0f : initialGain;
            limiterEnabled = limiter;
        }

        public WaveFormat WaveFormat
        {
            get { return source.WaveFormat; }
        }

        public float Gain
        {
            get { return gain; }
            set { gain = value < 0f ? 0f : value; }
        }

        public bool LimiterEnabled
        {
            get { return limiterEnabled; }
            set { limiterEnabled = value; }
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int read = source.Read(buffer, offset, count);
            if (read <= 0)
            {
                return read;
            }

            float currentGain = gain;
            bool useLimiter = limiterEnabled;
            const float knee = 0.92f;
            const float range = 1f - knee;

            for (int i = offset; i < offset + read; i++)
            {
                float sample = buffer[i] * currentGain;

                if (useLimiter)
                {
                    if (sample > knee)
                    {
                        sample = knee + range * (float)Math.Tanh((sample - knee) / range);
                    }
                    else if (sample < -knee)
                    {
                        sample = -knee - range * (float)Math.Tanh((-sample - knee) / range);
                    }
                }
                else
                {
                    if (sample > 1f)
                    {
                        sample = 1f;
                    }
                    else if (sample < -1f)
                    {
                        sample = -1f;
                    }
                }

                buffer[i] = sample;
            }

            return read;
        }
    }
}
