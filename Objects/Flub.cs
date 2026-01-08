namespace AngryMonkey.Objects
{
    [Serializable]
    public class Flub
    {
        public string Name { get; set; }
        public string Description { get; set; }

        public bool IsGroup { get; set; } = false;

        public Flub[] Flubs { get; set; }
    }
}