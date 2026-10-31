using System;
using NAudio.Wave;

namespace bitAudio
{
    internal sealed class ContinuousSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider source;

        public ContinuousSampleProvider(ISampleProvider source)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            this.source = source;
        }

        public WaveFormat WaveFormat
        {
            get { return source.WaveFormat; }
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int totalRead = 0;

            while (totalRead < count)
            {
                int read = source.Read(buffer, offset + totalRead, count - totalRead);
                if (read <= 0)
                {
                    Array.Clear(buffer, offset + totalRead, count - totalRead);
                    return count;
                }

                totalRead += read;
            }

            return totalRead;
        }
    }
}
