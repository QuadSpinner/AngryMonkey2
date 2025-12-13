using System.Text.Json;
using System.Text.Json.Serialization;

namespace AngryMonkey
{
    public sealed class SummaryTocOptions
    {
        public string BaseUrl { get; set; } = "/";          // e.g. "/" or "/docs/"
        public string ReadmeFileName { get; set; } = "README.md";

        // Makes "parent" non-clickable and inserts a clickable child:
        // Terrains (README.md) + children => Terrains (group) + [Overview](.../terrains/)
        public bool PromoteParentLinksToGroups { get; set; } = true;

        public string OverviewTitle { get; set; } = "Overview";
    }

    public static class SummaryToc
    {
        public static List<TocNode> BuildFromFile(string summaryMdPath, SummaryTocOptions options = null)
            => BuildFromText(File.ReadAllText(summaryMdPath), options);

        public static List<TocNode> BuildFromText(string summaryMd, SummaryTocOptions options = null)
        {
            options ??= new SummaryTocOptions();

            var root = new List<TocNode>();

            // Headings (## ...) create groups. Bullets under the current heading go inside it.
            var headingStack = new Stack<(int level, TocNode node, List<TocNode> list)>();

            // Bullet nesting by indentation under the current container (root or current heading group)
            var bulletStack = new Stack<(int indent, TocNode node, List<TocNode> list)>();

            List<TocNode> CurrentContainer() => headingStack.Count > 0 ? headingStack.Peek().list : root;

            void ResetBulletStack()
            {
                bulletStack.Clear();
                bulletStack.Push((-1, null, CurrentContainer()));
            }

            ResetBulletStack();

            foreach (var raw in ReadLines(summaryMd))
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;

                if (TryParseHeading(raw, out var hLevel, out var hText))
                {
                    while (headingStack.Count > 0 && headingStack.Peek().level >= hLevel)
                        headingStack.Pop();

                    var group = new TocNode { Title = hText, Children = [] };

                    if (headingStack.Count == 0) root.Add(group);
                    else headingStack.Peek().list.Add(group);

                    headingStack.Push((hLevel, group, group.Children!));
                    ResetBulletStack();
                    continue;
                }

                if (!TryParseBulletLink(raw, out var indent, out var title, out var target))
                    continue;

                while (bulletStack.Count > 0 && indent <= bulletStack.Peek().indent)
                    bulletStack.Pop();

                var parentList = bulletStack.Count > 0 ? bulletStack.Peek().list : CurrentContainer();

                var node = new TocNode
                {
                    Title = title,
                    Url = ToUrl(target, options),
                    Children = [] // filled if deeper indents follow
                };

                parentList.Add(node);
                bulletStack.Push((indent, node, node.Children!));
            }

            PruneEmptyChildren(root);

            if (options.PromoteParentLinksToGroups)
                PromoteParentLinks(root, options.OverviewTitle);

            return root;
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

        // ---------------- parsing ----------------

        private static bool TryParseHeading(string line, out int level, out string text)
        {
            level = 0; text = "";

            var t = line.TrimStart();
            if (t.Length < 3 || t[0] != '#') return false;

            int i = 0;
            while (i < t.Length && t[i] == '#') i++;

            if (i < 2) return false;                 // ignore "#"
            if (i < t.Length && t[i] != ' ') return false;

            level = i;
            text = t[(i + 1)..].Trim();
            return text.Length > 0;
        }

        private static bool TryParseBulletLink(string line, out int indent, out string title, out string target)
        {
            indent = CountIndent(line);
            title = ""; target = "";

            var t = line.TrimStart();
            if (!(t.StartsWith("* ") || t.StartsWith("- "))) return false;

            t = t[2..].TrimStart();
            if (!t.StartsWith("[", StringComparison.Ordinal)) return false;

            var closeTitle = t.IndexOf(']');
            if (closeTitle < 0) return false;

            var afterTitle = closeTitle + 1;
            if (afterTitle >= t.Length || t[afterTitle] != '(') return false;

            var closeParen = t.IndexOf(')', afterTitle + 1);
            if (closeParen < 0) return false;

            title = t[1..closeTitle].Trim();
            target = t[(afterTitle + 1)..closeParen].Trim();

            return title.Length > 0 && target.Length > 0;
        }

        private static int CountIndent(string line)
        {
            int n = 0;
            foreach (var ch in line)
            {
                if (ch == ' ') n++;
                else if (ch == '\t') n += 4;
                else break;
            }
            return n;
        }

        private static IEnumerable<string> ReadLines(string s)
        {
            using var sr = new StringReader(s);
            while (sr.ReadLine() is { } line)
                yield return line;
        }

        // ---------------- url mapping ----------------

        private static string ToUrl(string linkTarget, SummaryTocOptions opt)
        {
            if (linkTarget.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                linkTarget.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return linkTarget;

            var p = linkTarget.Replace('\\', '/').Trim();
            var cut = p.IndexOfAny(['?', '#']);
            if (cut >= 0) p = p[..cut];

            p = p.TrimStart('/');

            // README.md => /dir/
            if (p.EndsWith("/" + opt.ReadmeFileName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p, opt.ReadmeFileName, StringComparison.OrdinalIgnoreCase))
            {
                var dir = Path.GetDirectoryName(p)?.Replace('\\', '/').Trim('/') ?? "";
                var urlPath = dir.Length == 0 ? "/" : "/" + dir + "/";
                return CombineUrl(opt.BaseUrl, urlPath);
            }

            // foo/bar.md => /foo/bar/
            if (p.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                p = p[..^3] + ".html";

            // p = "/" + p.Trim('/');
            return CombineUrl(opt.BaseUrl, p);
        }

        private static string CombineUrl(string baseUrl, string path)
        {
            baseUrl = (baseUrl ?? "/").Trim();
            if (!baseUrl.StartsWith("/")) baseUrl = "/" + baseUrl;
            if (!baseUrl.EndsWith("/")) baseUrl += "/";

            path = (path ?? "/").Trim();
            if (path.StartsWith("/")) path = path[1..];

            var combined = baseUrl + path;
            while (combined.Contains("//", StringComparison.Ordinal))
                combined = combined.Replace("//", "/", StringComparison.Ordinal);

            return combined;
        }

        // ---------------- post-processing ----------------

        private static void PruneEmptyChildren(List<TocNode> nodes)
        {
            foreach (var n in nodes)
            {
                if (n.Children == null) continue;

                PruneEmptyChildren(n.Children);
                if (n.Children.Count == 0) n.Children = null;
            }
        }

        private static void PromoteParentLinks(List<TocNode> nodes, string overviewTitle)
        {
            foreach (var n in nodes)
            {
                if (n.Children is { Count: > 0 })
                {
                    if (!string.IsNullOrWhiteSpace(n.Url))
                    {
                        var url = n.Url;
                        n.Url = null;

                        n.Children.Insert(0, new TocNode { Title = overviewTitle, Url = url });
                    }

                    PromoteParentLinks(n.Children, overviewTitle);
                }
            }
        }
    }
}