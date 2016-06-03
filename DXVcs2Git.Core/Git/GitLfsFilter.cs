using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Text.RegularExpressions;
using LibGit2Sharp;

namespace DXVcs2Git.Core.Git {
    public interface IObservableProcess : IObservable<Unit> {
        IObservable<string> CombinedOutput { get; }
        IObservable<string> Output { get; }
        IObservable<string> Error { get; }
        IObserver<string> Input { get; }
    }

    public interface IPipableObservableProcess : IObservable<Unit> {
        Stream StandardInput { get; }
        Stream StandardOutput { get; }
    }

    public interface IGitShell {
        IObservableProcess Execute(string arguments, string workingDirectory, bool throwOnNonZeroExitCode);
        IPipableObservableProcess ExecutePipedProcess(string arguments, string workingDirectory, bool throwOnNonZeroExitCode);
    }

    public class GitLfsFilter : Filter {
        static readonly FilterAttributeEntry FilterAttribute = new FilterAttributeEntry("lfs");
        static readonly string FilterName = "git-lfs-filter";
        IGitShell Shell { get; }
        public GitLfsFilter(IGitShell shell) : base(FilterName, new List<FilterAttributeEntry> { FilterAttribute }) {
            Shell = shell;
        }
        protected override void Clean(string path, string repositoryWorkingDirectory, Stream input, Stream output) {
            CommandArguments command = new CommandArguments();
            command.AddArg("lfs");
            command.AddArg("clean");
            this.ExecuteMediaFilter(command, repositoryWorkingDirectory, input, output);
        }
        protected override void Smudge(string path, string repositoryWorkingDirectory, Stream input, Stream output) {
            CommandArguments command = new CommandArguments();
            command.AddArg("lfs");
            command.AddArg("smudge");
            this.ExecuteMediaFilter(command, repositoryWorkingDirectory, input, output);
        }
        int ExecuteMediaFilter(CommandArguments command, string workingDirectory, Stream inputBufferReader, Stream outputBufferWriter) {
            IPipableObservableProcess This = Shell.ExecutePipedProcess(command.ToString(), workingDirectory, true);
            IObservable<Unit> inUnit = WriteGitBufInputToStandardIn(This.StandardInput, inputBufferReader);
            IObservable<Unit> outUnit = WriteStandardOutToGitBufOutput(This.StandardOutput, outputBufferWriter);
            This.SelectUnit().Merge(inUnit).Merge(outUnit).Wait();
            return 0;
        }
        static IObservable<Unit> WriteStandardOutToGitBufOutput(Stream streamReader, Stream inputStream) {
            return Observable.Defer(() => Observable.Start(() => streamReader.CopyTo(inputStream)));
        }
        static IObservable<Unit> WriteGitBufInputToStandardIn(Stream streamWriter, Stream inputStream) {
            return Observable.Defer(() => Observable.Start(() => {
                try {
                    inputStream.CopyTo(streamWriter);
                    streamWriter.Close();
                }
                catch (Exception ex) {
                    if (ex.IsCriticalException())
                        throw;
                    Log.Error("Error occurred while trying to write memory stream to the standard input", ex);
                }
            }));
        }
        static IObservable<Unit> TryReparseProcessException(IPipableObservableProcess pipable) {
            return pipable.Catch((Func<ProcessException, IObservable<Unit>>)(ex => {
                Exception exception = ex;
                if (exception == null)
                    return Observable.Throw<Unit>((Exception)ex);
                return Observable.Throw<Unit>(exception);
            }));
        }
    }
    public static class StringExtensions {
        public static bool Contains(this string s, string expectedSubstring, StringComparison comparison) {
            return s.IndexOf(expectedSubstring, comparison) > -1;
        }

        public static string Humanize(this string s) {
            if (string.IsNullOrWhiteSpace(s))
                return s;
            MatchCollection matchCollection = Regex.Matches(s, "[a-zA-Z\\d]{1,}", RegexOptions.None);
            if (matchCollection.Count == 0)
                return s;
            string str = string.Join(" ", Enumerable.Select<Match, string>(Enumerable.Cast<Match>((IEnumerable)matchCollection), (Func<Match, string>)(match => match.Value.ToLower(CultureInfo.InvariantCulture))));
            return (string)(object)char.ToUpper(str[0], CultureInfo.InvariantCulture) + (object)str.Substring(1);
        }

        public static bool ContainsAny(this string s, IEnumerable<char> characters) {
            return s.IndexOfAny(Enumerable.ToArray<char>(characters)) > -1;
        }

        public static string DebugRepresentation(this string s) {
            s = s ?? "(null)";
            return string.Format((IFormatProvider)CultureInfo.InvariantCulture, "\"{0}\"", new object[1]
            {
        (object) s
            });
        }

        public static bool IsNotNullOrEmpty(this string s) {
            return !string.IsNullOrEmpty(s);
        }

        public static bool IsNullOrEmpty(this string s) {
            return string.IsNullOrEmpty(s);
        }

        public static string ToNullIfEmpty(this string s) {
            if (!StringExtensions.IsNullOrEmpty(s))
                return s;
            return (string)null;
        }

        public static bool StartsWith(this string s, char c) {
            if (string.IsNullOrEmpty(s))
                return false;
            return (int)Enumerable.First<char>((IEnumerable<char>)s) == (int)c;
        }

        public static string RightAfter(this string s, string search) {
            if (s == null)
                return (string)null;
            int num = s.IndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (num < 0)
                return (string)null;
            return s.Substring(num + search.Length);
        }

        public static string RightAfterLast(this string s, string search) {
            if (s == null)
                return (string)null;
            int num = s.LastIndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (num < 0)
                return (string)null;
            return s.Substring(num + search.Length);
        }

        public static string LeftBeforeLast(this string s, string search) {
            if (s == null)
                return (string)null;
            int length = s.LastIndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (length < 0)
                return (string)null;
            return s.Substring(0, length);
        }

        public static string ParseFileName(this string path) {
            if (path == null)
                return (string)null;
            return StringExtensions.RightAfterLast(path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar), string.Concat((object)Path.DirectorySeparatorChar));
        }

        public static string ParseParentDirectory(this string path) {
            if (path == null)
                return (string)null;
            return StringExtensions.LeftBeforeLast(path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar), string.Concat((object)Path.DirectorySeparatorChar));
        }

        public static string EnsureStartsWith(this string s, char c) {
            if (s == null)
                return (string)null;
            return (string)(object)c + (object)s.TrimStart(c);
        }

        public static string EnsureEndsWith(this string s, char c) {
            if (s == null)
                return (string)null;
            return s.TrimEnd(c) + (object)c;
        }

        public static string NormalizePath(this string path) {
            if (string.IsNullOrEmpty(path))
                return (string)null;
            return path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        }

        public static string TrimEnd(this string s, string suffix) {
            if (s == null)
                return (string)null;
            if (!s.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return s;
            return s.Substring(0, s.Length - suffix.Length);
        }

        public static string RemoveSurroundingQuotes(this string s) {
            if (s.Length < 2)
                return s;
            char[] chArray = new char[2]
            {
        '"',
        '\''
            };
            char ch = s[0];
            if (!Enumerable.Contains<char>((IEnumerable<char>)chArray, ch) || (int)ch != (int)s[s.Length - 1])
                return s;
            return s.Substring(1, s.Length - 2);
        }
        public static int ToInt32(this string s) {
            int result;
            if (!int.TryParse(s, out result))
                return 0;
            return result;
        }

        public static string Wrap(this string text, int maxLength = 72) {
            if (text == null)
                throw new ArgumentNullException("text");
            if (text.Length == 0)
                return string.Empty;
            StringBuilder stringBuilder1 = new StringBuilder();
            string str1 = text;
            string[] separator = new string[1]
            {
        Environment.NewLine
            };
            int num1 = 0;
            foreach (string str2 in str1.Split(separator, (StringSplitOptions)num1)) {
                StringBuilder stringBuilder2 = new StringBuilder();
                string str3 = str2;
                char[] chArray = new char[1]
                {
          ' '
                };
                foreach (string str4 in str3.Split(chArray)) {
                    bool flag = stringBuilder2.Length > 0;
                    int num2 = (flag ? 1 : 0) + str4.Length;
                    if (stringBuilder2.Length + num2 > maxLength) {
                        stringBuilder1.AppendLine(stringBuilder2.ToString());
                        stringBuilder2.Clear();
                        flag = false;
                    }
                    if (flag)
                        stringBuilder2.Append(" ");
                    stringBuilder2.Append(str4);
                }
                stringBuilder1.AppendLine(stringBuilder2.ToString());
            }
            return stringBuilder1.ToString();
        }

        public static Uri ToUriSafe(this string url) {
            Uri result;
            Uri.TryCreate(url, UriKind.Absolute, out result);
            return result;
        }

        public static bool StartsWithOrdinal(this string s, string value) {
            return s.StartsWith(value, StringComparison.Ordinal);
        }

        public static bool EndsWithOrdinal(this string s, string value) {
            return s.EndsWith(value, StringComparison.Ordinal);
        }

        public static int MaxIndex(this string text) {
            return text.Length - 1;
        }
    }
}
