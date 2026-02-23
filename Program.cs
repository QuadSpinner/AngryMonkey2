using AngryMonkey.Objects;
using AngryMonkey.POCO;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Extensions.MediaLinks;
using Newtonsoft.Json;
using Spectre.Console;
using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

namespace AngryMonkey;

public static partial class Program
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
    internal static ConcurrentBag<string> RogueHeadings = [];
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
            .UseDiagrams()
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
            //GenerateMissingFolderIndexMarkdown(hive);
            CollectPages(hive);
        }

        LoadFlubs();
        LoadMetadata();
        imgs.Clear();
        BuildSlugIndex();


        if (!Fast)
        {
            AnsiConsole.WriteLine("Copying Data Folders...");
            foreach (string dataFolder in DataFolders)
            {
                FileService.CopyAll(dataFolder, dataFolder.Replace(cfg.SourceRoot, cfg.StagingRoot));
            }
        }

        CreateChangelog();

        foreach (Hive hive in hives)
        {
            GenerateNavigation(hive);
            if (!hive.IsHome)
                FileService.CopyDirectory(hive.Source, hive.Destination);

            ProcessMarkdown(hive);

            CreateLLMS(hive);
        }

        FileService.CopyDirectory($@"{Templates}\Assets", $@"{StagingFolder}\assets", "*.*");
        FileService.CopyDirectory($@"{RootFolder}\Hives", $@"{StagingFolder}\assets\js\", "*.*");

        File.WriteAllText($@"{StagingFolder}\search.json", JsonConvert.SerializeObject(SearchObjects, new JsonSerializerSettings { Formatting = Formatting.None }));
        File.WriteAllText($@"{StagingFolder}\llms-full.txt", LLMS.ToString());
        File.WriteAllText($@"{RootFolder}\.vscode\atlinks.json", JsonConvert.SerializeObject(Links));

        AnsiConsole.MarkupLine($"[white][[{hives.Length}]][/] hives\n[white][[{Links.Count}]][/] pages\n[white][[{folderCount}]][/] sections");

        if (!RogueAts.IsEmpty)
        {
            var distinct = RogueAts.Distinct().ToArray();
            AnsiConsole.MarkupLine($"[DarkOrange][[{distinct.Length}]][/] rogue @s found! See RogueAts.txt");
            File.WriteAllText($@"{RootFolder}\rogueAts.txt", string.Join(Environment.NewLine, distinct));
        }

        if (imgs.Count > 0)
        {
            var distinct = imgs.OrderBy(x => x).ToArray();
            AnsiConsole.MarkupLine($"[DarkOrange][[{distinct.Length}]][/] rogue IMG found! See rogueImgs.txt");
            File.WriteAllText($@"{RootFolder}\rogueImgs.txt", string.Join(Environment.NewLine, distinct));
        }
        if (RogueHeadings.Count > 0)
        {
            var distinct = RogueHeadings.OrderBy(x => x).ToArray();
            AnsiConsole.MarkupLine($"[DarkOrange][[{distinct.Length}]][/] rogue HEADINGS found! See rogueHeadings.txt");
            File.WriteAllText($@"{RootFolder}\rogueHeadings.txt", string.Join(Environment.NewLine, distinct));
        }

        AnsiConsole.MarkupLine("[green][[Success - oo oo aa ahh ahh!]][/] [white]The Monkey is happy.[/]");
    }

    private static void CreateChangelog()
    {
        DateTime monthsAgo = DateTime.UtcNow.AddMonths(-12);
        var changedPages = Pages.Where(x => x.Modified > monthsAgo).OrderByDescending(x => x.Modified).ThenBy(x => x.Hive.Name);

        StringBuilder sb = new();

        sb.AppendLine("---");
        sb.AppendLine("title: Documentation Changelog");
        sb.AppendLine("uid: changelog");
        sb.AppendLine("icon: compass-drafting");
        sb.AppendLine("---\n");
        sb.AppendLine("# Changelog\n");


        string last = "";

        foreach (Page changedPage in changedPages)
        {
            if (changedPage.UID == "changelog")
                continue;

            string mod = changedPage.Modified.ToString("Y");

            if (mod != last)
            {
                sb.AppendLine($"## {mod}");

                last = mod;

                sb.AppendLine("| Page | Section | Last Modified |");
                sb.AppendLine("| ---- | ------- | ------------- |");
            }


            sb.AppendLine($"| [{changedPage.Title}]({changedPage.Link}) | {changedPage.Hive.Name} | {changedPage.Modified:yyyy-MM-dd} |");
        }

        File.WriteAllText($@"{RootFolder}\source\history\docs-changelog.md", sb.ToString());
    }

    private static void CreateLLMS(Hive hive)
    {
        if (hive.IsHome)
            return;

        string[] dirs = Pages.Where(x => x.Hive == hive).Select(x => x.Directory).Distinct().ToArray();

        foreach (string dir in dirs)
        {
            Page[] pages = Pages.Where(x => x.Hive == hive && x.Directory.Contains(dir)).ToArray();

            StringBuilder llms = new();

            llms.AppendLine("## SOURCE: " + hive.Destination.Replace(RootFolder, "https://docs.gaea.app").Replace("\\", "/") + "/llms.txt\n\n");

            foreach (Page page in pages)
            {
                llms.AppendLine($"// from {page.UID} / {Path.GetFileName(page.Filename)}");
                llms.AppendLine(page.Contents);
            }

            File.WriteAllText(dir.Replace(hive.Source, hive.Destination) + "\\llms.txt", llms.ToString());
        }
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
                    Directory = Path.GetDirectoryName(file),
                    Link = file.Replace(hive.Source, hive.URL).Replace("\\", "/").Replace(".md", ".html"),
                    Modified = new FileInfo(file).LastWriteTimeUtc,
                    Icon = yaml.ContainsKey("icon") ? yaml["icon"] : null,
                    Title = yaml["title"],
                    UID = yaml["uid"],
                    Hidden = yaml.ContainsKey("hidden") && yaml["hidden"] == "true",
                    StartsSection = yaml.ContainsKey("section") && yaml["section"] == "true"
                };

                Pages.Add(page);
                var destMd = file.Replace(hive.Source, hive.Destination);
                PageByDestMd[destMd] = page;

                var link = new Link(page.Link, title, page.UID, page.Icon, page.Hidden);
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
}