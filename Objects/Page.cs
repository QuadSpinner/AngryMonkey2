namespace AngryMonkey
{
    public class Page
    {
        public string Title { get; set; }

        public string Href => $"/{Hive?.URL}/{(string.IsNullOrEmpty(Parent.Href) ? string.Empty : Parent.Href + "/")}{Strip(Path.GetFileNameWithoutExtension(Filename))}.html";

        public string Filename { get; set; }

        public Dictionary<string, string> AlternateNames { get; set; } = [];
        public Dictionary<string, string> Sublinks { get; set; } = [];

        public string UID { get; set; }
        public string Icon { get; set; }

        public Hive Hive { get; set; }

        public Link Parent { get; set; }

        public bool Hidden { get; set; }
        public bool StartsSection { get; set; }
        public string Special { get; set; }

        public string Contents { get; set; }

        public Link HrefPrev { get; set; }
        public Link HrefNext { get; set; }
        public string Tag { get; set; }
        public string Link { get; set; }
        public string Directory { get; set; }
        public DateTime Modified { get; set; }

        private static string Strip(string name)
        {
            if (name.Contains("-"))
            {
                string prefix = name.Split('-').First();
                if (int.TryParse(prefix, out int _))
                {
                    return name.Replace($"{prefix}-", string.Empty);
                }
            }
            return name;
        }
    }
}