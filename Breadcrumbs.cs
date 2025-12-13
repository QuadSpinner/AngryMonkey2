using System.Text;
using System.Net;
using Humanizer;

namespace AngryMonkey
{
    public static class Breadcrumbs
    {
        public static string ToBreadcrumbHtml(string mdPath, string baseHref = "/",
            string homeText = "Home", string homeHref = "#")
        {
            mdPath ??= "";
            baseHref ??= "/";

            mdPath = mdPath.Replace('\\', '/').Trim('/');
            baseHref = baseHref.Replace('\\', '/');
            if (!baseHref.EndsWith("/")) baseHref += "/";

            var parts = mdPath.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
            if (parts.Count == 0) return Empty(homeText, homeHref);

            // last part: file -> drop extension
            if (parts[^1].EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                parts[^1] = Path.GetFileNameWithoutExtension(parts[^1]);

            var sb = new StringBuilder();
            sb.AppendLine(@"<ol class=""breadcrumb"" aria-label=""breadcrumbs"">");

            // Home
            sb.AppendLine(@"  <li class=""breadcrumb-item"">");
            sb.AppendLine($@"    <a href=""{WebUtility.HtmlEncode(homeHref)}"">{WebUtility.HtmlEncode(homeText)}</a>");
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

                    sb.AppendLine(@"  <li class=""breadcrumb-item"">");
                    sb.AppendLine($@"    <a href=""{WebUtility.HtmlEncode(href)}"">{WebUtility.HtmlEncode(label)}</a>");
                    sb.AppendLine(@"  </li>");
                }
                else
                {
                    sb.AppendLine(@"  <li class=""breadcrumb-item active"" aria-current=""page"">");
                    sb.AppendLine($@"    <a href=""#"">{WebUtility.HtmlEncode(label)}</a>");
                    sb.AppendLine(@"  </li>");
                }
            }

            sb.AppendLine(@"</ol>");
            return sb.ToString();
        }

        private static string Empty(string homeText, string homeHref) =>
    $@"<ol class=""breadcrumb"" aria-label=""breadcrumbs"">
  <li class=""breadcrumb-item"">
    <a href=""{WebUtility.HtmlEncode(homeHref)}"">{WebUtility.HtmlEncode(homeText)}</a>
  </li>
</ol>";
    }
}