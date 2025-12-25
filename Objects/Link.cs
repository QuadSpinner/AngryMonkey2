namespace AngryMonkey
{
    public class Link(string href = "", string title = "", string slug = "")
    {
        public string Title { get; set; } = title;

        public string Href { get; set; } = href;
        public string Slug { get; set; } = slug;

        public override string ToString() => $"{Href} | {Title} | {Slug}";
    }
}