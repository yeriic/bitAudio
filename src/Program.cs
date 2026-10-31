using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace bitAudio
{
    internal static class Program
    {
        private static readonly Dictionary<string, Assembly> LoadedAssemblies = new Dictionary<string, Assembly>();
        private static readonly object LoadLock = new object();

        [STAThread]
        private static void Main()
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveEmbeddedAssembly;

            bool createdNew;
            using (Mutex instanceLock = new Mutex(true, "bitAudio.SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    if (!HasCommandLineArgument("--startup"))
                    {
                        MessageBox.Show(
                            "bitAudio ya está en ejecución.\r\nBúscalo en la bandeja del sistema.",
                            "bitAudio",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());

                GC.KeepAlive(instanceLock);
            }
        }

        private static bool HasCommandLineArgument(string argument)
        {
            string[] args = Environment.GetCommandLineArgs();
            foreach (string current in args)
            {
                if (string.Equals(current, argument, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static Assembly ResolveEmbeddedAssembly(object sender, ResolveEventArgs args)
        {
            string simpleName = new AssemblyName(args.Name).Name;
            lock (LoadLock)
            {
                Assembly cached;
                if (LoadedAssemblies.TryGetValue(simpleName, out cached))
                {
                    return cached;
                }

                string resourceName = "bitAudio.Resources." + simpleName + ".dll";
                Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    return null;
                }

                try
                {
                    byte[] data = new byte[stream.Length];
                    int offset = 0;
                    while (offset < data.Length)
                    {
                        int read = stream.Read(data, offset, data.Length - offset);
                        if (read <= 0)
                        {
                            break;
                        }
                        offset += read;
                    }

                    Assembly assembly = Assembly.Load(data);
                    LoadedAssemblies[simpleName] = assembly;
                    return assembly;
                }
                finally
                {
                    stream.Dispose();
                }
            }
        }
    }
}
