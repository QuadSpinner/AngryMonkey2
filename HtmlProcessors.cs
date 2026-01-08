using AngryMonkey.Objects;
using Humanizer;
using System.Text;
using System.Text.RegularExpressions;

namespace AngryMonkey
{
    internal static class HtmlProcessors
    {
        internal static string GetFlubTable(string nodeName, Flub[] flubs)
        {
            StringBuilder sb = new();

            sb.AppendLine("<table class=\"properties-table\"><tbody>");

            if (flubs[0].Description != "T" && !flubs[0].IsGroup)
            {
                sb.AppendLine($"<tr><td colspan='2' class='head'><span class='title'>{nodeName}</span></td></tr>");
            }

            foreach (Flub flub in flubs)
            {
                if (flub.IsGroup)
                {
                    sb.AppendLine($"<tr><td colspan='2' class='head'><span class='title'>{flub.Name.Humanize(LetterCasing.Title)}</span><span class='title-desc'>{flub.Description}</span></td></tr>");
                }
                else
                {
                    if (flub.Flubs == null || (flub.Flubs != null && flub.Flubs.All(x => string.IsNullOrEmpty(x.Description))))
                    {
                        sb.AppendLine($"<tr><td>{flub.Name.Humanize(LetterCasing.Title)}</td><td>{flub.Description}</td></tr>");
                    }
                    else
                    {
                        sb.AppendLine($"<tr><td>{flub.Name.Humanize(LetterCasing.Title)}</td>" +
                                      $"<td>{flub.Description}" +
                                      "<div class=\"param-spacer\"></div>");

                        foreach (Flub flubFlub in flub.Flubs)
                        {
                            sb.AppendLine($"<span class=\"choice\">{flubFlub.Name.Humanize(LetterCasing.Title)}</span>" +
                                          $"<span class=\"choice-description\">{flubFlub.Description}</span>");
                        }

                        sb.AppendLine("</td></tr>");
                    }
                }
            }

            sb.AppendLine("</tbody>");
            sb.AppendLine("</table>");

            return sb.ToString();
        }

        // Matches: {% include "/path/file.md" %}  or  {% include '/path/file.md' %}
        private static readonly Regex IncludeRx = new(
            """\{\%\s*include\s+(?:"(?<path>[^"]+)"|'(?<path>[^']+)')\s*\%\}""",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        internal static string ExpandIncludes(
            string text,
            string rootDirectory,
            int maxDepth = 2,
            bool throwOnMissing = true)
        {
            if (text is null) throw new ArgumentNullException(nameof(text));
            if (rootDirectory is null) throw new ArgumentNullException(nameof(rootDirectory));

            Encoding encoding = Encoding.UTF8;
            rootDirectory = Path.GetFullPath(rootDirectory);

            var cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (var depth = 0; depth < maxDepth; depth++)
            {
                var any = false;

                text = IncludeRx.Replace(text, m =>
                {
                    any = true;

                    var raw = m.Groups["path"].Value.Trim();
                    var fullPath = ResolveIncludePath(rootDirectory, raw);

                    if (!File.Exists(fullPath))
                    {
                        return throwOnMissing ? throw new FileNotFoundException($"Include file not found: {raw}", fullPath) : m.Value; // keep original tag
                    }

                    if (!cache.TryGetValue(fullPath, out var included))
                    {
                        included = File.ReadAllText(fullPath, encoding);
                        cache[fullPath] = included;
                    }

                    return included;
                });

                if (!any) break;
            }

            return text;
        }

        private static string ResolveIncludePath(string rootDirectory, string includePath)
        {
            // Treat "/..." or "\..." as root-relative (relative to rootDirectory), not OS-root.
            if (includePath.StartsWith("/") || includePath.StartsWith("\\"))
            {
                includePath = includePath.TrimStart('/', '\\');
                includePath = includePath.Replace('/', Path.DirectorySeparatorChar)
                                         .Replace('\\', Path.DirectorySeparatorChar);
                return Path.GetFullPath(Path.Combine(rootDirectory, includePath));
            }

            // Absolute (e.g., "C:\...") stays absolute; otherwise relative to rootDirectory.
            if (!Path.IsPathRooted(includePath))
            {
                includePath = includePath.Replace('/', Path.DirectorySeparatorChar)
                                         .Replace('\\', Path.DirectorySeparatorChar);
                return Path.GetFullPath(Path.Combine(rootDirectory, includePath));
            }

            return Path.GetFullPath(includePath);
        }
    }
}
