// Mod Dev (launcher 2.0): community mods from the hub's catalog, mod dev accounts, uploads, and player feedback/reports.
// Talks to the hub API (/api/dev, /api/catalog). See api/dev.js for the rules the hub enforces.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RescueOpsModLauncher
{
    public partial class LauncherForm
    {
        const string DefaultApi = "https://rescueopsmodding.vercel.app/api/";
        string Api { get { return opt.Api ?? DefaultApi; } }
        Dictionary<string, object> devData;               // last /api/dev?a=mine answer
        HashSet<string> newIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);   // mods first seen this run
        string StagingDir { get { return Path.Combine(Path.GetTempPath(), "RescueOpsModLauncher", "upload"); } }

        // ---- community mods ----

        static string CatalogCache { get { return Path.Combine(Program.DataDir, "catalog.json"); } }

        // Adds the live community mods to the mods.json list. Kept on disk, so installed community mods are still
        // recognised (not shown as stray paks) when the hub can't be reached.
        async Task MergeCatalog(Manifest m)
        {
            string text = null;
            try
            {
                text = await Net.ReadText(Api + "catalog");
                Directory.CreateDirectory(Program.DataDir);
                File.WriteAllText(CatalogCache, text, new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Log.Write("Catalog: " + ex.Message);
                try { if (File.Exists(CatalogCache)) text = File.ReadAllText(CatalogCache, Encoding.UTF8); } catch (Exception) { }
            }
            if (text == null) return;
            Manifest cat;
            try { cat = json.Deserialize<Manifest>(text); } catch (Exception) { return; }
            if (cat == null || cat.mods == null) return;
            foreach (ModInfo c in cat.mods)
            {
                if (c == null || string.IsNullOrEmpty(c.id) || c.files == null || c.files.Count == 0) continue;
                c.community = true;
                c.includes = new List<string>();                      // a community mod never bundles other mods
                int i = m.mods.FindIndex(x => string.Equals(x.id, c.id, StringComparison.OrdinalIgnoreCase));
                if (i >= 0) m.mods[i] = c; else m.mods.Add(c);
            }
        }

        // ---- hub calls ----

        async Task<Dictionary<string, object>> Dev(string action, Dictionary<string, object> body)
        {
            string reply = await Net.PostJson(Api + "dev?a=" + action, json.Serialize(body));
            return json.Deserialize<Dictionary<string, object>>(reply) ?? new Dictionary<string, object>();
        }

        Dictionary<string, object> KeyBody() { return new Dictionary<string, object> { { "authorKey", settings.authorKey } }; }

        // Sends the mod dev's mods, uploads and inbox to the page ("dev" event). Without an account: { account: false }.
        async Task DevLoad()
        {
            if (string.IsNullOrEmpty(settings.authorKey)) { devData = null; Send("dev", new Dictionary<string, object> { { "account", false } }); return; }
            try
            {
                devData = await Dev("mine", KeyBody());
                devData["account"] = true;
                Send("dev", devData);
            }
            catch (Exception ex)
            {
                var api = ex as ApiException;
                if (api != null && api.Status == 403)
                {
                    // The key was replaced (account restored on another PC): this PC needs the recovery code again.
                    settings.authorKey = ""; settings.Save(); SendSettings();
                    Send("dev", new Dictionary<string, object> { { "account", false }, { "lost", true } });
                }
                else Send("dev", new Dictionary<string, object> { { "account", true }, { "offline", ex.Message }, { "author", new Dictionary<string, object> { { "name", settings.authorName } } } });
            }
        }

        async Task DevSetup(string name)
        {
            string key = Net.RandomHex(32);
            try
            {
                var r = await Dev("author", new Dictionary<string, object> { { "installId", settings.installId }, { "authorKey", key }, { "name", name } });
                settings.authorKey = key; settings.authorName = r["name"].ToString(); settings.Save(); SendSettings();
                Send("devRecovery", new Dictionary<string, object> { { "code", r["recoveryCode"] }, { "first", true } });
                Log.Write("Mod Dev: account " + settings.authorName);
            }
            catch (Exception ex) { DevFail(ex, "devSetupFailed"); return; }
            await DevLoad();
        }

        async Task DevRecover(string code)
        {
            string key = Net.RandomHex(32);
            try
            {
                var r = await Dev("recover", new Dictionary<string, object> { { "installId", settings.installId }, { "authorKey", key }, { "code", code } });
                settings.authorKey = key; settings.authorName = r["name"].ToString(); settings.Save(); SendSettings();
                Status(T("Mod dev account restored: ", "Compte de moddeur restauré : ") + settings.authorName, -1);
                Send("devRecovered", true);
            }
            catch (Exception ex) { DevFail(ex, "devRecoverFailed"); return; }
            await DevLoad();
        }

        async Task DevNewCode()
        {
            try
            {
                var r = await Dev("newcode", KeyBody());
                Send("devRecovery", new Dictionary<string, object> { { "code", r["recoveryCode"] }, { "first", false } });
            }
            catch (Exception ex) { Error(ex.Message); }
        }

        void DevFail(Exception ex, string evt)
        {
            var api = ex as ApiException;
            if (api != null && api.Status == 403 && api.Code == "register") { settings.profileName = ""; settings.Save(); SendSettings(); Send("needRegister", new Dictionary<string, object>()); }
            Send(evt, api != null ? api.Code : T("Couldn't reach the modding hub. Check your connection and try again.", "Impossible de joindre le hub de modding. Vérifiez votre connexion et réessayez."));
        }

        async Task DevSimple(string action, Dictionary<string, object> body)
        {
            try { await Dev(action, body); }
            catch (Exception ex) { Error(ex.Message); }
            await DevLoad();
        }

        // ---- picking files for an upload ----

        // Mod files: .pak/.utoc/.ucas, or a zip holding them (unpacked to a temp folder). Sends "devFiles" [{ name, size, path }].
        async Task DevPickFiles(List<string> given)
        {
            List<string> picked = given;
            if (picked == null)
                using (var dlg = new OpenFileDialog { Multiselect = true, Title = T("Choose your mod's files", "Choisissez les fichiers de votre mod"), Filter = T("Mod files", "Fichiers de mod") + " (*.pak;*.utoc;*.ucas;*.zip)|*.pak;*.utoc;*.ucas;*.zip" })
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    picked = dlg.FileNames.ToList();
                }
            var files = await Task.Run(() => ModFilesFrom(picked));
            Send("devFiles", files.Select(f => new Dictionary<string, object> { { "name", Path.GetFileName(f) }, { "size", new FileInfo(f).Length }, { "path", f } }).ToList());
        }

        List<string> ModFilesFrom(List<string> picked)
        {
            var result = new List<string>();
            foreach (string p in picked)
            {
                string ext = Path.GetExtension(p).ToLowerInvariant();
                if (ext == ".pak" || ext == ".utoc" || ext == ".ucas") { result.Add(p); continue; }
                if (ext != ".zip") continue;
                string dir = Path.Combine(StagingDir, Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(dir);
                using (ZipArchive zip = ZipFile.OpenRead(p))
                    foreach (ZipArchiveEntry e in zip.Entries)
                    {
                        string n = Path.GetFileName(e.FullName.Replace('/', '\\')), x = Path.GetExtension(n).ToLowerInvariant();
                        if (n.Length == 0 || (x != ".pak" && x != ".utoc" && x != ".ucas")) continue;
                        string target = Path.Combine(dir, n);
                        e.ExtractToFile(target, true);
                        result.Add(target);
                    }
            }
            return result.GroupBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase).Select(g => g.Last()).ToList();
        }

        // Pictures: the thumbnail is cropped to 16:9 at 640x360; screenshots fit 1920x1080. Both become JPEGs under 2 MB.
        // Sends "devImages" { which, items: [{ path, preview }] }.
        async Task DevPickImages(string which, List<string> given)
        {
            bool thumb = which == "thumb";
            List<string> picked = given;
            if (picked == null)
                using (var dlg = new OpenFileDialog { Multiselect = !thumb, Title = thumb ? T("Choose the mod's picture", "Choisissez l'image du mod") : T("Choose screenshots", "Choisissez des captures"), Filter = T("Pictures", "Images") + " (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp" })
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    picked = dlg.FileNames.ToList();
                }
            var items = new List<object>();
            await Task.Run(() =>
            {
                Directory.CreateDirectory(StagingDir);
                foreach (string p in picked.Take(thumb ? 1 : 6))
                {
                    try
                    {
                        string outPath = Path.Combine(StagingDir, Guid.NewGuid().ToString("N") + ".jpg");
                        string preview = Picture(p, outPath, thumb);
                        items.Add(new Dictionary<string, object> { { "path", outPath }, { "preview", preview }, { "name", Path.GetFileName(p) } });
                    }
                    catch (Exception ex) { Log.Write("Picture " + p + ": " + ex.Message); }
                }
            });
            if (items.Count == 0) { Error(T("That picture couldn't be opened. Use a JPG or PNG.", "Image illisible. Utilisez un JPG ou un PNG.")); return; }
            Send("devImages", new Dictionary<string, object> { { "which", which }, { "items", items } });
        }

        // Writes the resized JPEG; returns a small preview as a data: URL.
        static string Picture(string src, string dest, bool thumb)
        {
            using (var img = Image.FromFile(src))
            {
                int w, h; Rectangle from;
                if (thumb)
                {
                    w = 640; h = 360;
                    double target = 16.0 / 9, ratio = (double)img.Width / img.Height;
                    from = ratio > target ? new Rectangle((int)((img.Width - img.Height * target) / 2), 0, (int)(img.Height * target), img.Height)
                                          : new Rectangle(0, (int)((img.Height - img.Width / target) / 2), img.Width, (int)(img.Width / target));
                }
                else
                {
                    double k = Math.Min(1.0, Math.Min(1920.0 / img.Width, 1080.0 / img.Height));
                    w = Math.Max(1, (int)(img.Width * k)); h = Math.Max(1, (int)(img.Height * k));
                    from = new Rectangle(0, 0, img.Width, img.Height);
                }
                using (var bmp = Draw(img, from, w, h))
                {
                    long q = 88;
                    do { SaveJpeg(bmp, dest, q); q -= 8; } while (new FileInfo(dest).Length > 1900 * 1024 && q > 40);
                    using (var small = Draw(bmp, new Rectangle(0, 0, w, h), 320, Math.Max(1, h * 320 / w)))
                    using (var ms = new MemoryStream())
                    {
                        small.Save(ms, ImageFormat.Jpeg);
                        return "data:image/jpeg;base64," + Convert.ToBase64String(ms.ToArray());
                    }
                }
            }
        }

        static Bitmap Draw(Image img, Rectangle from, int w, int h)
        {
            var bmp = new Bitmap(w, h);
            using (var g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.DrawImage(img, new Rectangle(0, 0, w, h), from, GraphicsUnit.Pixel);
            }
            return bmp;
        }

        static void SaveJpeg(Bitmap bmp, string path, long quality)
        {
            ImageCodecInfo codec = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
            using (var p = new EncoderParameters(1))
            {
                p.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
                bmp.Save(path, codec, p);
            }
        }

        // ---- upload ----

        static List<string> StrList(Dictionary<string, object> m, string k)
        {
            var list = new List<string>();
            if (m.ContainsKey(k) && m[k] is System.Collections.ArrayList) foreach (object o in (System.Collections.ArrayList)m[k]) if (o != null) list.Add(o.ToString());
            return list;
        }

        // Sends the details, then every file in 2 MB pieces, then asks the hub to check it in. Reports "devSubmitResult".
        async Task DevSubmit(Dictionary<string, object> msg)
        {
            if (busy) return;
            busy = true; Send("busy", true);
            string name = Str(msg, "name") ?? "";
            try
            {
                List<string> files = StrList(msg, "files"), images = new List<string>();
                if (!string.IsNullOrEmpty(Str(msg, "thumb"))) images.Add(Str(msg, "thumb"));
                images.AddRange(StrList(msg, "shots"));
                foreach (string f in files.Concat(images)) if (!File.Exists(f)) throw new FileNotFoundException(T("A file went missing: ", "Fichier introuvable : ") + f);
                Status(T("Checking your files...", "Vérification de vos fichiers..."), -1);
                var fileInfo = await Task.Run(() => files.Select(f => new Dictionary<string, object> { { "name", Path.GetFileName(f) }, { "size", new FileInfo(f).Length }, { "sha256", Net.Sha256(f) } }).ToList());
                var imageInfo = await Task.Run(() => images.Select((f, i) => new Dictionary<string, object> { { "name", i == 0 ? "thumb.jpg" : "shot" + i + ".jpg" }, { "size", new FileInfo(f).Length }, { "sha256", Net.Sha256(f) } }).ToList());
                var body = KeyBody();
                foreach (string k in new[] { "modId", "name", "version", "category", "description", "changelog" }) body[k] = Str(msg, k) ?? "";
                if (string.IsNullOrEmpty(Str(msg, "modId"))) body.Remove("modId");
                body["conflicts"] = StrList(msg, "conflicts"); body["requires"] = StrList(msg, "requires");
                body["testedBuild"] = app != null ? app.BuildId ?? "" : "";
                body["files"] = fileInfo; body["images"] = imageInfo;
                var sub = await Dev("submit", body);
                long id = Convert.ToInt64(sub["id"]); int chunk = Convert.ToInt32(sub["chunkSize"]);
                List<string> all = files.Concat(images).ToList();
                long total = all.Sum(f => new FileInfo(f).Length), sent = 0;
                string label = T("Uploading ", "Envoi de ") + name;
                for (int i = 0; i < all.Count; i++)
                {
                    using (FileStream fs = File.OpenRead(all[i]))
                    {
                        var buf = new byte[chunk];
                        for (int n = 0; ; n++)
                        {
                            int read = 0, r;
                            while (read < chunk && (r = await fs.ReadAsync(buf, read, chunk - read)) > 0) read += r;
                            if (read == 0 && n > 0) break;
                            string data = Convert.ToBase64String(buf, 0, read);
                            // a dropped connection gets two more tries; the hub's own refusals don't
                            for (int attempt = 1; ; attempt++)
                            {
                                bool retry = false;
                                try { await Dev("chunk", new Dictionary<string, object> { { "authorKey", settings.authorKey }, { "id", id }, { "file", i }, { "n", n }, { "data", data } }); }
                                catch (Exception ex) { if (attempt >= 3 || ex is ApiException) throw; retry = true; }
                                if (!retry) break;
                                await Task.Delay(1500 * attempt);
                            }
                            sent += read;
                            Status(label, (int)(100 * sent / Math.Max(1, total)));
                            if (read < chunk) break;
                        }
                    }
                }
                Status(T("Checking the upload...", "Vérification de l'envoi..."), -1);
                await Dev("finish", new Dictionary<string, object> { { "authorKey", settings.authorKey }, { "id", id } });
                Log.Write("Mod Dev: uploaded " + name + " " + Str(msg, "version") + " (#" + id + ")");
                Status(T("Uploaded ", "Envoyé : ") + name + T(". It goes live after a quick safety check, usually within 24 hours.", ". Il sera en ligne après une vérification de sécurité rapide, généralement sous 24 heures."), -1);
                Send("devSubmitResult", new Dictionary<string, object> { { "ok", true }, { "name", name } });
            }
            catch (Exception ex)
            {
                Status("", -1);
                Log.Write("Mod Dev upload failed: " + ex.Message);
                Send("devSubmitResult", new Dictionary<string, object> { { "ok", false }, { "error", ex.Message } });
            }
            finally { busy = false; Send("busy", false); }
            await DevLoad();
        }

        // ---- player side: feedback to a mod's developer, reports to the hub ----

        async Task PlayerMessage(string action, Dictionary<string, object> msg)
        {
            var body = new Dictionary<string, object> { { "installId", settings.installId }, { "modId", Str(msg, "modId") }, { "text", Str(msg, "text") ?? "" } };
            if (action == "feedback") body["kind"] = Str(msg, "kind"); else body["reason"] = Str(msg, "reason");
            try
            {
                await Dev(action, body);
                Send("sent", new Dictionary<string, object> { { "ok", true }, { "what", action } });
            }
            catch (Exception ex)
            {
                var api = ex as ApiException;
                if (api != null && api.Status == 403 && api.Code == "register") { settings.profileName = ""; settings.Save(); SendSettings(); }
                Send("sent", new Dictionary<string, object> { { "ok", false }, { "what", action }, { "error", api != null ? api.Code : ex.Message } });
            }
        }

        // ---- drag and drop ----

        // Files dropped on the window (paths come with the message as CoreWebView2File objects).
        async Task Dropped(string target, List<string> paths)
        {
            if (paths.Count == 0) return;
            if (target == "devFiles") { await DevPickFiles(paths); return; }
            if (target == "devThumb") { await DevPickImages("thumb", paths); return; }
            if (target == "devShots") { await DevPickImages("shots", paths); return; }
            await Guard(async () =>
            {
                List<string> added = await Task.Run(() => lib.AddFiles(paths.ToArray()));
                Status(added.Count > 0 ? T("Added ", "Ajouté : ") + string.Join(", ", added) + T(". Tick it to load it.", ". Cochez-le pour le charger.") : T("No .pak, .utoc or .ucas files found.", "Aucun fichier .pak, .utoc ou .ucas trouvé."), -1);
            });
        }
    }
}
