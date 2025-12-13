using Humanizer;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AngryMonkey
{
    public sealed class SiteTocBuilderOptions
    {
        public string BaseUrl { get; set; } = "/";                 // e.g. "/" or "/docs/"
        public string ReadmeFileName { get; set; } = "README.md";
        public bool AddOverviewChildForDirectories { get; set; } = true;
        public string OverviewTitle { get; set; } = "Overview";
        public string RootHomeTitle { get; set; } = "Home";
        public bool IncludeRootHome { get; set; } = true;          // adds Home -> "/"
        public bool SkipHiddenDirs { get; set; } = true;           // skips .git, .gitbook, etc.

        public HashSet<string> ExcludeDirectoryNames { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".gitbook", "node_modules", "assets"
    };

        /// <summary>
        /// Optional: provide your own title resolver (markdown -> title).
        /// If null, a built-in YAML-frontmatter "title:" + first H1 fallback is used.
        /// </summary>
        public Func<string, string> TitleResolver { get; set; }
    }

    public sealed class TocNode
    {
        public string Title { get; set; } = "";
        public string Url { get; set; }
        public List<TocNode> Children { get; set; }
    }

    public static class SiteTocBuilder
    {
        public static List<TocNode> Build(string rootDir, SiteTocBuilderOptions options = null)
        {
            options ??= new SiteTocBuilderOptions();

            rootDir = Path.GetFullPath(rootDir);
            if (!Directory.Exists(rootDir)) throw new DirectoryNotFoundException(rootDir);

            var nodes = new List<TocNode>();

            // Root README -> Home
            var rootReadme = Path.Combine(rootDir, options.ReadmeFileName);
            if (options.IncludeRootHome && File.Exists(rootReadme))
            {
                nodes.Add(new TocNode
                {
                    Title = options.RootHomeTitle,
                    Url = CombineUrl(options.BaseUrl, "/")
                });
            }

            // Root content (dirs + md files excluding root README)
            var rootChildren = BuildDirectoryChildren(rootDir, relDir: "", options);
            nodes.AddRange(rootChildren);

            // Drop empties
            nodes.RemoveAll(n => (n.Url == null) && (n.Children == null || n.Children.Count == 0));

            return nodes;
        }

        public static string ToJavascript(List<TocNode> toc, string jsVar = "window.SITE_TOC")
        {
            var json = JsonSerializer.Serialize(toc, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            return $"{jsVar} = {json};";
        }

        // -------------------- internals --------------------

        private static List<TocNode> BuildDirectoryChildren(string absDir, string relDir, SiteTocBuilderOptions options)
        {
            var results = new List<TocNode>();

            // directories
            foreach (var d in SafeGetDirectories(absDir, options))
            {
                var name = Path.GetFileName(d);
                if (options.ExcludeDirectoryNames.Contains(name)) continue;
                if (options.SkipHiddenDirs && name.StartsWith('.')) continue;

                var relChildDir = JoinRel(relDir, name);
                var node = BuildDirectoryNode(d, relChildDir, options);
                if (node != null) results.Add(node);
            }

            // md files in this dir (excluding README.md)
            foreach (var f in SafeGetMarkdownFiles(absDir, options).Where(f => !IsReadme(f, options)))
            {
                var leaf = BuildLeafFromFile(f, relDir, options);
                if (leaf != null) results.Add(leaf);
            }

            return results;
        }

        private static TocNode BuildDirectoryNode(string absDir, string relDir, SiteTocBuilderOptions options)
        {
            var readmePath = Path.Combine(absDir, options.ReadmeFileName);
            var hasReadme = File.Exists(readmePath);

            var mdFiles = SafeGetMarkdownFiles(absDir, options).Where(f => !IsReadme(f, options)).ToList();
            var subDirs = SafeGetDirectories(absDir, options).ToList();

            // Build children first (so we can skip empty dirs)
            var children = new List<TocNode>();

            // Overview leaf (clickable) for directories that have README and other content
            string dirTitleFromReadme = null;
            string dirUrl = null;
            if (hasReadme)
            {
                dirTitleFromReadme = ResolveTitle(readmePath, options) ?? HumanizeToken(Path.GetFileName(absDir));
                dirUrl = UrlForReadme(relDir, options);
            }

            // Add MD leaves (direct)
            foreach (var f in mdFiles)
            {
                var leaf = BuildLeafFromFile(f, relDir, options);
                if (leaf != null) children.Add(leaf);
            }

            // Add subgroup directories
            foreach (var d in subDirs)
            {
                var name = Path.GetFileName(d);
                if (options.ExcludeDirectoryNames.Contains(name)) continue;
                if (options.SkipHiddenDirs && name.StartsWith('.')) continue;

                var relChildDir = JoinRel(relDir, name);
                var sub = BuildDirectoryNode(d, relChildDir, options);
                if (sub != null) children.Add(sub);
            }

            // Nothing inside, but if there's only README, return leaf; otherwise skip.
            if (children.Count == 0)
            {
                if (!hasReadme) return null;

                return new TocNode
                {
                    Title = dirTitleFromReadme!,
                    Url = dirUrl!
                };
            }

            // Directory has other content -> group node (not clickable)
            var groupTitle = dirTitleFromReadme ?? HumanizeToken(Path.GetFileName(absDir));

            if (hasReadme && options.AddOverviewChildForDirectories)
            {
                children.Insert(0, new TocNode
                {
                    Title = options.OverviewTitle,
                    Url = dirUrl!
                });
            }

            return new TocNode
            {
                Title = groupTitle,
                Children = children
            };
        }

        private static TocNode BuildLeafFromFile(string absFile, string relDir, SiteTocBuilderOptions options)
        {
            var title = ResolveTitle(absFile, options)
                        ?? HumanizeToken(Path.GetFileNameWithoutExtension(absFile));

            var relFile = JoinRel(relDir, Path.GetFileName(absFile));
            var url = UrlForMd(relFile, options);

            return new TocNode { Title = title, Url = url };
        }

        private static string ResolveTitle(string absMdFile, SiteTocBuilderOptions options)
        {
            //var md = File.ReadAllText(absMdFile);

            //if (options.TitleResolver != null)
            //    return options.TitleResolver(md) ?? "";

            return TryGetFrontMatterTitle(absMdFile) ?? "";
        }

        private static bool IsReadme(string absFile, SiteTocBuilderOptions options)
            => string.Equals(Path.GetFileName(absFile), options.ReadmeFileName, StringComparison.OrdinalIgnoreCase);

        private static IEnumerable<string> SafeGetDirectories(string absDir, SiteTocBuilderOptions options)
        {
            try { return Directory.EnumerateDirectories(absDir); }
            catch { return []; }
        }

        private static IEnumerable<string> SafeGetMarkdownFiles(string absDir, SiteTocBuilderOptions options)
        {
            try { return Directory.EnumerateFiles(absDir, "*.md", SearchOption.TopDirectoryOnly); }
            catch { return []; }
        }

        private static string UrlForReadme(string relDir, SiteTocBuilderOptions options)
        {
            // README.md => /relDir/
            var p = "/" + relDir.Replace('\\', '/').Trim('/');
            if (string.IsNullOrEmpty(p) || p == "/") return CombineUrl(options.BaseUrl, "/");
            return CombineUrl(options.BaseUrl, p + "/");
        }

        private static string UrlForMd(string relFile, SiteTocBuilderOptions options)
        {
            // foo/bar.md => /foo/bar/
            var p = "/" + relFile.Replace('\\', '/').TrimStart('/');
            if (p.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                p = p[..^3];
            p = p.TrimEnd('/');
            return CombineUrl(options.BaseUrl, p + "/");
        }

        private static string CombineUrl(string baseUrl, string path)
        {
            baseUrl = (baseUrl ?? "/").Trim();
            if (!baseUrl.StartsWith("/")) baseUrl = "/" + baseUrl;
            if (!baseUrl.EndsWith("/")) baseUrl += "/";

            path = (path ?? "/").Trim();
            if (path.StartsWith("/")) path = path[1..];

            var combined = baseUrl + path;
            if (!combined.StartsWith("/")) combined = "/" + combined;
            return combined.Replace("//", "/");
        }

        private static string JoinRel(string relDir, string name)
            => string.IsNullOrEmpty(relDir) ? name : relDir + "/" + name;

        private static string HumanizeToken(string token)
            => token.Replace('_', ' ').Replace('-', ' ').Humanize(LetterCasing.Title);

        // ---- minimal frontmatter + H1 extraction ----
        private static MarkdownTitleResolver resolver = null;
        private static string TryGetFrontMatterTitle(string md)
        {
            resolver ??= new MarkdownTitleResolver(Program.CurrentHive.Destination);

            return resolver.TryGetTitle(md);
        }
    }
}