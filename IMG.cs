using System.Net;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace AngryMonkey
{
    public static class IMG
    {
        public static async Task<IReadOnlyList<string>> ExtractImageUrlsFromHtmlAsync(
            string html,
            string baseUri = null,
            bool resolveToAbsolute = false)
        {
            var context = BrowsingContext.New(Configuration.Default);
            var parser = context.GetService<IHtmlParser>() ?? new HtmlParser();

            var doc = await parser.ParseDocumentAsync(html ?? "");

            Url baseUrl = !string.IsNullOrWhiteSpace(baseUri) ? new Url(baseUri) : null;

            var urls = new List<string>();

            // <img src="...">
            foreach (var el in doc.QuerySelectorAll("img[src]"))
                AddUrl(urls, el.GetAttribute("src"), baseUrl, resolveToAbsolute);

            // srcset on <img> or <source>
            foreach (var el in doc.QuerySelectorAll("img[srcset], source[srcset]"))
            {
                var srcset = el.GetAttribute("srcset");
                foreach (var u in ParseSrcSetUrls(srcset))
                    AddUrl(urls, u, baseUrl, resolveToAbsolute);
            }

            // <link rel="preload" as="image" href="...">
            foreach (var el in doc.QuerySelectorAll("link[rel~='preload'][as='image'][href]"))
                AddUrl(urls, el.GetAttribute("href"), baseUrl, resolveToAbsolute);

            return urls
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void AddUrl(List<string> list, string raw, Url baseUrl, bool resolve)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;

            raw = WebUtility.HtmlDecode(raw.Trim());

            if (!resolve || baseUrl is null)
            {
                list.Add(raw);
                return;
            }

            list.Add(new Url(baseUrl, raw).Href);
        }

        private static IEnumerable<string> ParseSrcSetUrls(string srcset)
        {
            if (string.IsNullOrWhiteSpace(srcset)) yield break;

            foreach (var part in srcset.Split(','))
            {
                var token = part.Trim();
                if (token.Length == 0) continue;

                var space = token.IndexOfAny([' ', '\t', '\r', '\n']);
                yield return (space >= 0 ? token[..space] : token).Trim();
            }
        }
    }
}