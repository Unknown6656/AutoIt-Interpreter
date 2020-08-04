using System.IO;
using System;
using System.Diagnostics;

namespace VersionIncrementer
{
    public static class Program
    {
        public const string REPOSITORY_AUTHOR = "Unknown6656";
        public const string REPOSITORY_NAME = "AutoIt-Interpreter";
        public const string REPOSITORY_URL = "https://github.com/" + REPOSITORY_AUTHOR + "/" + REPOSITORY_NAME;
        public const int START_YEAR = 2018;


        public static void Main(string[] args)
        {
            string metapath = args[0] + "/../AssemblyInfo.cs";
            string verspath = args[0] + "/../version.txt";
            string githash = "<unknown>";
            string vers = "0.0.0.0";

            if (File.Exists(verspath))
                vers = File.ReadAllText(verspath).Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();

            if (!Version.TryParse(vers, out Version v1))
                v1 = new Version(0, 0, 0, 0);

            DateTime now = DateTime.Now;
            Version v2 = new Version(v1.Major, v1.Minor, v1.Build + 1, (now.Year - 2000) * 356 + now.DayOfYear);

            try
            {
                using Process p = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "git",
                        Arguments = "rev-parse HEAD",
                        WorkingDirectory = new FileInfo(verspath).Directory.FullName,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                p.Start();

                githash = p.StandardOutput.ReadToEnd().Trim();

                p.WaitForExit();
            }
            catch
            {
            }

            if (string.IsNullOrWhiteSpace(githash))
                githash = "<unknown git commit hash>";

            string year = START_YEAR < now.Year ? $"{START_YEAR} - {now.Year}" : START_YEAR.ToString();
            string copyright = $"Copyright © {year}, {REPOSITORY_AUTHOR}";

            File.WriteAllText(verspath, $"{v2}\n{githash}");
            File.WriteAllText(metapath, $@"
//////////////////////////////////////////////////////////////////////////
// Auto-generated {now:yyyy-MM-dd HH:mm:ss.fff}                               //
// ANY CHANGES TO THIS DOCUMENT WILL BE LOST UPON RE-GENERATION         //
//////////////////////////////////////////////////////////////////////////

using System.Reflection;
using System;

[assembly: AssemblyVersion(""{v2}"")]
[assembly: AssemblyFileVersion(""{v2}"")]
[assembly: AssemblyInformationalVersion(""v.{v2}, commit: {githash}"")]
[assembly: AssemblyCompany(""{REPOSITORY_AUTHOR}"")]
[assembly: AssemblyCopyright(""{copyright}"")]
[assembly: AssemblyProduct(""{REPOSITORY_NAME} by {REPOSITORY_AUTHOR}"")]
[assembly: AssemblyTitle(""autoit3"")]

/// <summary>
/// A global module containing some meta-data.
/// </summary>
public static class __module__
{{
    /// <summary>
    /// The interpreter's author. This value is equal to the author of the GitHub repository associated with <see cref=""RepositoryURL""/>.
    /// </summary>
    public const string Author = ""{REPOSITORY_AUTHOR}"";
    /// <summary>
    /// Development year(s).
    /// </summary>
    public const string Year = ""{year}"";
    /// <summary>
    /// The interpreter's copyright information.
    /// </summary>
    public const string Copyright = ""{copyright}"";
    /// <summary>
    /// The interpreter's current version.
    /// </summary>
    public static Version? InterpreterVersion {{ get; }} = Version.Parse(""{v2}"");
    /// <summary>
    /// The Git hash associated with the current build.
    /// </summary>
    public const string GitHash = ""{githash}"";
    /// <summary>
    /// The name of the GitHub repository associated with <see cref=""RepositoryURL""/>.
    /// </summary>
    public const string RepositoryName = ""{REPOSITORY_NAME}"";
    /// <summary>
    /// The URL of this project's GitHub repository.
    /// </summary>
    public const string RepositoryURL = ""{REPOSITORY_URL}"";
    /// <summary>
    /// The date and time of the current build ({now:yyyy-MM-dd HH:mm:ss.fff}).
    /// </summary>
    public static DateTime DateBuilt {{ get; }} = DateTime.FromFileTimeUtc(0x{now.ToFileTimeUtc():x16}L);
}}
");
        }
    }
}
