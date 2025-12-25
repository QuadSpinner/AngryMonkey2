namespace AngryMonkey
{
    public class Note
    {
        public Note(Guid id, string name, string text)
        {
            Id = id;
            Name = name;
            Text = text;
        }

        public Note() { }

        public Guid Id { get; set; }

        public string Name { get; set; }

        public string Parameters { get; set; }

        public string Text { get; set; }
    }
}