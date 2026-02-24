using AngryMonkey.POCO;
using QuadSpinner.Adjunct;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AngryMonkey
{
    internal class YouTube
    {
        private Hive _hive;

        private HttpClient _yt;

        private string _youtubeApiKey;

        private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

        internal void MakeYouTubePages(Hive hive)
        {
            _hive = hive;

            _yt = new HttpClient();
            _youtubeApiKey = File.ReadAllText("youtube.txt");
            _yt.DefaultRequestHeaders.Accept.Clear();
            _yt.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            ImportConfig imports = YouTubeImporter.ReadFromFile($"{Program.RootFolder}\\youtube.json");

            foreach (Channel item in imports.Channels)
            {
                Import(item.IDs, item.Directory, item.Authorized, item.Official);
            }


        }

        public void Import(IEnumerable<string> youtubeUrls, string directory = "", bool authorized = false, bool official = false)
        {
            // Create in chronological order (optional but usually nicer for topic lists)
            var vids = new List<(string Url, string VideoId, VideoSnippet Snippet)>();

            foreach (var url in youtubeUrls)
            {
                try
                {
                    if (url is null)
                        throw new ArgumentException($"Could not extract YouTube video id from: {url}");

                    if (Program.Pages.Any(x => x.Tag == url))
                        continue;

                    var snippet = GetYoutubeSnippet(url);
                    Console.WriteLine($"Getting video {url}...");
                    vids.Add(("https://www.youtube.com/watch?v=" + url, url, snippet));
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }

            int order = 1;

            foreach (var v in vids.OrderByDescending(x => x.Snippet.PublishedAtUtc))
            {
                string title = "";

                if (official)
                {
                    if (v.Snippet.Title.Contains(":")) { title = v.Snippet.Title.Split(':')[^1]; }
                    if (v.Snippet.Title.Contains("-")) { title = v.Snippet.Title.Split('-')[^1]; }
                }

                if (v.Snippet.Title.Contains("#")) { title = v.Snippet.Title.Split('#')[0]; }

                title = title.Strip(":").Trim();

                if (title == "")
                    title = v.Snippet.Title;

                var createdAt = v.Snippet.PublishedAtUtc.ToString("D", CultureInfo.InvariantCulture);

                StringBuilder sb = new();

                string slug = $"yt-{FrontMatter.Slugify(title)}";

                if (!official)
                {
                    slug = "yt-" + v.VideoId;
                }

                sb.AppendLine("---");
                sb.AppendLine("title: " + title);
                sb.AppendLine($"uid: {slug}");
                sb.AppendLine($"tag: {v.VideoId}");
                sb.AppendLine($"order: {order:000}");
                order++;

                if (authorized)
                {
                    sb.AppendLine("icon: certificate");
                }

                if (official)
                {
                    sb.AppendLine("icon: kit fa-qs-logo");
                }
                sb.AppendLine("---");
                sb.AppendLine();
                sb.AppendLine($"![youtube.com]({v.Url.Trim()})");
                sb.AppendLine();
                if (authorized || official)
                {
                    sb.AppendLine(v.Snippet.Description ?? string.Empty);
                }
                if (!authorized && !official)
                {
                    sb.AppendLine(":::warning\n" +
                                  "Unofficial Training: This video is contributed by the community." +
                                  "\n:::");
                }
                sb.AppendLine();
                if (v.Snippet.PublishedAtUtc.Date < DateTime.UtcNow.AddMonths(-8))
                {
                    sb.AppendLine(":::note\n" +
                                  "This video is older than 8 months and some nodes may have changed since then. Please check the latest @changelogs-home." +
                                  "\n:::");
                }
                sb.AppendLine();
                sb.AppendLine($"Published on {createdAt}");

                if (official)
                {
                    if (v.Snippet.Title.Contains("Node:"))
                    {
                        Directory.CreateDirectory($@"{_hive.Source}\official\nodes\");
                        File.WriteAllText($@"{_hive.Source}\official\nodes\{slug}.md", sb.ToString());
                    }
                    else if (v.Snippet.Title.Contains("Breakdown") || v.Snippet.Title.Contains("Deep Dive"))
                    {
                        Directory.CreateDirectory($@"{_hive.Source}\official\deepdives\");
                        File.WriteAllText($@"{_hive.Source}\official\deepdives\{slug}.md", sb.ToString());
                    }
                    else
                    {
                        Directory.CreateDirectory($@"{_hive.Source}\official\tutorials\");
                        File.WriteAllText($@"{_hive.Source}\official\tutorials\{slug}.md", sb.ToString());
                    }
                }
                else
                {
                    if (authorized)
                    {
                        Directory.CreateDirectory($@"{_hive.Source}\partners\{directory}\");
                        File.WriteAllText($@"{_hive.Source}\partners\{directory}\{v.VideoId}.md", sb.ToString());
                    }
                    else
                    {
                        Directory.CreateDirectory($@"{_hive.Source}\community\{directory}\");
                        File.WriteAllText($@"{_hive.Source}\community\{directory}\{v.VideoId}.md", sb.ToString());
                    }
                }

                //File.WriteAllText(Program.RootFolder + @"\Source\.videos\" + v.VideoId, "");
            }
        }

        // -------- YouTube --------

        private VideoSnippet GetYoutubeSnippet(string videoId)
        {
            try
            {
                var url = $"https://www.googleapis.com/youtube/v3/videos?part=snippet&id={Uri.EscapeDataString(videoId)}&key={Uri.EscapeDataString(_youtubeApiKey)}";

                using var resp = _yt.GetAsync(url).Result;
                var json = resp.Content.ReadAsStringAsync().Result;

                if (!resp.IsSuccessStatusCode)
                    throw new HttpRequestException($"YouTube API error {(int)resp.StatusCode}: {json}");

                var parsed = JsonSerializer.Deserialize<YoutubeVideosListResponse>(json, JsonOpts)
                             ?? throw new InvalidOperationException("Failed to parse YouTube response.");

                var item = parsed.Items?.FirstOrDefault()
                           ?? throw new InvalidOperationException($"YouTube video not found: {videoId}");

                var sn = item.Snippet ?? throw new InvalidOperationException("YouTube snippet missing.");

                // publishedAt is RFC3339; parse as UTC.
                var published = DateTime.Parse(sn.PublishedAt, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

                return new VideoSnippet(
                    Title: sn.Title ?? $"YouTube Video {videoId}",
                    Description: sn.Description ?? string.Empty,
                    PublishedAtUtc: published);
            }
            catch (Exception ex)
            {
                return new VideoSnippet();
            }
        }

        // -------- Models --------

        public readonly record struct VideoSnippet(string Title, string Description, DateTime PublishedAtUtc);

        private sealed class YoutubeVideosListResponse
        {
            public List<YoutubeVideoItem> Items { get; init; }
        }

        private sealed class YoutubeVideoItem
        { public YoutubeSnippet Snippet { get; set; } }

        private sealed class YoutubeSnippet
        {
            public string Title { get; set; }
            public string Description { get; set; }
            public string PublishedAt { get; set; } = "";
        }
    }
}