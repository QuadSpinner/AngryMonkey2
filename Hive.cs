namespace AngryMonkey
{
    public class Hive

    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string URL { get; set; }

        public string Source { get; set; }
        public string Destination { get; set; }

        //internal string Name => this["Name"] ?? "MISSING";
        //internal string? Icon => this.ContainsKey("Icon") ? this["Icon"] : null;

        //internal static Hive Load(string path)
        //{
        //    Hive data = [];

        //    string[] raw = File.ReadAllLines(path);

        //    foreach (string line in raw)
        //    {
        //        string[] split = line.Split(':', StringSplitOptions.TrimEntries);
        //        data.Add(split[0], split[1]);
        //    }

        //    return data;
        //}

        //internal static void Save(string path, Hive data)
        //{
        //    StringBuilder sb = new();

        //    foreach (var kvp in data)
        //    {
        //        sb.AppendLine($"{kvp.Key}:{kvp.Value}");
        //    }

        //    File.WriteAllText(path, sb.ToString());
        //}
    }
}