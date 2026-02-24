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
    public static List<string> imgs = [];

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

                // get last modified date of file

                string content = File.ReadAllText(file); // keep raw content

                content = ProcessSlugs(content);

                if (!PageByDestMd.TryGetValue(file, out var page))
                    throw new InvalidOperationException($"No page metadata for: {file}");

                string title = page.Title;
                string uid = page.UID;
                var lastWrite = page.Modified;

                content = HtmlProcessors.ExpandIncludes(content, $@"{RootFolder}\Source\.data\includes", 2, false);
                page.Contents = content;

                MarkdownDocument doc = Markdown.Parse(content, pipeline);

                if (Validator.HasOutOfOrderHeadings(doc))
                {
                    RogueHeadings.Add(file);
                }

                // if first element is a heading and is same as title, remove it
                if (doc.Count > 0 && doc.Count(x => x is HeadingBlock) > 1 && doc.FirstOrDefault(x => x is HeadingBlock) is HeadingBlock heading)
                {
                    var headingText = heading.Inline?.FirstChild?.ToString() ?? "";
                    if (heading.Level == 1 && headingText.Equals(title, StringComparison.OrdinalIgnoreCase))
                    {
                        doc.Remove(heading);
                    }
                }

                string contentHTML = doc.ToHtml(pipeline);



                string flubTable = "";

                if (Flubs.ContainsKey(uid))
                {
                    (string htmlTable, string mdTable) = HtmlProcessors.GetFlubTable(title, Flubs[uid]);
                    contentHTML += "\n" + htmlTable;

                    flubTable = mdTable;
                    page.Contents += "\n" + flubTable;
                }

                {
                    string siteRoot = StagingFolder; // where "/assets/..." actually lives

                    string Mapper(string mdPath, string url)
                    {
                        if (url.StartsWith("/", StringComparison.Ordinal)) return Path.GetFullPath(Path.Combine(siteRoot, url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));

                        return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(mdPath)!, url.Replace('/', Path.DirectorySeparatorChar)));
                    }

                    var missing = Validator.CheckMissingImages(file, doc, siteRoot, pipeline, Mapper);
                    if (missing.Count > 0)
                    {
                        //var msg = string.Join(Environment.NewLine, missing.Select(m => $"{m.Ref.MarkdownFile}: missing {m.Ref.Url} -> {m.ResolvedPath} ({m.Ref.Origin})"));
                        foreach (Validator.MissingImage missingImage in missing)
                        {
                            imgs.Add(missingImage.Ref.MarkdownFile + "|" + missingImage.Ref.Url);
                        }
                        // imgs.AddRange(missing.Select(x=>x.ResolvedPath).ToArray());
                    }

                    if (contentHTML.Length < 300)
                    {
                        if (!hive.IsHome && hive.ShortName is not ("Videos" or "History") && !contentHTML.Contains("show-sublinks"))
                        {
                            ThinPages.Add(file);
                        }
                    }
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
                    .Replace("%%LASTUPDATED%%", lastWrite.ToString("yyyy-MM-d"))
                    .Replace("%%NODEFAMILY%%", nodeFamily)
                    .Replace("%%NODECATEGORY%%", nodeCategory)
                    .Replace("%%V%%", DateTime.Now.ToString("MM.dd.yyyy"));
                //.Replace("%%PAGETITLE%%", $"<span>{hive.Name}</span><span>{title}</span>");

                if (AtToken.IsMatch(content))
                    RogueAts.Add(file);

                var so = SearchBuilder.ToSearchObject(content, title, hive, href);
                SearchObjects.Add(so);
                LLMS.AppendLine("\n---");
                LLMS.AppendLine(uid);
                LLMS.AppendLine(title);
                LLMS.AppendLine("\n---\n");
                LLMS.AppendLine(so.text);
                LLMS.AppendLine(flubTable);
                LLMS.AppendLine("\n***\n");

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
            content = content.Replace($"@{key}", $"[{link.Title}]({link.Href}){{data-link-icon='{link.Icon}'}}", StringComparison.OrdinalIgnoreCase);
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
            {
                if (!File.ReadAllText(indexMd).Contains("show-sublinks"))
                    continue;
            }

            var folderName = Path.GetFileName(dir);

            var yaml =
                $"""
                 ---
                 title: {folderName.Humanize(LetterCasing.Title)}
                 uid: {folderName}
                 hidden: true
                 ---

                 # In this section

                 <div id='show-sublinks'></div>

                 """;

            File.WriteAllText(indexMd, yaml);
        }
    }
}