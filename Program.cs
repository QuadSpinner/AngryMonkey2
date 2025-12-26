using AngryMonkey.Objects;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Syntax;
using Newtonsoft.Json;
using Spectre.Console;

namespace AngryMonkey;

public static class Program
{
    //public static string source = "X:\\Gaea2\\Docs\\Source\\Nodes";
    //public static string destination = "X:\\Gaea2\\Docs\\staging";

    public static string RootFolder = @"X:\Docs\Gaea2-Docs";
    public static string StagingFolder = $@"{RootFolder}\staging";

    public static string Templates = @"X:\Docs\Gaea2-Docs\template\";
    public static string HtmlTemplate = @"X:\Docs\Gaea2-Docs\template\template.html";
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

    internal static Hive CurrentHive { get; set; }

    public static void Main(string[] arguments)
    {// Synchronous
        FileService.CopyDirectory($"{Templates}\\Assets", $"{StagingFolder}\\Assets");

        if (Environment.CommandLine.Contains("--assets"))
        {
            AnsiConsole.WriteLine();

            AnsiConsole.MarkupLine("[white]The Monkey copied assets only[/] [Fuchsia][[shows teeth]][/]");
            Environment.Exit(0);
            return;
        }
        AnsiConsole.Status()
            .Start("Monkey is getting angry...", ctx =>
            {
                pipeline = new MarkdownPipelineBuilder()
                    .UseAdvancedExtensions()
                    .UseAlertBlocks()
                    .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
                    .UseYamlFrontMatter()
                    .UsePipeTables()
                    .UseMathematics()
                    .UseFigures()
                    .UseBootstrap()
                    .UseEmojiAndSmiley()
                    .UseGenericAttributes()
                    .UseDefinitionLists()
                    .UseCustomContainers()
                    .Build();

                SearchObjects = [];

                Hive[] hives =
                [
                   new()
                    {
                        Name = "Home",
                        Source = @"X:\Docs\Gaea2-Docs\Source\",
                        Destination = @"X:\Docs\Gaea2-Docs\staging\",
                        ShortName = "Home",
                        IsHome = true,
                        URL = "/"
                    },
                   new()
                    {
                        Name = "Getting Started",
                        Source = @"X:\Docs\Gaea2-Docs\Source\introduction",
                        Destination = @"X:\Docs\Gaea2-Docs\staging\introduction",
                        ShortName = "Introduction",
                        URL = "/introduction"
                    },
                   new()
                    {
                        Name = "Using Gaea",
                        Source = @"X:\Docs\Gaea2-Docs\Source\using",
                        Destination = @"X:\Docs\Gaea2-Docs\staging\using",
                        ShortName = "Using",
                        URL = "/using"
                    },
                    new()
                    {
                        Name = "Node Reference",
                        Source = @"X:\Docs\Gaea2-Docs\Source\reference",
                        Destination = @"X:\Docs\Gaea2-Docs\staging\reference",
                        ShortName = "Reference",
                        URL = "/reference"
                    }
                ];

                Html = File.ReadAllText(HtmlTemplate);
                foreach (Hive hive in hives)
                {
                    CurrentHive = hive;
                    //var result = FoldersTxtIconUpdater.UpdateIcons(rootDir: hive.Source, getFrontMatter: FrontMatter.GetFrontMatter);
                    //Console.WriteLine($"Visited: {result.ManifestFilesVisited}, UpdatedFiles: {result.ManifestFilesUpdated}, Lines: {result.LinesUpdated}");

                    //continue;
                    //FolderManifest.GenerateFoldersTxtRecursively(hive.Source);
                    ctx.Status($"Collecting pages from {hive.Name}...");
                    CollectPages(hive);

                    ctx.Status($"Generating navigation for {hive.Name}...");
                    GenerateNavigation(hive);

                    ctx.Status($"Processing {hive.Name}...");
                    RecreateDestination(hive);
                    ProcessMarkdown(hive);
                }

                File.WriteAllText($"{StagingFolder}\\search.json", JsonConvert.SerializeObject(SearchObjects, new JsonSerializerSettings { Formatting = Formatting.None }));

                if (RogueAts.Any())
                {
                    AnsiConsole.MarkupLine($"[DarkOrange] {RogueAts.Count} rogue @s found![/] See RogueAts.txt");
                    File.WriteAllText($"{RootFolder}\\rogueAts.txt", string.Join(Environment.NewLine, RogueAts.Distinct()));
                }
            });

        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[white]The Monkey is happy.[/] [green][[Success - oo oo aa ahh ahh!]][/]");
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

    public static void RecreateDestination(Hive hive, bool delete = false)
    {
        if (delete)
        {
            if (Directory.Exists(hive.Destination))
            {
                Directory.Delete(hive.Destination, true);
            }
        }

        FileService.CopyDirectory(hive.Source, hive.Destination);
    }

    public static void ProcessMarkdown(Hive hive)
    {
        string[] md = Directory.GetFiles(hive.Destination, "*.md", hive.IsHome ? SearchOption.TopDirectoryOnly : SearchOption.AllDirectories);
        FileService.CopyDirectory($"{RootFolder}\\Hives", $@"{StagingFolder}\assets\js\");

        foreach (var file in md)
        {
            string html = Html;

            string content = File.ReadAllText(file).Replace(".md", ".html");

            content = ProcessSlugs(content);

            MarkdownDocument doc = Markdown.Parse(content, pipeline);

            string contentHTML = doc.ToHtml(pipeline);
            html = html.Replace("%%CONTENT%%", contentHTML);

            try
            {
                var dic = FrontMatter.GetFrontMatter(content.Split(Environment.NewLine));
                string title = dic["title"];

                html = html.Replace("%%TITLE%%", title)
                    .Replace("%%HIVE%%", hive.Name)
                    .Replace("%%CRUMBS%%", $"")
                    .Replace("%%SHORTNAME%%", hive.ShortName)
                    .Replace("%%SLUG%%", dic["uid"])
                    .Replace("%%PAGETITLE%%", $"<span>{hive.Name}</span><span>{title}</span>");

                if (content.Contains("@"))
                {
                    RogueAts.Add(file);
                }

                SearchObjects.Add(SearchObject.ToSearchObject(content, title, hive, dic["uid"]));
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
        foreach ((string key, Link link) in Slugs)
        {
            content = content.Replace($"(@{key})", link.Href);
            content = content.Replace($"@{key}", $"[{link.Title}]({link.Href})");
        }

        return content;
    }
}