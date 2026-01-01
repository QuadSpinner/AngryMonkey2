using AngleSharp;
using AngleSharp.Html;
using AngleSharp.Html.Parser;

namespace AngryMonkey
{

    public static class HtmlMin
    {
        public static string Minify(string html)
        {
            var doc = new HtmlParser().ParseDocument(html);

            // simplest (minifies using default MinifyMarkupFormatter options)
            return doc.DocumentElement.Minify(); // extension method
        }

        public static string Minify(string html, bool keepComments)
        {
            var doc = new HtmlParser().ParseDocument(html);

            var fmt = new MinifyMarkupFormatter
            {
                ShouldKeepComments = keepComments,
                ShouldKeepAttributeQuotes = false,
                ShouldKeepEmptyAttributes = false,
                ShouldKeepImpliedEndTag = false,
                ShouldKeepStandardElements = true
            };

            using var sw = new StringWriter();
            doc.DocumentElement.ToHtml(sw, fmt);
            return sw.ToString();
        }
    }
}
