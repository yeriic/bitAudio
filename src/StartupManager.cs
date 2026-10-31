using System;
using Microsoft.Win32;

namespace bitAudio
{
    internal static class StartupManager
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string SettingsKeyPath = @"Software\bitAudio";
        private const string RunValueName = "bitAudio";
        private const string PromptValueName = "SetupPromptShown";
        private const string MinimizedValueName = "StartMinimized";
        private const string GainValueName = "GainPercent";
        private const string NoiseGateValueName = "NoiseGateDbOffset";
        private const string LegacySettingsKeyPath = @"Software\AudioBoost";
        private const string LegacyRunValueName = "AudioBoost";

        public static void MigrateLegacySettings(string executablePath)
        {
            try
            {
                using (RegistryKey legacy = Registry.CurrentUser.OpenSubKey(LegacySettingsKeyPath, false))
                {
                    if (legacy != null)
                    {
                        using (RegistryKey current = Registry.CurrentUser.CreateSubKey(SettingsKeyPath))
                        {
                            if (current != null)
                            {
                                CopyValueIfMissing(legacy, current, PromptValueName);
                                CopyValueIfMissing(legacy, current, MinimizedValueName);
                                CopyValueIfMissing(legacy, current, GainValueName);
                            }
                        }

                        try
                        {
                            Registry.CurrentUser.DeleteSubKeyTree(LegacySettingsKeyPath, false);
                        }
                        catch (Exception)
                        {
                        }
                    }
                }

                using (RegistryKey run = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (run != null)
                    {
                        object legacyCommand = run.GetValue(LegacyRunValueName);
                        if (legacyCommand != null)
                        {
                            if (run.GetValue(RunValueName) == null)
                            {
                                run.SetValue(
                                    RunValueName,
                                    "\"" + executablePath + "\" --startup",
                                    RegistryValueKind.String);
                            }
                            run.DeleteValue(LegacyRunValueName, false);
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        private static void CopyValueIfMissing(RegistryKey source, RegistryKey destination, string name)
        {
            object value = source.GetValue(name);
            if (value != null && destination.GetValue(name) == null)
            {
                destination.SetValue(name, value);
            }
        }

        public static bool IsAutoStartEnabled()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
            {
                if (key == null)
                {
                    return false;
                }

                object value = key.GetValue(RunValueName);
                return value != null && value.ToString().Length > 0;
            }
        }

        public static void EnableAutoStart(string executablePath)
        {
            if (string.IsNullOrEmpty(executablePath))
            {
                throw new ArgumentNullException("executablePath");
            }

            string command = "\"" + executablePath + "\" --startup";
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath))
            {
                if (key == null)
                {
                    throw new InvalidOperationException("No se pudo abrir el registro de Windows.");
                }
                key.SetValue(RunValueName, command, RegistryValueKind.String);
            }
        }

        public static void DisableAutoStart()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
            {
                if (key != null)
                {
                    key.DeleteValue(RunValueName, false);
                }
            }
        }

        public static void EnsureAutoStartPath(string executablePath)
        {
            if (IsAutoStartEnabled())
            {
                EnableAutoStart(executablePath);
            }
        }

        public static bool WasSetupPromptShown()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(SettingsKeyPath, false))
            {
                if (key == null)
                {
                    return false;
                }

                object value = key.GetValue(PromptValueName);
                if (value == null)
                {
                    return false;
                }

                try
                {
                    return Convert.ToInt32(value) != 0;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        public static void MarkSetupPromptShown()
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(SettingsKeyPath))
            {
                if (key != null)
                {
                    key.SetValue(PromptValueName, 1, RegistryValueKind.DWord);
                }
            }
        }

        public static bool GetStartMinimized()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(SettingsKeyPath, false))
            {
                if (key == null)
                {
                    return true;
                }

                object value = key.GetValue(MinimizedValueName);
                if (value == null)
                {
                    return true;
                }

                try
                {
                    return Convert.ToInt32(value) != 0;
                }
                catch (Exception)
                {
                    return true;
                }
            }
        }

        public static void SetStartMinimized(bool startMinimized)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(SettingsKeyPath))
            {
                if (key != null)
                {
                    key.SetValue(MinimizedValueName, startMinimized ? 1 : 0, RegistryValueKind.DWord);
                }
            }
        }

        public static int GetGainPercent()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(SettingsKeyPath, false))
            {
                if (key == null)
                {
                    return 100;
                }

                object value = key.GetValue(GainValueName);
                if (value == null)
                {
                    return 100;
                }

                try
                {
                    int percent = Convert.ToInt32(value);
                    if (percent < 0)
                    {
                        return 0;
                    }
                    if (percent > 400)
                    {
                        return 400;
                    }
                    return percent;
                }
                catch (Exception)
                {
                    return 100;
                }
            }
        }

        public static void SetGainPercent(int percent)
        {
            if (percent < 0)
            {
                percent = 0;
            }
            else if (percent > 400)
            {
                percent = 400;
            }

            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(SettingsKeyPath))
            {
                if (key != null)
                {
                    key.SetValue(GainValueName, percent, RegistryValueKind.DWord);
                }
            }
        }

        public static int GetNoiseGateThresholdDb()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(SettingsKeyPath, false))
            {
                if (key == null)
                {
                    return -80;
                }

                object value = key.GetValue(NoiseGateValueName);
                if (value == null)
                {
                    return -80;
                }

                try
                {
                    int offset = Convert.ToInt32(value);
                    if (offset < 0)
                    {
                        offset = 0;
                    }
                    if (offset > 80)
                    {
                        offset = 80;
                    }
                    return offset - 80;
                }
                catch (Exception)
                {
                    return -80;
                }
            }
        }

        public static void SetNoiseGateThresholdDb(int thresholdDb)
        {
            if (thresholdDb < -80)
            {
                thresholdDb = -80;
            }
            else if (thresholdDb > 0)
            {
                thresholdDb = 0;
            }

            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(SettingsKeyPath))
            {
                if (key != null)
                {
                    key.SetValue(NoiseGateValueName, thresholdDb + 80, RegistryValueKind.DWord);
                }
            }
        }
    }
}
