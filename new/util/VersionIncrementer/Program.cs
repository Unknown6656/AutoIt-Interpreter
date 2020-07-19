using System.IO;
using System;
using System.Diagnostics;

namespace VersionIncrementer
{
    public static class Program
    {
        public const string AUTHOR = "Unknown6656";
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
            string copyright = $"Copyright © {year}, {AUTHOR}";

            File.WriteAllText(verspath, $"{v2}\n{githash}");
            File.WriteAllText(metapath, $@"
//////////////////////////////////////////////////////////////////////////
// Auto-generated {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}                               //
// ANY CHANGES TO THIS DOCUMENT WILL BE LOST UPON RE-GENERATION         //
//////////////////////////////////////////////////////////////////////////

using System.Reflection;
using System;

[assembly: AssemblyVersion(""{v2}"")]
[assembly: AssemblyFileVersion(""{v2}"")]
[assembly: AssemblyInformationalVersion(""{githash}"")]
[assembly: AssemblyCompany(""{AUTHOR}"")]
[assembly: AssemblyCopyright(""{copyright}"")]
[assembly: AssemblyProduct(""AutoIt3 Interpreter by {AUTHOR}"")]
[assembly: AssemblyTitle(""autoit3"")]

/// <summary>
/// A global module containing some meta-data.
/// </summary>
public static class __module__
{{
    /// <summary>
    /// The interpreter's author.
    /// </summary>
    public static string Author {{ get; }} = ""{AUTHOR}"";
    /// <summary>
    /// Development year(s).
    /// </summary>
    public static string Year {{ get; }} = ""{year}"";
    /// <summary>
    /// The interpreter's copyright information.
    /// </summary>
    public static string Copyright {{ get; }} = ""{copyright}"";
    /// <summary>
    /// The interpreter's current version.
    /// </summary>
    public static Version? InterpreterVersion {{ get; }} = Version.Parse(""{v2}"");
    /// <summary>
    /// The Git hash associated with the current build.
    /// </summary>
    public static string GitHash {{ get; }} = ""{githash}"";
}}
");
        }
    }
}
