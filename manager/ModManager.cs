// Rescue Ops Mod Manager
// Installs, updates and removes Tonnie's Rescue Ops: Wildfire mods.
//
// It reads mods.json from the latest GitHub release, which lists every mod's files and their
// SHA-256 hashes. It compares those with the files in the game's Wildfire\Content\Paks folder
// to show what's installed, downloads the zips it needs, checks every file's hash, then copies
// the files in. It only ever adds or deletes files named in mods.json: never the game's own.
//
// Build: build.bat (uses the C# compiler that comes with Windows, .NET Framework 4.8).
// Test switches: --manifest <url or path to mods.json>  --game <game folder or Paks folder>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Rescue Ops Mod Manager")]
[assembly: AssemblyProduct("Rescue Ops Mod Manager")]
[assembly: AssemblyDescription("Installs, updates and removes Tonnie's Rescue Ops: Wildfire mods")]
[assembly: AssemblyCompany("Tonnie")]
[assembly: AssemblyCopyright("Tonnie")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace RescueOpsModManager
{
    // ---- mods.json ---------------------------------------------------------------------------

    public class Manifest
    {
        public int schema { get; set; }
        public ManagerInfo manager { get; set; }
        public List<ModInfo> mods { get; set; }
    }

    public class ManagerInfo
    {
        public string version { get; set; }
        public string download { get; set; }
    }

    public class ModInfo
    {
        public string id { get; set; }
        public string name { get; set; }
        public string version { get; set; }
        public string zip { get; set; }
        public string page { get; set; }
        public string description { get; set; }
        public List<string> includes { get; set; }
        public List<string> conflicts { get; set; }
        public List<ModFile> files { get; set; }
    }

    public class ModFile
    {
        public string name { get; set; }
        public string sha256 { get; set; }
    }

    public enum ModState { NotInstalled, Installed, UpdateAvailable }

    // ---- Program -----------------------------------------------------------------------------

    static class Program
    {
        public const string AppVersion = "1.0.0";
        public const string DefaultManifest = "https://github.com/TonnieGit/rescueopsmodding/releases/latest/download/mods.json";
        public const string Patreon = "https://patreon.com/Tonnie001";
        public const string Website = "https://rescueopsmodding.vercel.app/mods/";

        [STAThread]
        static void Main(string[] args)
        {
            string manifest = DefaultManifest;
            string game = null;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--manifest") manifest = args[i + 1];
                else if (args[i] == "--game") game = args[i + 1];
            }

            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(manifest, game));
        }
    }

    // ---- Finding the game --------------------------------------------------------------------

    static class Game
    {
        static readonly string[] ProcessNames = { "WildfireDemo", "WildfireDemo-Win64-Shipping", "Wildfire", "Wildfire-Win64-Shipping" };

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

        // Every copy of the game in every Steam library on this PC.
        public static List<string> Find()
        {
            var libraries = new List<string>();
            string steam = null;
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
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

            var games = new List<string>();
            foreach (string library in libraries)
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

    static class Hash
    {
        // Cached by path + size + time, cleared after every install or uninstall.
        static readonly Dictionary<string, Tuple<long, DateTime, string>> cache = new Dictionary<string, Tuple<long, DateTime, string>>();

        public static void Clear() { cache.Clear(); }

        public static string Of(string path)
        {
            var info = new FileInfo(path);
            string key = info.FullName.ToLowerInvariant();
            Tuple<long, DateTime, string> hit;
            if (cache.TryGetValue(key, out hit) && hit.Item1 == info.Length && hit.Item2 == info.LastWriteTimeUtc) return hit.Item3;

            string hash;
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
            cache[key] = Tuple.Create(info.Length, info.LastWriteTimeUtc, hash);
            return hash;
        }
    }

    // ---- Which files belong to which mod -----------------------------------------------------

    class ModSet
    {
        public readonly Manifest Manifest;
        readonly Dictionary<string, ModInfo> byId;

        public ModSet(Manifest manifest)
        {
            Manifest = manifest;
            foreach (ModInfo m in manifest.mods)
            {
                if (m.includes == null) m.includes = new List<string>();
                if (m.conflicts == null) m.conflicts = new List<string>();
                if (m.files == null) m.files = new List<ModFile>();
            }
            byId = manifest.mods.ToDictionary(m => m.id, StringComparer.OrdinalIgnoreCase);
        }

        public ModInfo Get(string id)
        {
            ModInfo m;
            return byId.TryGetValue(id, out m) ? m : null;
        }

        public IEnumerable<ModInfo> Included(ModInfo mod)
        {
            return mod.includes.Select(Get).Where(m => m != null);
        }

        // The files that are this mod's alone, not one of the mods it includes.
        public List<ModFile> OwnFiles(ModInfo mod)
        {
            var included = new HashSet<string>(Included(mod).SelectMany(m => m.files).Select(f => f.name), StringComparer.OrdinalIgnoreCase);
            List<ModFile> own = mod.files.Where(f => !included.Contains(f.name)).ToList();
            return own.Count > 0 ? own : mod.files;
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
            foreach (ModInfo other in Manifest.mods)
                if (other.conflicts.Any(c => mine.Contains(c))) result.Add(other.id);
            result.ExceptWith(mine);
            return result;
        }

        public ModState State(ModInfo mod, string paks)
        {
            List<ModFile> own = OwnFiles(mod);
            int present = 0;
            bool allMatch = true;
            foreach (ModFile f in own)
            {
                string path = Path.Combine(paks, f.name);
                if (!File.Exists(path)) { allMatch = false; continue; }
                present++;
                try
                {
                    if (!string.Equals(Hash.Of(path), f.sha256, StringComparison.OrdinalIgnoreCase)) allMatch = false;
                }
                catch (IOException) { allMatch = false; }
            }
            if (present == 0) return ModState.NotInstalled;
            return allMatch ? ModState.Installed : ModState.UpdateAvailable;
        }

        // Every file name any mod in mods.json uses. These are the only files the manager deletes.
        public IEnumerable<string> AllFileNames()
        {
            return Manifest.mods.SelectMany(m => m.files).Select(f => f.name).Distinct(StringComparer.OrdinalIgnoreCase);
        }
    }

    // ---- Window ------------------------------------------------------------------------------

    class MainForm : Form
    {
        static readonly Color Red = Color.FromArgb(0xE9, 0x21, 0x25);
        static readonly Color Green = Color.FromArgb(0x1B, 0x7F, 0x3B);
        static readonly Color Amber = Color.FromArgb(0xB3, 0x5C, 0x00);

        readonly string manifestSource;
        readonly string gameOverride;
        ModSet mods;
        string paks;
        bool busy;

        readonly ComboBox gameBox = new ComboBox();
        readonly Button browseButton = new Button();
        readonly ListView list = new ListView();
        readonly Label descriptionLabel = new Label();
        readonly LinkLabel pageLink = new LinkLabel();
        readonly Button installButton = new Button();
        readonly Button uninstallButton = new Button();
        readonly Button uninstallAllButton = new Button();
        readonly Button openFolderButton = new Button();
        readonly ProgressBar progress = new ProgressBar();
        readonly Label statusLabel = new Label();
        readonly LinkLabel updateLink = new LinkLabel();

        static string SettingsFile
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"RescueOpsModManager\game.txt"); }
        }

        public MainForm(string manifestSource, string gameOverride)
        {
            this.manifestSource = manifestSource;
            this.gameOverride = gameOverride;
            BuildWindow();
        }

        void BuildWindow()
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "Rescue Ops Mod Manager";
            Font = new Font("Segoe UI", 9.75F);
            ClientSize = new Size(820, 560);
            MinimumSize = new Size(680, 460);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.White;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }

            // Header
            var header = new Panel { Dock = DockStyle.Top, Height = 70, BackColor = Red, Padding = new Padding(16, 10, 16, 8) };
            var title = new Label { Text = "RESCUE OPS MOD MANAGER", ForeColor = Color.White, Font = new Font("Segoe UI", 17F, FontStyle.Bold), AutoSize = true, Location = new Point(14, 8) };
            var subtitle = new Label { Text = "Mods for Rescue Ops: Wildfire by Tonnie  ·  v" + Program.AppVersion, ForeColor = Color.White, AutoSize = true, Location = new Point(17, 42) };
            updateLink.AutoSize = true;
            updateLink.Visible = false;
            updateLink.LinkColor = Color.White;
            updateLink.ActiveLinkColor = Color.White;
            updateLink.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold);
            updateLink.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            updateLink.Location = new Point(560, 42);
            header.Controls.AddRange(new Control[] { title, subtitle, updateLink });

            // Game folder row
            var gameRow = new TableLayoutPanel { Dock = DockStyle.Top, Height = 46, ColumnCount = 3, Padding = new Padding(12, 10, 12, 4) };
            gameRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            gameRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            gameRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var gameLabel = new Label { Text = "Game:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 6, 0) };
            gameBox.DropDownStyle = ComboBoxStyle.DropDownList;
            gameBox.Dock = DockStyle.Fill;
            browseButton.Text = "Browse...";
            browseButton.AutoSize = true;
            gameRow.Controls.Add(gameLabel, 0, 0);
            gameRow.Controls.Add(gameBox, 1, 0);
            gameRow.Controls.Add(browseButton, 2, 0);

            // Mod list
            list.View = View.Details;
            list.CheckBoxes = true;
            list.FullRowSelect = true;
            list.MultiSelect = false;
            list.HideSelection = false;
            list.Dock = DockStyle.Fill;
            list.Columns.Add("Mod", 300);
            list.Columns.Add("Installed", 110);
            list.Columns.Add("Latest", 80);
            list.Columns.Add("Status", 160);
            var listHolder = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15, 6, 15, 0) };
            listHolder.Controls.Add(list);

            // Description of the selected mod
            var info = new Panel { Dock = DockStyle.Bottom, Height = 58, Padding = new Padding(15, 6, 15, 0) };
            descriptionLabel.Dock = DockStyle.Top;
            descriptionLabel.Height = 24;
            descriptionLabel.Text = "Tick the mods you want, then click a button below.";
            pageLink.Dock = DockStyle.Top;
            pageLink.Text = "Open the mod's page";
            pageLink.Visible = false;
            info.Controls.Add(pageLink);
            info.Controls.Add(descriptionLabel);

            // Buttons
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 46, Padding = new Padding(12, 6, 12, 0), WrapContents = false };
            installButton.Text = "Install / update ticked";
            uninstallButton.Text = "Uninstall ticked";
            uninstallAllButton.Text = "Uninstall all mods";
            openFolderButton.Text = "Open mods folder";
            foreach (Button b in new[] { installButton, uninstallButton, uninstallAllButton, openFolderButton })
            {
                b.AutoSize = true;
                b.Padding = new Padding(6, 2, 6, 2);
                b.Margin = new Padding(3, 3, 8, 3);
                buttons.Controls.Add(b);
            }
            installButton.BackColor = Red;
            installButton.ForeColor = Color.White;
            installButton.FlatStyle = FlatStyle.Flat;
            installButton.FlatAppearance.BorderColor = Red;

            // Status bar
            var footer = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 40, ColumnCount = 3, Padding = new Padding(12, 4, 12, 8) };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            statusLabel.AutoEllipsis = true;
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            progress.Dock = DockStyle.Fill;
            progress.Visible = false;
            var patreon = new LinkLabel { Text = "Support my mods on Patreon", AutoSize = true, Anchor = AnchorStyles.Right, Margin = new Padding(10, 6, 3, 0) };
            patreon.LinkClicked += delegate { OpenUrl(Program.Patreon); };
            footer.Controls.Add(statusLabel, 0, 0);
            footer.Controls.Add(progress, 1, 0);
            footer.Controls.Add(patreon, 2, 0);

            Controls.Add(listHolder);
            Controls.Add(info);
            Controls.Add(buttons);
            Controls.Add(footer);
            Controls.Add(gameRow);
            Controls.Add(header);

            gameBox.SelectedIndexChanged += delegate { OnGameChanged(); };
            browseButton.Click += delegate { BrowseForGame(); };
            list.SelectedIndexChanged += delegate { ShowSelected(); };
            pageLink.LinkClicked += delegate
            {
                if (list.SelectedItems.Count > 0) OpenUrl(((ModInfo)list.SelectedItems[0].Tag).page);
            };
            installButton.Click += async delegate { await RunAction(InstallTicked); };
            uninstallButton.Click += async delegate { await RunAction(UninstallTicked); };
            uninstallAllButton.Click += async delegate { await RunAction(UninstallAll); };
            openFolderButton.Click += delegate { if (paks != null) Process.Start("explorer.exe", "\"" + paks + "\""); };
            Load += async delegate { await LoadEverything(); };
            Resize += delegate { updateLink.Left = header.ClientSize.Width - updateLink.Width - 16; };

            ResumeLayout(false);
            PerformLayout();

            // Column widths aren't scaled with the rest of the window.
            float scale = DeviceDpi / 96F;
            if (scale > 1.01F) foreach (ColumnHeader c in list.Columns) c.Width = (int)(c.Width * scale);
            UpdateButtons();
        }

        // ---- Loading ----

        async Task LoadEverything()
        {
            FindGames();
            SetStatus("Checking for mods...");
            try
            {
                string json = await ReadText(manifestSource);
                Manifest manifest = new JavaScriptSerializer().Deserialize<Manifest>(json);
                if (manifest == null || manifest.mods == null || manifest.mods.Count == 0) throw new InvalidDataException("The mod list is empty.");
                if (manifest.schema > 1) throw new InvalidDataException("This version of the mod manager is too old to read the mod list. Download the new one.");
                mods = new ModSet(manifest);
                ShowManagerUpdate(manifest.manager);
                RefreshList();
            }
            catch (Exception ex)
            {
                SetStatus("Couldn't load the mod list.");
                MessageBox.Show(this, "Couldn't load the list of mods from GitHub. Check your internet connection and open the mod manager again.\n\n" + ex.Message,
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            UpdateButtons();
        }

        void ShowManagerUpdate(ManagerInfo manager)
        {
            if (manager == null || string.IsNullOrEmpty(manager.version)) return;
            Version latest, mine;
            if (!Version.TryParse(manager.version, out latest) || !Version.TryParse(Program.AppVersion, out mine) || latest <= mine) return;
            updateLink.Text = "New mod manager v" + manager.version + " - download";
            updateLink.Visible = true;
            updateLink.Left = updateLink.Parent.ClientSize.Width - updateLink.Width - 16;
            string url = string.IsNullOrEmpty(manager.download) ? Program.Website : manager.download;
            updateLink.LinkClicked += delegate { OpenUrl(url); };
        }

        void FindGames()
        {
            gameBox.Items.Clear();
            var folders = new List<string>();
            if (gameOverride != null)
            {
                folders.Add(gameOverride);
            }
            else
            {
                folders.AddRange(Game.Find());
                try
                {
                    if (File.Exists(SettingsFile))
                    {
                        string saved = File.ReadAllText(SettingsFile).Trim();
                        if (Game.PaksOf(saved) != null && !folders.Any(f => string.Equals(f, saved, StringComparison.OrdinalIgnoreCase)))
                            folders.Insert(0, saved);
                        else if (folders.Remove(saved))
                            folders.Insert(0, saved);
                    }
                }
                catch (Exception) { }
            }

            foreach (string f in folders) gameBox.Items.Add(f);
            if (gameBox.Items.Count > 0) gameBox.SelectedIndex = 0;
            else
            {
                paks = null;
                SetStatus("Couldn't find the game. Click Browse and pick its folder.");
            }
        }

        void OnGameChanged()
        {
            paks = Game.PaksOf(gameBox.SelectedItem as string);
            if (paks != null && gameOverride == null)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile));
                    File.WriteAllText(SettingsFile, (string)gameBox.SelectedItem);
                }
                catch (Exception) { }
            }
            RefreshList();
            UpdateButtons();
        }

        void BrowseForGame()
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Pick the game folder. In Steam: right-click Rescue Ops: Wildfire > Manage > Browse local files shows it. It contains WildfireDemo.exe.";
                dialog.ShowNewFolderButton = false;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                if (Game.PaksOf(dialog.SelectedPath) == null)
                {
                    MessageBox.Show(this, "That isn't the game folder: it has no Wildfire\\Content\\Paks\\Wildfire-Windows.pak.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                string chosen = dialog.SelectedPath.TrimEnd('\\');
                int index = gameBox.Items.Add(chosen);
                gameBox.SelectedIndex = index;
            }
        }

        // ---- The list ----

        void RefreshList()
        {
            Hash.Clear();
            var ticked = new HashSet<string>(list.CheckedItems.Cast<ListViewItem>().Select(i => ((ModInfo)i.Tag).id));
            string selected = list.SelectedItems.Count > 0 ? ((ModInfo)list.SelectedItems[0].Tag).id : null;

            list.BeginUpdate();
            list.Items.Clear();
            if (mods != null)
            {
                int updates = 0, installed = 0;
                foreach (ModInfo mod in mods.Manifest.mods)
                {
                    ModState state = paks == null ? ModState.NotInstalled : mods.State(mod, paks);
                    string installedText = state == ModState.Installed ? "v" + mod.version : state == ModState.UpdateAvailable ? "other version" : "-";
                    string statusText = paks == null ? "" : state == ModState.Installed ? "Installed" : state == ModState.UpdateAvailable ? "Update available" : "Not installed";
                    var item = new ListViewItem(new[] { mod.name, installedText, "v" + mod.version, statusText }) { Tag = mod, UseItemStyleForSubItems = false };
                    item.SubItems[3].ForeColor = state == ModState.Installed ? Green : state == ModState.UpdateAvailable ? Amber : SystemColors.GrayText;
                    if (state == ModState.UpdateAvailable) item.SubItems[3].Font = new Font(list.Font, FontStyle.Bold);
                    item.Checked = ticked.Contains(mod.id);
                    item.Selected = mod.id == selected;
                    list.Items.Add(item);
                    if (state == ModState.Installed) installed++;
                    if (state == ModState.UpdateAvailable) updates++;
                }
                if (paks != null)
                    SetStatus(installed + " installed" + (updates > 0 ? ", " + updates + " with an update available" : "") + ".");
            }
            list.EndUpdate();
            ShowSelected();
        }

        void ShowSelected()
        {
            if (list.SelectedItems.Count == 0)
            {
                descriptionLabel.Text = "Tick the mods you want, then click a button below.";
                pageLink.Visible = false;
                return;
            }
            var mod = (ModInfo)list.SelectedItems[0].Tag;
            string text = mod.description;
            List<string> included = mods.Included(mod).Select(m => m.name).ToList();
            if (included.Count > 0) text += "  Includes " + string.Join(" and ", included) + ".";
            descriptionLabel.Text = text;
            pageLink.Visible = !string.IsNullOrEmpty(mod.page);
        }

        List<ModInfo> Ticked()
        {
            return list.CheckedItems.Cast<ListViewItem>().Select(i => (ModInfo)i.Tag).ToList();
        }

        // ---- Actions ----

        async Task RunAction(Func<Task> action)
        {
            if (busy || mods == null || paks == null) return;
            while (Game.IsRunning())
            {
                if (MessageBox.Show(this, "The game is running. Close it, then click Retry.", Text, MessageBoxButtons.RetryCancel, MessageBoxIcon.Information) != DialogResult.Retry)
                    return;
            }
            busy = true;
            UpdateButtons();
            try
            {
                await action();
            }
            catch (UnauthorizedAccessException)
            {
                ShowError("Windows blocked the change. Make sure the game is closed, then close this window, right-click the mod manager and choose Run as administrator.");
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
            finally
            {
                busy = false;
                progress.Visible = false;
                RefreshList();
                UpdateButtons();
            }
        }

        async Task InstallTicked()
        {
            List<ModInfo> ticked = Ticked();
            if (ticked.Count == 0) { Tell("Tick the mods you want to install first."); return; }

            // Two ticked mods that change the same thing.
            foreach (ModInfo a in ticked)
                foreach (ModInfo b in ticked)
                    if (string.CompareOrdinal(a.id, b.id) < 0 && mods.Conflicts(a).Contains(b.id))
                    {
                        Tell(a.name + " and " + b.name + " change the same lights, so you can only use one of them. Untick one.");
                        return;
                    }

            // A ticked mod that comes with another ticked mod doesn't need its own download.
            var includedByTicked = new HashSet<string>(ticked.SelectMany(m => m.includes), StringComparer.OrdinalIgnoreCase);
            List<ModInfo> toInstall = ticked.Where(m => !includedByTicked.Contains(m.id)).ToList();

            // Installed mods that clash with what's being installed.
            var clashIds = new HashSet<string>(toInstall.SelectMany(m => mods.Conflicts(m)), StringComparer.OrdinalIgnoreCase);
            List<ModInfo> clashing = clashIds.Select(mods.Get).Where(m => m != null && mods.State(m, paks) != ModState.NotInstalled).ToList();
            if (clashing.Count > 0)
            {
                string names = string.Join(", ", clashing.Select(m => m.name));
                if (MessageBox.Show(this, "You have " + names + " installed. It changes the same lights as what you're installing, so only one can be used.\n\nRemove " + names + " and continue?",
                        Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
            }

            string work = Path.Combine(Path.GetTempPath(), "RescueOpsModManager", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(work);
            try
            {
                // Download and check everything before touching the game folder.
                var staged = new List<Tuple<ModInfo, string>>();
                for (int i = 0; i < toInstall.Count; i++)
                {
                    ModInfo mod = toInstall[i];
                    string zipPath = Path.Combine(work, mod.zip);
                    SetStatus("Downloading " + mod.name + " (" + (i + 1) + " of " + toInstall.Count + ")...");
                    await Download(Resolve(mod.zip), zipPath);

                    SetStatus("Checking " + mod.name + "...");
                    string folder = Path.Combine(work, mod.id);
                    await Task.Run(() => Unpack(mod, zipPath, folder));
                    staged.Add(Tuple.Create(mod, folder));
                }

                foreach (ModInfo clash in clashing)
                {
                    SetStatus("Removing " + clash.name + "...");
                    foreach (ModFile f in clash.files) DeleteModFile(f.name);
                }

                foreach (Tuple<ModInfo, string> s in staged)
                {
                    SetStatus("Installing " + s.Item1.name + "...");
                    foreach (ModFile f in s.Item1.files)
                        File.Copy(Path.Combine(s.Item2, f.name), Path.Combine(paks, f.name), true);
                }

                foreach (ListViewItem item in list.Items) item.Checked = false;
                string done = string.Join(", ", toInstall.Select(m => m.name));
                SetStatus("Installed " + done + ".");
                Tell("Installed " + done + ". Start the game to use " + (toInstall.Count == 1 ? "it." : "them."));
            }
            finally
            {
                try { Directory.Delete(work, true); } catch (Exception) { }
            }
        }

        async Task UninstallTicked()
        {
            List<ModInfo> ticked = Ticked();
            if (ticked.Count == 0) { Tell("Tick the mods you want to uninstall first."); return; }

            List<string> names = ticked.Select(m => m.name).ToList();
            List<string> alsoRemoved = ticked.SelectMany(m => mods.Included(m))
                .Where(inc => !ticked.Contains(inc) && mods.State(inc, paks) != ModState.NotInstalled)
                .Select(m => m.name).Distinct().ToList();
            string message = "Uninstall " + string.Join(", ", names) + "?";
            if (alsoRemoved.Count == 1) message += "\n\nThis also removes " + alsoRemoved[0] + ", which comes with it. You can install it again on its own.";
            else if (alsoRemoved.Count > 1) message += "\n\nThis also removes " + string.Join(" and ", alsoRemoved) + ", which come with it. You can install them again on their own.";
            if (MessageBox.Show(this, message, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            int removed = 0;
            foreach (ModInfo mod in ticked)
                foreach (ModFile f in mod.files)
                    if (DeleteModFile(f.name)) removed++;

            foreach (ListViewItem item in list.Items) item.Checked = false;
            SetStatus("Uninstalled " + string.Join(", ", names) + ".");
            await Task.FromResult(removed);
        }

        async Task UninstallAll()
        {
            List<string> present = mods.AllFileNames().Where(n => File.Exists(Path.Combine(paks, n))).ToList();
            if (present.Count == 0) { Tell("None of these mods are installed."); return; }
            if (MessageBox.Show(this, "Remove all " + present.Count + " mod files listed here from the game?\n\nThe game's own files aren't touched.",
                    Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            foreach (string name in present) DeleteModFile(name);
            SetStatus("All mods uninstalled.");
            await Task.FromResult(0);
        }

        // Deletes one mod file from the Paks folder. Only ever called with names from mods.json.
        bool DeleteModFile(string name)
        {
            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.StartsWith("Wildfire-Windows", StringComparison.OrdinalIgnoreCase) || name.StartsWith("global.", StringComparison.OrdinalIgnoreCase))
                return false;
            string path = Path.Combine(paks, name);
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }

        // Pulls the mod's files out of its zip and checks each one against mods.json.
        static void Unpack(ModInfo mod, string zipPath, string folder)
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

        // ---- Downloads ----

        bool IsWeb(string source)
        {
            return source.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || source.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }

        // A file next to mods.json: the same release on GitHub, or the same folder when testing.
        string Resolve(string fileName)
        {
            if (IsWeb(manifestSource)) return new Uri(new Uri(manifestSource), fileName).ToString();
            return Path.Combine(Path.GetDirectoryName(Path.GetFullPath(manifestSource)), fileName);
        }

        WebClient NewClient()
        {
            var client = new WebClient { Encoding = Encoding.UTF8 };
            client.Headers[HttpRequestHeader.UserAgent] = "RescueOpsModManager/" + Program.AppVersion;
            client.Headers[HttpRequestHeader.CacheControl] = "no-cache";
            return client;
        }

        async Task<string> ReadText(string source)
        {
            if (!IsWeb(source)) return File.ReadAllText(source, Encoding.UTF8);
            using (WebClient client = NewClient()) return await client.DownloadStringTaskAsync(source);
        }

        async Task Download(string source, string target)
        {
            progress.Value = 0;
            progress.Visible = true;
            if (!IsWeb(source))
            {
                await Task.Run(() => File.Copy(source, target, true));
                progress.Value = 100;
                return;
            }
            using (WebClient client = NewClient())
            {
                client.DownloadProgressChanged += (s, e) => { progress.Value = Math.Max(0, Math.Min(100, e.ProgressPercentage)); };
                await client.DownloadFileTaskAsync(new Uri(source), target);
            }
        }

        // ---- Small helpers ----

        void UpdateButtons()
        {
            bool ready = !busy && mods != null && paks != null;
            installButton.Enabled = ready;
            uninstallButton.Enabled = ready;
            uninstallAllButton.Enabled = ready;
            openFolderButton.Enabled = !busy && paks != null;
            gameBox.Enabled = !busy;
            browseButton.Enabled = !busy && gameOverride == null;
            list.Enabled = !busy;
        }

        void SetStatus(string text) { statusLabel.Text = text; }

        void Tell(string text) { MessageBox.Show(this, text, Text, MessageBoxButtons.OK, MessageBoxIcon.Information); }

        void ShowError(string text)
        {
            SetStatus("Something went wrong. Nothing more was changed.");
            MessageBox.Show(this, text, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        static void OpenUrl(string url)
        {
            if (string.IsNullOrEmpty(url) || !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return;
            try { Process.Start(url); } catch (Exception) { }
        }
    }
}
