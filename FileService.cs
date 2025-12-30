namespace AngryMonkey
{
    internal static class FileService
    {
        internal static void CopyDirectory(string source, string destination, string pattern = "*.md")
        {
            Directory.CreateDirectory(destination);

            foreach (string dirPath in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(source, destination));
            }

            foreach (string newPath in Directory.GetFiles(source, pattern, SearchOption.AllDirectories))
            {
                File.Copy(newPath, newPath.Replace(source, destination), true);
            }
        }
        internal static void CopyAll(string sourcePath, string destinationPath)
        {
            foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(sourcePath, destinationPath));
            }

            foreach (string newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
            {
                File.Copy(newPath, newPath.Replace(sourcePath, destinationPath), true);
            }
        }
    }
}
