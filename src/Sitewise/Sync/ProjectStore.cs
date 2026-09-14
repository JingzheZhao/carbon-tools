using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Rhino;

namespace Sitewise.Sync
{
    /// <summary>
    /// A project is a folder the user picks:
    ///
    ///     Park1/
    ///       project.json      name, climate zone, display units
    ///       schemes/001.json  one file per scheme
    ///       thumbs/           screenshots
    ///
    /// The folder dialog lives here rather than in the browser because Chrome's
    /// File System Access API refuses a list of protected directories - the
    /// desktop among them - and asks permission again every session for the rest.
    /// A native dialog has neither limit, and the user still starts it by
    /// clicking a button in the web page: the page asks the server, the server
    /// asks Windows.
    /// </summary>
    internal static class ProjectStore
    {
        private const string SchemesDir = "schemes";
        private const string MetaFile = "project.json";
        private const int MaxRecent = 3;

        /// <summary>The folder currently open, or null. Paths never come from the browser.</summary>
        public static string CurrentPath { get; private set; }

        // ─────────────────────────────────────────────────────────── dialogs ──

        /// <summary>
        /// Shows the folder dialog on Rhino's UI thread and waits for it. The HTTP
        /// listener runs on a background thread, where a WinForms dialog cannot be
        /// shown at all, so the call has to be marshalled and the result carried back.
        /// </summary>
        private static string PickFolder(string description)
        {
            string picked = null;
            var done = new ManualResetEventSlim(false);

            RhinoApp.InvokeOnUiThread((Action)(() =>
            {
                try
                {
                    // the dialog belongs to Rhino, which may be behind the browser;
                    // without this it opens behind and looks like nothing happened
                    RhinoApp.SetFocusToMainWindow();

                    using (var dialog = new FolderBrowserDialog())
                    {
                        dialog.Description = description;
                        dialog.ShowNewFolderButton = true;

                        if (dialog.ShowDialog() == DialogResult.OK)
                        {
                            picked = dialog.SelectedPath;
                        }
                    }
                }
                catch (Exception)
                {
                    // a dialog that fails to open is a cancel, not a crash
                }
                finally
                {
                    done.Set();
                }
            }));

            // a dialog left open forever must not pin an HTTP thread forever
            done.Wait(TimeSpan.FromMinutes(10));
            return picked;
        }

        // ──────────────────────────────────────────────────────── operations ──

        /// <summary>
        /// Create: the folder the user picks becomes the project. The dialog has a
        /// "New folder" button, so there is no reason to ask for a name separately.
        /// </summary>
        public static JObject Create()
        {
            string path = PickFolder("Choose or create a folder for this project");
            if (string.IsNullOrEmpty(path))
            {
                return Cancelled();
            }

            string meta = Path.Combine(path, MetaFile);
            if (File.Exists(meta))
            {
                // overwriting the project file would erase a real project's identity
                return Failed($"“{new DirectoryInfo(path).Name}” is already a project. Use Open Project to open it.");
            }

            try
            {
                Directory.CreateDirectory(Path.Combine(path, SchemesDir));
                var project = new JObject
                {
                    ["name"] = new DirectoryInfo(path).Name,
                    ["projectName"] = new DirectoryInfo(path).Name,
                    ["created"] = DateTime.UtcNow.ToString("o"),
                    ["format"] = 1
                };
                File.WriteAllText(meta, project.ToString(Formatting.Indented), Encoding.UTF8);
                CurrentPath = path;
                Remember(path, 0);
                return Opened(path, project, new JArray());
            }
            catch (UnauthorizedAccessException)
            {
                return Failed("Sitewise is not allowed to write there. Choose a folder you own, such as one in your Documents.");
            }
            catch (Exception ex)
            {
                // the system's own words are kept: "disk full" and "name too long"
                // need different fixes, and only it knows which happened
                return Failed("The project could not be created there. " + ex.Message);
            }
        }

        /// <summary>Open: the folder must already carry a project.json.</summary>
        public static JObject Open()
        {
            string path = PickFolder("Choose an existing project folder");
            return string.IsNullOrEmpty(path) ? Cancelled() : Load(path);
        }

        /// <summary>Reopen a folder from the recent list, with no dialog.</summary>
        public static JObject OpenRecent(string path)
        {
            // only a path this machine already recorded may be opened without a
            // dialog, so the browser cannot name an arbitrary folder
            if (string.IsNullOrEmpty(path) || !Recent().Any(r => PathEquals((string)r["path"], path)))
            {
                return Failed("That project cannot be opened from here. Use Open Project to find it.");
            }

            return Load(path);
        }

        private static JObject Load(string path)
        {
            string meta = Path.Combine(path, MetaFile);
            if (!File.Exists(meta))
            {
                return Failed(Directory.Exists(path)
                    ? $"There is no project in “{new DirectoryInfo(path).Name}”. Pick the folder a project was created in, or use Create New Project."
                    : "That folder is not there any more. It may have been moved, renamed or deleted.");
            }

            try
            {
                var project = JObject.Parse(File.ReadAllText(meta, Encoding.UTF8));
                var schemes = new JArray();
                string dir = Path.Combine(path, SchemesDir);

                if (Directory.Exists(dir))
                {
                    foreach (string file in Directory.GetFiles(dir, "*.json").OrderBy(f => f))
                    {
                        try { schemes.Add(JObject.Parse(File.ReadAllText(file, Encoding.UTF8))); }
                        catch (Exception) { /* one unreadable scheme must not lose the rest */ }
                    }
                }

                CurrentPath = path;
                Remember(path, schemes.Count);
                return Opened(path, project, schemes);
            }
            catch (Exception ex)
            {
                return Failed("That project could not be read. " + ex.Message);
            }
        }

        /// <summary>
        /// Write the project back. Schemes are one file each, so saving one does
        /// not rewrite the rest, and a scheme deleted in the page is deleted here.
        /// </summary>
        public static JObject Save(JObject body)
        {
            if (string.IsNullOrEmpty(CurrentPath) || !Directory.Exists(CurrentPath))
            {
                return Failed("No project is open, so there is nothing to save into.");
            }

            try
            {
                if (body["project"] is JObject project)
                {
                    File.WriteAllText(Path.Combine(CurrentPath, MetaFile),
                                      project.ToString(Formatting.Indented), Encoding.UTF8);
                }

                string dir = Path.Combine(CurrentPath, SchemesDir);
                Directory.CreateDirectory(dir);

                var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (body["schemes"] is JArray schemes)
                {
                    foreach (JObject scheme in schemes.OfType<JObject>())
                    {
                        string name = SafeName((string)scheme["id"]) + ".json";
                        keep.Add(name);
                        File.WriteAllText(Path.Combine(dir, name),
                                          scheme.ToString(Formatting.Indented), Encoding.UTF8);
                    }

                    foreach (string file in Directory.GetFiles(dir, "*.json"))
                    {
                        if (!keep.Contains(Path.GetFileName(file)))
                        {
                            try { File.Delete(file); } catch (Exception) { }
                        }
                    }

                    Remember(CurrentPath, keep.Count);
                }

                return new JObject { ["ok"] = true };
            }
            catch (UnauthorizedAccessException)
            {
                return Failed("Sitewise is not allowed to write to the project folder. Check it is not read-only.");
            }
            catch (Exception ex)
            {
                return Failed("The project could not be saved. " + ex.Message);
            }
        }

        // ───────────────────────────────────────────────────────────── recent ──

        private static string RecentFile =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "Sitewise", "recent.json");

        public static JArray Recent()
        {
            try
            {
                return File.Exists(RecentFile)
                    ? JArray.Parse(File.ReadAllText(RecentFile, Encoding.UTF8))
                    : new JArray();
            }
            catch (Exception)
            {
                return new JArray();
            }
        }

        private static void Remember(string path, int schemeCount)
        {
            try
            {
                var list = new JArray(Recent().OfType<JObject>()
                    .Where(r => !PathEquals((string)r["path"], path)));

                list.Insert(0, new JObject
                {
                    ["path"] = path,
                    ["name"] = new DirectoryInfo(path).Name,
                    ["schemes"] = schemeCount,
                    ["opened"] = DateTime.UtcNow.ToString("o")
                });

                while (list.Count > MaxRecent)
                {
                    list.RemoveAt(list.Count - 1);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(RecentFile));
                File.WriteAllText(RecentFile, list.ToString(Formatting.Indented), Encoding.UTF8);
            }
            catch (Exception)
            {
                // a recent list that cannot be written is a convenience lost,
                // not a project lost
            }
        }

        // ───────────────────────────────────────────────────────────── helpers ──

        private static bool PathEquals(string a, string b)
        {
            if (a == null || b == null) return false;
            return string.Equals(a.TrimEnd('\\', '/'), b.TrimEnd('\\', '/'),
                                 StringComparison.OrdinalIgnoreCase);
        }

        private static string SafeName(string id)
        {
            if (string.IsNullOrEmpty(id)) return "scheme";
            var clean = new string(id.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());
            return clean.Length > 0 ? clean : "scheme";
        }

        private static JObject Opened(string path, JObject project, JArray schemes) =>
            new JObject
            {
                ["ok"] = true,
                ["path"] = path,
                ["name"] = new DirectoryInfo(path).Name,
                ["project"] = project,
                ["schemes"] = schemes
            };

        private static JObject Cancelled() => new JObject { ["ok"] = false, ["cancelled"] = true };

        private static JObject Failed(string message) =>
            new JObject { ["ok"] = false, ["error"] = message };
    }
}
