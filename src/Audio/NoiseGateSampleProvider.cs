using System;
using NAudio.Wave;

namespace bitAudio
{
    internal sealed class NoiseGateSampleProvider : ISampleProvider
    {
        public const int MinimumThresholdDb = -80;
        public const int MaximumThresholdDb = 0;

        private const double KneeDb = 6.0;
        private const double ExpansionRatio = 3.0;
        private const double FloorGainDb = -36.0;

        private readonly ISampleProvider source;
        private readonly int channels;
        private readonly float attackCoefficient;
        private readonly float releaseCoefficient;
        private readonly float envelopeReleaseCoefficient;

        private volatile int thresholdDb;
        private volatile float levelDb = -90f;
        private float envelope;
        private float smoothGain = 1f;

        public NoiseGateSampleProvider(ISampleProvider source, int initialThresholdDb)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            this.source = source;
            channels = source.WaveFormat.Channels;

            double sampleRate = source.WaveFormat.SampleRate;
            attackCoefficient = TimeCoefficient(0.010, sampleRate);
            releaseCoefficient = TimeCoefficient(0.220, sampleRate);
            envelopeReleaseCoefficient = TimeCoefficient(0.100, sampleRate);

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
            int threshold = thresholdDb;
            bool active = threshold > MinimumThresholdDb;

            double thresholdAt = threshold;
            double lowerKnee = thresholdAt - KneeDb / 2.0;
            double upperKnee = thresholdAt + KneeDb / 2.0;
            double lowerKneeGainDb = -(KneeDb / 2.0) * (ExpansionRatio - 1.0);

            float currentEnvelope = envelope;
            float currentGain = smoothGain;

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

                float targetGain = 1f;
                if (active)
                {
                    double level = 20.0 * Math.Log10(Math.Max(currentEnvelope, 0.000001));
                    double gainDb;

                    if (level >= upperKnee)
                    {
                        gainDb = 0.0;
                    }
                    else if (level <= lowerKnee)
                    {
                        gainDb = Math.Max(FloorGainDb, (level - thresholdAt) * (ExpansionRatio - 1.0));
                    }
                    else
                    {
                        double position = (level - lowerKnee) / KneeDb;
                        gainDb = lowerKneeGainDb * (1.0 - position);
                    }

                    targetGain = (float)Math.Pow(10.0, gainDb / 20.0);
                }

                float coefficient = targetGain > currentGain ? attackCoefficient : releaseCoefficient;
                currentGain += (targetGain - currentGain) * coefficient;

                for (int channel = 0; channel < channels; channel++)
                {
                    buffer[baseIndex + channel] *= currentGain;
                }
            }

            envelope = currentEnvelope;
            smoothGain = currentGain;

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

        private static float TimeCoefficient(double milliseconds, double sampleRate)
        {
            double samples = Math.Max(1.0, sampleRate * milliseconds / 1000.0);
            return (float)(1.0 - Math.Exp(-1.0 / samples));
        }
    }
}
