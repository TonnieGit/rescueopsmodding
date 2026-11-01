// Settings, downloads, news (Steam + modding hub + mod releases) and self-update.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace RescueOpsModLauncher
{
    // ---- Settings (%LOCALAPPDATA%\RescueOpsModLauncher\state.json) -----------------------------

    public class Settings
    {
        public string language { get; set; }            // "" until the player picks, then "en" or "fr"
        public string gamePath { get; set; }             // chosen game folder, or "" to find it through Steam
        public string afterStart { get; set; }           // "minimise", "close" or "none"
        public bool checkUpdates { get; set; }
        public bool smoothGraphics { get; set; }         // false = WebView2 without GPU, flat look
        public string startPage { get; set; }            // "dashboard" or "mods"
        public string lastBuildId { get; set; }          // game build when the launcher last saw it
        public bool gameUpdated { get; set; }            // the build changed since mods were last played
        public List<string> newsRead { get; set; }
        public Dictionary<string, string> knownMods { get; set; }   // id -> version, for "new mod" news
        public List<NewsItem> modNews { get; set; }

        public Settings()
        {
            language = ""; gamePath = ""; afterStart = "minimise"; checkUpdates = true; smoothGraphics = true; startPage = "dashboard";
            lastBuildId = ""; newsRead = new List<string>(); knownMods = new Dictionary<string, string>(); modNews = new List<NewsItem>();
        }

        static string FilePath { get { return Path.Combine(Program.DataDir, "state.json"); } }

        public static Settings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    Settings s = new JavaScriptSerializer().Deserialize<Settings>(File.ReadAllText(FilePath, Encoding.UTF8));
                    if (s != null)
                    {
                        if (s.newsRead == null) s.newsRead = new List<string>();
                        if (s.knownMods == null) s.knownMods = new Dictionary<string, string>();
                        if (s.modNews == null) s.modNews = new List<NewsItem>();
                        if (string.IsNullOrEmpty(s.afterStart)) s.afterStart = "minimise";
                        if (string.IsNullOrEmpty(s.startPage)) s.startPage = "dashboard";
                        return s;
                    }
                }
            }
            catch (Exception) { }
            return new Settings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Program.DataDir);
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, new JavaScriptSerializer().Serialize(this), new UTF8Encoding(false));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
            catch (Exception) { }
        }
    }

    // ---- Downloads ---------------------------------------------------------------------------

    public static class Net
    {
        public static bool IsWeb(string source)
        {
            return source.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || source.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }

        public static WebClient Client()
        {
            var client = new WebClient { Encoding = Encoding.UTF8 };
            client.Headers[HttpRequestHeader.UserAgent] = "RescueOpsModLauncher/" + Program.AppVersion;
            client.Headers[HttpRequestHeader.CacheControl] = "no-cache";
            return client;
        }

        public static async Task<string> ReadText(string source)
        {
            if (!IsWeb(source)) return File.ReadAllText(source, Encoding.UTF8);
            using (WebClient client = Client()) return await client.DownloadStringTaskAsync(source);
        }

        public static async Task Download(string source, string target, Action<int> progress)
        {
            if (!IsWeb(source))
            {
                await Task.Run(() => File.Copy(source, target, true));
                if (progress != null) progress(100);
                return;
            }
            using (WebClient client = Client())
            {
                if (progress != null) client.DownloadProgressChanged += (s, e) => progress(Math.Max(0, Math.Min(100, e.ProgressPercentage)));
                await client.DownloadFileTaskAsync(new Uri(source), target);
            }
        }

        public static string Sha256(string path)
        {
            using (FileStream s = File.OpenRead(path))
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(s)).Replace("-", "");
        }
    }

    // ---- News --------------------------------------------------------------------------------

    public class NewsItem
    {
        public string id { get; set; }
        public string kind { get; set; }        // patch, official, hub, mods, event
        public string title { get; set; }
        public string summary { get; set; }
        public string source { get; set; }      // "steam", "hub", "launcher"
        public string url { get; set; }
        public long date { get; set; }          // unix seconds
    }

    public static class News
    {
        public const string SteamNews = "https://api.steampowered.com/ISteamNews/GetNewsForApp/v2/?appid=" + Game.FullGameAppId + "&count=12&maxlength=0&feeds=steam_community_announcements";
        public const string HubNews = "https://rescueopsmodding.vercel.app/news.json";

        static long Unix(DateTime d) { return (long)(d.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds; }

        // Strips leading emoji/symbols the devs put in titles ("⚙️ Patch Note 0.8.10").
        static string CleanTitle(string t)
        {
            if (t == null) return "";
            int i = 0;
            while (i < t.Length && !char.IsLetterOrDigit(t[i])) i++;
            return t.Substring(i).Trim();
        }

        // Steam's BBCode to one line: headings and images dropped, paragraphs and list items joined with "; ".
        static string Summary(string bb)
        {
            string t = Regex.Replace(bb, @"\[h\d\].*?\[/h\d\]|\[img\].*?\[/img\]|\{STEAM_CLAN_IMAGE\}\S*", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            t = Regex.Replace(t, @"\[\*\]|\[/p\]|
", "; ", RegexOptions.IgnoreCase);
            t = Regex.Replace(t, @"\[[^\]]*\]|<[^>]*>", " ");
            t = Regex.Replace(t, @"\s+", " ");
            t = Regex.Replace(t, @"(\s*;\s*)+", "; ").Trim(' ', ';');
            t = Regex.Replace(t, @"([.!?:,])\s*;\s*", "$1 ");
            return t;
        }

        public static async Task<List<NewsItem>> Steam()
        {
            var list = new List<NewsItem>();
            var root = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(await Net.ReadText(SteamNews));
            var appnews = root["appnews"] as Dictionary<string, object>;
            foreach (object o in (appnews["newsitems"] as System.Collections.ArrayList))
            {
                var n = o as Dictionary<string, object>;
                var tags = n.ContainsKey("tags") ? n["tags"] as System.Collections.ArrayList : null;
                bool patch = tags != null && tags.Cast<object>().Any(t => string.Equals(t as string, "patchnotes", StringComparison.OrdinalIgnoreCase));
                string contents = n.ContainsKey("contents") ? (n["contents"] as string ?? "") : "";
                contents = Summary(contents);
                list.Add(new NewsItem
                {
                    id = "steam:" + n["gid"], kind = patch ? "patch" : "official", source = "steam",
                    title = CleanTitle(n["title"] as string), summary = contents.Length > 160 ? contents.Substring(0, 157) + "..." : contents,
                    url = n["url"] as string, date = Convert.ToInt64(n["date"])
                });
            }
            return list;
        }

        public static async Task<List<NewsItem>> Hub()
        {
            var list = new List<NewsItem>();
            var root = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(await Net.ReadText(HubNews));
            foreach (object o in (root["items"] as System.Collections.ArrayList))
            {
                var n = o as Dictionary<string, object>;
                DateTime d;
                DateTime.TryParse(n["date"] as string, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out d);
                list.Add(new NewsItem
                {
                    id = "hub:" + n["id"], kind = (n["kind"] as string) ?? "hub", source = "hub",
                    title = n["title"] as string, summary = n["summary"] as string, url = n["url"] as string, date = Unix(d)
                });
            }
            return list;
        }

        // "New mod" / "updated" items from changes in mods.json since the last run (not on the very first run).
        public static void TrackModReleases(Settings st, ModSet mods)
        {
            bool first = st.knownMods.Count == 0;
            foreach (ModInfo m in mods.All)
            {
                if (m.hidden) continue;
                string known;
                bool had = st.knownMods.TryGetValue(m.id, out known);
                if (!first && (!had || known != m.version))
                {
                    ChangeEntry change = m.changelog.FirstOrDefault(c => c.version == m.version);
                    st.modNews.Add(new NewsItem
                    {
                        id = "mod:" + m.id + ":" + m.version, kind = "mods", source = "launcher",
                        title = had ? m.name + " " + m.version + " released" : "New mod: " + m.name + " by " + m.author,
                        summary = change != null && change.notes != null && change.notes.Count > 0 ? string.Join("; ", change.notes) : (m.description ?? ""),
                        url = m.page, date = Unix(DateTime.UtcNow)
                    });
                }
                st.knownMods[m.id] = m.version;
            }
            if (st.modNews.Count > 30) st.modNews.RemoveRange(0, st.modNews.Count - 30);
        }

        // The game version named in the newest patch note ("Mini patch Note 0.8.11" -> "0.8.11").
        public static string LatestGameVersion(IEnumerable<NewsItem> steam)
        {
            foreach (NewsItem n in steam.Where(x => x.kind == "patch").OrderByDescending(x => x.date))
            {
                Match m = Regex.Match(n.title ?? "", @"\d+\.\d+(\.\d+)?");
                if (m.Success) return m.Value;
            }
            return null;
        }
    }

    // ---- Self-update -------------------------------------------------------------------------

    public static class SelfUpdate
    {
        public static bool IsNewer(string version)
        {
            Version latest, mine;
            return Version.TryParse(version ?? "", out latest) && Version.TryParse(Program.AppVersion, out mine) && latest > mine;
        }

        // Deletes the copy left behind by the previous update.
        public static void CleanUp()
        {
            try
            {
                string old = Program.ExePath + ".old";
                if (File.Exists(old)) File.Delete(old);
            }
            catch (Exception) { }
        }

        // Downloads the new exe, checks it, swaps it in for the running one (Windows allows renaming a running exe).
        public static async Task Apply(LauncherInfo info, Action<int> progress)
        {
            string exe = Program.ExePath, fresh = exe + ".new", old = exe + ".old";
            if (File.Exists(fresh)) File.Delete(fresh);
            await Net.Download(info.download, fresh, progress);
            if (!string.IsNullOrEmpty(info.sha256) && !string.Equals(Net.Sha256(fresh), info.sha256, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(fresh);
                throw new InvalidDataException("The downloaded launcher didn't match the expected file, so it wasn't installed. Try again later.");
            }
            if (File.Exists(old)) File.Delete(old);
            File.Move(exe, old);
            File.Move(fresh, exe);
        }
    }
}
