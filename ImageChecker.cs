using System.Text.RegularExpressions;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace AngryMonkey
{
    public static class Validator
    {
        // Rule:
        // - First heading (if any) must be H1
        // - Heading level may decrease to any higher-level heading (e.g., H3 -> H1 ok)
        // - Heading level may only increase by +1 max (e.g., H1 -> H2 ok, H1 -> H3 not ok)
        public static bool HasOutOfOrderHeadings(MarkdownDocument doc)
        {
            int prevLevel = 0;
            bool sawAnyHeading = false;

            foreach (var h in EnumerateHeadingBlocks(doc))
            {
                int level = h.Level;
                if ((uint)(level - 1) >= 6) continue; // ignore non 1..6

                if (!sawAnyHeading)
                {
                    sawAnyHeading = true;
                    if (level != 1) return true; // disallow starting with H2/H3/...
                    prevLevel = level;
                    continue;
                }

                if (level > prevLevel + 1) return true; // disallow jumps down the hierarchy
                prevLevel = level;
            }

            return false;
        }

        private static IEnumerable<HeadingBlock> EnumerateHeadingBlocks(ContainerBlock container)
        {
            foreach (var block in container)
            {
                switch (block)
                {
                    case HeadingBlock hb:
                        yield return hb;
                        break;
                    case ContainerBlock child:
                    {
                        foreach (var inner in EnumerateHeadingBlocks(child))
                            yield return inner;
                        break;
                    }
                }
            }
        }

        public sealed record ImageRef(string MarkdownFile, string Url, string Origin, int SpanStart, int SpanEnd);

        public sealed record MissingImage(ImageRef Ref, string ResolvedPath);

        // Configure which URL schemes to ignore as "external"
        private static readonly string[] ExternalSchemes = ["http:", "https:", "data:", "mailto:", "tel:"];

        // <img ... src="..."> (also handles single quotes and unquoted)
        private static readonly Regex ImgSrcRegex = new(@"<img\b[^>]*\bsrc\s*=\s*(?:""([^""]+)""|'([^']+)'|([^\s>]+))", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // <img ... srcset="..."> or <source ... srcset="...">
        private static readonly Regex SrcsetRegex = new(@"<(?:img|source)\b[^>]*\bsrcset\s*=\s*(?:""([^""]+)""|'([^']+)'|([^\s>]+))", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static IReadOnlyList<MissingImage> CheckMissingImages(
            string markdownFilePath,
            MarkdownDocument doc,
            string siteRoot,                          // filesystem root for leading "/" urls
            MarkdownPipeline pipeline,
            Func<string, string, string> mapUrlToPath = null)
        {
            markdownFilePath = Path.GetFullPath(markdownFilePath);
            siteRoot = Path.GetFullPath(siteRoot);

            mapUrlToPath ??= DefaultMapUrlToPath;

            // var markdown = File.ReadAllText(markdownFilePath);

            var refs = CollectImageRefs(doc, markdownFilePath);

            var missing = new List<MissingImage>(capacity: 16);
            foreach (var r in refs)
            {
                if (!TryNormalizeLocalUrl(r.Url, out var normalizedLocal))
                    continue;

                var resolved = mapUrlToPath(markdownFilePath, normalizedLocal);
                if (!File.Exists(resolved))
                    missing.Add(new MissingImage(r, resolved));
            }

            return missing;
        }

        public static IReadOnlyList<ImageRef> CollectImageRefs(MarkdownDocument doc, string markdownFilePath)
        {
            var list = new List<ImageRef>(capacity: 32);

            foreach (var node in doc.Descendants())
            {
                switch (node)
                {
                    // Markdown image: ![](...)
                    case LinkInline { IsImage: true } li:
                    {
                        var url = li.Url ?? "";
                        list.Add(new ImageRef(markdownFilePath, url, "markdown", li.Span.Start, li.Span.End));
                        continue;
                    }
                    // Raw HTML inline: <img ...>
                    case HtmlInline hi:
                        ExtractFromHtml(hi.Tag, markdownFilePath, hi.Span.Start, hi.Span.End, list);
                        continue;
                    // Raw HTML block: <img ...> spanning lines
                    case HtmlBlock hb:
                    {
                        var html = hb.Lines.ToString();
                        ExtractFromHtml(html, markdownFilePath, hb.Span.Start, hb.Span.End, list);
                        break;
                    }
                }
            }

            return list;
        }

        private static void ExtractFromHtml(
            string html,
            string markdownFilePath,
            int spanStart,
            int spanEnd,
            List<ImageRef> outList)
        {
            foreach (Match m in ImgSrcRegex.Matches(html))
            {
                var url = FirstNonEmpty(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value);
                if (!string.IsNullOrWhiteSpace(url))
                    outList.Add(new ImageRef(markdownFilePath, url.Trim(), "html-src", spanStart, spanEnd));
            }

            foreach (Match m in SrcsetRegex.Matches(html))
            {
                var srcset = FirstNonEmpty(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value);
                foreach (var candidateUrl in ParseSrcsetUrls(srcset))
                {
                    outList.Add(new ImageRef(markdownFilePath, candidateUrl, "html-srcset", spanStart, spanEnd));
                }
            }
        }

        private static IEnumerable<string> ParseSrcsetUrls(string srcset)
        {
            if (string.IsNullOrWhiteSpace(srcset))
                yield break;

            // "a.webp 1x, b.webp 2x" OR "a.webp 480w, b.webp 960w"
            // URL is the first token of each comma-separated candidate.
            foreach (var part in srcset.Split(','))
            {
                var trimmed = part.Trim();
                if (trimmed.Length == 0) continue;

                var firstToken = trimmed.Split((char[])null, StringSplitOptions.RemoveEmptyEntries)
                                        .FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(firstToken))
                    yield return firstToken;
            }
        }

        private static bool TryNormalizeLocalUrl(string url, out string normalized)
        {
            normalized = "";

            if (string.IsNullOrWhiteSpace(url))
                return false;

            url = url.Trim();

            // ignore templated/unknown
            if (url.Contains("{{") || url.Contains("}}"))
                return false;

            // strip fragment/query
            url = StripQueryAndFragment(url);

            // ignore anchors / empty after strip
            if (url.Length == 0 || url[0] == '#')
                return false;

            // ignore protocol-relative
            if (url.StartsWith("//", StringComparison.Ordinal))
                return false;

            // ignore external schemes
            foreach (var s in ExternalSchemes)
                if (url.StartsWith(s, StringComparison.OrdinalIgnoreCase))
                    return false;

            // unescape %20, etc.
            try { url = Uri.UnescapeDataString(url); } catch { /* keep raw */ }

            normalized = url;
            return true;
        }

        private static string StripQueryAndFragment(string url)
        {
            var q = url.IndexOf('?');
            var h = url.IndexOf('#');

            var cut = -1;
            switch (q)
            {
                case >= 0 when h >= 0:
                    cut = Math.Min(q, h);
                    break;

                case >= 0:
                    cut = q;
                    break;

                default:
                {
                    if (h >= 0) cut = h;
                    break;
                }
            }

            return cut >= 0 ? url[..cut] : url;
        }

        private static string DefaultMapUrlToPath(string markdownFilePath, string normalizedUrl)
        {
            var mdDir = Path.GetDirectoryName(markdownFilePath)!;

            // leading "/" => from site root
            if (normalizedUrl.StartsWith("/", StringComparison.Ordinal))
            {
                // caller should pass siteRoot via closure if desired;
                // this default assumes siteRoot == repo root == mdDir ancestor isn’t knowable here.
                // Prefer providing mapUrlToPath for correctness.
                throw new InvalidOperationException("Provide mapUrlToPath to resolve leading '/' URLs.");
            }

            // relative URL => relative to markdown file
            var rel = normalizedUrl.Replace('/', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(mdDir, rel));
        }

        private static string FirstNonEmpty(params string[] values)
            => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "";
    }
}