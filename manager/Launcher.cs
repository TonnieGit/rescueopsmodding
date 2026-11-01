// Rescue Ops Mod Launcher
// Installs, updates, loads and unloads Rescue Ops: Wildfire mods, and starts the game.
//
// The window is a WebView2 (Edge) view showing ui/index.html; all file, network and process work
// happens here in C#. The WebView2 SDK DLLs are embedded in this exe so it stays one file: the
// managed ones load from resources, the native loader is extracted to %LOCALAPPDATA%.
//
// Build: build_launcher.bat (C# compiler that comes with Windows, .NET Framework 4.8).

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: AssemblyTitle("Rescue Ops Mod Launcher")]
[assembly: AssemblyProduct("Rescue Ops Mod Launcher")]
[assembly: AssemblyDescription("Mods for Rescue Ops: Wildfire")]
[assembly: AssemblyCompany("Tonnie")]
[assembly: AssemblyCopyright("Tonnie")]
[assembly: AssemblyVersion("1.2.0.0")]
[assembly: AssemblyFileVersion("1.2.0.0")]

namespace RescueOpsModLauncher
{
    static class Program
    {
        public const string AppVersion = "1.2.0";
        public static readonly string DataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RescueOpsModLauncher");

        [STAThread]
        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveEmbedded;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Run(args);
        }

        // Kept out of Main so nothing touches the WebView2 types before AssemblyResolve is hooked.
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void Run(string[] args)
        {
            Application.Run(new LauncherForm(args));
        }

        static Assembly ResolveEmbedded(object sender, ResolveEventArgs e)
        {
            string name = new AssemblyName(e.Name).Name + ".dll";
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (s == null) return null;
                byte[] bytes = new byte[s.Length];
                s.Read(bytes, 0, bytes.Length);
                return Assembly.Load(bytes);
            }
        }

        public static byte[] Resource(string name)
        {
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (s == null) return null;
                byte[] bytes = new byte[s.Length];
                s.Read(bytes, 0, bytes.Length);
                return bytes;
            }
        }

        // Writes the embedded native WebView2Loader.dll next to our data, once per version.
        public static string ExtractLoader()
        {
            string dir = Path.Combine(DataDir, "bin", AppVersion);
            string dll = Path.Combine(dir, "WebView2Loader.dll");
            if (!File.Exists(dll))
            {
                Directory.CreateDirectory(dir);
                File.WriteAllBytes(dll, Resource("WebView2Loader.dll"));
            }
            return dir;
        }
    }
}
