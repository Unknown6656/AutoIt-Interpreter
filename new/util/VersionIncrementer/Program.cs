using System.Diagnostics;
using System.Linq;
using System.IO;
using System;

namespace VersionIncrementer;


public static class Program
{
    public const string GITHUB_APPVEYOR_AUTH_TOKEN = ""; // <-- insert your GitHub AppVeyor auth token here
    public const string REPOSITORY_AUTHOR = "Unknown6656";
    public const string REPOSITORY_NAME = "AutoIt-Interpreter";
    public const string REPOSITORY_URL = $"https://github.com/{REPOSITORY_AUTHOR}/{REPOSITORY_NAME}";
    private const string APPVEYOR_PR_TEMPLATE = @"{{#passed}}:white_check_mark:{{/passed}}{{#failed}}:x:{{/failed}} [Build {{&projectName}} {{buildVersion}} {{status}}]({{buildUrl}}) (commit {{commitUrl}} by @{{&commitAuthorUsername}})";
    public const int START_YEAR = 2018;


    public static FileInfo? GetSLN(FileInfo csproj)
    {
        DirectoryInfo? dir = csproj.Directory;
        string relative;

        while (dir is { })
        {
            relative = Path.GetRelativePath(dir.FullName, csproj.FullName);

            foreach (FileInfo sln in dir.GetFiles("*.sln", SearchOption.TopDirectoryOnly))
                try
                {
                    using FileStream fs = sln.OpenRead();
                    using StreamReader sr = new(fs);

                    string content = sr.ReadToEnd();

                    if (content.Contains(relative, StringComparison.OrdinalIgnoreCase))
                        return sln;
                }
                catch
                {
                }

            dir = dir.Parent;
        }

        return null;
    }

    public static DirectoryInfo? GetRepoRootDir(FileInfo csproj)
    {
        DirectoryInfo? dir = csproj.Directory;

        while (dir is { } && !dir.GetDirectories().Any(d => d.Name == ".git"))
            dir = dir.Parent;

        return dir;
    }

    public static void Main(string[] args)
    {
        FileInfo path_csproj = new(args[0]);

        if (!path_csproj.Exists)
            throw new FileNotFoundException("The specified .csproj file does not exist.", path_csproj.FullName);

        FileInfo? path_sln = GetSLN(path_csproj);

        if (path_sln is null)
            throw new FileNotFoundException("The .csproj file does not belong to any .sln file.", path_csproj.FullName);

        DirectoryInfo dir_project = path_csproj.Directory!;
        DirectoryInfo dir_solution = path_sln.Directory!;
        DirectoryInfo dir_reporoot = GetRepoRootDir(path_csproj)!;

        string metapath = Path.Combine(dir_project.FullName, "AssemblyInfo.cs");
        string verspath = Path.Combine(dir_project.FullName, "version.txt");
        string appveyorpath = Path.Combine(dir_reporoot.FullName, "appveyor.yml");
        string githash = "<unknown>";
        string vers = "0.0.0.0";

        if (File.Exists(verspath))
            vers = File.ReadAllText(verspath).Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();

        if (!Version.TryParse(vers, out Version? version_curr))
            version_curr = new Version(0, 0, 0, 0);

        DateTime now = DateTime.Now;
        Version version_next = new(version_curr.Major, version_curr.Minor, version_curr.Build + 1, (now.Year - 2000) * 356 + now.DayOfYear);

        try
        {
            using Process p = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "rev-parse HEAD",
                    WorkingDirectory = dir_reporoot.FullName,
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

        File.WriteAllText(verspath, $"{version_next}\n{githash}");
        File.WriteAllText(metapath, $$"""

        //////////////////////////////////////////////////////////////////////////
        // Auto-generated {{now:yyyy-MM-dd HH:mm:ss.fff}}                               //
        // ANY CHANGES TO THIS DOCUMENT WILL BE LOST UPON RE-GENERATION         //
        //////////////////////////////////////////////////////////////////////////

        using System.Reflection;
        using System;

        [assembly: AssemblyVersion("{{version_next}}")]
        [assembly: AssemblyFileVersion("{{version_next}}")]
        [assembly: AssemblyInformationalVersion("v.{{version_next}}, commit: {{githash}}")]
        [assembly: AssemblyCompany("{{REPOSITORY_AUTHOR}}")]
        [assembly: AssemblyCopyright("{{copyright}}")]
        [assembly: AssemblyProduct("{{REPOSITORY_NAME}} by {{REPOSITORY_AUTHOR}}")]
        [assembly: AssemblyTitle("autoit3")]

        /// <summary>
        /// A global module containing some meta-data.
        /// </summary>
        public static class __module__
        {
            /// <summary>
            /// The interpreter's author. This value is equal to the author of the GitHub repository associated with <see cref="RepositoryURL"/>.
            /// </summary>
            public const string Author = "{{REPOSITORY_AUTHOR}}";
            /// <summary>
            /// Development year(s).
            /// </summary>
            public const string Year = "{{year}}";
            /// <summary>
            /// The interpreter's copyright information.
            /// </summary>
            public const string Copyright = "{{copyright}}";
            /// <summary>
            /// The interpreter's current version.
            /// </summary>
            public static Version? InterpreterVersion { get; } = Version.Parse("{{version_next}}");
            /// <summary>
            /// The Git hash associated with the current build.
            /// </summary>
            public const string GitHash = "{{githash}}";
            /// <summary>
            /// The name of the GitHub repository associated with <see cref="RepositoryURL"/>.
            /// </summary>
            public const string RepositoryName = "{{REPOSITORY_NAME}}";
            /// <summary>
            /// The URL of this project's GitHub repository.
            /// </summary>
            public const string RepositoryURL = "{{REPOSITORY_URL}}";
            /// <summary>
            /// The date and time of the current build ({{now:yyyy-MM-dd HH:mm:ss.fff}}).
            /// </summary>
            public static DateTime DateBuilt { get; } = DateTime.FromFileTimeUtc(0x{{now.ToFileTimeUtc():x16}}L);
        }

        """);
        File.WriteAllText(appveyorpath, $"""
        ################################################################
        # Auto-generated {now:yyyy-MM-dd HH:mm:ss.fff}                       #
        # ANY CHANGES TO THIS DOCUMENT WILL BE LOST UPON RE-GENERATION #
        ################################################################
        #
        # git commit: {githash}
        version: {version_next}
        image: Visual Studio 2022
        configuration: Release
        before_build:
        - cmd: nuget restore "{Path.GetRelativePath(dir_reporoot.FullName, path_sln.FullName).Replace('\\', '/')}"
        build:
            project: "{Path.GetRelativePath(dir_reporoot.FullName, path_sln.FullName).Replace('\\', '/')}"
            verbosity: minimal
        notifications:
        - provider: GitHubPullRequest
          # auth_token:
          #   secure: "{GITHUB_APPVEYOR_AUTH_TOKEN}"
          template: "{APPVEYOR_PR_TEMPLATE}"
        """);
    }
}
