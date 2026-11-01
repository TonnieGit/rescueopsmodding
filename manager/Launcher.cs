// Rescue Ops Mod Launcher
// Installs, updates, loads and unloads Rescue Ops: Wildfire mods, and starts the game.
//
// The window is a WebView2 (Edge) view showing ui\index.html; all file, network and process work
// happens in C# (LauncherForm, Core, Services). The WebView2 SDK DLLs and the UI files are embedded
// in this exe so it stays one file: the managed DLLs load from resources, the native loader and the
// UI are extracted to %LOCALAPPDATA%\RescueOpsModLauncher.
//
// Build: build_launcher.bat (C# compiler that comes with Windows, .NET Framework 4.8).
// Switches: --manifest <url or path to mods.json>  --game <game folder>  --ui <folder with index.html>  --devtools

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

[assembly: AssemblyTitle("Rescue Ops Mod Launcher")]
[assembly: AssemblyProduct("Rescue Ops Mod Launcher")]
[assembly: AssemblyDescription("Mods for Rescue Ops: Wildfire")]
[assembly: AssemblyCompany("Tonnie")]
[assembly: AssemblyCopyright("Tonnie")]
[assembly: AssemblyVersion("1.3.0.0")]
[assembly: AssemblyFileVersion("1.3.0.0")]

namespace RescueOpsModLauncher
{
    public class Options
    {
        public string Manifest, Game, UiDir, Raw = "";
        public bool DevTools;

        public static Options Parse(string[] args)
        {
            var o = new Options();
            for (int i = 0; i < args.Length; i++)
            {
                string next = i + 1 < args.Length ? args[i + 1] : null;
                if (args[i] == "--manifest" && next != null) { o.Manifest = next; i++; }
                else if (args[i] == "--game" && next != null) { o.Game = next; i++; }
                else if (args[i] == "--ui" && next != null) { o.UiDir = Path.GetFullPath(next); i++; }
                else if (args[i] == "--devtools") o.DevTools = true;
            }
            o.Raw = string.Join(" ", args.Select(a => a.Contains(" ") ? "\"" + a + "\"" : a));
            return o;
        }
    }

    static class Program
    {
        public const string AppVersion = "1.3.0";
        public const string DefaultManifest = "https://github.com/TonnieGit/rescueopsmodding/releases/latest/download/mods.json";
        public static readonly string DataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RescueOpsModLauncher");
        public static string ExePath { get { return Assembly.GetExecutingAssembly().Location; } }

        [STAThread]
        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveEmbedded;
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            SelfUpdate.CleanUp();
            Run(Options.Parse(args));
        }

        // Kept out of Main so nothing touches the WebView2 types before AssemblyResolve is hooked.
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void Run(Options opt)
        {
            Application.Run(new LauncherForm(opt));
        }

        static Assembly ResolveEmbedded(object sender, ResolveEventArgs e)
        {
            byte[] bytes = Resource(new AssemblyName(e.Name).Name + ".dll");
            return bytes != null ? Assembly.Load(bytes) : null;
        }

        public static byte[] Resource(string name)
        {
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (s == null) return null;
                byte[] bytes = new byte[s.Length];
                int read = 0;
                while (read < bytes.Length) read += s.Read(bytes, read, bytes.Length - read);
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

        // Writes the embedded UI files (resources named "ui/...") to a folder the page is served from.
        public static string ExtractUi()
        {
            string dir = Path.Combine(DataDir, "ui", AppVersion);
            foreach (string name in Assembly.GetExecutingAssembly().GetManifestResourceNames())
            {
                if (!name.StartsWith("ui/", StringComparison.Ordinal)) continue;
                string path = Path.Combine(dir, name.Substring(3).Replace('/', '\\'));
                byte[] bytes = Resource(name);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, bytes);
            }
            return dir;
        }
    }
}
