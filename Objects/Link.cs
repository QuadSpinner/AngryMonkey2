using Newtonsoft.Json;

namespace AngryMonkey
{
    public class Link(string href = "", string title = "", string slug = "", string icon = "", bool hidden = false)

    {
        [JsonProperty("title")]
        public string Title { get; set; } = title;

        [JsonProperty("href")]
        public string Href { get; set; } = href;

        [JsonProperty("slug")]
        public string Slug { get; set; } = slug;

        [JsonProperty("icon")]
        public string Icon { get; set; } = icon;

        [JsonProperty("hidden")]
        public bool Hidden { get; set; } = hidden;

        public override string ToString() => $"{Href} | {Title} | {Slug} | {Icon}";
    }
}