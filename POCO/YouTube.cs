using System.Text.Json;
using System.Text.Json.Serialization;

namespace AngryMonkey.POCO
{
    public static class YouTubeImporter
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static ImportConfig ReadFromFile(string path)
        {
            var json = File.ReadAllText(path);
            return ReadFromString(json);
        }

        public static ImportConfig ReadFromString(string json)
            => JsonSerializer.Deserialize<ImportConfig>(json, Options)
               ?? throw new JsonException("Invalid JSON: deserialized to null.");
    }

    public sealed class ImportConfig
    {
        [JsonPropertyName("channels")]
        public List<Channel> Channels { get; set; } = [];
    }

    public sealed class Channel
    {
        // Either youtubeUrls OR youtubeUrlsRef should be present
        [JsonPropertyName("ids")]
        public List<string> IDs { get; set; }

        [JsonPropertyName("directory")]
        public string Directory { get; set; } = "";

        [JsonPropertyName("authorized")]
        public bool Authorized { get; set; } = false;

        [JsonPropertyName("official")]
        public bool Official { get; set; } = false;

        public void Validate()
        {
            Directory ??= "";
        }
    }
}