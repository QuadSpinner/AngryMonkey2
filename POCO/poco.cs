namespace AngryMonkey.POCO
{
    public sealed class AppConfig
    {
        public string RootFolder { get; set; } = "";
        public string SourceRoot { get; set; } = "Source";
        public string StagingRoot { get; set; } = "staging";
        public string TemplatesFolder { get; set; } = "template";
        public string HtmlTemplate { get; set; } = "template\\template.html";

        public string[] DataFolders { get; set; } = [];
        public List<HiveConfig> Hives { get; set; } = new();
    }

    public sealed class HiveConfig
    {
        public string Name { get; set; } = "";
        public string Folder { get; set; } = "";
        public string ShortName { get; set; } = "";
        public bool IsHome { get; set; }
        public string Url { get; set; } = "";
    }


}
