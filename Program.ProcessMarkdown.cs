using System.Reflection.Metadata.Ecma335;
using System.Text;
using AngryMonkey.Objects;
using AngryMonkey.POCO;
using Humanizer;
using Markdig;
using Markdig.Helpers;
using Markdig.Syntax;

namespace AngryMonkey;

public static partial class Program
{
    public static void ProcessMarkdown(Hive hive)
    {
        string[] md = Directory.GetFiles(
            hive.Destination, "*.md",
            hive.IsHome ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories);

        var po = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) // tweak as you like
        };

        Parallel.ForEach(md, po, file =>
        {
            try
            {
                string html = Html;


                if (Path.GetFileNameWithoutExtension(file) == "node-map")
                {
                    File.WriteAllText(file, HtmlProcessors.GetNodeMap(Meta.Values.ToArray()));
                }

                string content = File.ReadAllText(file); // keep raw content

                content = ProcessSlugs(content);

                if (!PageByDestMd.TryGetValue(file, out var page))
                    throw new InvalidOperationException($"No page metadata for: {file}");

                string title = page.Title;
                string uid = page.UID;

                content = HtmlProcessors.ExpandIncludes(content, $@"{RootFolder}\Source", 2, false);

                MarkdownDocument doc = Markdown.Parse(content, pipeline);

                // if first element is a heading and is same as title, remove it
                if (doc.Count > 0 && doc.FirstOrDefault(x => x is HeadingBlock) is HeadingBlock heading)
                {
                    var headingText = heading.Inline?.FirstChild?.ToString() ?? "";
                    if (heading.Level == 1 && headingText.Equals(title, StringComparison.OrdinalIgnoreCase))
                    {
                        doc.Remove(heading);
                    }
                }

                string contentHTML = doc.ToHtml(pipeline);

                if (Flubs.ContainsKey(uid))
                {
                    contentHTML += "\n" + HtmlProcessors.GetFlubTable(title, Flubs[uid]);
                }

                string nodeData = "<hr>";
                string nodeFamily = "";
                string nodeCategory = "";
                if (Meta.ContainsKey(uid))
                {
                    nodeData = Markdown.ToHtml(HtmlProcessors.ExpandIncludes(GetNodeData(Meta[uid]), $@"{RootFolder}\Source", 2, false), pipeline);
                    nodeFamily = Meta[uid].Family;
                    nodeCategory = Meta[uid].Toolbox;
                }



                html = html.Replace("%%CONTENT%%", contentHTML);

                Slugs.TryGetValue(uid, out var selfLink);
                var href = selfLink?.Href ?? file.Replace(hive.Destination, hive.URL).Replace("\\", "/").Replace(".md", ".html");

                html = html.Replace("%%TITLE%%", title[0].IsAlphaUpper() ? title : title.Humanize(LetterCasing.Title))
                    .Replace("%%HIVE%%", hive.Name)
                    .Replace("%%HIVEPATH%%", hive.URL)
                    .Replace("%%SHORTNAME%%", hive.ShortName)
                    .Replace("%%HREF%%", href)
                    .Replace("%%NODEDATA%%", nodeData)
                    .Replace("%%SLUG%%", uid)
                    .Replace("%%NODEFAMILY%%", nodeFamily)
                    .Replace("%%NODECATEGORY%%", nodeCategory)
                    .Replace("%%V%%", DateTime.Now.ToString("MM.dd.yyyy"));
                //.Replace("%%PAGETITLE%%", $"<span>{hive.Name}</span><span>{title}</span>");

                if (AtToken.IsMatch(content))
                    RogueAts.Add(file);

                var so = SearchBuilder.ToSearchObject(content, title, hive, href);
                SearchObjects.Add(so);
                LLMS.AppendLine(so.text);

                File.WriteAllText(file.Replace(".md", ".html"), html);
            }
            catch (Exception ex)
            {
                Console.WriteLine(file);
                Console.WriteLine(ex.Message);
            }
        });
    }

    private static string GetNodeData(NodeMetadata m)
    {
        StringBuilder sb = new();

        sb.AppendLine($"<div class='node-info d-flex justify-content-between'>");
        sb.AppendLine($"    <div class='toolbox d-flex'>{m.Toolbox} › {m.Family}</div>");
        sb.AppendLine($"    <div class='shortcut d-flex'>Shortcode <kbd>{m.ShortCode}</kbd></div>");
        sb.AppendLine($"</div>");
        sb.AppendLine($"<span class='description'>{m.Description}</span>");

        if (m.AccumulationType != null) sb.AppendLine($"<div class='accumulation'>{m.AccumulationType}</div>");
        if (m.CanCreatePorts) sb.AppendLine("\n{% include \"/.data/includes/add-ports.md\" %}\n");
        if (m.RequiresBaking) sb.AppendLine("\n{% include \"/.data/includes/must-be-baked.md\" %}\n");

        sb.AppendLine("<hr>");
        return sb.ToString();
    }

    private static string ProcessSlugs(string content)
    {
        foreach ((string key, Link link) in Slugs.OrderByDescending(x => x.Key.Length))
        {
            content = content.Replace($"(@{key})", $"({link.Href})", StringComparison.OrdinalIgnoreCase);
            content = content.Replace($"@{key}", $"[{link.Title}]({link.Href})", StringComparison.OrdinalIgnoreCase);
        }

        return content;
    }

    public static void GenerateMissingFolderIndexMarkdown(Hive hive)
    {
        if (hive.IsHome) return;

        var dirs = Directory.GetDirectories(hive.Source, "*", SearchOption.AllDirectories);

        foreach (var dir in dirs)
        {
            var indexMd = Path.Combine(dir, "index.md");
            if (File.Exists(indexMd))
                continue;

            var folderName = Path.GetFileName(dir);

            var yaml =
                $"""
                 ---
                 title: {folderName}
                 uid: {folderName}
                 ---

                 # {folderName}

                 <div id='show-sublinks'></div>

                 """;

            File.WriteAllText(indexMd, yaml);
        }
    }
}