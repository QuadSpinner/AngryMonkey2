using AngryMonkey.Objects;
using AngryMonkey.POCO;
using Humanizer;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Extensions.MediaLinks;
using Markdig.Syntax;
using Newtonsoft.Json;
using Spectre.Console;
using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using Markdig.Helpers;

namespace AngryMonkey;

public static class Program
{
    //public static string source = "X:\\Gaea2\\Docs\\Source\\Nodes";
    //public static string destination = "X:\\Gaea2\\Docs\\staging";

    public static string RootFolder;
    public static string StagingFolder;

    public static string Templates;
    public static string HtmlTemplate;
    public static string Html;
    public static int folderCount;
    public static MarkdownPipeline pipeline;

    //private static Dictionary<string, string> TOC = [];
    //private static Dictionary<string, string> Titles = [];
    //private static Dictionary<string, string> mdTitles = [];
    internal static List<Link> Links = [];

    internal static List<Page> Pages = [];
    internal static string[] DataFolders = [];

    internal static Dictionary<string, Link> Slugs = [];

    internal static ConcurrentBag<SearchObject> SearchObjects = [];
    internal static ConcurrentBag<string> RogueAts = [];
    internal static Dictionary<string, Page> PageByDestMd = new(StringComparer.OrdinalIgnoreCase);

    internal static StringBuilder LLMS = new();

    internal static Dictionary<string, Flub[]> Flubs = [];
    internal static Dictionary<string, NodeMetadata> Meta = [];

    internal static bool Fast { get; set; } = false;

    private static readonly Regex AtToken = new Regex(@"(?<!\w)@([A-Za-z][A-Za-z0-9_-]*)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static void Main(string[] args)
    {// Synchronous
        SearchObjects = [];

        string config = "";

        if (args.Length <= 1)
        {
            if (File.Exists(Environment.CurrentDirectory + "\\hives.json"))
            {
                config = Environment.CurrentDirectory + "\\hives.json";
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Configuration file not found![/] The Monkey is angry!");
                return;
            }
        }

        //if (args.Length > 0)
        //{
        //    if (File.Exists(args[0]))
        //    {
        //        config = args[0];
        //    }
        //    else
        //    {
        //        if (Directory.Exists(args[0]))
        //        {
        //            if (File.Exists(args[0] + "\\hives.json"))
        //            {
        //                config = args[0] + "\\hives.json";
        //            }
        //        }
        //    }
        //}

        if (config == "")
        {
            AnsiConsole.MarkupLine("[red]Configuration file not found![/] The Monkey is angry!");
            return;
        }

        AppConfig cfg = LoadConfiguration(config);

        DataFolders = cfg.DataFolders;
        Hive[] hives = Config.BuildHives(cfg);

        //if (Directory.Exists(StagingFolder))
        //    Directory.Delete(StagingFolder, true);
        //Directory.CreateDirectory(StagingFolder);

        if (args.Any(a => string.Equals(a, "--fast", StringComparison.OrdinalIgnoreCase)))
        {
            Fast = true;
        }

        if (args.Any(a => string.Equals(a, "--assets", StringComparison.OrdinalIgnoreCase)))
        {
            AnsiConsole.WriteLine();
            FileService.CopyDirectory($"{Templates}\\Assets", $"{StagingFolder}\\assets", "*.*");
            AnsiConsole.MarkupLine("[Fuchsia][[shows teeth]][/] [white]The Monkey copied assets only[/]");
            return;
        }

        Html = File.ReadAllText(HtmlTemplate);

        Links.Clear();
        Pages.Clear();
        SearchObjects.Clear();
        RogueAts.Clear();
        Slugs.Clear();

        pipeline = new MarkdownPipelineBuilder()
            .UseCustomContainers()
            .UseAlertBlocks()
            .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
            .UseYamlFrontMatter()
            .UsePipeTables()
            .UseMediaLinks(new MediaOptions()
            {
                Class = "border rounded ratio ratio-16x9",
                Width = "",
                Height = ""
            })
            .UseFigures()
            .UseEmojiAndSmiley()
            .UseDefinitionLists()
            .UseGenericAttributes()
            .Build();

        foreach (Hive hive in hives)
        {
            DeleteStagedHive(hive);
        }

        foreach (Hive hive in hives)
        {
            CollectPages(hive);
        }

        LoadFlubs();
        LoadMetadata();

        BuildSlugIndex();

        foreach (Hive hive in hives)
        {
            GenerateNavigation(hive);
            //GenerateMissingFolderIndexMarkdown(hive);
            if (!hive.IsHome)
                FileService.CopyDirectory(hive.Source, hive.Destination);

            ProcessMarkdown(hive);
        }

        FileService.CopyDirectory($"{Templates}\\Assets", $"{StagingFolder}\\assets", "*.*");
        FileService.CopyDirectory($"{RootFolder}\\Hives", $@"{StagingFolder}\assets\js\", "*.*");

        if (!Fast)
        {
            AnsiConsole.WriteLine("Copying Data Folders...");
            foreach (string dataFolder in DataFolders)
            {
                FileService.CopyAll(dataFolder, dataFolder.Replace(cfg.SourceRoot, cfg.StagingRoot));
            }
        }

        File.WriteAllText($"{StagingFolder}\\search.json", JsonConvert.SerializeObject(SearchObjects, new JsonSerializerSettings { Formatting = Formatting.None }));
        File.WriteAllText($"{StagingFolder}\\llms-full.txt", LLMS.ToString());
        File.WriteAllText($"{RootFolder}\\.vscode\\atlinks.json", JsonConvert.SerializeObject(Links));

        AnsiConsole.MarkupLine($"[white][[{hives.Length}]][/] hives\n[white][[{Links.Count}]][/] pages\n[white][[{folderCount}]][/] sections");

        if (!RogueAts.IsEmpty)
        {
            var distinct = RogueAts.Distinct().ToArray();
            AnsiConsole.MarkupLine($"[DarkOrange][[{distinct.Length}]][/] rogue @s found! See RogueAts.txt");
            File.WriteAllText($"{RootFolder}\\rogueAts.txt", string.Join(Environment.NewLine, distinct));
        }

        AnsiConsole.MarkupLine("[green][[Success - oo oo aa ahh ahh!]][/] [white]The Monkey is happy.[/]");
    }

    private static void LoadFlubs()
    {
        var files = Directory.GetFiles($@"{RootFolder}\source\.flubs", "*.json");

        foreach (var file in files)
        {
            try
            {
                var flubArray = JsonConvert.DeserializeObject<Flub[]>(File.ReadAllText(file));
                if (flubArray is { Length: > 0 })
                {
                    var key = Path.GetFileNameWithoutExtension(file).ToLower();
                    Flubs[key] = flubArray;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(file);
            }
        }
    }

    private static void LoadMetadata()
    {
        var files = Directory.GetFiles($@"{RootFolder}\source\.meta", "*.json");

        foreach (var file in files)
        {
            try
            {
                var flubArray = JsonConvert.DeserializeObject<NodeMetadata>(File.ReadAllText(file));
                var key = Path.GetFileNameWithoutExtension(file).ToLower();
                Meta[key] = flubArray;
            }
            catch (Exception ex)
            {
                Console.WriteLine(file);
            }
        }
    }

    private static AppConfig LoadConfiguration(string config)
    {
        var cfg = Config.LoadConfig(config);

        RootFolder = cfg.RootFolder;
        StagingFolder = cfg.StagingRoot;
        Templates = cfg.TemplatesFolder;
        HtmlTemplate = cfg.HtmlTemplate;
        return cfg;
    }

    private static void BuildSlugIndex()
    {
        var duplicates = Links.GroupBy(l => l.Slug, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Count > 0)
        {
            AnsiConsole.WriteLine("DUPLICATES:");
            foreach (var s in duplicates) AnsiConsole.WriteLine(s);
            throw new Exception("Duplicate slugs found");
        }

        Slugs = Links.ToDictionary(x => x.Slug, StringComparer.OrdinalIgnoreCase);
    }

    private static void CollectPages(Hive hive)
    {
        string[] md = Directory.GetFiles(hive.Source, "*.md", hive.IsHome ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories);

        foreach (var file in md)
        {
            try
            {
                var yaml = FrontMatter.GetFrontMatter(File.ReadAllLines(file));
                string title = yaml["title"];

                var page = new Page()
                {
                    Filename = file,
                    Hive = hive,
                    Link = file.Replace(hive.Source, hive.URL).Replace("\\", "/").Replace(".md", ".html"),
                    //Contents = File.ReadAllText(file),
                    Title = yaml["title"],
                    UID = yaml["uid"],
                    Hidden = yaml.ContainsKey("show") && yaml["show"] == "no",
                    StartsSection = yaml.ContainsKey("section") && yaml["section"] == "true"
                };

                Pages.Add(page);
                var destMd = file.Replace(hive.Source, hive.Destination);
                PageByDestMd[destMd] = page;

                var link = new Link(page.Link, title, yaml["uid"]);
                Links.Add(link);
            }
            catch (Exception ex)
            {
                Console.WriteLine(file);
                Console.WriteLine(ex.Message);
            }
        }

        // find duplicates in LINKS by slug
        List<string> duplicateSlugs = Links.GroupBy(l => l.Slug)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateSlugs.Any())
        {
            AnsiConsole.WriteLine("DUPLICATES:");
            foreach (string duplicateSlug in duplicateSlugs)
            {
                AnsiConsole.WriteLine(" - " + duplicateSlug);
            }

            AnsiConsole.MarkupLine($"[red][[{duplicateSlugs.Count}]] duplicate slugs found![/] The Monkey is angry!");
            Environment.Exit(50);
        }

        Slugs = Links.ToDictionary(x => x.Slug);
    }

    private static void GenerateNavigation(Hive hive)
    {
        if (hive.IsHome)
        {
            File.WriteAllText($@"{RootFolder}\Hives\TOC_{hive.ShortName}.js", "window.SITE_TOC = null;");
            return;
        }

        TocGenerator.WriteHiveTocJs(hive.Source, hive.URL, $@"{RootFolder}\Hives\TOC_{hive.ShortName}.js");
    }

    public static void DeleteStagedHive(Hive hive)
    {
        if (hive.IsHome)
            return;

        if (Directory.Exists(hive.Destination))
        {
            Directory.Delete(hive.Destination, true);
        }
    }

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

                string content = File.ReadAllText(file); // keep raw content

                content = ProcessSlugs(content);

                if (!PageByDestMd.TryGetValue(file, out var page))
                    throw new InvalidOperationException($"No page metadata for: {file}");

                string title = page.Title;
                string uid = page.UID;

                content = HtmlProcessors.ExpandIncludes(content, $@"{RootFolder}\Source", 2, false);

                MarkdownDocument doc = Markdown.Parse(content, pipeline);
                string contentHTML = doc.ToHtml(pipeline);

                if (Flubs.ContainsKey(uid))
                {
                    contentHTML += "\n" + HtmlProcessors.GetFlubTable(title, Flubs[uid]);
                }

                string nodeData = "<hr>";

                if (Meta.ContainsKey(uid))
                {
                    nodeData = Markdown.ToHtml(HtmlProcessors.ExpandIncludes(GetNodeData(Meta[uid]), $@"{RootFolder}\Source", 2, false), pipeline);
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