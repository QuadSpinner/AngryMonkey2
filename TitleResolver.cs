using System.Collections.Concurrent;
using System.Text;
using Markdig.Helpers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace AngryMonkey
{
    public sealed class MarkdownTitleResolver(string docsRootFullPath)
    {
        private readonly string _docsRootFullPath = Path.GetFullPath(docsRootFullPath);
        private readonly ConcurrentDictionary<string, string> _titleCache = new(StringComparer.OrdinalIgnoreCase);

        public string TryGetTitle(string markdownFileFullPath)
            => _titleCache.GetOrAdd(Path.GetFullPath(markdownFileFullPath), ReadYamlTitleOrNull);

        public bool TryResolveTitleFromLink(string currentMarkdownFileFullPath, string linkUrl, out string title)
        {
            title = "";
            var target = TryResolveLinkedMarkdownPath(currentMarkdownFileFullPath, linkUrl);
            if (target is null) return false;

            var yamlTitle = TryGetTitle(target);
            title = !string.IsNullOrWhiteSpace(yamlTitle)
                ? yamlTitle!
                : HumanizeFileName(Path.GetFileNameWithoutExtension(target));

            return true;
        }

        private string TryResolveLinkedMarkdownPath(string currentMarkdownFileFullPath, string linkUrl)
        {
            if (string.IsNullOrWhiteSpace(linkUrl)) return null;

            // ignore external-ish links
            if (linkUrl.StartsWith("http:", StringComparison.OrdinalIgnoreCase) ||
                linkUrl.StartsWith("https:", StringComparison.OrdinalIgnoreCase) ||
                linkUrl.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                return null;

            var clean = StripQueryAndFragment(linkUrl);
            if (!clean.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) return null;

            clean = clean.Replace('/', Path.DirectorySeparatorChar);

            string full;
            try
            {
                full = Path.GetFullPath(Path.IsPathRooted(clean) ? Path.Combine(_docsRootFullPath, clean.TrimStart(Path.DirectorySeparatorChar)) : Path.Combine(Path.GetDirectoryName(currentMarkdownFileFullPath)!, clean));
            }
            catch
            {
                return null;
            }

            return File.Exists(full) ? full : null;
        }

        private static string StripQueryAndFragment(string url)
        {
            var q = url.IndexOf('?');
            var h = url.IndexOf('#');

            var cut = -1;
            if (q >= 0) cut = q;
            if (h >= 0) cut = cut < 0 ? h : Math.Min(cut, h);

            return cut < 0 ? url : url[..cut];
        }

        private static string ReadYamlTitleOrNull(string fullPath)
        {
            // Fast + “vanilla”: only read the front of the file, don’t parse YAML fully.
            using var fs = File.OpenRead(fullPath);
            using var sr = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: false);

            string line;

            // find first non-empty line
            while ((line = sr.ReadLine()) != null)
            {
                if (!string.IsNullOrWhiteSpace(line)) break;
            }

            if (line is null) return null;
            if (!IsYamlFence(line)) return null;

            // scan until closing fence; look for "title:"
            var maxLines = 200;
            while (maxLines-- > 0 && (line = sr.ReadLine()) != null)
            {
                var t = line.Trim();
                if (IsYamlFence(t) || t == "...") break;

                if (t.StartsWith("title:", StringComparison.OrdinalIgnoreCase))
                    return ParseYamlScalar(t["title:".Length..].Trim());
            }

            return null;
        }

        private static bool IsYamlFence(string line) => line.Trim() == "---";

        private static string ParseYamlScalar(string raw)
        {
            if (raw.Length == 0) return "";

            if ((raw[0] == '"' && raw.Length >= 2 && raw[^1] == '"') ||
                (raw[0] == '\'' && raw.Length >= 2 && raw[^1] == '\''))
                return raw[1..^1];

            return raw;
        }

        private static string HumanizeFileName(string name)
        {
            // command-line-interface -> Command Line Interface
            var parts = name.Replace('_', '-')
                            .Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return string.Join(" ", parts.Select(p => p.Length == 0 ? p : char.ToUpperInvariant(p[0]) + p[1..]));
        }
    }

    public static class MarkdownLinkTextToTargetTitle
    {
        public static void Rewrite(MarkdownDocument doc, string currentMarkdownFileFullPath, MarkdownTitleResolver resolver)
        {
            foreach (var link in doc.Descendants().OfType<LinkInline>()) // supported traversal pattern :contentReference[oaicite:0]{index=0}
            {
                if (link.IsImage) continue;
                if (string.IsNullOrWhiteSpace(link.Url)) continue;

                if (!TryGetSingleLiteralLabel(link, out var label)) continue;
                if (!label.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) continue;

                if (!resolver.TryResolveTitleFromLink(currentMarkdownFileFullPath, link.Url!, out var title)) continue;

                link.Remove(); // RemoveAll();
                link.AppendChild(new LiteralInline { Content = new StringSlice(title) }); // LiteralInline + StringSlice pattern :contentReference[oaicite:1]{index=1}
            }
        }

        private static bool TryGetSingleLiteralLabel(LinkInline link, out string label)
        {
            label = "";
            if (link.FirstChild is not LiteralInline lit) return false;
            if (!ReferenceEquals(link.FirstChild, link.LastChild)) return false;

            label = lit.Content.ToString();
            return true;
        }
    }
}