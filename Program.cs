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
        AnsiConsole.Status()
            .Start("Monkey is getting angry...", ctx =>
            {
                FileService.CopyDirectory($"{Templates}\\Assets", $"{StagingFolder}\\Assets");

                if (Environment.CommandLine.Contains("--assets"))
                {
                    AnsiConsole.WriteLine();

                    AnsiConsole.MarkupLine("[white]The Monkey copied assets only[/] [Fuchsia][[shows teeth]][/]");
                    Environment.Exit(0);
                    return;
                }

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
                        Name = "Getting Started",
                        Source = @"X:\Docs\Gaea2-Docs\Source\GettingStarted",
                        Destination = @"X:\Docs\Gaea2-Docs\staging\introduction",
                        ShortName = "Introduction",
                        URL = "/introduction"
                    },  new()
                    {
                        Name = "Using Gaea",
                        Source = @"X:\Docs\Gaea2-Docs\Source\UserGuide",
                        Destination = @"X:\Docs\Gaea2-Docs\staging\Guide",
                        ShortName = "Manual",
                        URL = "/Guide"
                    },
                    new()
                    {
                        Name = "Node Reference",
                        Source = @"X:\Docs\Gaea2-Docs\Source\Nodes",
                        Destination = @"X:\Docs\Gaea2-Docs\staging\reference",
                        ShortName = "Reference",
                        URL = "/Reference"
                    }
                ];

                Html = File.ReadAllText(HtmlTemplate);

                //Directory.Delete(Root, true);
                //Directory.CreateDirectory(Root);

                foreach (Hive hive in hives)
                {
                    CurrentHive = hive;
                    //FolderManifest.GenerateFoldersTxtRecursively(hive.Source);
                    ctx.Status($"Collecting pages from {hive.Name}...");
                    CollectPages(hive);

                    ctx.Status($"Generating navigation for {hive.Name}...");
                    GenerateNavigation(hive);

                    ctx.Status($"Processing {hive.Name}...");
                    // RenameFiles(hive);
                    RecreateDestination(hive);
                    ProcessMarkdown(hive);
                }

                File.WriteAllText($"{StagingFolder}\\search.json", JsonConvert.SerializeObject(SearchObjects, new JsonSerializerSettings { Formatting = Formatting.None }));

                if (RogueAts.Any())
                {
                    AnsiConsole.MarkupLine($"[DarkOrange] {RogueAts.Count} rogue @s found![/] See RogueAts.txt");
                    File.WriteAllText($"{RootFolder}\\rogueAts.txt", string.Join(Environment.NewLine, RogueAts.Distinct()));
                }

                //ctx.Status("Running PageFind...");

                //Process.Start(new ProcessStartInfo("PageFind", @"--site .\staging\ --quiet")
                //{
                //    WorkingDirectory = RootFolder
                //}).WaitForExit();
            });

        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[white]The Monkey is happy.[/] [green][[Success - oo oo aa ahh ahh!]][/]");

        //File.WriteAllLines($"{Root}\\images.txt", images);
    }

    private static void GenerateNavigation(Hive hive)
    {
        TocGenerator.WriteHiveTocJs(
            hiveRootDir: hive.Source,
            baseUrl: hive.URL,
            outputJsPath: $@"{RootFolder}\Hives\TOC_{hive.ShortName}.js");
    }

    private static void CollectPages(Hive hive)
    {
        string[] md = Directory.GetFiles(hive.Source, "*.md", SearchOption.AllDirectories);

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
                    Contents = File.ReadAllText(file),
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
        else
        {
            Slugs = Links.ToDictionary(x => x.Slug);
        }
    }

    private static void RenameFiles(Hive hive)
    {
        string tocFile = Path.Combine(hive.Source, "SUMMARY.md");

        string[] lines = File.ReadAllLines(tocFile);

        int counter = 1;

        string lastPath = "";

        foreach (string line in lines)
        {
            if (!line.Contains(".md"))
            {
                counter = 1;
                continue;
            }

            string file = line.Split('(')[1].TrimEnd(')');
            string fullPath = Path.Combine(hive.Source, file.Replace('/', '\\'));
            string filePath = Path.GetDirectoryName(fullPath);

            if (filePath != lastPath)
            {
                lastPath = filePath;
                counter = 1;
            }

            string[] mdlines = File.ReadAllLines(fullPath);
            var yaml = FrontMatter.GetFrontMatter(mdlines);

            if (!yaml.ContainsKey("order"))
            {
                yaml["order"] = $"{counter:00}";
            }

            File.WriteAllText(fullPath, FrontMatter.ReplaceFrontMatter(mdlines, yaml));

            // Console.WriteLine($"{filePath}\\{counter:00}-{Path.GetFileName(fullPath)}");
            // File.Move(fullPath, $"{filePath}\\{counter:00}-{Path.GetFileName(fullPath)}");
            counter++;
        }
    }

    private static void PreprocessMarkdown(Hive hive)
    {
        string[] md = Directory.GetFiles(hive.Source, "*.md", SearchOption.AllDirectories);
        foreach (var file in md)
        {
            // Console.WriteLine(file);
            string[] content = File.ReadAllLines(file);
            var dic = FrontMatter.GetFrontMatter(content);
            if (!dic.ContainsKey("uid"))
            {
                if (dic.ContainsKey("title"))
                { dic.Add("uid", FrontMatter.Slugify(dic["title"])); }
                else
                { dic.Add("uid", Path.GetFileNameWithoutExtension(file)); }
            }
            File.WriteAllText(file, FrontMatter.ReplaceFrontMatter(content, dic));
        }
    }

    //private static void ParseTOC(Hive hive)
    //{
    //    TOC.Clear();
    //    Titles.Clear();
    //    mdTitles.Clear();

    //    string tocFile = Path.Combine(hive.Source, "SUMMARY.md");
    //    string hivePrefix = hive.Destination.Split('\\')[^1];

    //    string[] lines = File.ReadAllLines(tocFile);
    //    foreach (var line in lines)
    //    {
    //        if (line.Trim().StartsWith("* ["))
    //        {
    //            int startTitle = line.IndexOf('[') + 1;
    //            int endTitle = line.IndexOf(']');
    //            int startLink = line.IndexOf('(') + 1;
    //            int endLink = line.IndexOf(')');
    //            if (startTitle >= 0 && endTitle > startTitle && startLink >= 0 && endLink > startLink)
    //            {
    //                string title = line.Substring(startTitle, endTitle - startTitle);
    //                string link = $"{hivePrefix}/{line.Substring(startLink, endLink - startLink)}";
    //                TOC[link] = title;
    //                Titles[title] = link;
    //            }
    //        }
    //    }
    //}

    public static void RecreateDestination(Hive hive, bool delete = false)
    {
        if (delete)
        {
            // delete all contents in the destination directory
            if (Directory.Exists(hive.Destination))
            {
                Directory.Delete(hive.Destination, true);
            }
        }

        FileService.CopyDirectory(hive.Source, hive.Destination);
        //// copy all contents from source to destination
        //Directory.CreateDirectory(hive.Destination);
        //foreach (var dirPath in Directory.GetDirectories(hive.Source, "*", SearchOption.AllDirectories))
        //{
        //    Directory.CreateDirectory(dirPath.Replace(hive.Source, hive.Destination));
        //}

        //foreach (var newPath in Directory.GetFiles(hive.Source, "*", SearchOption.AllDirectories))
        //{
        //    File.Copy(newPath, newPath.Replace(hive.Source, hive.Destination), true);
        //}
    }

    //private static List<string> images = new List<string>(5000);

    public static void ProcessMarkdown(Hive hive)
    {
        string[] md = Directory.GetFiles(hive.Destination, "*.md", SearchOption.AllDirectories);

        //List<TocNode> toc = SummaryToc.BuildFromFile(summaryMdPath: hive.Destination + "\\summary.md",
        //    options: new SummaryTocOptions
        //    {
        //        BaseUrl = hive.URL ?? "/",
        //        PromoteParentLinksToGroups = true,
        //        OverviewTitle = "Overview"
        //    });

        //string js = SummaryToc.ToJavascript(toc);
        //Directory.CreateDirectory($"{StagingFolder}\\assets");
        //File.WriteAllText($@"{StagingFolder}\assets\TOC_{hive.ShortName}.js", js);

        FileService.CopyDirectory($"{RootFolder}\\Hives", $@"{StagingFolder}\assets\");

        foreach (var file in md)
        {
            string html = Html;

            string content = File.ReadAllText(file).Replace(".md", ".html");

            content = ProcessSlugs(content);

            MarkdownDocument doc = Markdown.Parse(content, pipeline);

            string contentHTML = doc.ToHtml(pipeline);
            html = html.Replace("%%CONTENT%%", contentHTML);

            //images.AddRange(IMG.ExtractImageUrlsFromHtmlAsync(contentHTML).Result);

            try
            {
                var dic = FrontMatter.GetFrontMatter(content.Split(Environment.NewLine));
                string title = dic["title"];

                //var nav = DocNav.Build(file.Replace(hive.Destination, ""), hive.Name, title);

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