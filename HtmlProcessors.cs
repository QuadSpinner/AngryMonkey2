using AngryMonkey.Objects;
using Humanizer;
using System.Text;
using System.Text.RegularExpressions;
using AngryMonkey.POCO;

namespace AngryMonkey
{
    internal static class HtmlProcessors
    {

        internal static string GetNodeMap(NodeMetadata[] meta)
        {
            string[] cats = ["Primitive", "Terrain", "Modify", "Surface", "Simulate", "Derive", "Colorize", "Output", "Utility"];

            StringBuilder sb = new();
            sb.AppendLine("---\r\nicon: location-dot\r\ntitle: Node Map\r\nuid: node-map\r\norder: 01\r\n---\r\n\r\n# Node Map\r\n\r\n");
            sb.AppendLine(":::reference-tables");
            foreach (string c in cats)
            {
                string family = "";
                NodeMetadata[] nodesInCat = meta.Where(x => x.Toolbox == c).OrderBy(x => x.Family).ThenBy(x => x.Name).ToArray();

                sb.AppendLine($"\n### {c}\n");

                sb.AppendLine("| Family | Node | Shortcode |");
                sb.AppendLine("| ------ | ---- | --------- |");

                foreach (NodeMetadata m in nodesInCat)
                {
                    string localFamily = "";
                    if (family != m.Family)
                    {
                        family = m.Family;
                        localFamily = family;
                    }
                    else
                    {
                        localFamily = "";
                    }

                    sb.AppendLine($"| {localFamily} | @{m.Name.ToLower()} <br> <em>{m.Description}</em> | `{m.ShortCode}` |");
                }

                sb.AppendLine();
            }
            sb.AppendLine(":::");

            return sb.ToString();
        }


        internal static (string, string) GetFlubTable(string nodeName, Flub[] flubs)
        {
            Flub[] normals = flubs.Where(x => x.Type != "Command").ToArray();
            Flub[] commands = flubs.Where(x => x.Type == "Command").ToArray();

            string icon = "";
            StringBuilder sb = new();
            StringBuilder md = new();

            md.AppendLine("| Property | Description |");
            md.AppendLine("| --- | --- |");

            sb.AppendLine("<table class=\"properties-table\"><tbody>");

            if (normals[0].Description != "T" && !normals[0].IsGroup)
            {
                sb.AppendLine($"<tr><td colspan='2' class='head'><span class='title'>{nodeName}</span></td></tr>");
            }

            foreach (Flub flub in normals)
            {
                icon = "";
                if (flub.Type != null)
                {
                    icon = parameterIcons.ContainsKey(flub.Type) ? parameterIcons[flub.Type] : "";
                }

                if (flub.IsGroup)
                {
                    sb.AppendLine($"<tr><td colspan='2' class='head'><span class='title'>{flub.Name.Humanize(LetterCasing.Title)}</span><span class='title-desc'>{flub.Description}</span></td></tr>");
                    md.AppendLine($"|{flub.Name}|{flub.Description}|");
                }
                else
                {
                    if (flub.Flubs == null || (flub.Flubs != null && flub.Flubs.All(x => string.IsNullOrEmpty(x.Description))))
                    {
                        sb.AppendLine($"<tr><td data-type='{flub.Type}'>{icon} {flub.Name.Humanize(LetterCasing.Title)}</td><td>{flub.Description}</td></tr>");
                        md.AppendLine($"|{flub.Name}|{flub.Description}|");
                    }
                    else
                    {
                        sb.AppendLine($"<tr><td data-type='{flub.Type}'>{icon} {flub.Name.Humanize(LetterCasing.Title)}</td>" +
                                      $"<td>{flub.Description}" +
                                      "<div class=\"param-spacer\"></div>");

                        md.AppendLine($"|{flub.Name}|{flub.Description}|");
                        foreach (Flub flubFlub in flub.Flubs)
                        {
                            sb.AppendLine($"<span class=\"choice\">{icon} {flubFlub.Name.Humanize(LetterCasing.Title)}</span>" +
                                          $"<span class=\"choice-description\">{flubFlub.Description}</span>");
                            md.AppendLine($"|{flubFlub.Name}|{flubFlub.Description}|");

                        }

                        sb.AppendLine("</td></tr>");
                    }
                }
            }

            if (commands.Any())
            {
                sb.AppendLine($"<tr><td colspan='2' class='head'><span class='title'>Commands</span></td></tr>");

                foreach (Flub command in commands)
                {
                    icon = parameterIcons["Command"];

                    sb.AppendLine($"<tr><td data-type='{command.Type}'>{icon} {command.Name.Humanize(LetterCasing.Title)}</td><td>{command.Description}</td></tr>");
                    md.AppendLine($"| Command: {command.Name} | {command.Description}");
                }
            }

            sb.AppendLine("</tbody>");
            sb.AppendLine("</table>");

            return (sb.ToString(), md.ToString());
        }

        private static readonly Dictionary<string, string> parameterIcons = new()
        {
            {"Single", "<i data-bs-toggle='tooltip' data-bs-placement='bottom' title='A single decimal number.' class=\"ti ti-decimal\"></i>"},
            {"Int32", "<i data-bs-toggle='tooltip' data-bs-placement='bottom' title='An integer (whole number).' class=\"ti ti-number-123\"></i>"},
            {"Enum", "<i data-bs-toggle='tooltip' data-bs-placement='bottom' title='A selectable list of predefined options.' class=\"ti ti-selector\"></i>"},
            {"Boolean", "<i data-bs-toggle='tooltip' data-bs-placement='bottom' title='A true or false value.' class=\"ti ti-toggle-left\"></i>"},
            {"Command", "<i data-bs-toggle='tooltip' data-bs-placement='bottom' title='An executable command or action.' class=\"ti ti-input-spark\"></i>"},
            {"Float2", "<i data-bs-toggle='tooltip' data-bs-placement='bottom' title='A pair of decimal numbers representing a range.' class=\"ti ti-brackets-contain\"></i>"},
            {"String", "<i data-bs-toggle='tooltip' data-bs-placement='bottom' title='A text or file input.' class=\"ti ti-forms\"></i>"},
        };

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