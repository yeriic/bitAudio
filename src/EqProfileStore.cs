using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace bitAudio
{
    internal sealed class EqProfile
    {
        public string Name { get; private set; }
        public float[] Gains { get; set; }
        public bool IsBuiltIn { get; private set; }

        public EqProfile(string name, float[] gains, bool isBuiltIn)
        {
            Name = name;
            Gains = CopyGains(gains);
            IsBuiltIn = isBuiltIn;
        }

        public static float[] CopyGains(float[] gains)
        {
            float[] copy = new float[EqualizerSampleProvider.BandCount];
            if (gains != null)
            {
                int count = Math.Min(gains.Length, copy.Length);
                for (int i = 0; i < count; i++)
                {
                    copy[i] = ClampGain(gains[i]);
                }
            }
            return copy;
        }

        public static float ClampGain(float gain)
        {
            if (gain < EqualizerSampleProvider.MinimumGainDb)
            {
                return EqualizerSampleProvider.MinimumGainDb;
            }
            if (gain > EqualizerSampleProvider.MaximumGainDb)
            {
                return EqualizerSampleProvider.MaximumGainDb;
            }
            return gain;
        }

        public override string ToString()
        {
            return Name;
        }
    }

    internal sealed class EqState
    {
        public string ActiveProfileName { get; set; }
        public float[] Gains { get; set; }
    }

    internal static class EqProfileStore
    {
        public const string PersonalProfileName = "Personalizado";
        private static readonly string[] ReservedNames = { "Plano", PersonalProfileName, "Voz", "Podcast", "Graves", "Agudos" };

        public static string BaseDirectory
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "bitAudio");
            }
        }

        public static string ProfilesDirectory
        {
            get { return Path.Combine(BaseDirectory, "eq"); }
        }

        public static string StatePath
        {
            get { return Path.Combine(BaseDirectory, "eq-state.txt"); }
        }

        public static List<EqProfile> GetBuiltInProfiles()
        {
            List<EqProfile> profiles = new List<EqProfile>();
            profiles.Add(new EqProfile("Plano", new float[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, true));
            profiles.Add(new EqProfile("Voz", new float[] { -6, -4, -2, 0, 1, 2, 3, 2, 1, 0 }, true));
            profiles.Add(new EqProfile("Podcast", new float[] { -10, -8, -4, -1, 0, 1, 2, 2, 1, -1 }, true));
            profiles.Add(new EqProfile("Graves", new float[] { 4, 3, 2, 1, 0, 0, 0, 0, -1, -1 }, true));
            profiles.Add(new EqProfile("Agudos", new float[] { 0, 0, 0, 0, 0, 1, 2, 3, 4, 3 }, true));
            return profiles;
        }

        public static bool IsReservedName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return true;
            }

            foreach (string reserved in ReservedNames)
            {
                if (string.Equals(reserved, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public static List<EqProfile> LoadCustomProfiles()
        {
            List<EqProfile> profiles = new List<EqProfile>();
            try
            {
                if (!Directory.Exists(ProfilesDirectory))
                {
                    return profiles;
                }

                foreach (string path in Directory.GetFiles(ProfilesDirectory, "*.eq"))
                {
                    EqProfile profile = LoadProfileFile(path);
                    if (profile != null && !IsReservedName(profile.Name))
                    {
                        bool duplicate = false;
                        foreach (EqProfile existing in profiles)
                        {
                            if (string.Equals(existing.Name, profile.Name, StringComparison.OrdinalIgnoreCase))
                            {
                                duplicate = true;
                                break;
                            }
                        }
                        if (!duplicate)
                        {
                            profiles.Add(profile);
                        }
                    }
                }
            }
            catch (Exception)
            {
            }

            profiles.Sort(delegate(EqProfile left, EqProfile right)
            {
                return string.Compare(left.Name, right.Name, StringComparison.CurrentCultureIgnoreCase);
            });

            return profiles;
        }

        public static EqProfile SaveCustomProfile(string name, float[] gains)
        {
            name = (name ?? string.Empty).Trim();
            if (IsReservedName(name))
            {
                throw new InvalidOperationException("Ese nombre está reservado.");
            }

            float[] copy = EqProfile.CopyGains(gains);
            Directory.CreateDirectory(ProfilesDirectory);

            string path = FindProfilePathByName(name);
            if (path == null)
            {
                path = GetUniqueProfilePath(name);
            }

            string[] lines = new string[2];
            lines[0] = "name=" + name.Replace("\r", " ").Replace("\n", " ");
            lines[1] = "gains=" + FormatGains(copy);
            File.WriteAllLines(path, lines);
            return new EqProfile(name, copy, false);
        }

        public static EqState LoadState()
        {
            try
            {
                if (!File.Exists(StatePath))
                {
                    return null;
                }

                EqState state = new EqState();
                state.ActiveProfileName = "Plano";
                state.Gains = new float[EqualizerSampleProvider.BandCount];

                foreach (string line in File.ReadAllLines(StatePath))
                {
                    if (line.StartsWith("profile=", StringComparison.OrdinalIgnoreCase))
                    {
                        state.ActiveProfileName = line.Substring("profile=".Length);
                    }
                    else if (line.StartsWith("gains=", StringComparison.OrdinalIgnoreCase))
                    {
                        float[] gains = ParseGains(line.Substring("gains=".Length));
                        if (gains != null)
                        {
                            state.Gains = gains;
                        }
                    }
                }

                return state;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void SaveState(string activeProfileName, float[] gains)
        {
            Directory.CreateDirectory(BaseDirectory);
            string[] lines = new string[2];
            lines[0] = "profile=" + (activeProfileName ?? "Plano").Replace("\r", " ").Replace("\n", " ");
            lines[1] = "gains=" + FormatGains(EqProfile.CopyGains(gains));
            File.WriteAllLines(StatePath, lines);
        }

        private static EqProfile LoadProfileFile(string path)
        {
            try
            {
                string name = Path.GetFileNameWithoutExtension(path);
                float[] gains = null;

                foreach (string line in File.ReadAllLines(path))
                {
                    if (line.StartsWith("name=", StringComparison.OrdinalIgnoreCase))
                    {
                        name = line.Substring("name=".Length);
                    }
                    else if (line.StartsWith("gains=", StringComparison.OrdinalIgnoreCase))
                    {
                        gains = ParseGains(line.Substring("gains=".Length));
                    }
                }

                if (string.IsNullOrEmpty(name) || gains == null)
                {
                    return null;
                }

                return new EqProfile(name, gains, false);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string FindProfilePathByName(string name)
        {
            try
            {
                foreach (string path in Directory.GetFiles(ProfilesDirectory, "*.eq"))
                {
                    EqProfile profile = LoadProfileFile(path);
                    if (profile != null && string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        return path;
                    }
                }
            }
            catch (Exception)
            {
            }
            return null;
        }

        private static string GetUniqueProfilePath(string name)
        {
            string baseName = SanitizeFileName(name);
            string path = Path.Combine(ProfilesDirectory, baseName + ".eq");
            int suffix = 2;
            while (File.Exists(path))
            {
                path = Path.Combine(ProfilesDirectory, baseName + "_" + suffix + ".eq");
                suffix++;
            }
            return path;
        }

        private static string SanitizeFileName(string name)
        {
            string sanitized = name;
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                sanitized = sanitized.Replace(invalid, '_');
            }
            sanitized = sanitized.Trim();
            if (sanitized.Length == 0)
            {
                sanitized = "perfil";
            }
            if (sanitized.Length > 60)
            {
                sanitized = sanitized.Substring(0, 60);
            }
            return sanitized;
        }

        private static string FormatGains(float[] gains)
        {
            string[] parts = new string[gains.Length];
            for (int i = 0; i < gains.Length; i++)
            {
                parts[i] = gains[i].ToString("0.0", CultureInfo.InvariantCulture);
            }
            return string.Join(";", parts);
        }

        private static float[] ParseGains(string text)
        {
            try
            {
                string[] parts = text.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                float[] gains = new float[EqualizerSampleProvider.BandCount];
                int count = Math.Min(parts.Length, gains.Length);
                for (int i = 0; i < count; i++)
                {
                    float value;
                    if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                    {
                        return null;
                    }
                    gains[i] = EqProfile.ClampGain(value);
                }
                if (count < gains.Length)
                {
                    return null;
                }
                return gains;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
