using AngryMonkey.POCO;
using Newtonsoft.Json;

namespace AngryMonkey
{
    internal static class Config
    {
        internal static AppConfig LoadConfig(string path)
        {
            var cfg = JsonConvert.DeserializeObject<AppConfig>(File.ReadAllText(path))
                      ?? throw new InvalidOperationException("Invalid config JSON.");

            cfg.RootFolder = Path.GetFullPath(cfg.RootFolder);

            cfg.SourceRoot = MakePath(cfg.RootFolder, cfg.SourceRoot);
            cfg.StagingRoot = MakePath(cfg.RootFolder, cfg.StagingRoot);
            cfg.TemplatesFolder = MakePath(cfg.RootFolder, cfg.TemplatesFolder);
            cfg.HtmlTemplate = MakePath(cfg.RootFolder, cfg.HtmlTemplate);

            return cfg;
        }

        internal static string MakePath(string root, string p)
        {
            if (string.IsNullOrWhiteSpace(p)) return root;
            return Path.IsPathRooted(p) ? Path.GetFullPath(p) : Path.GetFullPath(Path.Combine(root, p));
        }

        internal static Hive[] BuildHives(AppConfig cfg)
        {
            return cfg.Hives.Select(h =>
            {
                var folder = (h.Folder ?? "").Trim().Trim('\\', '/');

                string source = string.IsNullOrEmpty(folder) ? cfg.SourceRoot : Path.Combine(cfg.SourceRoot, folder);
                string dest = string.IsNullOrEmpty(folder) ? cfg.StagingRoot : Path.Combine(cfg.StagingRoot, folder);

                return new Hive
                {
                    Name = h.Name,
                    Source = source,
                    Destination = dest,
                    ShortName = h.ShortName,
                    IsHome = h.IsHome,
                    URL = h.Url
                };
            }).ToArray();
        }
    }
}