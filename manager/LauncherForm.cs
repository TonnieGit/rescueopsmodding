// The launcher window: hosts the WebView2 page and passes messages between it and the C# side.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace RescueOpsModLauncher
{
    public class LauncherForm : Form
    {
        readonly WebView2 view = new WebView2();
        readonly JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        readonly string[] args;

        public LauncherForm(string[] args)
        {
            this.args = args;
            Text = "Rescue Ops Mod Launcher";
            ClientSize = new Size(1280, 820);
            MinimumSize = new Size(960, 640);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(13, 12, 12);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }
            view.Dock = DockStyle.Fill;
            view.DefaultBackgroundColor = Color.FromArgb(13, 12, 12);
            Controls.Add(view);
            Load += async delegate { await StartView(); };
        }

        async Task StartView()
        {
            try
            {
                CoreWebView2Environment.SetLoaderDllFolderPath(Program.ExtractLoader());
                var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(Program.DataDir, "WebView2"));
                await view.EnsureCoreWebView2Async(env);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "The launcher needs the Microsoft Edge WebView2 Runtime, which comes with Windows 10 and 11.\n\n" +
                    "Install it from https://go.microsoft.com/fwlink/p/?LinkId=2124703 and start the launcher again.\n\n" + ex.Message,
                    "Rescue Ops Mod Launcher", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Close();
                return;
            }
            var s = view.CoreWebView2.Settings;
            s.AreDefaultContextMenusEnabled = false;
            s.IsStatusBarEnabled = false;
            s.IsZoomControlEnabled = false;
            s.AreDevToolsEnabled = Array.IndexOf(args, "--devtools") >= 0;
            view.CoreWebView2.WebMessageReceived += OnMessage;
            view.CoreWebView2.NavigateToString(Encoding.UTF8.GetString(Program.Resource("index.html")));
        }

        // JS -> C#: chrome.webview.postMessage({ cmd: '...', ... })
        void OnMessage(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            var msg = json.Deserialize<Dictionary<string, object>>(e.WebMessageAsJson);
            string cmd = msg.ContainsKey("cmd") ? msg["cmd"] as string : null;
            if (cmd == "hello")
                Send("hello", new Dictionary<string, object> {
                    { "version", Program.AppVersion }, { "dotnet", Environment.Version.ToString() },
                    { "webview", view.CoreWebView2.Environment.BrowserVersionString } });
        }

        // C# -> JS: window.chrome.webview 'message' event with { event: '...', data: ... }
        public void Send(string evt, object data)
        {
            view.CoreWebView2.PostWebMessageAsJson(json.Serialize(new Dictionary<string, object> { { "event", evt }, { "data", data } }));
        }
    }
}
