using System.Text;

namespace AngryMonkey
{
    public static class FrontMatter
    {
        public static string EnsureYamlTitle(string markdown, string title, bool replaceExisting = true)
        {
            if (markdown is null) throw new ArgumentNullException(nameof(markdown));
            title ??= "";

            // Preserve BOM + newline style
            var hasBom = markdown.Length > 0 && markdown[0] == '\uFEFF';
            var bom = hasBom ? "\uFEFF" : "";
            var src = hasBom ? markdown[1..] : markdown;

            var nl = src.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

            // Normalize for parsing
            var norm = src.Replace("\r\n", "\n").Replace("\r", "\n");
            var lines = norm.Split('\n');

            // Allow leading blank lines before YAML (optional)
            int i = 0;
            while (i < lines.Length && string.IsNullOrWhiteSpace(lines[i])) i++;

            bool looksLikeYamlStart = i < lines.Length && IsDelimiter(lines[i], "---");
            if (looksLikeYamlStart)
            {
                int start = i;
                int end = -1;

                for (int j = start + 1; j < lines.Length; j++)
                {
                    if (IsDelimiter(lines[j], "---") || IsDelimiter(lines[j], "..."))
                    {
                        end = j;
                        break;
                    }
                }

                if (end != -1)
                {
                    // YAML content is (start+1 .. end-1)
                    var yaml = lines[(start + 1)..end].ToArray();
                    var escapedTitle = title; //YamlDoubleQuoted(title);

                    int titleLine = FindTopLevelKeyLine(yaml, "title");
                    if (titleLine >= 0)
                    {
                        if (replaceExisting)
                            yaml[titleLine] = $"title: {escapedTitle}";
                    }
                    else
                    {
                        yaml = yaml.Concat([$"title: {escapedTitle}"]).ToArray();
                    }

                    // Rebuild
                    var rebuilt = string.Join("\n", lines[..(start + 1)])
                                + "\n"
                                + string.Join("\n", yaml)
                                + "\n"
                                + string.Join("\n", lines[end..]);

                    return bom + rebuilt.Replace("\n", nl);
                }
            }

            // No valid YAML front matter -> add one at top
            var header = $"---\n" +
                         $"title: {title}\n" +
                         $"---\n\n";

            return bom + (header + norm).Replace("\n", nl);
        }

        public static void EnsureYamlTitleInFolder(string root, Func<string, string> titleSelector = null)
        {
            titleSelector ??= Path.GetFileNameWithoutExtension;

            foreach (var file in Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories))
            {
                var original = File.ReadAllText(file, Encoding.UTF8);
                var updated = EnsureYamlTitle(original, titleSelector(file));

                if (!string.Equals(original, updated, StringComparison.Ordinal))
                    File.WriteAllText(file, updated, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
        }

        private static bool IsDelimiter(string line, string token)
            => string.Equals(line.Trim(), token, StringComparison.Ordinal);

        private static int FindTopLevelKeyLine(string[] yamlLines, string key)
        {
            // naive but effective for typical front matter: ignore comments; match "key:"
            for (int i = 0; i < yamlLines.Length; i++)
            {
                var s = yamlLines[i].TrimStart();
                if (s.StartsWith("#")) continue;
                if (s.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                {
                    var rest = s[key.Length..];
                    if (rest.Length > 0 && rest[0] == ':')
                        return i;
                }
            }
            return -1;
        }

        //private static string YamlDoubleQuoted(string value)
        //{
        //    // safest “vanilla” quoting
        //    var v = value
        //        .Replace("\\", "\\\\")
        //        .Replace("\"", "\\\"")
        //        .Replace("\t", "\\t")
        //        .Replace("\n", "\\n")
        //        .Replace("\r", "\\r");
        //    return $"\"{v}\"";
        //}
    }
}