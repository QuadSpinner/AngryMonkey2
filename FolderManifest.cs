using System;
using System.Collections.Generic;
using System.Text;

using System;
using System.Collections.Generic;

using System.Globalization;
using System.IO;
using System.Linq;

using System.Text;

using System.Text.RegularExpressions;
using Humanizer;

namespace AngryMonkey
{
    public static class FolderManifest
    {
        public static int GenerateFoldersTxtRecursively(
            string rootDir,
            string manifestFileName = "folders.txt",
            bool includeTitles = true,
            bool overwrite = false)
        {
            if (string.IsNullOrWhiteSpace(rootDir))
                throw new ArgumentException("Root directory is required.", nameof(rootDir));

            var root = new DirectoryInfo(rootDir);
            if (!root.Exists)
                throw new DirectoryNotFoundException(root.FullName);

            int written = 0;

            foreach (var dir in EnumerateDirsDepthFirst(root))
            {
                DirectoryInfo[] subdirs;
                try
                {
                    subdirs = dir.EnumerateDirectories("*", SearchOption.TopDirectoryOnly)
                        .Where(d => !IsHiddenOrInternal(d.Name))
                        .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                }
                catch
                {
                    continue; // unreadable folder; skip
                }

                if (subdirs.Length == 0)
                    continue;

                var manifestPath = Path.Combine(dir.FullName, manifestFileName);

                if (!overwrite && File.Exists(manifestPath))
                    continue;

                var lines = subdirs.Select(d =>
                {
                    var name = d.Name;
                    if (!includeTitles) return name;

                    return $"{name}|{name.Humanize(LetterCasing.Title)}";
                });

                try
                {
                    File.WriteAllText(manifestPath,
                        string.Join(Environment.NewLine, lines) + Environment.NewLine,
                        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                    written++;
                }
                catch
                {
                    // can't write here; skip
                }
            }

            return written;
        }

        private static IEnumerable<DirectoryInfo> EnumerateDirsDepthFirst(DirectoryInfo root)
        {
            var stack = new Stack<DirectoryInfo>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                yield return current;

                DirectoryInfo[] children;
                try
                {
                    children = current.EnumerateDirectories("*", SearchOption.TopDirectoryOnly)
                        .Where(d => !IsHiddenOrInternal(d.Name))
                        .ToArray();
                }
                catch
                {
                    continue;
                }

                // depth-first (stable-ish)
                for (int i = children.Length - 1; i >= 0; i--)
                    stack.Push(children[i]);
            }
        }

        private static bool IsHiddenOrInternal(string name)
            => name.StartsWith(".", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("_", StringComparison.OrdinalIgnoreCase);


    }
}