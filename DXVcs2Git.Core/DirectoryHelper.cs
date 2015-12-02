﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace DXVcs2Git.Core {
    public static class DirectoryHelper {
        static readonly Dictionary<string, string> toRename = new Dictionary<string, string> {
            {"dot_git", ".git"},
            {"gitmodules", ".gitmodules"}
        };

        static readonly Type[] whitelist = {typeof (IOException), typeof (UnauthorizedAccessException)};
        public static bool IsGitDir(string path) {
            if (!Directory.Exists(path))
                return false;
            return Directory.EnumerateDirectories(path).Any(x => Path.GetFileName(x) == ".git");
        }

        public static void DeleteDirectory(string directoryPath) {
            // From http://stackoverflow.com/questions/329355/cannot-delete-directory-with-directory-deletepath-true/329502#329502

            if (!Directory.Exists(directoryPath)) {
                Trace.WriteLine(string.Format("Directory '{0}' is missing and can't be removed.", directoryPath));
                return;
            }
            NormalizeAttributes(directoryPath);
            DeleteDirectory(directoryPath, 5, 16, 2);
        }
        static void NormalizeAttributes(string directoryPath) {
            string[] filePaths = Directory.GetFiles(directoryPath);
            string[] subdirectoryPaths = Directory.GetDirectories(directoryPath);

            foreach (string filePath in filePaths)
                File.SetAttributes(filePath, FileAttributes.Normal);
            foreach (string subdirectoryPath in subdirectoryPaths)
                NormalizeAttributes(subdirectoryPath);
            File.SetAttributes(directoryPath, FileAttributes.Normal);
        }

        static void DeleteDirectory(string directoryPath, int maxAttempts, int initialTimeout, int timeoutFactor) {
            for (var attempt = 1; attempt <= maxAttempts; attempt++) {
                try {
                    Directory.Delete(directoryPath, true);
                    return;
                }
                catch (Exception ex) {
                    Type caughtExceptionType = ex.GetType();

                    if (!whitelist.Any(knownExceptionType => knownExceptionType.IsAssignableFrom(caughtExceptionType)))
                        throw;

                    if (attempt < maxAttempts) {
                        Thread.Sleep(initialTimeout * (int)Math.Pow(timeoutFactor, attempt - 1));
                        continue;
                    }

                    Trace.WriteLine(string.Format("{0}The directory '{1}' could not be deleted ({2} attempts were made) due to a {3}: {4}" +
                                                  "{0}Most of the time, this is due to an external process accessing the files in the temporary repositories created during the test runs, and keeping a handle on the directory, thus preventing the deletion of those files." +
                                                  "{0}Known and common causes include:" +
                                                  "{0}- Windows Search Indexer (go to the Indexing Options, in the Windows Control Panel, and exclude the bin folder of LibGit2Sharp.Tests)" +
                                                  "{0}- Antivirus (exclude the bin folder of LibGit2Sharp.Tests from the paths scanned by your real-time antivirus)" +
                                                  "{0}- TortoiseGit (change the 'Icon Overlays' settings, e.g., adding the bin folder of LibGit2Sharp.Tests to 'Exclude paths' and appending an '*' to exclude all subfolders as well)",
                        Environment.NewLine, Path.GetFullPath(directoryPath), maxAttempts, caughtExceptionType, ex.Message));
                }
            }
        }

        public static void CopyFilesRecursively(DirectoryInfo source, DirectoryInfo target) {
            // From http://stackoverflow.com/questions/58744/best-way-to-copy-the-entire-contents-of-a-directory-in-c/58779#58779

            foreach (DirectoryInfo dir in source.GetDirectories())
                CopyFilesRecursively(dir, target.CreateSubdirectory(Rename(dir.Name)));
            foreach (FileInfo file in source.GetFiles())
                file.CopyTo(Path.Combine(target.FullName, Rename(file.Name)));
        }

        static string Rename(string name) {
            return toRename.ContainsKey(name) ? toRename[name] : name;
        }
    }
}