using System.Text.RegularExpressions;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace AngryMonkey
{
    public static class ImageChecker
    {
        public sealed record ImageRef(
            string MarkdownFile,
            string Url,
            string Origin,          // "markdown" | "html-src" | "html-srcset"
            int SpanStart,
            int SpanEnd);

        public sealed record MissingImage(ImageRef Ref, string ResolvedPath);

        // Configure which URL schemes to ignore as "external"
        private static readonly string[] ExternalSchemes = ["http:", "https:", "data:", "mailto:", "tel:"];

        // <img ... src="..."> (also handles single quotes and unquoted)
        private static readonly Regex ImgSrcRegex = new(@"<img\b[^>]*\bsrc\s*=\s*(?:""([^""]+)""|'([^']+)'|([^\s>]+))", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // <img ... srcset="..."> or <source ... srcset="...">
        private static readonly Regex SrcsetRegex = new(@"<(?:img|source)\b[^>]*\bsrcset\s*=\s*(?:""([^""]+)""|'([^']+)'|([^\s>]+))", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static IReadOnlyList<MissingImage> CheckFile(
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
                // Markdown image: ![](...)
                if (node is LinkInline { IsImage: true } li)
                {
                    var url = li.Url ?? "";
                    list.Add(new ImageRef(markdownFilePath, url, "markdown", li.Span.Start, li.Span.End));
                    continue;
                }

                // Raw HTML inline: <img ...>
                if (node is HtmlInline hi)
                {
                    ExtractFromHtml(hi.Tag, markdownFilePath, hi.Span.Start, hi.Span.End, list);
                    continue;
                }

                // Raw HTML block: <img ...> spanning lines
                if (node is HtmlBlock hb)
                {
                    var html = hb.Lines.ToString();
                    ExtractFromHtml(html, markdownFilePath, hb.Span.Start, hb.Span.End, list);
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
            if (q >= 0 && h >= 0) cut = Math.Min(q, h);
            else if (q >= 0) cut = q;
            else if (h >= 0) cut = h;

            return cut >= 0 ? url.Substring(0, cut) : url;
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