using System.Runtime.CompilerServices;

namespace AngryMonkey
{
    public class Hive
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string URL { get; set; } = null;
        public HiveType Type { get; set; } = HiveType.Articles;
        public string Source { get; set; }
        public string Destination { get; set; }
        public string ShortName { get; set; }
        public bool IsHome => this.Type == HiveType.TopLevel;
        public double SearchWeight { get; set; }
    }

    public enum HiveType
    {
        Articles,
        TopLevel,
        Reference,
        Videos,
        Changelogs,
        Blog
    }


}