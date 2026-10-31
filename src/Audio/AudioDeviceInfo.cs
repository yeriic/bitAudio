using System;
using NAudio.CoreAudioApi;

namespace bitAudio
{
    internal sealed class AudioDeviceInfo
    {
        public string Id { get; private set; }
        public string Name { get; private set; }
        public DataFlow Flow { get; private set; }
        public bool IsDefault { get; private set; }
        public bool IsVirtual { get; private set; }

        public AudioDeviceInfo(string id, string name, DataFlow flow, bool isDefault)
        {
            Id = id;
            Name = name;
            Flow = flow;
            IsDefault = isDefault;
            IsVirtual = IsVirtualDeviceName(name);
        }

        public static bool IsVirtualDeviceName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            string lower = name.ToLowerInvariant();
            return lower.Contains("cable")
                || lower.Contains("virtual")
                || lower.Contains("voicemeeter")
                || lower.Contains("sonar")
                || lower.Contains("vb-audio");
        }

        public static bool IsCableCaptureName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            string lower = name.ToLowerInvariant();
            return lower.Contains("cable output")
                || lower.Contains("voicemeeter output")
                || lower.Contains("voicemeeter aux output");
        }

        public static bool IsCableRenderName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            string lower = name.ToLowerInvariant();
            return lower.Contains("cable input")
                || lower.Contains("voicemeeter input")
                || lower.Contains("voicemeeter aux input");
        }

        public override string ToString()
        {
            string result = Name;
            if (IsDefault)
            {
                result += "   ·   Predeterminado";
            }
            if (IsVirtual)
            {
                result += "   ·   Virtual";
            }
            return result;
        }
    }
}
