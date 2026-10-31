using System;
using NAudio.Wave;

namespace bitAudio
{
    internal sealed class NoiseGateSampleProvider : ISampleProvider
    {
        public const int MinimumThresholdDb = -80;
        public const int MaximumThresholdDb = 0;

        private readonly ISampleProvider source;
        private readonly int channels;
        private readonly float attackCoefficient;
        private readonly float releaseCoefficient;
        private readonly float envelopeReleaseCoefficient;

        private volatile int thresholdDb;
        private volatile float levelDb = -90f;
        private float envelope;
        private float gateGain = 1f;
        private bool gateOpen = true;

        public NoiseGateSampleProvider(ISampleProvider source, int initialThresholdDb)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            this.source = source;
            channels = source.WaveFormat.Channels;

            double sampleRate = source.WaveFormat.SampleRate;
            attackCoefficient = AttackCoefficient(0.005, sampleRate);
            releaseCoefficient = AttackCoefficient(0.150, sampleRate);
            envelopeReleaseCoefficient = AttackCoefficient(0.250, sampleRate);

            SetThreshold(initialThresholdDb);
        }

        public WaveFormat WaveFormat
        {
            get { return source.WaveFormat; }
        }

        public int ThresholdDb
        {
            get { return thresholdDb; }
        }

        public float LevelDb
        {
            get { return levelDb; }
        }

        public void SetThreshold(int db)
        {
            if (db < MinimumThresholdDb)
            {
                db = MinimumThresholdDb;
            }
            else if (db > MaximumThresholdDb)
            {
                db = MaximumThresholdDb;
            }

            thresholdDb = db;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int read = source.Read(buffer, offset, count);
            if (read <= 0 || channels <= 0)
            {
                return read;
            }

            int frames = read / channels;
            double threshold = Math.Pow(10.0, thresholdDb / 20.0);
            double closeThreshold = threshold * 0.5;

            float currentEnvelope = envelope;
            float currentGain = gateGain;
            bool currentOpen = gateOpen;

            for (int frame = 0; frame < frames; frame++)
            {
                int baseIndex = offset + frame * channels;
                float peak = 0f;
                for (int channel = 0; channel < channels; channel++)
                {
                    float sample = buffer[baseIndex + channel];
                    float absolute = sample < 0f ? -sample : sample;
                    if (absolute > peak)
                    {
                        peak = absolute;
                    }
                }

                if (peak > currentEnvelope)
                {
                    currentEnvelope = peak;
                }
                else
                {
                    currentEnvelope += (peak - currentEnvelope) * envelopeReleaseCoefficient;
                }

                if (currentOpen)
                {
                    if (currentEnvelope < closeThreshold)
                    {
                        currentOpen = false;
                    }
                }
                else
                {
                    if (currentEnvelope >= threshold)
                    {
                        currentOpen = true;
                    }
                }

                float target = currentOpen ? 1f : 0f;
                float coefficient = target > currentGain ? attackCoefficient : releaseCoefficient;
                currentGain += (target - currentGain) * coefficient;

                for (int channel = 0; channel < channels; channel++)
                {
                    buffer[baseIndex + channel] *= currentGain;
                }
            }

            envelope = currentEnvelope;
            gateGain = currentGain;
            gateOpen = currentOpen;

            double display = 20.0 * Math.Log10(Math.Max(currentEnvelope, 0.000001));
            if (display < -90.0)
            {
                display = -90.0;
            }
            else if (display > 0.0)
            {
                display = 0.0;
            }
            levelDb = (float)display;

            return read;
        }

        private static float AttackCoefficient(double milliseconds, double sampleRate)
        {
            double samples = Math.Max(1.0, sampleRate * milliseconds / 1000.0);
            return (float)(1.0 - Math.Exp(-1.0 / samples));
        }
    }
}
