// The launcher window: hosts the WebView2 page (ui\index.html) and does everything the page asks.
// Page -> C#: chrome.webview.postMessage({ cmd, ... }).  C# -> page: { event, data } messages.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace RescueOpsModLauncher
{
    public partial class LauncherForm : Form
    {
        readonly WebView2 view = new WebView2();
        readonly JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        readonly Options opt;
        readonly Settings settings = Settings.Load();
        readonly Timer gameTimer = new Timer { Interval = 2000 };
        readonly Timer devTimer = new Timer { Interval = 20 * 60 * 1000 };     // Mod Dev: new feedback and decisions while open

        ModSet mods;
        Library lib;
        string paks;
        SteamApp app;
        string manifestError;
        LauncherInfo launcherUpdate;
        List<NewsItem> steamNews = new List<NewsItem>(), hubNews = new List<NewsItem>();
        string gameVersion;
        Dictionary<string, object> stats;                // /api/stats "mods": downloads and ratings per mod id
        bool busy, gameRunning, pageReady;
        readonly Dictionary<string, TaskCompletionSource<string>> asks = new Dictionary<string, TaskCompletionSource<string>>();

        public LauncherForm(Options opt)
        {
            this.opt = opt;
            Text = "Rescue Ops Mod Launcher";
            ClientSize = new Size(1280, 820);
            MinimumSize = new Size(1000, 680);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(13, 12, 12);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }
            view.Dock = DockStyle.Fill;
            view.DefaultBackgroundColor = Color.FromArgb(13, 12, 12);
            Controls.Add(view);
            Load += async delegate { await StartView(); };
            gameTimer.Tick += delegate { CheckGame(); };
            devTimer.Tick += async delegate { if (!busy && !gameRunning && !string.IsNullOrEmpty(settings.authorKey)) await DevLoad(); };
        }

        // ---- WebView2 ----

        async Task StartView()
        {
            try
            {
                CoreWebView2Environment.SetLoaderDllFolderPath(Program.ExtractLoader());
                string args = (settings.smoothGraphics ? "" : "--disable-gpu --disable-gpu-compositing ") + (opt.DevTools ? "--remote-debugging-port=9222" : "");
                var envOptions = new CoreWebView2EnvironmentOptions(args.Trim().Length > 0 ? args.Trim() : null);
                var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(Program.DataDir, "WebView2"), envOptions);
                await view.EnsureCoreWebView2Async(env);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "The launcher needs the Microsoft Edge WebView2 Runtime, which comes with Windows 10 and 11.\n\n" +
                    "Install it from https://go.microsoft.com/fwlink/p/?LinkId=2124703 and start the launcher again.\n\n" + ex.Message,
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Close();
                return;
            }
            CoreWebView2Settings s = view.CoreWebView2.Settings;
            s.AreDefaultContextMenusEnabled = opt.DevTools;
            s.AreDevToolsEnabled = opt.DevTools;
            s.IsStatusBarEnabled = false;
            s.IsZoomControlEnabled = false;
            s.AreBrowserAcceleratorKeysEnabled = opt.DevTools;
            view.CoreWebView2.WebMessageReceived += OnMessage;
            view.CoreWebView2.NewWindowRequested += (o, e) => { e.Handled = true; OpenUrl(e.Uri); };
            view.CoreWebView2.SetVirtualHostNameToFolderMapping("launcher.app", opt.UiDir ?? Program.ExtractUi(), CoreWebView2HostResourceAccessKind.Allow);
            view.CoreWebView2.Navigate("https://launcher.app/index.html");
        }

        public void Send(string evt, object data)
        {
            if (view.CoreWebView2 == null) return;
            view.CoreWebView2.PostWebMessageAsJson(json.Serialize(new Dictionary<string, object> { { "event", evt }, { "data", data } }));
        }

        void Status(string text, int percent) { Send("status", new Dictionary<string, object> { { "text", text }, { "percent", percent } }); }

        // Shows a question in the page and waits for the button the player picks (its index, or -1 for cancel).
        async Task<int> Ask(string title, string text, params string[] buttons)
        {
            string id = Guid.NewGuid().ToString("N");
            var tcs = new TaskCompletionSource<string>();
            asks[id] = tcs;
            Send("ask", new Dictionary<string, object> { { "id", id }, { "title", title }, { "text", text }, { "buttons", buttons } });
            string answer = await tcs.Task;
            int i;
            return int.TryParse(answer, out i) ? i : -1;
        }

        void Error(string text) { Log.Write("Error: " + text); Send("error", new Dictionary<string, object> { { "text", text } }); }

        // ---- Messages from the page ----

        async void OnMessage(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            Dictionary<string, object> msg;
            try { msg = json.Deserialize<Dictionary<string, object>>(e.WebMessageAsJson); } catch (Exception) { return; }
            string cmd = Str(msg, "cmd");
            try
            {
                switch (cmd)
                {
                    case "ready": pageReady = true; SendSettings(); await RefreshAll(true); gameTimer.Start(); devTimer.Start(); break;
                    case "answer":
                        TaskCompletionSource<string> tcs;
                        if (asks.TryGetValue(Str(msg, "id"), out tcs)) { asks.Remove(Str(msg, "id")); tcs.TrySetResult(Str(msg, "choice")); }
                        break;
                    case "setOption": SetOption(Str(msg, "key"), msg.ContainsKey("value") ? msg["value"] : null); break;
                    case "refresh": await RefreshAll(false); break;
                    case "load": await Guard(() => LoadMod(Str(msg, "id"))); break;
                    case "unload": await Guard(() => UnloadMod(Str(msg, "id"))); break;
                    case "install": await Guard(() => Install(Ids(msg).Select(mods.Get).Where(m => m != null).ToList(), true)); break;
                    case "update": await Guard(() => Update(Ids(msg))); break;
                    case "remove": await Guard(() => Remove(Str(msg, "id"))); break;
                    case "switchReplaced": await Guard(() => SwitchReplaced(Str(msg, "id"))); break;
                    case "addFiles": await Guard(AddFiles); break;
                    case "play": await Guard(Play); break;
                    case "pickGame": PickGame(); break;
                    case "openUrl": OpenUrl(Str(msg, "url")); break;
                    case "openFolder": OpenFolder(Str(msg, "which")); break;
                    case "markRead": foreach (string id in Ids(msg)) if (!settings.newsRead.Contains(id)) settings.newsRead.Add(id); settings.Save(); break;
                    case "copyDiagnostics": Clipboard.SetText(Diagnostics()); Status(T("Diagnostics copied. Paste them into your bug report.", "Diagnostic copié. Collez-le dans votre signalement."), -1); break;
                    case "selfUpdate": await Guard(DoSelfUpdate); break;
                    case "savePreset": SavePreset(Str(msg, "name"), msg.ContainsKey("ids") ? Ids(msg) : null); break;
                    case "renamePreset": RenamePreset(Str(msg, "id"), Str(msg, "name")); break;
                    case "deletePreset": settings.presets.RemoveAll(p => p.id == Str(msg, "id")); settings.Save(); SendSettings(); break;
                    case "applyPreset": { string pid = Str(msg, "id"); bool andPlay = msg.ContainsKey("play") && msg["play"] is bool && (bool)msg["play"]; await Guard(() => ApplyPreset(pid, andPlay)); break; }
                    case "repair": await Guard(Repair); break;
                    case "register": await Register(Str(msg, "name"), Str(msg, "email"), Str(msg, "modId"), Str(msg, "stars")); break;
                    case "rate": await Rate(Str(msg, "modId"), Str(msg, "stars")); break;
                    case "devLoad": await DevLoad(); break;
                    case "devSetup": await DevSetup(Str(msg, "name")); break;
                    case "devRecover": await DevRecover(Str(msg, "code")); break;
                    case "devNewCode": await DevNewCode(); break;
                    case "devPickFiles": await DevPickFiles(opt.DevTools && msg.ContainsKey("paths") ? StrList(msg, "paths") : null); break;
                    case "devPickImages": await DevPickImages(Str(msg, "which"), opt.DevTools && msg.ContainsKey("paths") ? StrList(msg, "paths") : null); break;
                    case "devSubmit": await DevSubmit(msg); break;
                    case "devWithdraw": await DevSimple("withdraw", new Dictionary<string, object> { { "authorKey", settings.authorKey }, { "id", Convert.ToInt64(Str(msg, "id")) } }); break;
                    case "devSeen": await DevSimple("seen", KeyBody()); break;
                    case "devRead": { var b = KeyBody(); b["ids"] = StrList(msg, "ids").Select(long.Parse).ToList(); await DevSimple("read", b); break; }
                    case "feedback": await PlayerMessage("feedback", msg); break;
                    case "report": await PlayerMessage("report", msg); break;
                    case "drop":
                        {
                            var paths = new List<string>();
                            if (e.AdditionalObjects != null)
                                foreach (object o in e.AdditionalObjects) { var f = o as CoreWebView2File; if (f != null && !string.IsNullOrEmpty(f.Path)) paths.Add(f.Path); }
                            await Dropped(Str(msg, "target"), paths);
                            break;
                        }
                }
            }
            catch (Exception ex) { Error(ex.Message); }
        }

        static string Str(Dictionary<string, object> m, string k) { return m.ContainsKey(k) && m[k] != null ? m[k].ToString() : null; }

        static List<string> Ids(Dictionary<string, object> m)
        {
            var list = new List<string>();
            if (m.ContainsKey("ids") && m["ids"] is System.Collections.ArrayList)
                foreach (object o in (System.Collections.ArrayList)m["ids"]) list.Add(o.ToString());
            else if (m.ContainsKey("id") && m["id"] != null) list.Add(m["id"].ToString());
            return list;
        }

        string T(string en, string fr) { return settings.language == "fr" ? fr : en; }

        // Runs a change to the mod files: one at a time, never while the game is running, then refreshes the list.
        async Task Guard(Func<Task> action)
        {
            if (busy) return;
            if (lib == null || mods == null) { Error(T("The game wasn't found. Choose its folder in Options.", "Jeu introuvable. Choisissez son dossier dans Options.")); return; }
            if (Game.IsRunning()) { Error(T("The game is running. Close it to change your mods.", "Le jeu est lancé. Fermez-le pour modifier vos mods.")); return; }
            busy = true;
            Send("busy", true);
            try { await action(); }
            catch (UnauthorizedAccessException) { Error(T("Windows blocked the change. Close the game, then start the launcher with Run as administrator.", "Windows a bloqué la modification. Fermez le jeu puis lancez le launcher en tant qu'administrateur.")); }
            catch (Exception ex) { Error(ex.Message); Status(T("Something went wrong. Nothing more was changed.", "Un problème est survenu. Rien d'autre n'a été modifié."), -1); }
            finally
            {
                busy = false;
                Send("busy", false);
            }
            await SendState();
        }

        // ---- Loading everything ----

        string ManifestSource { get { return opt.Manifest ?? Program.DefaultManifest; } }

        async Task RefreshAll(bool startup)
        {
            Status(T("Checking for updates...", "Recherche de mises à jour..."), -1);
            FindGame();
            try
            {
                string text = await Net.ReadText(ManifestSource);
                Manifest m = json.Deserialize<Manifest>(text);
                if (m == null || m.mods == null) throw new InvalidDataException("mods.json is empty.");
                await MergeCatalog(m);
                mods = new ModSet(m);
                manifestError = null;
                launcherUpdate = m.launcher != null && SelfUpdate.IsNewer(m.launcher.version) ? m.launcher : null;
                foreach (string id in News.TrackModReleases(settings, mods)) newIds.Add(id);
                // What's new: the first start after the launcher updated itself (or after 1.x, which didn't record it).
                if (startup && settings.lastVersion != Program.AppVersion)
                {
                    bool upgraded = settings.lastVersion.Length > 0 || settings.knownMods.Count > 0;
                    if (upgraded && m.launcher != null && m.launcher.version == Program.AppVersion && m.launcher.notes != null && m.launcher.notes.Count > 0)
                        Send("whatsNew", new Dictionary<string, object> { { "version", Program.AppVersion }, { "notes", m.launcher.notes } });
                    settings.lastVersion = Program.AppVersion;
                    settings.Save();
                }
            }
            catch (Exception ex)
            {
                manifestError = T("Couldn't get the mod list: ", "Impossible de récupérer la liste des mods : ") + ex.Message;
                if (mods == null) mods = new ModSet(new Manifest { mods = new List<ModInfo>() });
            }
            lib = paks != null ? new Library(paks, mods) : null;
            await SendState();
            Status("", -1);

            // News doesn't hold up the mod list.
            try { steamNews = await News.Steam(); gameVersion = News.LatestGameVersion(steamNews); } catch (Exception) { }
            try { hubNews = await News.Hub(); } catch (Exception) { }
            await LoadStats();
            settings.Save();
            SendNews();
            await SendState();
            if (startup && launcherUpdate != null && settings.checkUpdates) Send("launcherUpdate", LauncherUpdateInfo());
            if (!string.IsNullOrEmpty(settings.authorKey)) await DevLoad();
        }

        void FindGame()
        {
            paks = null;
            if (!string.IsNullOrEmpty(opt.Game)) paks = Game.PaksOf(opt.Game);
            if (paks == null && !string.IsNullOrEmpty(settings.gamePath)) paks = Game.PaksOf(settings.gamePath);
            if (paks == null) { List<string> found = Game.Find(); if (found.Count > 0) paks = Game.PaksOf(found[0]); }
            app = paks != null ? Game.AppOf(Game.GameDirOf(paks)) : null;
            if (app != null && !string.IsNullOrEmpty(app.BuildId))
            {
                if (!string.IsNullOrEmpty(settings.lastBuildId) && settings.lastBuildId != app.BuildId) settings.gameUpdated = true;
                settings.lastBuildId = app.BuildId;
                settings.Save();
            }
        }

        void CheckGame()
        {
            bool running = Game.IsRunning();
            if (running == gameRunning) return;
            gameRunning = running;
            Send("gameRunning", running);
            if (!running && settings.afterStart == "minimise" && WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        }

        // ---- State for the page ----

        void SendSettings()
        {
            Send("settings", new Dictionary<string, object> {
                { "version", Program.AppVersion }, { "language", settings.language }, { "afterStart", settings.afterStart },
                { "checkUpdates", settings.checkUpdates }, { "smoothGraphics", settings.smoothGraphics }, { "startPage", settings.startPage }, { "modsView", settings.modsView },
                { "gamePath", settings.gamePath }, { "devtools", opt.DevTools },
                { "profileName", settings.profileName }, { "profileEmail", settings.profileEmail }, { "myRatings", settings.myRatings },
                { "authorName", settings.authorName }, { "isAuthor", !string.IsNullOrEmpty(settings.authorKey) }, { "buildId", app != null ? app.BuildId : null },
                { "presets", settings.presets.Select(p => new Dictionary<string, object> { { "id", p.id }, { "name", p.name }, { "mods", p.mods }, { "lastPlayed", p.lastPlayed } }).ToList() } });
        }

        async Task SendState()
        {
            if (!pageReady) return;
            Dictionary<string, object> state = await Task.Run(() => BuildState());
            Send("state", state);
        }

        Dictionary<string, object> BuildState()
        {
            var list = new List<object>();
            if (mods != null)
                foreach (ModInfo m in mods.All)
                {
                    ModState st = lib != null ? lib.State(m) : ModState.NotDownloaded;
                    ModInfo repl = mods.Get(m.replacedBy);
                    list.Add(new Dictionary<string, object> {
                        { "id", m.id }, { "name", m.name }, { "version", m.version }, { "author", m.author }, { "category", m.category },
                        { "description", m.description }, { "page", m.page }, { "thumb", m.thumb }, { "screenshots", m.screenshots ?? new List<string>() }, { "hidden", m.hidden },
                        { "state", st.ToString() }, { "loaded", lib != null && lib.IsLoaded(m) },
                        { "size", lib != null ? lib.SizeOf(m) : 0L },
                        { "requires", mods.Required(m).Select(r => r.name).ToList() },
                        { "includes", mods.Included(m).Select(r => r.name).ToList() },
                        { "conflicts", mods.Conflicts(m).Select(id => mods.Get(id)).Where(x => x != null).Select(x => x.name).ToList() },
                        { "replacedBy", repl != null ? repl.name : null }, { "replacedById", repl != null ? repl.id : null },
                        { "testedBuild", m.testedBuild }, { "tested", TestedOn(m) }, { "changelog", m.changelog },
                        { "community", m.community }, { "added", m.added }, { "isNew", newIds.Contains(m.id) },
                        { "requiresIds", m.requires }, { "conflictsIds", m.conflicts } });
                }
            var local = new List<object>();
            if (lib != null)
                foreach (LocalMod l in lib.LocalMods())
                    local.Add(new Dictionary<string, object> { { "id", l.Id }, { "name", l.Name }, { "loaded", l.Loaded }, { "size", l.Size }, { "files", l.Files } });
            return new Dictionary<string, object> {
                { "game", new Dictionary<string, object> {
                    { "found", paks != null }, { "paks", paks }, { "library", lib != null ? lib.Lib : null },
                    { "folder", paks != null ? Game.GameDirOf(paks) : null },
                    { "appId", app != null ? app.AppId : null }, { "build", app != null ? app.BuildId : null },
                    { "isDemo", app != null && app.AppId == Game.DemoAppId }, { "version", gameVersion },
                    { "updated", settings.gameUpdated }, { "running", Game.IsRunning() } } },
                { "mods", list }, { "local", local }, { "manifestError", manifestError },
                { "launcherUpdate", launcherUpdate != null ? LauncherUpdateInfo() : null }, { "busy", busy } };
        }

        object LauncherUpdateInfo()
        {
            return new Dictionary<string, object> { { "version", launcherUpdate.version }, { "notes", launcherUpdate.notes ?? new List<string>() } };
        }

        void SendNews()
        {
            var all = steamNews.Concat(hubNews).Concat(settings.modNews)
                .GroupBy(n => n.id).Select(g => g.First())
                .OrderByDescending(n => n.date).Take(25)
                .Select(n => new Dictionary<string, object> {
                    { "id", n.id }, { "kind", n.kind }, { "title", n.title }, { "summary", n.summary }, { "source", n.source },
                    { "url", n.url }, { "date", n.date }, { "unread", !settings.newsRead.Contains(n.id) } }).ToList();
            Send("news", all);
        }

        // ---- Mod changes ----

        // testedBuild in mods.json: the Steam build ids the mod was checked on, comma-separated. Empty = not stated (no warning).
        bool TestedOn(ModInfo m)
        {
            if (string.IsNullOrWhiteSpace(m.testedBuild) || app == null || string.IsNullOrEmpty(app.BuildId)) return true;
            return m.testedBuild.Split(',').Any(b => b.Trim() == app.BuildId);
        }

        bool IsLocal(string id) { return id != null && id.StartsWith("local:", StringComparison.Ordinal); }

        LocalMod Local(string id) { return lib.LocalMods().FirstOrDefault(l => l.Id == id); }

        List<ModInfo> LoadedMods() { return mods.All.Where(m => lib.State(m) == ModState.Loaded).ToList(); }

        async Task LoadMod(string id)
        {
            if (IsLocal(id)) { LocalMod l = Local(id); if (l != null) { lib.LoadLocal(l); Status(T("Loaded ", "Chargé : ") + l.Name + ".", -1); } return; }
            ModInfo mod = mods.Get(id);
            if (mod == null) return;
            ModState st = lib.State(mod);
            if (st == ModState.NotDownloaded || st == ModState.UpdateAvailable) { await Install(new List<ModInfo> { mod }, true); return; }
            List<ModInfo> clashing = await AskClashes(new List<ModInfo> { mod });
            if (clashing == null) return;
            UnloadClashes(clashing);
            List<ModInfo> needed = mods.Required(mod).Where(r => lib.State(r) != ModState.Loaded).ToList();
            List<ModInfo> missing = needed.Where(r => lib.State(r) == ModState.NotDownloaded || lib.State(r) == ModState.UpdateAvailable).ToList();
            if (missing.Count > 0) await Install(missing, true);
            foreach (ModInfo r in needed) lib.Load(r);
            lib.Load(mod);
            Status(T("Loaded ", "Chargé : ") + mod.name + ".", -1);
        }

        async Task UnloadMod(string id)
        {
            if (IsLocal(id)) { LocalMod l = Local(id); if (l != null) { lib.UnloadLocal(l); Status(T("Unloaded ", "Déchargé : ") + l.Name + ".", -1); } return; }
            ModInfo mod = mods.Get(id);
            if (mod == null) return;
            List<ModInfo> deps = mods.Dependents(mod).Where(d => lib.State(d) == ModState.Loaded).ToList();
            if (deps.Count > 0)
            {
                string names = string.Join(", ", deps.Select(d => d.name));
                if (await Ask(T("Also unload?", "Décharger aussi ?"),
                        T(names + " needs " + mod.name + ". Unload " + names + " too?", names + " a besoin de " + mod.name + ". Décharger aussi " + names + " ?"),
                        T("Unload both", "Décharger les deux"), T("Cancel", "Annuler")) != 0) return;
            }
            var all = deps.Concat(new[] { mod }).ToList();
            var ids = new HashSet<string>(all.Select(m => m.id), StringComparer.OrdinalIgnoreCase);
            foreach (ModInfo m in all) lib.Unload(m, ids);
            Status(T("Unloaded ", "Déchargé : ") + string.Join(", ", all.Select(m => m.name)) + ".", -1);
        }

        // Loaded mods that clash with the ones being loaded. Asks first; null means the player kept what they had.
        // The caller unloads them (UnloadClashes) only once the new mods are ready, so a failed download changes nothing.
        async Task<List<ModInfo>> AskClashes(List<ModInfo> loading)
        {
            var loadingIds = new HashSet<string>(loading.Select(m => m.id), StringComparer.OrdinalIgnoreCase);
            List<ModInfo> clashing = loading.SelectMany(m => mods.Conflicts(m)).Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(mods.Get).Where(m => m != null && !loadingIds.Contains(m.id) && lib.IsLoaded(m)).ToList();
            if (clashing.Count == 0) return clashing;
            string mine = string.Join(", ", loading.Select(m => m.name)), theirs = string.Join(", ", clashing.Select(m => m.name));
            int pick = await Ask(T("Mods clash", "Conflit de mods"),
                T(mine + " and " + theirs + " change the same thing, so only one can load.", mine + " et " + theirs + " modifient la même chose : un seul peut être chargé."),
                T("Use ", "Utiliser ") + mine, T("Keep ", "Garder ") + theirs);
            return pick == 0 ? clashing : null;
        }

        void UnloadClashes(List<ModInfo> clashing)
        {
            var ids = new HashSet<string>(clashing.Select(m => m.id), StringComparer.OrdinalIgnoreCase);
            foreach (ModInfo c in clashing) lib.Unload(c, ids);
        }

        // Downloads mods (plus what they need), checks every file, then puts them in place.
        async Task Install(List<ModInfo> picked, bool load)
        {
            if (picked.Count == 0) return;
            var includedByPicked = new HashSet<string>(picked.SelectMany(m => m.includes), StringComparer.OrdinalIgnoreCase);
            List<ModInfo> todo = picked.Where(m => !includedByPicked.Contains(m.id)).ToList();
            var pickedIds = new HashSet<string>(todo.Select(m => m.id).Concat(includedByPicked), StringComparer.OrdinalIgnoreCase);
            List<ModInfo> needed = todo.SelectMany(m => mods.Required(m))
                .Where(r => !pickedIds.Contains(r.id) && lib.State(r) != ModState.Loaded)
                .GroupBy(r => r.id, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
            List<ModInfo> clashing = new List<ModInfo>();
            if (load) { clashing = await AskClashes(todo); if (clashing == null) return; }

            List<ModInfo> download = needed.Where(r => lib.State(r) != ModState.Available).Concat(todo).ToList();
            string work = Path.Combine(Path.GetTempPath(), "RescueOpsModLauncher", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(work);
            try
            {
                var staged = new List<Tuple<ModInfo, string>>();
                for (int i = 0; i < download.Count; i++)
                {
                    ModInfo mod = download[i];
                    string label = T("Downloading ", "Téléchargement de ") + mod.name + (download.Count > 1 ? " (" + (i + 1) + "/" + download.Count + ")" : "");
                    Status(label, 0);
                    string zip = Path.Combine(work, mod.id + ".zip");
                    await Net.Download(ResolveFile(mod.zip), zip, p => Status(label, p));
                    Status(T("Checking ", "Vérification de ") + mod.name + "...", -1);
                    string folder = Path.Combine(work, mod.id);
                    await Task.Run(() => Library.Unpack(mod, zip, folder));
                    staged.Add(Tuple.Create(mod, folder));
                }
                UnloadClashes(clashing);
                foreach (ModInfo mod in download) lib.DeleteReplaced(mod);
                foreach (Tuple<ModInfo, string> s in staged) lib.Place(s.Item1, s.Item2, load);
                if (load) foreach (ModInfo r in needed) lib.Load(r);
                Status(T("Installed ", "Installé : ") + string.Join(", ", todo.Select(m => m.name)) + ".", -1);
                Log.Write("Installed " + string.Join(", ", todo.Select(m => m.name + " " + m.version)));
            }
            finally
            {
                try { Directory.Delete(work, true); } catch (Exception) { }
            }
        }

        async Task Update(List<string> ids)
        {
            List<ModInfo> outdated = (ids.Count > 0 ? ids.Select(mods.Get).Where(m => m != null) : mods.All)
                .Where(m => lib.State(m) == ModState.UpdateAvailable).ToList();
            foreach (ModInfo m in outdated.ToList())
                if (outdated.Any(o => o != m && o.includes.Contains(m.id, StringComparer.OrdinalIgnoreCase))) outdated.Remove(m);
            if (outdated.Count == 0) { Status(T("Everything is up to date.", "Tout est à jour."), -1); return; }
            foreach (ModInfo m in outdated) await Install(new List<ModInfo> { m }, lib.IsLoaded(m));
        }

        async Task Remove(string id)
        {
            if (IsLocal(id))
            {
                LocalMod l = Local(id);
                if (l == null) return;
                if (await Ask(T("Delete mod files", "Supprimer les fichiers"), T("Delete " + l.Name + " from your PC?", "Supprimer " + l.Name + " de votre PC ?"),
                        T("Delete", "Supprimer"), T("Cancel", "Annuler")) == 0) lib.RemoveLocal(l);
                return;
            }
            ModInfo mod = mods.Get(id);
            if (mod == null) return;
            List<ModInfo> deps = mods.Dependents(mod).Where(d => lib.State(d) != ModState.NotDownloaded).ToList();
            string text = T("Delete " + mod.name + " from your PC? You can download it again any time.", "Supprimer " + mod.name + " de votre PC ? Vous pourrez le retélécharger à tout moment.");
            if (deps.Count > 0) text += " " + T(string.Join(", ", deps.Select(d => d.name)) + " needs it and will be deleted too.", string.Join(", ", deps.Select(d => d.name)) + " en a besoin et sera aussi supprimé.");
            if (await Ask(T("Delete mod", "Supprimer le mod"), text, T("Delete", "Supprimer"), T("Cancel", "Annuler")) != 0) return;
            var all = deps.Concat(new[] { mod }).ToList();
            var ids = new HashSet<string>(all.Select(m => m.id), StringComparer.OrdinalIgnoreCase);
            foreach (ModInfo m in all) lib.Remove(m, ids);
            Status(T("Deleted ", "Supprimé : ") + string.Join(", ", all.Select(m => m.name)) + ".", -1);
        }

        async Task SwitchReplaced(string id)
        {
            ModInfo old = mods.Get(id), target = old != null ? mods.Get(old.replacedBy) : null;
            if (old == null || target == null) return;
            bool wasLoaded = lib.IsLoaded(old);
            await Install(new List<ModInfo> { target }, wasLoaded);
            lib.Remove(old, new[] { old.id });
            Status(T("Switched to ", "Remplacé par ") + target.name + ".", -1);
        }

        async Task AddFiles()
        {
            using (var dlg = new OpenFileDialog { Multiselect = true, Title = T("Add mod files", "Ajouter des fichiers de mod"), Filter = T("Mod files", "Fichiers de mod") + " (*.pak;*.utoc;*.ucas;*.zip)|*.pak;*.utoc;*.ucas;*.zip" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                List<string> added = await Task.Run(() => lib.AddFiles(dlg.FileNames));
                Status(added.Count > 0 ? T("Added ", "Ajouté : ") + string.Join(", ", added) + T(". Tick it to load it.", ". Cochez-le pour le charger.") : T("No .pak, .utoc or .ucas files found.", "Aucun fichier .pak, .utoc ou .ucas trouvé."), -1);
            }
        }

        // ---- Presets ----

        // Everything loaded right now, as preset ids.
        List<string> CurrentIds()
        {
            if (lib == null) return new List<string>();
            return mods.All.Where(m => lib.IsLoaded(m)).Select(m => m.id).Concat(lib.LocalMods().Where(l => l.Loaded).Select(l => l.Id)).ToList();
        }

        // Saves the loaded mods (or the ids given, for an imported code). A name that exists already is updated.
        void SavePreset(string name, List<string> ids)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0) return;
            if (name.Length > 60) name = name.Substring(0, 60);
            List<string> list = (ids ?? CurrentIds()).Where(i => !string.IsNullOrEmpty(i)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            Preset p = settings.presets.FirstOrDefault(x => string.Equals(x.name, name, StringComparison.OrdinalIgnoreCase));
            if (p == null) { p = new Preset { id = Guid.NewGuid().ToString("N"), name = name }; settings.presets.Add(p); }
            p.mods = list;
            settings.Save();
            SendSettings();
            Status(T("Saved the preset ", "Préréglage enregistré : ") + name + ".", -1);
        }

        void RenamePreset(string id, string name)
        {
            Preset p = settings.presets.FirstOrDefault(x => x.id == id);
            name = (name ?? "").Trim();
            if (p == null || name.Length == 0) return;
            p.name = name.Length > 60 ? name.Substring(0, 60) : name;
            settings.Save();
            SendSettings();
        }

        // Makes the loaded mods exactly the preset's: downloads what's missing (into the library),
        // then unloads everything else and loads the preset. "base" is the built-in no-mods preset.
        async Task ApplyPreset(string id, bool andPlay)
        {
            Preset p = id == "base" ? new Preset { id = "base", name = T("Base game", "Jeu de base"), mods = new List<string>() } : settings.presets.FirstOrDefault(x => x.id == id);
            if (p == null) return;
            List<LocalMod> locals = lib.LocalMods();
            var wanted = new List<ModInfo>();
            var wantedLocal = new List<LocalMod>();
            var missing = new List<string>();
            foreach (string mid in p.mods)
            {
                if (IsLocal(mid)) { LocalMod l = locals.FirstOrDefault(x => string.Equals(x.Id, mid, StringComparison.OrdinalIgnoreCase)); if (l != null) wantedLocal.Add(l); else missing.Add(mid.Substring(6)); continue; }
                ModInfo m = mods.Get(mid);
                if (m == null) { missing.Add(mid); continue; }
                // A mod that's been replaced loads its replacement instead.
                ModInfo repl = mods.Get(m.replacedBy);
                wanted.Add(repl ?? m);
            }
            // Two mods in the preset that clash: the first one wins.
            foreach (ModInfo m in wanted.ToList())
                if (wanted.Contains(m) && wanted.Any(o => o != m && wanted.IndexOf(o) < wanted.IndexOf(m) && mods.Conflicts(o).Contains(m.id))) { wanted.Remove(m); missing.Add(m.name); }

            var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ModInfo m in wanted) { keep.Add(m.id); foreach (ModInfo r in mods.Required(m)) keep.Add(r.id); foreach (ModInfo i in mods.Included(m)) keep.Add(i.id); }
            List<ModInfo> keepMods = keep.Select(mods.Get).Where(m => m != null).ToList();

            List<ModInfo> download = keepMods.Where(m => { ModState s = lib.State(m); return s == ModState.NotDownloaded || s == ModState.UpdateAvailable; }).ToList();
            if (download.Count > 0) await Install(download, false);

            List<ModInfo> off = mods.All.Where(m => lib.IsLoaded(m) && !keep.Contains(m.id)).ToList();
            var offIds = new HashSet<string>(off.Select(m => m.id), StringComparer.OrdinalIgnoreCase);
            foreach (ModInfo m in off) lib.Unload(m, offIds);
            foreach (LocalMod l in lib.LocalMods().Where(l => l.Loaded && !wantedLocal.Any(w => w.Id == l.Id))) lib.UnloadLocal(l);
            foreach (ModInfo m in keepMods) lib.Load(m);
            foreach (LocalMod l in wantedLocal) lib.LoadLocal(l);

            string text = T("Loaded the preset ", "Préréglage chargé : ") + p.name + ".";
            if (missing.Count > 0) text += " " + T("Not available: ", "Indisponible : ") + string.Join(", ", missing) + ".";
            Status(text, -1);
            if (missing.Count > 0) Log.Write("Preset " + p.name + ": not available: " + string.Join(", ", missing));
            if (andPlay)
            {
                Preset saved = settings.presets.FirstOrDefault(x => x.id == id);
                if (saved != null) { saved.lastPlayed = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds; settings.Save(); SendSettings(); }
                await Play();
            }
        }

        // ---- Ratings and downloads (hub API) ----

        async Task LoadStats()
        {
            try
            {
                var d = json.Deserialize<Dictionary<string, object>>(await Net.ReadText(Api + "stats"));
                stats = d != null && d.ContainsKey("mods") ? d["mods"] as Dictionary<string, object> : null;
            }
            catch (Exception ex) { Log.Write("Stats: " + ex.Message); }
            if (stats != null) Send("stats", stats);
        }

        // Saves name + email with the hub once, then sends the rating the player was giving.
        async Task Register(string name, string email, string modId, string stars)
        {
            try
            {
                await Net.PostJson(Api + "register", json.Serialize(new Dictionary<string, object> {
                    { "installId", settings.installId }, { "name", name }, { "email", email }, { "consent", true } }));
            }
            catch (Exception ex) { Send("registerFailed", ex.Message); return; }
            settings.profileName = (name ?? "").Trim();
            settings.profileEmail = (email ?? "").Trim();
            settings.Save();
            SendSettings();
            Send("registered", true);
            if (!string.IsNullOrEmpty(modId)) await Rate(modId, stars);
        }

        async Task Rate(string modId, string starsText)
        {
            int stars;
            if (string.IsNullOrEmpty(modId) || !int.TryParse(starsText, out stars) || stars < 0 || stars > 5) return;
            try
            {
                string reply = await Net.PostJson(Api + "rate", json.Serialize(new Dictionary<string, object> {
                    { "installId", settings.installId }, { "modId", modId }, { "stars", stars } }));
                var d = json.Deserialize<Dictionary<string, object>>(reply);
                if (stars == 0) settings.myRatings.Remove(modId); else settings.myRatings[modId] = stars;
                settings.Save();
                if (stats == null) stats = new Dictionary<string, object>();
                var entry = stats.ContainsKey(modId) ? stats[modId] as Dictionary<string, object> : null;
                if (entry == null) { entry = new Dictionary<string, object> { { "downloads", 0 } }; stats[modId] = entry; }
                entry["rating"] = d["rating"]; entry["ratings"] = d["ratings"];
                SendSettings();
                Send("stats", stats);
                Status(stars == 0 ? T("Rating removed.", "Note retirée.") : T("Thanks for rating!", "Merci pour votre note !"), -1);
            }
            catch (Exception ex)
            {
                var api = ex as ApiException;
                if (api != null && api.Status == 403 && api.Code == "register")
                {
                    // The hub doesn't know this PC (e.g. its database was reset): ask for the details again.
                    settings.profileName = ""; settings.Save(); SendSettings();
                    Send("needRegister", new Dictionary<string, object> { { "modId", modId }, { "stars", stars } });
                }
                else Error(T("Couldn't save your rating: ", "Impossible d'enregistrer votre note : ") + ex.Message);
            }
        }

        // ---- Repair ----

        // Re-checks every downloaded mod's files from scratch, tidies half-moved mods, re-downloads damaged or
        // missing files, and loads what loaded mods need. Then says what it did.
        async Task Repair()
        {
            Status(T("Checking every mod file...", "Vérification de tous les fichiers..."), -1);
            Hash.Clear();
            var report = new List<string>();
            int checkedCount = 0;
            foreach (ModInfo m in mods.All)
            {
                if (await Task.Run(() => lib.State(m)) == ModState.NotDownloaded) continue;
                checkedCount++;
                List<string> done = lib.Tidy(m);
                if (done.Count > 0) report.Add(m.name + ": " + string.Join(", ", done));
            }
            List<ModInfo> broken = mods.All.Where(m => lib.State(m) == ModState.UpdateAvailable).ToList();
            foreach (ModInfo m in broken.ToList())
                if (broken.Any(o => o != m && o.includes.Contains(m.id, StringComparer.OrdinalIgnoreCase))) broken.Remove(m);
            foreach (ModInfo m in broken)
            {
                await Install(new List<ModInfo> { m }, lib.IsLoaded(m));
                report.Add(m.name + ": " + T("downloaded again", "retéléchargé"));
            }
            foreach (ModInfo m in mods.All.Where(x => lib.State(x) == ModState.Loaded).ToList())
                foreach (ModInfo r in mods.Required(m))
                    if (lib.State(r) != ModState.Loaded) { await LoadMod(r.id); report.Add(r.name + ": " + T("loaded (", "chargé (") + m.name + T(" needs it)", " en a besoin)")); }
            Log.Write("Repair: checked " + checkedCount + " mods. " + (report.Count > 0 ? string.Join("; ", report) : "Nothing to fix."));
            Status("", -1);
            await SendState();
            await Ask(T("Check and repair", "Vérifier et réparer"),
                report.Count == 0 ? T("Checked " + checkedCount + " mods. Every file is fine.", checkedCount + " mods vérifiés. Tous les fichiers sont bons.")
                    : T("Checked " + checkedCount + " mods and fixed:", checkedCount + " mods vérifiés. Réparé :") + "\n• " + string.Join("\n• ", report),
                T("OK", "OK"));
        }

        // ---- Play ----

        async Task Play()
        {
            // Two loaded mods that clash: the player picks one.
            List<ModInfo> loaded = LoadedMods();
            foreach (ModInfo a in loaded)
                foreach (ModInfo b in loaded)
                    if (string.CompareOrdinal(a.id, b.id) < 0 && mods.Conflicts(a).Contains(b.id) && lib.IsLoaded(a) && lib.IsLoaded(b))
                    {
                        int pick = await Ask(T("Mods clash", "Conflit de mods"),
                            T(a.name + " and " + b.name + " change the same thing. Only one can load:", a.name + " et " + b.name + " modifient la même chose. Un seul peut être chargé :"),
                            a.name, b.name, T("Cancel", "Annuler"));
                        if (pick < 0 || pick > 1) return;
                        ModInfo drop = pick == 0 ? b : a;
                        lib.Unload(drop, new[] { drop.id });
                    }
            // Loaded mods that need something that isn't loaded.
            foreach (ModInfo m in LoadedMods())
                foreach (ModInfo r in mods.Required(m))
                    if (lib.State(r) != ModState.Loaded) await LoadMod(r.id);

            string gameDir = Game.GameDirOf(paks);
            if (app != null && !string.IsNullOrEmpty(app.AppId)) Process.Start("steam://rungameid/" + app.AppId);
            else
            {
                string exe = Directory.GetFiles(gameDir, "*.exe").FirstOrDefault(f => Path.GetFileName(f).StartsWith("Wildfire", StringComparison.OrdinalIgnoreCase));
                if (exe == null) { Error(T("Couldn't find the game's exe.", "Exécutable du jeu introuvable.")); return; }
                Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = gameDir });
            }
            Log.Write("Play: " + string.Join(", ", CurrentIds()));
            settings.gameUpdated = false;
            settings.Save();
            Status(T("Starting the game...", "Lancement du jeu..."), -1);
            if (settings.afterStart == "minimise") WindowState = FormWindowState.Minimized;
            else if (settings.afterStart == "close") { await Task.Delay(1500); Close(); }
        }

        // ---- Options and small actions ----

        void SetOption(string key, object value)
        {
            switch (key)
            {
                case "language": settings.language = value as string == "fr" ? "fr" : "en"; break;
                case "afterStart": settings.afterStart = value as string; break;
                case "checkUpdates": settings.checkUpdates = value is bool && (bool)value; break;
                case "smoothGraphics": settings.smoothGraphics = value is bool && (bool)value; break;
                case "startPage": settings.startPage = value as string; break;
                case "modsView": settings.modsView = value as string == "list" ? "list" : "grid"; break;
                case "dismissGameUpdated": settings.gameUpdated = false; break;
            }
            settings.Save();
            SendSettings();
        }

        async void PickGame()
        {
            using (var dlg = new FolderBrowserDialog { Description = T("Pick the game folder. In Steam: right-click Rescue Ops > Manage > Browse local files.", "Choisissez le dossier du jeu. Dans Steam : clic droit sur Rescue Ops > Gérer > Parcourir les fichiers locaux.") })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                if (Game.PaksOf(dlg.SelectedPath) == null) { Error(T("That isn't the game folder. It should contain the Wildfire folder.", "Ce n'est pas le dossier du jeu. Il doit contenir le dossier Wildfire.")); return; }
                settings.gamePath = dlg.SelectedPath;
                settings.Save();
                SendSettings();
                await RefreshAll(false);
            }
        }

        void OpenFolder(string which)
        {
            string path = which == "paks" ? paks : which == "library" && lib != null ? lib.Lib
                : which == "logs" ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Wildfire\Saved\Logs") : null;
            if (path == null) return;
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            Process.Start("explorer.exe", "\"" + path + "\"");
        }

        static void OpenUrl(string url)
        {
            if (string.IsNullOrEmpty(url) || !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return;
            try { Process.Start(url); } catch (Exception) { }
        }

        string ResolveFile(string fileName)
        {
            if (Net.IsWeb(fileName)) return fileName;                 // community mods give a full download URL
            if (Net.IsWeb(ManifestSource)) return new Uri(new Uri(ManifestSource), fileName).ToString();
            return Path.Combine(Path.GetDirectoryName(Path.GetFullPath(ManifestSource)), fileName);
        }

        string Diagnostics()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Rescue Ops Mod Launcher " + Program.AppVersion);
            sb.AppendLine("Game: " + (app != null ? (app.Name + " (app " + app.AppId + ", build " + app.BuildId + ")") : "not found") + (gameVersion != null ? ", latest patch " + gameVersion : ""));
            sb.AppendLine("Windows: " + Environment.OSVersion.VersionString);
            if (manifestError != null) sb.AppendLine("Mod list: " + manifestError);
            if (lib != null)
            {
                sb.AppendLine("Game folder: " + Game.GameDirOf(paks));
                sb.AppendLine("Loaded: " + string.Join(", ", mods.All.Where(m => lib.State(m) == ModState.Loaded).Select(m => m.name + " " + m.version)));
                List<string> odd = mods.All.Select(m => new { m, s = lib.State(m) }).Where(x => x.s == ModState.UpdateAvailable || (x.s == ModState.Available && lib.IsLoaded(x.m)))
                    .Select(x => x.m.name + (x.s == ModState.UpdateAvailable ? " (old or damaged files)" : " (half loaded)")).ToList();
                if (odd.Count > 0) sb.AppendLine("Needs attention: " + string.Join(", ", odd));
                List<string> untested = mods.All.Where(m => lib.IsLoaded(m) && !TestedOn(m)).Select(m => m.name).ToList();
                if (untested.Count > 0) sb.AppendLine("Not checked on this game build: " + string.Join(", ", untested));
                List<LocalMod> local = lib.LocalMods().Where(l => l.Loaded).ToList();
                if (local.Count > 0) sb.AppendLine("Other paks loaded: " + string.Join(", ", local.Select(l => l.Name)));
                try
                {
                    sb.AppendLine("Paks folder:");
                    foreach (FileInfo f in new DirectoryInfo(paks).GetFiles().OrderBy(f => f.Name)) sb.AppendLine("  " + f.Name + "  " + f.Length);
                    string[] sub = Directory.GetDirectories(paks);
                    if (sub.Length > 0) sb.AppendLine("  Subfolders (the game loads these too): " + string.Join(", ", sub.Select(Path.GetFileName)));
                }
                catch (Exception) { }
            }
            List<string> log = Log.Tail(15);
            if (log.Count > 0) { sb.AppendLine("Recent launcher log:"); foreach (string l in log) sb.AppendLine("  " + l); }
            return sb.ToString();
        }

        async Task DoSelfUpdate()
        {
            if (launcherUpdate == null) return;
            Status(T("Downloading launcher ", "Téléchargement du launcher ") + launcherUpdate.version, 0);
            await SelfUpdate.Apply(launcherUpdate, p => Status(T("Downloading launcher ", "Téléchargement du launcher ") + launcherUpdate.version, p));
            Process.Start(new ProcessStartInfo(Program.ExePath, opt.Raw) { UseShellExecute = false });
            Application.Exit();
        }
    }
}
