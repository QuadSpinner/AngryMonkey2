using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Syntax;
using Spectre.Console;

namespace AngryMonkey;

public static class Program
{
    //public static string source = "X:\\Gaea2\\Docs\\Source\\Nodes";
    //public static string destination = "X:\\Gaea2\\Docs\\staging";

    public static string Root = @"X:\Docs\Gaea2-Docs\staging";

    public static string Templates = @"X:\Docs\Gaea2-Docs\template\";
    public static string HtmlTemplate = @"X:\Docs\Gaea2-Docs\template\template.html";
    public static string Html;
    public static MarkdownPipeline pipeline;

    private static Dictionary<string, string> TOC = [];
    private static Dictionary<string, string> Titles = [];
    private static Dictionary<string, string> mdTitles = [];

    internal static Hive CurrentHive { get; set; }

    private const string SiteTitle = "Gaea Documentation";

    public static void Main(string[] arguments)
    {// Synchronous
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

                Hive[] hives =
                [
                    new()
                    {
                        Name = "User Guide",
                        Source = @"X:\Docs\Gaea2-Docs\Source\UserGuide",
                        Destination = @"X:\Docs\Gaea2-Docs\staging\Manual",
                    },
                    new()
                    {
                        Name = "Node Reference",
                        Source = @"X:\Docs\Gaea2-Docs\Source\Nodes",
                        Destination = @"X:\Docs\Gaea2-Docs\staging\Nodes",
                    }
                ];

                Html = File.ReadAllText(HtmlTemplate);

                FileService.CopyDirectory($"{Templates}\\Assets", $"{Root}\\Assets");

                foreach (Hive hive in hives)
                {
                    ctx.Status($"Processing {hive.Name}...");
                    CurrentHive = hive;
                    ParseTOC(hive);
                    // PreprocessMarkdown();
                    RecreateDestination(hive);
                    ProcessMarkdown(hive);
                }
            });

        Console.WriteLine("The Monkey has gone home [Success]");
    }

    private static void PreprocessMarkdown(Hive hive)
    {
        string[] md = Directory.GetFiles(hive.Source, "*.md", SearchOption.AllDirectories);
        foreach (var file in md)
        {
            Console.WriteLine(file);
            string content = File.ReadAllText(file);
            content = content
                .Replace("{% hint style=\"warning\" %}", ":::warning")
                .Replace("{% hint style=\"danger\" %}", ":::danger")
                .Replace("{% hint style=\"note\" %}", ":::note")
                .Replace("{% hint style=\"tip\" %}", ":::tip")
                .Replace("{% hint style=\"success\" %}", ":::success")
                .Replace("{% hint style=\"info\" %}", ":::info")
                .Replace("{% endhint %}", ":::")
                .Replace(".md \"mention\"", ".html");

            string title = "untitled";
            try
            {
                title = TOC[file.Replace(hive.Source, "").Replace("\\", "/").TrimStart('/')];
            }
            catch (Exception)
            {
                title = "untitled";
            }

            content = FrontMatter.EnsureYamlTitle(content, title);

            File.WriteAllText(file, content);
        }
    }

    private static void ParseTOC(Hive hive)
    {
        TOC.Clear();
        Titles.Clear();
        mdTitles.Clear();

        string tocFile = Path.Combine(hive.Source, "SUMMARY.md");

        string[] lines = File.ReadAllLines(tocFile);
        foreach (var line in lines)
        {
            if (line.Trim().StartsWith("* ["))
            {
                int startTitle = line.IndexOf('[') + 1;
                int endTitle = line.IndexOf(']');
                int startLink = line.IndexOf('(') + 1;
                int endLink = line.IndexOf(')');
                if (startTitle >= 0 && endTitle > startTitle && startLink >= 0 && endLink > startLink)
                {
                    string title = line.Substring(startTitle, endTitle - startTitle);
                    string link = line.Substring(startLink, endLink - startLink);
                    TOC[link] = title;
                    Titles[title] = link;
                }
            }
        }
    }

    public static void RecreateDestination(Hive hive)
    {
        // delete all contents in the destination directory
        if (Directory.Exists(hive.Destination))
        {
            Directory.Delete(hive.Destination, true);
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

    public static void ProcessMarkdown(Hive hive)
    {
        string[] md = Directory.GetFiles(hive.Destination, "*.md", SearchOption.AllDirectories);

        var resolver = new MarkdownTitleResolver(hive.Destination);
        var toc = SummaryToc.BuildFromFile(summaryMdPath: hive.Destination + "\\summary.md",
            options: new SummaryTocOptions
            {
                BaseUrl = "/",
                PromoteParentLinksToGroups = true,
                OverviewTitle = "Overview"
            });

        var js = SummaryToc.ToJavascript(toc);

        foreach (var file in md)
        {
            string html = Html;

            string hiveName = hive.Name ?? "NO_HIVE";

            string content = File.ReadAllText(file).Replace(".md", ".html");

            MarkdownDocument doc = Markdown.Parse(content, pipeline);

            MarkdownLinkTextToTargetTitle.Rewrite(doc, file, resolver);

            html = html.Replace("%%CONTENT%%", doc.ToHtml(pipeline));

            try
            {
                string title = resolver.TryGetTitle(file) ?? "Untitled";

                var nav = DocNav.Build(file.Replace(hive.Destination, ""), hiveName, title);

                html = html.Replace("%%TITLE%%", $"{title} - {SiteTitle}")
                    .Replace("%%HIVE%%", hiveName)
                    .Replace("//%%TOC%%", js)
                    .Replace("%%CRUMBS%%", $"{nav.BreadcrumbHtml}")
                    .Replace("%%PAGETITLE%%", $"\n{nav.PretitleHtml}\n{nav.PageTitleHtml}\n<hr>");
            }
            catch (Exception ex)
            {
                Console.WriteLine(file);
                Console.WriteLine(ex.Message);
            }

            File.WriteAllText(file.Replace("README", "index").Replace(".md", ".html"), html);
        }
    }
}