using Newtonsoft.Json;

namespace AngryMonkey
{
    public class Link(string href = "", string title = "", string slug = "")
    {
        [JsonProperty("title")]
        public string Title { get; set; } = title;

        [JsonProperty("href")]
        public string Href { get; set; } = href;

        [JsonProperty("slug")]
        public string Slug { get; set; } = slug;

        public override string ToString() => $"{Href} | {Title} | {Slug}";
    }
}