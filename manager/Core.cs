// Mod list, game discovery, hashes and the mod library (load / unload / install / remove).
//
// Library model: the game loads every pak under Wildfire\Content\Paks (subfolders too), so mods that
// are switched off live in Wildfire\ModLibrary next to it (same drive, so a move is instant). The
// library is one flat folder because bundles (e.g. the truck reskin) share files with the mods they
// include. The launcher only ever moves or deletes files named in mods.json, plus paks the player
// added themselves; never the game's own files.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace RescueOpsModLauncher
{
    // ---- mods.json ---------------------------------------------------------------------------
    // schema stays 2 so Mod Manager 1.1 can still read it; the launcher fields are extra.

    public class Manifest
    {
        public int schema { get; set; }
        public ManagerInfo manager { get; set; }
        public LauncherInfo launcher { get; set; }
        public List<ModInfo> mods { get; set; }
    }

    public class ManagerInfo
    {
        public string version { get; set; }
        public string download { get; set; }
    }

    public class LauncherInfo
    {
        public string version { get; set; }
        public string download { get; set; }
        public string sha256 { get; set; }
        public List<string> notes { get; set; }
    }

    public class ModInfo
    {
        public string id { get; set; }
        public string name { get; set; }
        public string version { get; set; }
        public string zip { get; set; }
        public string page { get; set; }
        public string description { get; set; }
        public string category { get; set; }
        public string author { get; set; }
        public string thumb { get; set; }
        public List<string> screenshots { get; set; }     // full-size images for the viewer; the thumb is used when empty
        public string replacedBy { get; set; }
        public string testedBuild { get; set; }
        public bool hidden { get; set; }
        public List<string> includes { get; set; }
        public List<string> conflicts { get; set; }
        public List<string> requires { get; set; }
        public List<string> replaces { get; set; }
        public List<ModFile> files { get; set; }
        public List<ChangeEntry> changelog { get; set; }
    }

    public class ModFile
    {
        public string name { get; set; }
        public string sha256 { get; set; }
        public long size { get; set; }
    }

    public class ChangeEntry
    {
        public string version { get; set; }
        public List<string> notes { get; set; }
    }

    // ---- Finding the game --------------------------------------------------------------------

    public class SteamApp
    {
        public string AppId;
        public string BuildId;
        public string Name;
    }

    public static class Game
    {
        public static readonly string[] ProcessNames = { "WildfireDemo", "WildfireDemo-Win64-Shipping", "Wildfire", "Wildfire-Win64-Shipping" };
        public const string FullGameAppId = "2915770";
        public const string DemoAppId = "5064860";

        // Takes the game folder or its Paks folder; returns the Paks folder, or null if it isn't the game.
        public static string PaksOf(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder)) return null;
            folder = folder.Trim().Trim('"').TrimEnd('\\');
            try
            {
                string paks = Path.Combine(folder, @"Wildfire\Content\Paks");
                if (File.Exists(Path.Combine(paks, "Wildfire-Windows.pak"))) return Path.GetFullPath(paks);
                if (File.Exists(Path.Combine(folder, "Wildfire-Windows.pak"))) return Path.GetFullPath(folder);
            }
            catch (ArgumentException) { }
            return null;
        }

        // The game folder (the one Steam installs) from its Paks folder.
        public static string GameDirOf(string paks)
        {
            DirectoryInfo d = new DirectoryInfo(paks);           // ...\<game>\Wildfire\Content\Paks
            return d.Parent.Parent.Parent.FullName;
        }

        public static List<string> SteamLibraries()
        {
            var libraries = new List<string>();
            string steam = null;
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                    if (key != null) steam = key.GetValue("SteamPath") as string;
            }
            catch (Exception) { }
            if (!string.IsNullOrEmpty(steam))
            {
                steam = steam.Replace('/', '\\');
                libraries.Add(steam);
                string vdf = Path.Combine(steam, @"steamapps\libraryfolders.vdf");
                try
                {
                    if (File.Exists(vdf))
                        foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                            libraries.Add(m.Groups[1].Value.Replace(@"\\", @"\"));
                }
                catch (Exception) { }
            }
            libraries.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
            return libraries.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        // Every copy of the game in every Steam library on this PC.
        public static List<string> Find()
        {
            var games = new List<string>();
            foreach (string library in SteamLibraries())
            {
                try
                {
                    string common = Path.Combine(library, @"steamapps\common");
                    if (!Directory.Exists(common)) continue;
                    foreach (string dir in Directory.GetDirectories(common, "Rescue Ops*"))
                    {
                        if (PaksOf(dir) == null) continue;
                        string full = Path.GetFullPath(dir);
                        if (!games.Any(g => string.Equals(g, full, StringComparison.OrdinalIgnoreCase))) games.Add(full);
                    }
                }
                catch (Exception) { }
            }
            return games;
        }

        // The Steam app this game folder belongs to (appmanifest_<id>.acf in the same library), or null.
        public static SteamApp AppOf(string gameDir)
        {
            try
            {
                DirectoryInfo common = new DirectoryInfo(gameDir).Parent;      // steamapps\common
                if (common == null || common.Parent == null) return null;
                string folderName = new DirectoryInfo(gameDir).Name;
                foreach (string acf in Directory.GetFiles(common.Parent.FullName, "appmanifest_*.acf"))
                {
                    string text = File.ReadAllText(acf);
                    Match dir = Regex.Match(text, "\"installdir\"\\s+\"([^\"]+)\"");
                    if (!dir.Success || !string.Equals(dir.Groups[1].Value, folderName, StringComparison.OrdinalIgnoreCase)) continue;
                    return new SteamApp
                    {
                        AppId = Regex.Match(text, "\"appid\"\\s+\"(\\d+)\"").Groups[1].Value,
                        BuildId = Regex.Match(text, "\"buildid\"\\s+\"(\\d+)\"").Groups[1].Value,
                        Name = Regex.Match(text, "\"name\"\\s+\"([^\"]+)\"").Groups[1].Value
                    };
                }
            }
            catch (Exception) { }
            return null;
        }

        public static bool IsRunning()
        {
            foreach (string name in ProcessNames)
            {
                Process[] found = Process.GetProcessesByName(name);
                bool any = found.Length > 0;
                foreach (Process p in found) p.Dispose();
                if (any) return true;
            }
            return false;
        }
    }

    // ---- Hashes ------------------------------------------------------------------------------

    public static class Hash
    {
        // Cached by path + size + time.
        static readonly Dictionary<string, Tuple<long, DateTime, string>> cache = new Dictionary<string, Tuple<long, DateTime, string>>();

        public static string Of(string path)
        {
            var info = new FileInfo(path);
            string key = info.FullName.ToLowerInvariant();
            Tuple<long, DateTime, string> hit;
            lock (cache)
                if (cache.TryGetValue(key, out hit) && hit.Item1 == info.Length && hit.Item2 == info.LastWriteTimeUtc) return hit.Item3;
            string hash;
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha = SHA256.Create())
                hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
            lock (cache) cache[key] = Tuple.Create(info.Length, info.LastWriteTimeUtc, hash);
            return hash;
        }

        // Repair re-reads every file instead of trusting the cache.
        public static void Clear() { lock (cache) cache.Clear(); }

        // A moved file keeps its contents, so carry the cached hash over to the new path.
        public static void Moved(string from, string to)
        {
            lock (cache)
            {
                Tuple<long, DateTime, string> hit;
                if (!cache.TryGetValue(from.ToLowerInvariant(), out hit)) return;
                cache.Remove(from.ToLowerInvariant());
                var info = new FileInfo(to);
                if (info.Exists && info.Length == hit.Item1) cache[info.FullName.ToLowerInvariant()] = Tuple.Create(info.Length, info.LastWriteTimeUtc, hit.Item3);
            }
        }
    }

    // ---- Which files belong to which mod -----------------------------------------------------

    public class ModSet
    {
        public readonly Manifest Manifest;
        readonly Dictionary<string, ModInfo> byId;

        public ModSet(Manifest manifest)
        {
            Manifest = manifest;
            if (manifest.mods == null) manifest.mods = new List<ModInfo>();
            foreach (ModInfo m in manifest.mods)
            {
                if (m.includes == null) m.includes = new List<string>();
                if (m.conflicts == null) m.conflicts = new List<string>();
                if (m.requires == null) m.requires = new List<string>();
                if (m.replaces == null) m.replaces = new List<string>();
                if (m.files == null) m.files = new List<ModFile>();
                if (m.changelog == null) m.changelog = new List<ChangeEntry>();
                if (string.IsNullOrEmpty(m.author)) m.author = "Tonnie";
                if (string.IsNullOrEmpty(m.category)) m.category = "Other";
            }
            byId = manifest.mods.GroupBy(m => m.id, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        }

        public IEnumerable<ModInfo> All { get { return Manifest.mods; } }

        public ModInfo Get(string id)
        {
            ModInfo m;
            return id != null && byId.TryGetValue(id, out m) ? m : null;
        }

        public IEnumerable<ModInfo> Included(ModInfo mod) { return mod.includes.Select(Get).Where(m => m != null); }

        // The files that are this mod's alone, not one of the mods it includes.
        public List<ModFile> OwnFiles(ModInfo mod)
        {
            var included = new HashSet<string>(Included(mod).SelectMany(m => m.files).Select(f => f.name), StringComparer.OrdinalIgnoreCase);
            List<ModFile> own = mod.files.Where(f => !included.Contains(f.name)).ToList();
            return own.Count > 0 ? own : mod.files;
        }

        // Every mod this one needs alongside it (requirements of it and what it includes, recursively).
        public List<ModInfo> Required(ModInfo mod)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { mod.id };
            var result = new List<ModInfo>();
            var queue = new Queue<ModInfo>();
            queue.Enqueue(mod);
            while (queue.Count > 0)
            {
                ModInfo m = queue.Dequeue();
                foreach (string id in m.requires.Concat(m.includes))
                {
                    ModInfo r = Get(id);
                    if (r == null || !seen.Add(r.id)) continue;
                    if (m.requires.Contains(id, StringComparer.OrdinalIgnoreCase)) result.Add(r);
                    queue.Enqueue(r);
                }
            }
            return result;
        }

        // Mods that need this one (directly, or through what they include or require).
        public List<ModInfo> Dependents(ModInfo mod)
        {
            return All.Where(m => !string.Equals(m.id, mod.id, StringComparison.OrdinalIgnoreCase)
                && Required(m).Any(r => string.Equals(r.id, mod.id, StringComparison.OrdinalIgnoreCase))).ToList();
        }

        // Mods that change the same thing as this one or one it includes. Works both ways.
        public HashSet<string> Conflicts(ModInfo mod)
        {
            var mine = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { mod.id };
            foreach (ModInfo inc in Included(mod)) mine.Add(inc.id);
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string id in mine)
            {
                ModInfo m = Get(id);
                if (m != null) foreach (string c in m.conflicts) result.Add(c);
            }
            foreach (ModInfo other in All)
                if (other.conflicts.Any(c => mine.Contains(c))) result.Add(other.id);
            result.ExceptWith(mine);
            return result;
        }

        // Every file name any mod uses (plus old names they replace). Only these are ever moved or deleted.
        public HashSet<string> AllFileNames()
        {
            return new HashSet<string>(All.SelectMany(m => m.files.Select(f => f.name).Concat(m.replaces)), StringComparer.OrdinalIgnoreCase);
        }
    }

    // ---- The mod library ---------------------------------------------------------------------

    public enum ModState { NotDownloaded, Loaded, Available, UpdateAvailable }

    public class LocalMod
    {
        public string Id;          // "local:<stem>"
        public string Name;        // file stem, e.g. z_Custom_Sirens_P
        public List<string> Files = new List<string>();
        public bool Loaded;
        public long Size;
    }

    public class Library
    {
        public readonly string Paks;
        public readonly string Lib;
        public ModSet Mods;

        public Library(string paks, ModSet mods)
        {
            Paks = paks;
            Lib = Path.Combine(new DirectoryInfo(paks).Parent.Parent.FullName, "ModLibrary");   // <game>\Wildfire\ModLibrary
            Mods = mods;
        }

        static bool SafeName(string name)
        {
            return !string.IsNullOrEmpty(name) && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
                && !IsGameFile(name);
        }

        public static bool IsGameFile(string name)
        {
            return name.StartsWith("Wildfire-Windows", StringComparison.OrdinalIgnoreCase) || name.StartsWith("global.", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Wildfire-WindowsNoEditor", StringComparison.OrdinalIgnoreCase);
        }

        public string InPaks(string name) { return Path.Combine(Paks, name); }
        public string InLib(string name) { return Path.Combine(Lib, name); }

        bool Matches(string path, ModFile f)
        {
            try { return File.Exists(path) && string.Equals(Hash.Of(path), f.sha256, StringComparison.OrdinalIgnoreCase); }
            catch (IOException) { return false; }
        }

        public ModState State(ModInfo mod)
        {
            List<ModFile> own = Mods.OwnFiles(mod);
            int inPaks = 0, inLib = 0, good = 0;
            foreach (ModFile f in own)
            {
                bool p = File.Exists(InPaks(f.name)), l = File.Exists(InLib(f.name));
                if (p) inPaks++; else if (l) inLib++;
                if ((p && Matches(InPaks(f.name), f)) || (!p && l && Matches(InLib(f.name), f))) good++;
            }
            if (inPaks == 0 && inLib == 0) return ModState.NotDownloaded;
            if (good < own.Count) return ModState.UpdateAvailable;
            return inPaks == own.Count ? ModState.Loaded : ModState.Available;
        }

        // Whether any of the mod's own files are in the Paks folder (loaded, even if out of date).
        public bool IsLoaded(ModInfo mod)
        {
            return Mods.OwnFiles(mod).Any(f => File.Exists(InPaks(f.name)));
        }

        public long SizeOf(ModInfo mod)
        {
            long total = 0;
            foreach (ModFile f in mod.files)
            {
                if (f.size > 0) { total += f.size; continue; }
                FileInfo p = new FileInfo(InPaks(f.name)), l = new FileInfo(InLib(f.name));
                if (p.Exists) total += p.Length; else if (l.Exists) total += l.Length;
            }
            return total;
        }

        static void MoveFile(string from, string to)
        {
            if (File.Exists(to)) File.Delete(to);
            File.Move(from, to);
            Hash.Moved(from, to);
        }

        // Puts a downloaded mod's files into the Paks folder (they're already checked).
        public void Load(ModInfo mod)
        {
            foreach (ModFile f in mod.files)
            {
                if (!SafeName(f.name)) continue;
                if (!File.Exists(InPaks(f.name)) && File.Exists(InLib(f.name))) MoveFile(InLib(f.name), InPaks(f.name));
            }
        }

        // Moves a mod's files out of the Paks folder into the library, except files a mod that stays loaded still uses.
        public void Unload(ModInfo mod, ICollection<string> alsoUnloading)
        {
            var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ModInfo other in Mods.All)
            {
                if (string.Equals(other.id, mod.id, StringComparison.OrdinalIgnoreCase) || alsoUnloading.Contains(other.id)) continue;
                if (State(other) != ModState.Loaded) continue;
                foreach (ModFile f in other.files) keep.Add(f.name);
            }
            Directory.CreateDirectory(Lib);
            foreach (ModFile f in mod.files)
            {
                if (!SafeName(f.name) || keep.Contains(f.name)) continue;
                if (File.Exists(InPaks(f.name))) MoveFile(InPaks(f.name), InLib(f.name));
            }
        }

        // Deletes a mod's files everywhere, except files another downloaded mod still uses.
        public void Remove(ModInfo mod, ICollection<string> alsoRemoving)
        {
            var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ModInfo other in Mods.All)
            {
                if (string.Equals(other.id, mod.id, StringComparison.OrdinalIgnoreCase) || alsoRemoving.Contains(other.id)) continue;
                if (State(other) == ModState.NotDownloaded) continue;
                foreach (ModFile f in other.files) keep.Add(f.name);
            }
            foreach (ModFile f in mod.files)
            {
                if (!SafeName(f.name) || keep.Contains(f.name)) continue;
                if (File.Exists(InPaks(f.name))) File.Delete(InPaks(f.name));
                if (File.Exists(InLib(f.name))) File.Delete(InLib(f.name));
            }
        }

        // Repair: a mod half in Paks and half in the library is put back together where it's loaded,
        // and a file that's in both places loses its library copy if the Paks one is good. Returns what changed.
        public List<string> Tidy(ModInfo mod)
        {
            var done = new List<string>();
            foreach (ModFile f in mod.files)
            {
                if (!SafeName(f.name) || !File.Exists(InPaks(f.name)) || !File.Exists(InLib(f.name))) continue;
                if (Matches(InPaks(f.name), f)) { File.Delete(InLib(f.name)); done.Add("duplicate " + f.name); }
            }
            // Own files only: a mod it includes (e.g. the tones inside the truck) can rightly be loaded on its own.
            List<ModFile> own = Mods.OwnFiles(mod);
            bool someIn = own.Any(f => File.Exists(InPaks(f.name))), someOut = own.Any(f => !File.Exists(InPaks(f.name)) && File.Exists(InLib(f.name)));
            if (someIn && someOut) { Load(mod); done.Add("put back together"); }
            return done;
        }

        // Old file names a mod supersedes (e.g. the v1 beacon paks) are deleted when it's installed.
        public void DeleteReplaced(ModInfo mod)
        {
            foreach (string old in mod.replaces)
            {
                if (!SafeName(old)) continue;
                if (File.Exists(InPaks(old))) File.Delete(InPaks(old));
                if (File.Exists(InLib(old))) File.Delete(InLib(old));
            }
        }

        // Puts freshly downloaded, checked files in place: into Paks if the mod should load, else the library.
        public void Place(ModInfo mod, string stagedFolder, bool load)
        {
            Directory.CreateDirectory(Lib);
            foreach (ModFile f in mod.files)
            {
                if (!SafeName(f.name)) continue;
                string src = Path.Combine(stagedFolder, f.name);
                // Loading, or the file is loaded already (an update): Paks. Otherwise it stays switched off in the library.
                bool toPaks = load || File.Exists(InPaks(f.name));
                string dest = toPaks ? InPaks(f.name) : InLib(f.name);
                string other = toPaks ? InLib(f.name) : InPaks(f.name);
                if (File.Exists(other)) File.Delete(other);
                File.Copy(src, dest, true);
            }
        }

        // Paks that aren't from the mod list and aren't the game's own: the player's own mods.
        public List<LocalMod> LocalMods()
        {
            HashSet<string> known = Mods.AllFileNames();
            var byStem = new Dictionary<string, LocalMod>(StringComparer.OrdinalIgnoreCase);
            foreach (var place in new[] { Tuple.Create(Paks, true), Tuple.Create(Lib, false) })
            {
                if (!Directory.Exists(place.Item1)) continue;
                foreach (string path in Directory.GetFiles(place.Item1))
                {
                    string name = Path.GetFileName(path), ext = Path.GetExtension(path).ToLowerInvariant();
                    if (ext != ".pak" && ext != ".utoc" && ext != ".ucas" && ext != ".sig") continue;
                    if (IsGameFile(name) || known.Contains(name)) continue;
                    string stem = Path.GetFileNameWithoutExtension(name);
                    LocalMod m;
                    if (!byStem.TryGetValue(stem, out m)) { m = new LocalMod { Id = "local:" + stem, Name = stem }; byStem[stem] = m; }
                    if (!m.Files.Contains(name, StringComparer.OrdinalIgnoreCase)) m.Files.Add(name);
                    if (place.Item2) m.Loaded = true;
                    m.Size += new FileInfo(path).Length;
                }
            }
            return byStem.Values.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public void LoadLocal(LocalMod m)
        {
            foreach (string name in m.Files)
                if (SafeName(name) && File.Exists(InLib(name)) && !File.Exists(InPaks(name))) MoveFile(InLib(name), InPaks(name));
        }

        public void UnloadLocal(LocalMod m)
        {
            Directory.CreateDirectory(Lib);
            foreach (string name in m.Files)
                if (SafeName(name) && File.Exists(InPaks(name))) MoveFile(InPaks(name), InLib(name));
        }

        public void RemoveLocal(LocalMod m)
        {
            foreach (string name in m.Files)
            {
                if (!SafeName(name)) continue;
                if (File.Exists(InPaks(name))) File.Delete(InPaks(name));
                if (File.Exists(InLib(name))) File.Delete(InLib(name));
            }
        }

        // Copies pak sets the player picked (loose files or inside zips) into the library. Returns the stems added.
        public List<string> AddFiles(IEnumerable<string> paths)
        {
            Directory.CreateDirectory(Lib);
            var added = new List<string>();
            foreach (string path in paths)
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".zip")
                {
                    using (ZipArchive zip = ZipFile.OpenRead(path))
                        foreach (ZipArchiveEntry e in zip.Entries)
                        {
                            string n = Path.GetFileName(e.FullName), x = Path.GetExtension(n).ToLowerInvariant();
                            if ((x != ".pak" && x != ".utoc" && x != ".ucas" && x != ".sig") || !SafeName(n)) continue;
                            e.ExtractToFile(InLib(n), true);
                            added.Add(Path.GetFileNameWithoutExtension(n));
                        }
                }
                else if (ext == ".pak" || ext == ".utoc" || ext == ".ucas" || ext == ".sig")
                {
                    string n = Path.GetFileName(path);
                    if (!SafeName(n)) continue;
                    File.Copy(path, InLib(n), true);
                    added.Add(Path.GetFileNameWithoutExtension(n));
                }
            }
            return added.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        // Pulls a mod's files out of its downloaded zip into a folder and checks each against mods.json.
        public static void Unpack(ModInfo mod, string zipPath, string folder)
        {
            Directory.CreateDirectory(folder);
            using (ZipArchive zip = ZipFile.OpenRead(zipPath))
            {
                foreach (ModFile f in mod.files)
                {
                    if (f.name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new InvalidDataException("Bad file name in the mod list: " + f.name);
                    string suffix = "Wildfire/Content/Paks/" + f.name;
                    ZipArchiveEntry entry = zip.Entries.FirstOrDefault(e => e.FullName.Replace('\\', '/').EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
                    if (entry == null) throw new InvalidDataException(mod.name + ": " + f.name + " is missing from the download.");
                    string target = Path.Combine(folder, f.name);
                    entry.ExtractToFile(target, true);
                    if (!string.Equals(Hash.Of(target), f.sha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException(mod.name + ": " + f.name + " doesn't match the mod list, so it wasn't installed. Try again in a few minutes.");
                }
            }
        }
    }
}
