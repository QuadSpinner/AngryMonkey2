namespace AngryMonkey
{
    public class Link(string href = "", string title = "", Page page = null)
    {
        public string Title { get; set; } = title;

        public string Href { get; set; } = href;
        public Page Page { get; set; } = page;
    }
}