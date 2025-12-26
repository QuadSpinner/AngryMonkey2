using Markdig;

namespace AngryMonkey.Objects
{
    public class SearchObject
    {
        public string url { get; set; }

        public string hive { get; set; }

        public string title { get; set; }

        public string text { get; set; }

        public static SearchObject ToSearchObject(string markdown, string title, Hive hive, string url)
        {
            string doc = Markdown.ToPlainText(markdown, Program.pipeline);
            //.ToLower()
            //.Replace("\n", " ")
            //.Replace("'", string.Empty)
            //.Replace(" is ", " ")
            //.Replace(" that ", " ")
            //.Replace(" a ", " ")
            //.Replace("\\", " ")
            //.Replace("/", " ")
            //.Replace(",", string.Empty)
            //.Replace(":", string.Empty)
            //.Replace("!", string.Empty)
            //.Replace("`", string.Empty)
            //.Replace("@", string.Empty);
            //                    .Replace(".", " ")
            //                    .Replace("  ", " ")
            //                    .Replace(" an ", " ")
            //                    .Replace(" for ", " ")
            //                    .Replace(" this ", " ")
            //                    .Replace(" some ", " ")
            //                    .Replace(" other ", " ")
            //                    .Replace(" be ", " ")
            //                    .Replace(" to ", " ")
            //                    .Replace(" it ", " ")
            //                    .Replace(" from ", " ")
            //                    .Replace(" the ", " ")
            //                    .Replace("the ", " ")
            //                    .Replace(" can ", " ")
            //                    .Replace(" else ", " ")
            //                    .Replace(" you ", " ")
            //                    .Replace(" than ", " ")
            //                    .Replace(" of ", " ")
            //                    .Replace(" into ", " ")
            //                    .Replace(" lets ", " ")
            //                    .Replace(" let ", " ")
            //                    .Replace(" on ", " ")
            //                    .Replace(" even ", " ")
            //                    .Replace(" more ", " ")
            //                    .Replace(" help ", " ")
            //                    .Replace(" better ", " ")
            //                    .Replace(" gives ", " ")
            //                    .Replace(" easy ", " ")
            //                    .Replace(" go ", " ")
            //                    .Replace(" need ", " ")
            //                    .Replace(" are ", " ")
            //                    .Replace(" is ", " ")
            //                    .Replace(" choose ", " ")
            //                    .Replace(" see ", " ")
            //                    .Replace(" there ", " ")
            //                    .Replace(" give ", " ")
            //                    .Replace(" at ", " ")
            //                    .Replace(" here ", " ")
            //                    .Replace(" using ", " ")
            //                    .Replace(" and ", " ")
            //                    .Replace(" then ", " ")
            //                    .Replace(" found ", " ")
            //                    .Replace(" wont ", " ")
            //                    .Replace(" cant ", " ")
            //                    .Replace(" get ", " ")
            //                    .Replace(" will ", " ")
            //                    .Replace("-", " ")
            //                    .Replace(" we ", " ")
            //                    .Replace(" or ", " ")
            //                    .Replace(" only ", " ")
            //                    .Replace(" was ", " ")
            //                    .Replace(" also ", " ")
            //                    .Replace(" by ", " ")
            //                    .Replace(" has ", " ")
            //                    .Replace(" has ", " ")
            //                    .Replace(" your ", " ")
            //                    .Replace(" not ", " ")
            //                    .Replace(" have ", " ")
            //                    .Replace("All ", " ")
            //                    .Replace(" all ", " ")
            //                    .Replace(" these ", " ")
            //                    .Replace(" if ", " ")
            //                    .Replace("if ", " ")
            //                    .Replace(" their ", " ")
            //                    .Replace(" with ", " ")
            //                    .Replace(" as ", " ")
            //                    .Replace("  ", " ")
            //                    .Replace("  ", " ")
            //                    .Trim().Split(' ', StringSplitOptions.TrimEntries);

            //StringBuilder temp = new();
            //foreach (string s in doc.Distinct())
            //{
            //    temp.Append($"{s} ");
            //}

            return new SearchObject
            {
                hive = hive.Name,
                text = doc,
                title = title,
                url = url,
                //w = Hive.SearchWeight
            };
        }
    }
}