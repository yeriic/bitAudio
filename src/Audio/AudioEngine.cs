using System;
using System.Collections.Generic;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace bitAudio
{
    internal sealed class AudioEngine : IDisposable
    {
        private readonly MMDeviceEnumerator enumerator;
        private readonly object syncRoot = new object();

        private MMDevice volumeOutputDevice;
        private MMDevice volumeInputDevice;

        private WasapiCapture capture;
        private WasapiOut output;
        private BufferedWaveProvider captureBuffer;
        private GainLimiterSampleProvider processor;
        private EqualizerSampleProvider equalizer;
        private NoiseGateSampleProvider noiseGate;
        private volatile int noiseGateThresholdDb = -80;
        private MonitorTapSampleProvider monitorTap;
        private BufferedWaveProvider monitorBuffer;
        private WasapiOut monitorOutput;
        private MMDevice monitorDevice;
        private volatile bool monitoring;
        private readonly float[] equalizerGains = new float[EqualizerSampleProvider.BandCount];
        private MMDevice captureDevice;
        private MMDevice playbackDevice;

        private volatile bool running;
        private volatile bool intentionalStop;
        private volatile float gain = 1f;
        private volatile bool limiterEnabled = true;
        private int latencyMilliseconds;
        private int targetBufferBytes;
        private int trimThresholdBytes;
        private byte[] trimBuffer;
        private string pipelineDescription;
        private bool disposed;

        public event Action<string> StatusChanged;
        public event Action<string> ErrorOccurred;

        public AudioEngine()
        {
            enumerator = new MMDeviceEnumerator();
        }

        public bool IsRunning
        {
            get { return running; }
        }

        public string PipelineDescription
        {
            get { return pipelineDescription; }
        }

        public float Gain
        {
            get { return gain; }
            set
            {
                gain = value < 0f ? 0f : value;
                GainLimiterSampleProvider current = processor;
                if (current != null)
                {
                    current.Gain = gain;
                }
            }
        }

        public bool LimiterEnabled
        {
            get { return limiterEnabled; }
            set
            {
                limiterEnabled = value;
                GainLimiterSampleProvider current = processor;
                if (current != null)
                {
                    current.LimiterEnabled = value;
                }
            }
        }

        public void SetEqualizerGains(float[] gains)
        {
            if (gains == null)
            {
                return;
            }

            lock (syncRoot)
            {
                int count = Math.Min(gains.Length, equalizerGains.Length);
                for (int i = 0; i < count; i++)
                {
                    equalizerGains[i] = gains[i] < EqualizerSampleProvider.MinimumGainDb
                        ? EqualizerSampleProvider.MinimumGainDb
                        : gains[i] > EqualizerSampleProvider.MaximumGainDb
                            ? EqualizerSampleProvider.MaximumGainDb
                            : gains[i];
                }

                EqualizerSampleProvider current = equalizer;
                if (current != null)
                {
                    current.SetGains(equalizerGains);
                }
            }
        }

        public bool IsMonitoring
        {
            get { return monitoring; }
        }

        public float InputLevelDb
        {
            get
            {
                NoiseGateSampleProvider current = noiseGate;
                return current != null ? current.LevelDb : -90f;
            }
        }

        public void SetNoiseGateThreshold(int thresholdDb)
        {
            lock (syncRoot)
            {
                if (thresholdDb < NoiseGateSampleProvider.MinimumThresholdDb)
                {
                    thresholdDb = NoiseGateSampleProvider.MinimumThresholdDb;
                }
                else if (thresholdDb > NoiseGateSampleProvider.MaximumThresholdDb)
                {
                    thresholdDb = NoiseGateSampleProvider.MaximumThresholdDb;
                }

                noiseGateThresholdDb = thresholdDb;
                NoiseGateSampleProvider current = noiseGate;
                if (current != null)
                {
                    current.SetThreshold(thresholdDb);
                }
            }
        }

        public void StartMonitoring(string outputDeviceId)
        {
            lock (syncRoot)
            {
                if (!running || monitorTap == null || monitorBuffer == null)
                {
                    throw new InvalidOperationException("El procesamiento no está activo.");
                }

                StopMonitoringInternal();

                MMDevice device = null;
                WasapiOut newOutput = null;

                try
                {
                    device = enumerator.GetDevice(outputDeviceId);
                    if (device.DataFlow != DataFlow.Render)
                    {
                        throw new InvalidOperationException("El dispositivo de monitor no es una salida.");
                    }

                    monitorBuffer.ClearBuffer();
                    newOutput = new WasapiOut(device, AudioClientShareMode.Shared, true, 30);
                    ISampleProvider monitorSamples = new ContinuousSampleProvider(monitorBuffer.ToSampleProvider());
                    newOutput.Init(monitorSamples.ToWaveProvider());

                    monitorDevice = device;
                    device = null;
                    monitorOutput = newOutput;
                    newOutput = null;

                    monitorTap.Enabled = true;
                    monitorOutput.Play();
                    monitoring = true;
                }
                catch (Exception)
                {
                    if (newOutput != null)
                    {
                        try
                        {
                            newOutput.Dispose();
                        }
                        catch (Exception)
                        {
                        }
                    }
                    if (device != null)
                    {
                        try
                        {
                            device.Dispose();
                        }
                        catch (Exception)
                        {
                        }
                    }
                    if (monitorTap != null)
                    {
                        monitorTap.Enabled = false;
                    }
                    throw;
                }
            }
        }

        public void StopMonitoring()
        {
            lock (syncRoot)
            {
                StopMonitoringInternal();
            }
        }

        private void StopMonitoringInternal()
        {
            monitoring = false;

            if (monitorTap != null)
            {
                monitorTap.Enabled = false;
            }

            if (monitorOutput != null)
            {
                try
                {
                    monitorOutput.Stop();
                }
                catch (Exception)
                {
                }
                try
                {
                    monitorOutput.Dispose();
                }
                catch (Exception)
                {
                }
                monitorOutput = null;
            }

            if (monitorDevice != null)
            {
                try
                {
                    monitorDevice.Dispose();
                }
                catch (Exception)
                {
                }
                monitorDevice = null;
            }

            if (monitorBuffer != null)
            {
                try
                {
                    monitorBuffer.ClearBuffer();
                }
                catch (Exception)
                {
                }
            }
        }

        public List<AudioDeviceInfo> GetInputDevices()
        {
            return EnumerateDevices(DataFlow.Capture);
        }

        public List<AudioDeviceInfo> GetOutputDevices()
        {
            return EnumerateDevices(DataFlow.Render);
        }

        public string GetDefaultOutputName()
        {
            MMDevice device = null;
            try
            {
                device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                return device.FriendlyName;
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                if (device != null)
                {
                    device.Dispose();
                }
            }
        }

        private List<AudioDeviceInfo> EnumerateDevices(DataFlow flow)
        {
            string defaultId = null;
            MMDevice defaultDevice = null;
            try
            {
                defaultDevice = enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia);
                defaultId = defaultDevice.ID;
            }
            catch (Exception)
            {
            }
            finally
            {
                if (defaultDevice != null)
                {
                    defaultDevice.Dispose();
                }
            }

            List<AudioDeviceInfo> result = new List<AudioDeviceInfo>();
            MMDeviceCollection collection = enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active);
            foreach (MMDevice device in collection)
            {
                try
                {
                    result.Add(new AudioDeviceInfo(device.ID, device.FriendlyName, flow, device.ID == defaultId));
                }
                catch (Exception)
                {
                }
                finally
                {
                    device.Dispose();
                }
            }

            result.Sort(delegate(AudioDeviceInfo left, AudioDeviceInfo right)
            {
                return string.Compare(left.Name, right.Name, StringComparison.CurrentCultureIgnoreCase);
            });

            return result;
        }

        public void SelectVolumeOutput(string deviceId)
        {
            lock (syncRoot)
            {
                if (volumeOutputDevice != null)
                {
                    try
                    {
                        volumeOutputDevice.Dispose();
                    }
                    catch (Exception)
                    {
                    }
                    volumeOutputDevice = null;
                }

                if (!string.IsNullOrEmpty(deviceId))
                {
                    volumeOutputDevice = enumerator.GetDevice(deviceId);
                }
            }
        }

        public void SelectVolumeInput(string deviceId)
        {
            lock (syncRoot)
            {
                if (volumeInputDevice != null)
                {
                    try
                    {
                        volumeInputDevice.Dispose();
                    }
                    catch (Exception)
                    {
                    }
                    volumeInputDevice = null;
                }

                if (!string.IsNullOrEmpty(deviceId))
                {
                    volumeInputDevice = enumerator.GetDevice(deviceId);
                }
            }
        }

        public bool TryGetOutputVolume(out float scalar)
        {
            scalar = 0f;
            MMDevice device = volumeOutputDevice;
            if (device == null)
            {
                return false;
            }

            try
            {
                scalar = device.AudioEndpointVolume.MasterVolumeLevelScalar;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool TrySetOutputVolume(float scalar)
        {
            if (scalar < 0f)
            {
                scalar = 0f;
            }
            else if (scalar > 1f)
            {
                scalar = 1f;
            }

            MMDevice device = volumeOutputDevice;
            if (device == null)
            {
                return false;
            }

            try
            {
                device.AudioEndpointVolume.MasterVolumeLevelScalar = scalar;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool TryGetOutputMute(out bool muted)
        {
            muted = false;
            MMDevice device = volumeOutputDevice;
            if (device == null)
            {
                return false;
            }

            try
            {
                muted = device.AudioEndpointVolume.Mute;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool TrySetOutputMute(bool muted)
        {
            MMDevice device = volumeOutputDevice;
            if (device == null)
            {
                return false;
            }

            try
            {
                device.AudioEndpointVolume.Mute = muted;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool TryGetInputVolume(out float scalar)
        {
            scalar = 0f;
            MMDevice device = volumeInputDevice;
            if (device == null)
            {
                return false;
            }

            try
            {
                scalar = device.AudioEndpointVolume.MasterVolumeLevelScalar;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool TrySetInputVolume(float scalar)
        {
            if (scalar < 0f)
            {
                scalar = 0f;
            }
            else if (scalar > 1f)
            {
                scalar = 1f;
            }

            MMDevice device = volumeInputDevice;
            if (device == null)
            {
                return false;
            }

            try
            {
                device.AudioEndpointVolume.MasterVolumeLevelScalar = scalar;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void Start(string inputId, string outputId, float initialGain, bool limiter, int latency, int captureBufferMilliseconds)
        {
            lock (syncRoot)
            {
                if (disposed)
                {
                    throw new ObjectDisposedException("AudioEngine");
                }
                if (running)
                {
                    throw new InvalidOperationException("El procesamiento ya está activo.");
                }

                CleanupPipeline();
                intentionalStop = false;
                gain = initialGain < 0f ? 0f : initialGain;
                limiterEnabled = limiter;
                latencyMilliseconds = latency;

                MMDevice inputDevice = null;
                MMDevice outputDevice = null;

                try
                {
                    inputDevice = enumerator.GetDevice(inputId);
                    outputDevice = enumerator.GetDevice(outputId);

                    if (inputDevice.DataFlow != DataFlow.Capture)
                    {
                        throw new InvalidOperationException("El dispositivo de entrada no es un dispositivo de captura.");
                    }
                    if (outputDevice.DataFlow != DataFlow.Render)
                    {
                        throw new InvalidOperationException("El dispositivo de salida no es un dispositivo de reproducción.");
                    }
                    if (AudioDeviceInfo.IsCableCaptureName(inputDevice.FriendlyName)
                        && AudioDeviceInfo.IsCableRenderName(outputDevice.FriendlyName))
                    {
                        throw new InvalidOperationException("Entrada y salida pertenecen al mismo cable virtual. Eso crea un bucle de realimentación.");
                    }

                    captureDevice = inputDevice;
                    inputDevice = null;
                    playbackDevice = outputDevice;
                    outputDevice = null;

                    int captureBufferMs = Math.Max(10, Math.Min(50, captureBufferMilliseconds));
                    capture = new WasapiCapture(captureDevice, true, captureBufferMs);
                    capture.ShareMode = AudioClientShareMode.Shared;

                    WaveFormat captureFormat = capture.WaveFormat;
                    ValidateCaptureFormat(captureFormat);

                    captureBuffer = new BufferedWaveProvider(captureFormat);
                    captureBuffer.BufferDuration = TimeSpan.FromMilliseconds(500);
                    captureBuffer.DiscardOnBufferOverflow = true;
                    captureBuffer.ReadFully = false;

                    int targetMilliseconds = Math.Max(30, latency + 20);
                    targetBufferBytes = (int)((long)captureFormat.AverageBytesPerSecond * targetMilliseconds / 1000L);
                    trimThresholdBytes = (int)((long)captureFormat.AverageBytesPerSecond * (targetMilliseconds + 60) / 1000L);
                    trimBuffer = new byte[Math.Max(8192, trimThresholdBytes)];

                    ISampleProvider samples = captureBuffer.ToSampleProvider();

                    WaveFormat outputMixFormat = playbackDevice.AudioClient.MixFormat;
                    int outputSampleRate = outputMixFormat.SampleRate;
                    int outputChannels = outputMixFormat.Channels;

                    if (captureFormat.SampleRate != outputSampleRate)
                    {
                        samples = new WdlResamplingSampleProvider(samples, outputSampleRate);
                    }

                    samples = new ChannelMapperSampleProvider(samples, outputChannels);
                    equalizer = new EqualizerSampleProvider(samples, equalizerGains);
                    samples = equalizer;
                    noiseGate = new NoiseGateSampleProvider(samples, noiseGateThresholdDb);
                    samples = noiseGate;
                    processor = new GainLimiterSampleProvider(samples, gain, limiterEnabled);
                    ISampleProvider continuous = new ContinuousSampleProvider(processor);

                    WaveFormat outputFormat = WaveFormat.CreateIeeeFloatWaveFormat(outputSampleRate, outputChannels);
                    monitorBuffer = new BufferedWaveProvider(continuous.WaveFormat);
                    monitorBuffer.BufferDuration = TimeSpan.FromMilliseconds(250);
                    monitorBuffer.DiscardOnBufferOverflow = true;
                    monitorTap = new MonitorTapSampleProvider(continuous, monitorBuffer);
                    IWaveProvider waveProvider = monitorTap.ToWaveProvider();

                    output = new WasapiOut(playbackDevice, AudioClientShareMode.Shared, true, latency);
                    output.PlaybackStopped += HandleOutputStopped;
                    output.Init(waveProvider);

                    capture.DataAvailable += HandleDataAvailable;
                    capture.RecordingStopped += HandleCaptureStopped;

                    pipelineDescription = string.Format(
                        "{0} Hz / {1} canales → {2} Hz / {3} canales",
                        captureFormat.SampleRate,
                        captureFormat.Channels,
                        outputSampleRate,
                        outputChannels);

                    running = true;
                    capture.StartRecording();
                    output.Play();

                    RaiseStatus("Procesamiento activo.");
                }
                catch (Exception)
                {
                    intentionalStop = true;
                    running = false;
                    CleanupPipeline();
                    throw;
                }
                finally
                {
                    if (inputDevice != null)
                    {
                        inputDevice.Dispose();
                    }
                    if (outputDevice != null)
                    {
                        outputDevice.Dispose();
                    }
                }
            }
        }

        public void Stop()
        {
            lock (syncRoot)
            {
                if (!running && capture == null && output == null)
                {
                    return;
                }

                intentionalStop = true;
                running = false;

                WasapiOut currentOutput = output;
                if (currentOutput != null)
                {
                    try
                    {
                        currentOutput.Stop();
                    }
                    catch (Exception)
                    {
                    }
                }

                CleanupPipeline();
                pipelineDescription = null;
                RaiseStatus("Procesamiento detenido.");
            }
        }

        private void CleanupPipeline()
        {
            StopMonitoringInternal();

            if (capture != null)
            {
                try
                {
                    capture.DataAvailable -= HandleDataAvailable;
                    capture.RecordingStopped -= HandleCaptureStopped;
                    capture.Dispose();
                }
                catch (Exception)
                {
                }
                capture = null;
            }

            if (output != null)
            {
                try
                {
                    output.PlaybackStopped -= HandleOutputStopped;
                    output.Dispose();
                }
                catch (Exception)
                {
                }
                output = null;
            }

            if (captureDevice != null)
            {
                try
                {
                    captureDevice.Dispose();
                }
                catch (Exception)
                {
                }
                captureDevice = null;
            }

            if (playbackDevice != null)
            {
                try
                {
                    playbackDevice.Dispose();
                }
                catch (Exception)
                {
                }
                playbackDevice = null;
            }

            captureBuffer = null;
            processor = null;
            equalizer = null;
            noiseGate = null;
            monitorTap = null;
            monitorBuffer = null;
            trimBuffer = null;
        }

        private void HandleDataAvailable(object sender, WaveInEventArgs args)
        {
            BufferedWaveProvider buffer = captureBuffer;
            if (!running || buffer == null || args.BytesRecorded <= 0)
            {
                return;
            }

            try
            {
                buffer.AddSamples(args.Buffer, 0, args.BytesRecorded);

                int available = buffer.BufferedBytes;
                if (available > trimThresholdBytes && trimBuffer != null)
                {
                    int toDiscard = available - targetBufferBytes;
                    while (toDiscard > 0)
                    {
                        int chunk = Math.Min(toDiscard, trimBuffer.Length);
                        int read = buffer.Read(trimBuffer, 0, chunk);
                        if (read <= 0)
                        {
                            break;
                        }
                        toDiscard -= read;
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private void HandleCaptureStopped(object sender, StoppedEventArgs args)
        {
            if (intentionalStop || !object.ReferenceEquals(sender, capture))
            {
                return;
            }

            running = false;
            if (args.Exception != null)
            {
                RaiseError("La captura se detuvo: " + args.Exception.Message);
            }
            else
            {
                RaiseError("La captura se detuvo inesperadamente.");
            }
        }

        private void HandleOutputStopped(object sender, StoppedEventArgs args)
        {
            if (intentionalStop || !object.ReferenceEquals(sender, output))
            {
                return;
            }

            running = false;
            if (args.Exception != null)
            {
                RaiseError("La reproducción se detuvo: " + args.Exception.Message);
            }
            else
            {
                RaiseError("La reproducción se detuvo inesperadamente.");
            }
        }

        private static void ValidateCaptureFormat(WaveFormat format)
        {
            if (format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32)
            {
                return;
            }

            if (format.Encoding == WaveFormatEncoding.Pcm)
            {
                int bits = format.BitsPerSample;
                if (bits == 8 || bits == 16 || bits == 24 || bits == 32)
                {
                    return;
                }
            }

            throw new NotSupportedException("Formato de captura no soportado: " + format);
        }

        private void RaiseStatus(string message)
        {
            Action<string> handler = StatusChanged;
            if (handler != null)
            {
                try
                {
                    handler(message);
                }
                catch (Exception)
                {
                }
            }
        }

        private void RaiseError(string message)
        {
            Action<string> handler = ErrorOccurred;
            if (handler != null)
            {
                try
                {
                    handler(message);
                }
                catch (Exception)
                {
                }
            }
        }

        public void Dispose()
        {
            lock (syncRoot)
            {
                if (disposed)
                {
                    return;
                }
                disposed = true;
            }

            try
            {
                Stop();
            }
            catch (Exception)
            {
            }

            lock (syncRoot)
            {
                if (volumeOutputDevice != null)
                {
                    try
                    {
                        volumeOutputDevice.Dispose();
                    }
                    catch (Exception)
                    {
                    }
                    volumeOutputDevice = null;
                }
                if (volumeInputDevice != null)
                {
                    try
                    {
                        volumeInputDevice.Dispose();
                    }
                    catch (Exception)
                    {
                    }
                    volumeInputDevice = null;
                }
                try
                {
                    enumerator.Dispose();
                }
                catch (Exception)
                {
                }
            }
        }
    }
}
