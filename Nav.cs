using System.Net;
using System.Text;
using Humanizer;

namespace AngryMonkey
{
    public static class DocNav
    {
        public sealed record Result(string BreadcrumbHtml, string PretitleHtml, string PageTitleHtml);

        // mdPath: "getting-started/user-interface/data-editor/automation-view.md"
        // hive: provided by you (e.g. "Docs")
        // title: provided by you (e.g. YAML title)
        // section: first segment after Home (e.g. "Getting Started")
        public static Result Build(string mdPath, string hive, string title,
            string baseHref = "/", string homeHref = "#")
        {
            mdPath ??= "";
            hive ??= "";
            title ??= "";

            mdPath = mdPath.Replace('\\', '/').Trim('/');
            baseHref = (baseHref ?? "/").Replace('\\', '/');
            if (!baseHref.EndsWith("/")) baseHref += "/";

            var parts = mdPath.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();

            // Drop filename segment for breadcrumb label (keep for href building)
            var crumbParts = parts.ToList();
            if (crumbParts.Count > 0 && crumbParts[^1].EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                crumbParts[^1] = Path.GetFileNameWithoutExtension(crumbParts[^1]);

            var section = crumbParts.Count > 0 ? crumbParts[0].Humanize(LetterCasing.Title) : "";

            var breadcrumbHtml = BuildBreadcrumbHtml(crumbParts, baseHref, hive, homeHref);
            var pretitleHtml = $"""<div class="page-pretitle">{WebUtility.HtmlEncode(section)}</div>""";
            var pageTitleHtml = $"""<h2 class="page-title">{WebUtility.HtmlEncode(title)}</h2>""";

            return new Result(breadcrumbHtml, pretitleHtml, pageTitleHtml);
        }

        private static string BuildBreadcrumbHtml(IReadOnlyList<string> parts,
            string baseHref, string homeText, string homeHref)
        {
            var sb = new StringBuilder();
            sb.AppendLine("""<ol class="breadcrumb" aria-label="breadcrumbs">""");

            sb.AppendLine("""  <li class="breadcrumb-item">""");
            sb.AppendLine($"""    <a href="{WebUtility.HtmlEncode(homeHref)}">{WebUtility.HtmlEncode(homeText)}</a>""");
            sb.AppendLine(@"  </li>");

            var cumulative = "";
            for (int i = 0; i < parts.Count; i++)
            {
                var isLast = i == parts.Count - 1;

                var slug = parts[i];
                var label = slug.Humanize(LetterCasing.Title);

                if (!isLast)
                {
                    cumulative += slug + "/";
                    var href = baseHref + cumulative;

                    sb.AppendLine("""  <li class="breadcrumb-item">""");
                    sb.AppendLine($"""    <a href="{WebUtility.HtmlEncode(href)}">{WebUtility.HtmlEncode(label)}</a>""");
                    sb.AppendLine(@"  </li>");
                }
                else
                {
                    sb.AppendLine("""  <li class="breadcrumb-item active" aria-current="page">""");
                    sb.AppendLine($"""    <a href="#">{WebUtility.HtmlEncode(label)}</a>""");
                    sb.AppendLine(@"  </li>");
                }
            }

            sb.AppendLine(@"</ol>");
            return sb.ToString();
        }
    }
}