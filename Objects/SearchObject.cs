using Markdig;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System.Text;
using System.Text.RegularExpressions;

namespace AngryMonkey.Objects
{
    // Add this to your SearchObject (or create a new DTO)
    public sealed class SearchHeading
    {
        public string id { get; set; } = "";
        public string text { get; set; } = "";
        public int level { get; set; }
        public string ctx { get; set; } // optional: first paragraph after heading
    }

    public sealed class SearchObject
    {
        public string hive { get; set; } = "";
        public string text { get; set; } = "";
        public string title { get; set; } = "";
        public string url { get; set; } = "";
        public List<SearchHeading> headings { get; set; } = [];
    }

    public static class SearchBuilder
    {
        public static SearchObject ToSearchObject(string markdown, string title, Hive hive, string url)
        {
            // IMPORTANT: Program.pipeline must include UseAutoIdentifiers(...) so headings get stable ids.
            // e.g.: new MarkdownPipelineBuilder().UseAdvancedExtensions().UseAutoIdentifiers(AutoIdentifierOptions.GitHub).Build();

            var plain = Markdown.ToPlainText(markdown, Program.pipeline);

            var md = Markdown.Parse(markdown, Program.pipeline);

            var heads = md.Descendants<HeadingBlock>()
                .Select(h =>
                {
                    var text = InlineToText(h.Inline).Trim();
                    if (string.IsNullOrWhiteSpace(text)) return null;

                    var id = h.TryGetAttributes()?.Id;
                    if (string.IsNullOrWhiteSpace(id))
                        id = Slugify(text); // fallback (only matches your HTML if your renderer uses the same rule)

                    return new SearchHeading
                    {
                        id = id!,
                        text = text,
                        level = h.Level,
                        ctx = GetNextParagraphText(h)?.Trim()
                    };
                })
                .Where(x => x != null)
                .Cast<SearchHeading>()
                .ToList();

            return new SearchObject
            {
                hive = hive.Name,
                text = plain,
                title = title,
                url = url,
                headings = heads
            };
        }

        private static string InlineToText(ContainerInline inline)
        {
            if (inline is null) return "";
            var sb = new StringBuilder();
            foreach (var lit in inline.Descendants().OfType<LiteralInline>())
            {
                var slice = lit.Content;
                sb.Append(slice.Text?.Substring(slice.Start, slice.Length));
            }
            return sb.ToString();
        }

        private static string GetNextParagraphText(HeadingBlock h)
        {
            // Markdig doesn't expose NextSibling; walk the parent block list instead.
            if (h.Parent is not { } parent) return null;

            var seen = false;

            foreach (var b in parent)
            {
                if (!seen)
                {
                    if (ReferenceEquals(b, h)) seen = true;
                    continue;
                }

                if (b is HeadingBlock) break;

                if (b is ParagraphBlock p)
                {
                    var t = InlineToText(p.Inline).Trim();
                    if (t.Length == 0) continue;
                    return t.Length > 220 ? t[..219] + "…" : t;
                }
            }

            return null;
        }

        private static string Slugify(string s)
        {
            s = s.ToLowerInvariant().Trim();
            s = Regex.Replace(s, @"[^\w\s-]", "");     // remove punctuation
            s = Regex.Replace(s, @"\s+", "-");        // spaces -> dashes
            s = Regex.Replace(s, @"-+", "-");         // collapse dashes
            return s.Trim('-');
        }
    }
}