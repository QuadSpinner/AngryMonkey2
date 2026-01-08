using Humanizer;

namespace AngryMonkey
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;

    public static class FoldersTxtIconUpdater
    {
        public sealed record Options(
            string ManifestFileName = "folders.txt",
            string IndexFileName = "index.md",
            string IconKey = "icon",
            bool Recursive = true,
            bool OverwriteExistingIcon = true,
            bool WriteUtf8NoBom = true);

        public sealed record Result(
            int ManifestFilesVisited,
            int ManifestFilesUpdated,
            int LinesUpdated,
            List<string> Warnings);

        /// <summary>
        /// Scans for folders.txt files under rootDir. For each entry (folderName|title|icon),
        /// looks for child folder's index.md, reads front matter via getFrontMatter(lines),
        /// and writes the icon into the 3rd field.
        /// </summary>
        public static Result UpdateIcons(
            string rootDir,
            Func<string[], Dictionary<string, string>> getFrontMatter,
            Options options = null)
        {
            if (string.IsNullOrWhiteSpace(rootDir))
                throw new ArgumentException("rootDir is required.", nameof(rootDir));
            if (getFrontMatter is null)
                throw new ArgumentNullException(nameof(getFrontMatter));

            options ??= new Options();

            var warnings = new List<string>();
            int visited = 0, updatedFiles = 0, updatedLines = 0;

            var search = options.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var manifests = Directory.EnumerateFiles(rootDir, options.ManifestFileName, search);

            foreach (var manifestPath in manifests)
            {
                visited++;

                string[] lines;
                try { lines = File.ReadAllLines(manifestPath, Encoding.UTF8); }
                catch (Exception ex)
                {
                    warnings.Add($"Read failed: {manifestPath} :: {ex.Message}");
                    continue;
                }

                var dirPath = Path.GetDirectoryName(manifestPath)!;

                bool fileChanged = false;
                for (int i = 0; i < lines.Length; i++)
                {
                    var raw = lines[i];
                    var trimmed = raw.Trim();

                    if (trimmed.Length == 0) continue;
                    if (trimmed.StartsWith("#", StringComparison.Ordinal)) continue;

                    // folderName|title|icon (title + icon optional)
                    var parts = trimmed.Split('|');

                    var name = parts.Length > 0 ? parts[0].Trim() : "";
                    if (name.Length == 0) continue;

                    var title = parts.Length > 1 ? parts[1].Trim() : "";
                    var existingIcon = parts.Length > 2 ? parts[2].Trim() : "";

                    var childDir = Path.Combine(dirPath, name);
                    if (!Directory.Exists(childDir))
                    {
                        warnings.Add($"Missing folder: {childDir} (referenced by {manifestPath})");
                        continue;
                    }

                    var indexPath = Path.Combine(childDir, options.IndexFileName);
                    if (!File.Exists(indexPath))
                        continue;

                    Dictionary<string, string> fm;
                    try
                    {
                        var mdLines = File.ReadAllLines(indexPath, Encoding.UTF8);
                        fm = getFrontMatter(mdLines) ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    }
                    catch (Exception ex)
                    {
                        warnings.Add($"Front matter read failed: {indexPath} :: {ex.Message}");
                        continue;
                    }

                    if (!fm.TryGetValue(options.IconKey, out var icon) || string.IsNullOrWhiteSpace(icon))
                        continue;

                    icon = icon.Trim();

                    // Only replace if allowed or if empty
                    if (!options.OverwriteExistingIcon && !string.IsNullOrWhiteSpace(existingIcon))
                        continue;

                    // Ensure icon is 3rd field (name|title|icon). If title missing, use empty: name||icon
                    string newLine =
                        parts.Length switch
                        {
                            1 => $"{name}||{icon}",
                            2 => $"{name}|{title}|{icon}",
                            _ => $"{name}|{title}|{icon}"
                        };

                    // Preserve original line if unchanged
                    if (string.Equals(raw, newLine, StringComparison.Ordinal))
                        continue;

                    lines[i] = newLine;
                    fileChanged = true;
                    updatedLines++;
                }

                if (!fileChanged) continue;

                try
                {
                    var enc = options.WriteUtf8NoBom ? new UTF8Encoding(false) : Encoding.UTF8;
                    File.WriteAllLines(manifestPath, lines, enc);
                    updatedFiles++;
                }
                catch (Exception ex)
                {
                    warnings.Add($"Write failed: {manifestPath} :: {ex.Message}");
                }
            }

            return new Result(visited, updatedFiles, updatedLines, warnings);
        }
    }

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