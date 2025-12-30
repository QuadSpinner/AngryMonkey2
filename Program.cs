using AngryMonkey.Objects;
using AngryMonkey.POCO;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Extensions.MediaLinks;
using Markdig.Syntax;
using Newtonsoft.Json;
using Spectre.Console;

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
    public static MarkdownPipeline pipeline;

    //private static Dictionary<string, string> TOC = [];
    //private static Dictionary<string, string> Titles = [];
    //private static Dictionary<string, string> mdTitles = [];
    internal static List<Link> Links = [];

    internal static List<Page> Pages = [];

    internal static List<SearchObject> SearchObjects = [];

    internal static Dictionary<string, Link> Slugs = [];
    internal static List<string> RogueAts = [];

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
                AnsiConsole.MarkupLine("[red]Configuration file not found![/] Pass the config file as the second argument.");
                return;
            }
        }

        if (args.Length > 1)
        {
            if (File.Exists(args[1]))
            {
                config = args[1];
            }
            else
            {
                if (Directory.Exists(args[1]))
                {
                    if (File.Exists(args[1] + "\\hives.json"))
                    {
                        config = args[1] + "\\hives.json";
                    }
                }
            }
        }

        if (config == "")
        {
            AnsiConsole.MarkupLine("[red]Configuration file not found![/] Pass the config file as the second argument.");
            return;
        }

        AppConfig cfg = LoadConfiguration(config);

        Hive[] hives = Config.BuildHives(cfg);

        //if (Directory.Exists(StagingFolder))
        //    Directory.Delete(StagingFolder, true);
        //Directory.CreateDirectory(StagingFolder);

        if (args.Any(a => string.Equals(a, "--assets", StringComparison.OrdinalIgnoreCase)))
        {
            AnsiConsole.WriteLine();
            FileService.CopyDirectory($"{Templates}\\Assets", $"{StagingFolder}\\assets", "*.*");
            AnsiConsole.MarkupLine("[white]The Monkey copied assets only[/] [Fuchsia][[shows teeth]][/]");
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

        BuildSlugIndex();

        foreach (Hive hive in hives)
        {
            GenerateNavigation(hive);
            FileService.CopyDirectory(hive.Source, hive.Destination);
            ProcessMarkdown(hive);
        }

        FileService.CopyDirectory($"{Templates}\\Assets", $"{StagingFolder}\\assets", "*.*");
        FileService.CopyDirectory($"{RootFolder}\\Hives", $@"{StagingFolder}\assets\js\", "*.*");
        File.WriteAllText($"{StagingFolder}\\search.json", JsonConvert.SerializeObject(SearchObjects, new JsonSerializerSettings { Formatting = Formatting.None }));

        if (RogueAts.Any())
        {
            AnsiConsole.MarkupLine($"[DarkOrange] {RogueAts.Count} rogue @s found![/] See RogueAts.txt");
            File.WriteAllText($"{RootFolder}\\rogueAts.txt", string.Join(Environment.NewLine, RogueAts.Distinct()));
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[white]The Monkey is happy.[/] [green][[Success - oo oo aa ahh ahh!]][/]");
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
                AnsiConsole.WriteLine(duplicateSlug);
            }
            throw new Exception("Duplicate slugs found");
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
        string[] md = Directory.GetFiles(hive.Destination, "*.md", hive.IsHome ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories);

        foreach (var file in md)
        {
            string html = Html;

            string content = File.ReadAllText(file);

            content = ProcessSlugs(content);

            MarkdownDocument doc = Markdown.Parse(content, pipeline);

            string contentHTML = doc.ToHtml(pipeline);
            html = html.Replace("%%CONTENT%%", contentHTML);

            try
            {
                var splitter = content.Contains("\r\n") ? Environment.NewLine : "\n";
                var dic = FrontMatter.GetFrontMatter(content.Split(splitter));
                string title = dic["title"];

                html = html.Replace("%%TITLE%%", title)
                    .Replace("%%HIVE%%", hive.Name)
                    .Replace("%%SHORTNAME%%", hive.ShortName)
                    .Replace("%%SLUG%%", dic["uid"])
                    .Replace("%%PAGETITLE%%", $"<span>{hive.Name}</span><span>{title}</span>");

                if (content.Contains("@"))
                {
                    RogueAts.Add(file);
                }

                if (!Slugs.TryGetValue(dic["uid"], out var slugLink))
                    slugLink = new Link(hive.URL, title, dic["uid"]); // fallback (or page.Link if you have it)

                SearchObjects.Add(SearchBuilder.ToSearchObject(content, title, hive, slugLink.Href));
            }
            catch (Exception ex)
            {
                Console.WriteLine(file);
                Console.WriteLine(ex.Message);
            }

            File.WriteAllText(file.Replace(".md", ".html"), html);
        }
    }

    private static string ProcessSlugs(string content)
    {
        foreach ((string key, Link link) in Slugs.OrderByDescending(x => x.Key.Length))
        {
            content = content.Replace($"(@{key})", link.Href, StringComparison.OrdinalIgnoreCase);
            content = content.Replace($"@{key}", $"[{link.Title}]({link.Href})", StringComparison.OrdinalIgnoreCase);
        }

        return content;
    }
}