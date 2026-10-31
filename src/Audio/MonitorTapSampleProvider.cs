using System;
using NAudio.Wave;

namespace bitAudio
{
    internal sealed class MonitorTapSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider source;
        private readonly BufferedWaveProvider target;
        private byte[] byteBuffer;
        private volatile bool enabled;

        public MonitorTapSampleProvider(ISampleProvider source, BufferedWaveProvider target)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            if (target == null)
            {
                throw new ArgumentNullException("target");
            }

            this.source = source;
            this.target = target;
            byteBuffer = new byte[0];
        }

        public WaveFormat WaveFormat
        {
            get { return source.WaveFormat; }
        }

        public bool Enabled
        {
            get { return enabled; }
            set { enabled = value; }
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int read = source.Read(buffer, offset, count);

            if (enabled && read > 0)
            {
                int bytes = read * 4;
                if (byteBuffer.Length < bytes)
                {
                    byteBuffer = new byte[bytes];
                }

                Buffer.BlockCopy(buffer, offset * 4, byteBuffer, 0, bytes);
                target.AddSamples(byteBuffer, 0, bytes);
            }

            return read;
        }
    }
}
