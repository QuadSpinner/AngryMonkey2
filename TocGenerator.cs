using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Humanizer;

namespace AngryMonkey
{
    public static class TocGenerator
    {
        // -----------------------------
        // Public API
        // -----------------------------

        /// <summary>
        /// Builds a TOC for a "Hive" directory and returns JSON array (matching your sample shape).
        /// Uses:
        /// - folders.txt for ordering child folders + optional folder title override: folderName|Title
        /// - getFrontMatter(lines) for page title/order from YAML front matter
        /// URL rules:
        /// - index.md => "/Base/rel/dir/"
        /// - other .md => "/Base/rel/file.html"
        /// </summary>
        public static string GenerateHiveTocJson(
            string hiveRootDir,
            string baseUrl,
            string manifestFileName = "folders.txt",
            string indexFileName = "index.md",
            bool indented = true)
        {
            var toc = GenerateHiveToc(hiveRootDir, baseUrl, manifestFileName, indexFileName);

            var jsonOpts = new JsonSerializerOptions
            {
                WriteIndented = indented,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            return JsonSerializer.Serialize(toc, jsonOpts);
        }

        /// <summary>
        /// Writes a JS file like: window.SITE_TOC = [...];
        /// </summary>
        public static void WriteHiveTocJs(
            string hiveRootDir,
            string baseUrl,
            string outputJsPath,
            string jsVarPath = "window.SITE_TOC",
            string manifestFileName = "folders.txt",
            string indexFileName = "index.md",
            bool indented = true)
        {
            var json = GenerateHiveTocJson(hiveRootDir, baseUrl, manifestFileName, indexFileName, indented);

            var sb = new StringBuilder();
            sb.Append(jsVarPath).Append(" = ").Append(json).Append(';').AppendLine();

            Directory.CreateDirectory(Path.GetDirectoryName(outputJsPath)!);
            File.WriteAllText(outputJsPath, sb.ToString(), Utf8NoBom);
        }

        /// <summary>
        /// Returns TOC object graph (List of TocItem).
        /// Root dir is not wrapped in a group; it produces the top-level array.
        /// </summary>
        public static List<TocItem> GenerateHiveToc(
            string hiveRootDir,
            string baseUrl,
            string manifestFileName = "folders.txt",
            string indexFileName = "index.md")
        {
            if (string.IsNullOrWhiteSpace(hiveRootDir))
                throw new ArgumentException("Hive root directory is required.", nameof(hiveRootDir));

            var root = new DirectoryInfo(hiveRootDir);
            if (!root.Exists)
                throw new DirectoryNotFoundException(root.FullName);

            baseUrl = NormalizeBaseUrl(baseUrl);

            // Root folder becomes the TOC list.
            return BuildFolderChildren(
                hiveRoot: root,
                currentDir: root,
                baseUrl: baseUrl,
                manifestFileName: manifestFileName,
                indexFileName: indexFileName,
                includeIndexAsFirstChild: true);
        }

        // -----------------------------
        // Output model (matches your JSON)
        // -----------------------------

        public sealed class TocItem
        {
            [JsonPropertyName("title")]
            public string Title { get; set; } = "";

            [JsonPropertyName("url")]
            public string Url { get; set; }
            [JsonPropertyName("icon")]
            public string Icon { get; set; }

            [JsonPropertyName("section")]
            public bool? Section { get; set; }

            [JsonPropertyName("children")]
            public List<TocItem> Children { get; set; }
        }

        // -----------------------------
        // Build (folders as groups)
        // -----------------------------

        // 1) change signature to accept iconOverrideFromParent
        private static TocItem BuildFolderGroup(
            DirectoryInfo hiveRoot,
            DirectoryInfo dir,
            string baseUrl,
            string manifestFileName,
            string indexFileName,
            string titleOverrideFromParent,
            string iconOverrideFromParent)
        {
            var title = !string.IsNullOrWhiteSpace(titleOverrideFromParent)
                ? titleOverrideFromParent!
                : ToTitle(dir.Name);

            var children = BuildFolderChildren(
                hiveRoot: hiveRoot,
                currentDir: dir,
                baseUrl: baseUrl,
                manifestFileName: manifestFileName,
                indexFileName: indexFileName,
                includeIndexAsFirstChild: true);

            return new TocItem
            {
                Title = title,
                Children = children.Count > 0 ? children : null,
                Icon = !string.IsNullOrWhiteSpace(iconOverrideFromParent) ? iconOverrideFromParent : null
            };
        }


        private static List<TocItem> BuildFolderChildren(
            DirectoryInfo hiveRoot,
            DirectoryInfo currentDir,
            string baseUrl,
            string manifestFileName,
            string indexFileName,
            bool includeIndexAsFirstChild)
        {
            var items = new List<TocItem>();

            // ---- index.md => folder landing page entry (Overview / or title from front matter)
            if (includeIndexAsFirstChild)
            {
                var indexPath = Path.Combine(currentDir.FullName, indexFileName);
                if (File.Exists(indexPath))
                {
                    //var meta = ReadFrontMatter(indexPath);
                    var title = "Overview";
                    var url = UrlForIndex(hiveRoot.FullName, indexPath, baseUrl);
                    items.Add(new TocItem { Title = title, Url = url });
                }
            }

            Program.folderCount++;

            // ---- pages (.md excluding index.md)
            var pages = currentDir.EnumerateFiles("*.md", SearchOption.TopDirectoryOnly)
                .Where(f => !IsHiddenOrInternal(f.Name))
                .Where(f => !StringEquals(f.Name, indexFileName))
                .Select(f =>
                {
                    var meta = ReadFrontMatter(f.FullName);
                    return new
                    {
                        File = f,
                        Title = meta.Title ?? ToTitle(Path.GetFileNameWithoutExtension(f.Name)),
                        meta.Order,
                        meta.Section,
                        meta.Icon
                    };
                })
                .OrderBy(x => x.Order)
                .ThenBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.File.Name, StringComparer.OrdinalIgnoreCase)
                .Select(x => new TocItem
                {
                    Title = x.Title,
                    Url = UrlForPage(hiveRoot.FullName, x.File.FullName, baseUrl),
                    Section = x.Section,
                    Icon = x.Icon
                });

            items.AddRange(pages);

            // ---- subfolders (ordered by folders.txt then alpha)
            var manifest = ReadFoldersManifest(currentDir.FullName, manifestFileName);

            var subdirs = currentDir.EnumerateDirectories("*", SearchOption.TopDirectoryOnly)
                .Where(d => !IsHiddenOrInternal(d.Name))
                .ToArray();

            var sortedSubdirs = SortSubdirs(subdirs, manifest.Order);

            foreach (var d in sortedSubdirs)
            {
                manifest.Titles.TryGetValue(d.Name, out var overrideTitle);
                manifest.Icons.TryGetValue(d.Name, out var overrideIcon);

                items.Add(BuildFolderGroup(
                    hiveRoot,
                    d,
                    baseUrl,
                    manifestFileName,
                    indexFileName,
                    overrideTitle,
                    overrideIcon));
            }

            return items;
        }

        // -----------------------------
        // folders.txt manifest
        // -----------------------------

        private sealed class FolderManifest
        {
            public List<string> Order { get; } = [];
            public Dictionary<string, string> Titles { get; } = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, string> Icons { get; } = new(StringComparer.OrdinalIgnoreCase);

        }

        private static FolderManifest ReadFoldersManifest(string dirPath, string manifestFileName)
        {
            var m = new FolderManifest();
            var path = Path.Combine(dirPath, manifestFileName);

            if (!File.Exists(path))
                return m;

            string[] lines;
            try { lines = File.ReadAllLines(path, Utf8NoBom); }
            catch { return m; }

            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("#", StringComparison.Ordinal)) continue;

                // name|title|icon   (title, icon optional)
                var parts = line.Split('|');

                var name = parts.Length > 0 ? parts[0].Trim() : "";
                var title = parts.Length > 1 ? parts[1].Trim() : "";
                var icon = parts.Length > 2 ? parts[2].Trim() : "";

                if (name.Length == 0) continue;

                m.Order.Add(name);

                if (title.Length > 0)
                    m.Titles[name] = title;

                if (icon.Length > 0)
                    m.Icons[name] = icon;
            }

            // de-dupe (keep first)
            if (m.Order.Count > 1)
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                m.Order.RemoveAll(x => !seen.Add(x));
            }

            return m;
        }


        private static IEnumerable<DirectoryInfo> SortSubdirs(DirectoryInfo[] subdirs, List<string> orderedNames)
        {
            if (orderedNames.Count == 0)
                return subdirs.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase);

            var rank = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < orderedNames.Count; i++)
                rank[orderedNames[i]] = i;

            return subdirs
                .OrderBy(d => rank.GetValueOrDefault(d.Name, int.MaxValue))
                .ThenBy(d => d.Name, StringComparer.OrdinalIgnoreCase);
        }

        // -----------------------------
        // Front matter (uses your parser)
        // -----------------------------

        private sealed class PageMeta
        {
            public string Title { get; init; }
            public string Icon { get; init; }
            public int Order { get; init; }
            public bool? Section { get; init; }
        }

        private static PageMeta ReadFrontMatter(string mdPath)
        {
            var fm = FrontMatter.GetFrontMatter(File.ReadAllLines(mdPath));

            fm.TryGetValue("title", out var title);
            fm.TryGetValue("order", out var orderStr);
            fm.TryGetValue("icon", out var iconStr);
            bool? section = fm.ContainsKey("section") ? true : null;

            if (string.IsNullOrWhiteSpace(title))
                title = null;

            int order = 0;
            if (!string.IsNullOrWhiteSpace(orderStr) &&
                int.TryParse(orderStr.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
                order = n;

            return new PageMeta { Title = title, Order = order, Section = section, Icon = iconStr };
        }

        // -----------------------------
        // URL mapping
        // -----------------------------

        private static string NormalizeBaseUrl(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new ArgumentException("Base URL is required.", nameof(baseUrl));

            if (!baseUrl.StartsWith("/", StringComparison.Ordinal))
                baseUrl = "/" + baseUrl;

            return baseUrl.TrimEnd('/');
        }

        private static string UrlForIndex(string hiveRootDir, string indexPath, string baseUrl)
        {
            // index.md -> "/Base/relDir/"
            var dir = Path.GetDirectoryName(indexPath)!;
            var relDir = Path.GetRelativePath(hiveRootDir, dir).Replace('\\', '/').Trim('/');

            return relDir.Length == 0
                ? $"{baseUrl}/"
                : $"{baseUrl}/{relDir}/";
        }

        private static string UrlForPage(string hiveRootDir, string mdPath, string baseUrl)
        {
            // page.md -> "/Base/rel/page.html"
            var rel = Path.GetRelativePath(hiveRootDir, mdPath).Replace('\\', '/');
            rel = rel[..^Path.GetExtension(rel).Length]; // remove ".md"
            return $"{baseUrl}/{rel}.html";
        }

        // -----------------------------
        // Helpers
        // -----------------------------

        private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private static bool IsHiddenOrInternal(string name)
            => name.StartsWith(".", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("_", StringComparison.OrdinalIgnoreCase);

        private static bool StringEquals(string a, string b)
            => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        private static string ToTitle(string s) => s.Humanize(LetterCasing.Title);
    }
}