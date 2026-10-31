using System;
using NAudio.Wave;

namespace bitAudio
{
    internal sealed class NoiseGateSampleProvider : ISampleProvider
    {
        public const int MinimumThresholdDb = -80;
        public const int MaximumThresholdDb = 0;

        private const double CloseHysteresisDb = 3.0;
        private const double HoldMilliseconds = 180.0;

        private readonly ISampleProvider source;
        private readonly int channels;
        private readonly float attackCoefficient;
        private readonly float releaseCoefficient;
        private readonly float envelopeReleaseCoefficient;
        private readonly int holdSamples;

        private volatile int thresholdDb;
        private volatile float levelDb = -90f;
        private float envelope;
        private float smoothGain;
        private bool gateOpen;
        private int holdCounter;

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
            envelopeReleaseCoefficient = TimeCoefficient(0.080, sampleRate);
            holdSamples = (int)Math.Round(sampleRate * HoldMilliseconds / 1000.0);

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

            double openThreshold = Math.Pow(10.0, threshold / 20.0);
            double closeThreshold = Math.Pow(10.0, (threshold - CloseHysteresisDb) / 20.0);

            float currentEnvelope = envelope;
            float currentGain = smoothGain;
            bool currentOpen = gateOpen;
            int currentHold = holdCounter;

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

                if (!active)
                {
                    currentOpen = true;
                    currentHold = holdSamples;
                }
                else
                {
                    if (currentEnvelope >= openThreshold)
                    {
                        currentOpen = true;
                        currentHold = holdSamples;
                    }
                    else if (currentEnvelope < closeThreshold)
                    {
                        if (currentHold > 0)
                        {
                            currentHold--;
                        }
                        else
                        {
                            currentOpen = false;
                        }
                    }
                }

                float targetGain = currentOpen ? 1f : 0f;
                float coefficient = targetGain > currentGain ? attackCoefficient : releaseCoefficient;
                currentGain += (targetGain - currentGain) * coefficient;

                if (targetGain <= 0f && currentGain < 0.0001f)
                {
                    currentGain = 0f;
                }

                if (currentGain != 1f)
                {
                    for (int channel = 0; channel < channels; channel++)
                    {
                        buffer[baseIndex + channel] *= currentGain;
                    }
                }
            }

            envelope = currentEnvelope;
            smoothGain = currentGain;
            gateOpen = currentOpen;
            holdCounter = currentHold;

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
