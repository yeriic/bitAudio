using System;
using NAudio.Wave;

namespace bitAudio
{
    internal sealed class ChannelMapperSampleProvider : ISampleProvider
    {
        private enum MappingMode
        {
            Identity,
            MonoToMulti,
            MultiToMono,
            MultiToStereo,
            StereoToMulti,
            Generic
        }

        private readonly ISampleProvider source;
        private readonly WaveFormat waveFormat;
        private readonly int inputChannels;
        private readonly int outputChannels;
        private readonly MappingMode mode;
        private float[] temporaryBuffer;

        public ChannelMapperSampleProvider(ISampleProvider source, int outputChannels)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            if (outputChannels < 1)
            {
                throw new ArgumentOutOfRangeException("outputChannels");
            }

            this.source = source;
            inputChannels = source.WaveFormat.Channels;
            this.outputChannels = outputChannels;

            waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, outputChannels);
            temporaryBuffer = new float[Math.Max(4096, inputChannels * 1024)];

            if (inputChannels == outputChannels)
            {
                mode = MappingMode.Identity;
            }
            else if (inputChannels == 1)
            {
                mode = MappingMode.MonoToMulti;
            }
            else if (outputChannels == 1)
            {
                mode = MappingMode.MultiToMono;
            }
            else if (inputChannels == 2)
            {
                mode = MappingMode.StereoToMulti;
            }
            else if (outputChannels == 2)
            {
                mode = MappingMode.MultiToStereo;
            }
            else
            {
                mode = MappingMode.Generic;
            }
        }

        public WaveFormat WaveFormat
        {
            get { return waveFormat; }
        }

        public int Read(float[] buffer, int offset, int count)
        {
            if (mode == MappingMode.Identity)
            {
                return source.Read(buffer, offset, count);
            }

            int framesWanted = count / outputChannels;
            if (framesWanted <= 0)
            {
                return 0;
            }

            int samplesWanted = framesWanted * inputChannels;
            if (temporaryBuffer.Length < samplesWanted)
            {
                temporaryBuffer = new float[samplesWanted];
            }

            int read = source.Read(temporaryBuffer, 0, samplesWanted);
            int framesRead = read / inputChannels;
            int written = framesRead * outputChannels;

            for (int frame = 0; frame < framesRead; frame++)
            {
                int sourceIndex = frame * inputChannels;
                int targetIndex = offset + frame * outputChannels;

                if (mode == MappingMode.MonoToMulti)
                {
                    float value = temporaryBuffer[sourceIndex];
                    buffer[targetIndex] = value;
                    if (outputChannels > 1)
                    {
                        buffer[targetIndex + 1] = value;
                    }
                    for (int channel = 2; channel < outputChannels; channel++)
                    {
                        buffer[targetIndex + channel] = 0f;
                    }
                }
                else if (mode == MappingMode.MultiToMono)
                {
                    float sum = 0f;
                    for (int channel = 0; channel < inputChannels; channel++)
                    {
                        sum += temporaryBuffer[sourceIndex + channel];
                    }
                    buffer[targetIndex] = sum / inputChannels;
                }
                else if (mode == MappingMode.StereoToMulti)
                {
                    buffer[targetIndex] = temporaryBuffer[sourceIndex];
                    buffer[targetIndex + 1] = temporaryBuffer[sourceIndex + 1];
                    for (int channel = 2; channel < outputChannels; channel++)
                    {
                        buffer[targetIndex + channel] = 0f;
                    }
                }
                else if (mode == MappingMode.MultiToStereo)
                {
                    buffer[targetIndex] = temporaryBuffer[sourceIndex];
                    buffer[targetIndex + 1] = temporaryBuffer[sourceIndex + 1];
                }
                else
                {
                    int channelsToCopy = Math.Min(inputChannels, outputChannels);
                    for (int channel = 0; channel < channelsToCopy; channel++)
                    {
                        buffer[targetIndex + channel] = temporaryBuffer[sourceIndex + channel];
                    }
                    for (int channel = channelsToCopy; channel < outputChannels; channel++)
                    {
                        buffer[targetIndex + channel] = 0f;
                    }
                }
            }

            return written;
        }
    }
}
