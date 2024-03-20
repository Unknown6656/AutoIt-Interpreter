using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.IO.Compression;
using System.IO;
using System;

using Octokit;

using Unknown6656.Mathematics.Cryptography;
using Unknown6656.AutoIt3.CLI;
using Unknown6656.Generics;
using Unknown6656.IO;

namespace Unknown6656.AutoIt3;


/// <summary>
/// An enumeration of known GitHub updater modes.
/// </summary>
public enum GithubUpdaterMode
{
    /// <summary>
    /// Indicates that the newest version should only be fetched from GitHub if it is marked as "release".
    /// </summary>
    ReleaseOnly,
    /// <summary>
    /// Indicates that the newest version should be fetched from GitHub, even if it is marked as "beta".
    /// </summary>
    IncludeBetaVersions,
}

/// <summary>
/// A class managing software updates by fetching the newest releases from GitHub.
/// </summary>
public sealed class GithubUpdater
{
    private Release[] _releases;


    /// <summary>
    /// The underlying <see cref="Telemetry"/> module associated with this updater.
    /// </summary>
    public Telemetry Telemetry { get; }

    /// <summary>
    /// The underlying <see cref="GitHubClient"/> associated with this updater.
    /// </summary>
    public GitHubClient Client { get; }

    /// <summary>
    /// The GitHub repository author name associated with this software.
    /// </summary>
    public string RepositoryAuthor { get; }

    /// <summary>
    /// The GitHub repository name associated with this software.
    /// </summary>
    public string RepositoryName { get; }

    /// <summary>
    /// The <see cref="GithubUpdaterMode"/> associated with this instance of the GitHub software updater.
    /// </summary>
    public GithubUpdaterMode UpdaterMode { get; set; }

    /// <summary>
    /// Returns whether any updates are available.
    /// <para/>
    /// This property requires <see cref="FetchReleaseInformationAsync"/> to have been run previously, in order to return any meaningful results.
    /// </summary>
    public bool UpdatesAvailable => _releases.Length > 0;

    /// <summary>
    /// Returns the latest available release version.
    /// <para/>
    /// This property requires <see cref="FetchReleaseInformationAsync"/> to have been run previously, in order to return any meaningful results.
    /// </summary>
    public Release? LatestReleaseAvailable => _releases.FirstOrDefault();


    /// <summary>
    /// Creates a new instance of the GitHub software updater.
    /// </summary>
    /// <param name="telemetry">Reference to the telemetry instance for this application.</param>
    internal GithubUpdater(Telemetry telemetry)
        : this(telemetry, __module__.Author, __module__.RepositoryName)
    {
    }

    /// <summary>
    /// Creates a new instance of the GitHub software updater.
    /// </summary>
    /// <param name="telemetry">Reference to the telemetry instance for this application.</param>
    /// <param name="repo_author">Name of the GitHub repository author/owner/organization (user handle, not canonical name).</param>
    /// <param name="repo_name">Name of the GitHub repository (URN name, not canonical name).</param>
    public GithubUpdater(Telemetry telemetry, string repo_author, string repo_name)
    {
        Client = new GitHubClient(new ProductHeaderValue($"{repo_author}.{repo_name}", __module__.InterpreterVersion?.ToString()));
        RepositoryAuthor = repo_author;
        RepositoryName = repo_name;
        Telemetry = telemetry;
        _releases = [];
    }

    /// <summary>
    /// Fetches the newest release information from the GitHub repo.
    /// </summary>
    /// <returns>
    /// Returns whether the the information could be fetched successfully.
    /// <para/>
    /// <i>NOTE: This does <b>NOT</b> indicate whether an update is available!</i>.
    /// </returns>
    public async Task<bool> FetchReleaseInformationAsync() => await Telemetry.MeasureAsync(TelemetryCategory.GithubUpdater, async delegate
    {
        try
        {
            MainProgram.PrintfDebugMessage("debug.update.searching");

            _releases = (from release in await Client.Repository.Release.GetAll(RepositoryAuthor, RepositoryName).ConfigureAwait(true)
                         let date = release.PublishedAt ?? release.CreatedAt
                         where !release.Draft
                         where date > __module__.DateBuilt
                         where UpdaterMode is GithubUpdaterMode.IncludeBetaVersions || !release.Prerelease
                         where release.Assets.Count > 0
                         orderby date descending
                         select release).ToArray();

            if (UpdatesAvailable)
                MainProgram.PrintfDebugMessage("debug.update.new_releases", _releases.Length, _releases.Select(r => $"\n\t- 0x{r.Id:x8}: {r.Name} ({r.PublishedAt})").StringConcat());
            else
                MainProgram.PrintfDebugMessage("debug.update.no_releases");

            return true;
        }
        catch
        {
            return false;
        }
    }).ConfigureAwait(true);

    /// <summary>
    /// Tries to update the software to the latest GitHub release.
    /// </summary>
    /// <returns>Returns whether the the information could be fetched successfully <b>and</b> whether the update could be successfully performed.</returns>
    public async Task<bool> TryUpdateToLatestAsync() => LatestReleaseAvailable is Release latest && await TryUpdateTo(latest).ConfigureAwait(true);

    /// <summary>
    /// Tries to update the software to the given GitHub release.
    /// </summary>
    /// <param name="release">Release version, to which the software should be updated.</param>
    /// <returns>Returns whether the the information could be fetched successfully <b>and</b> whether the update could be successfully performed.</returns>
    public async Task<bool> TryUpdateTo(Release release) => await Telemetry.MeasureAsync(TelemetryCategory.GithubUpdater, async delegate
    {
        try
        {
            MainProgram.PrintfDebugMessage("debug.update.updating", release.Id, release.Name, release.PublishedAt);

            string prefix = $"update-{release.TagName.Select(c => char.IsLetterOrDigit(c) ? c : '-').StringConcat()}--";
            FileInfo download_target = new($"{MainProgram.ASM_DIR.FullName}/{prefix}downloaded_asset.zip");
            IReadOnlyList<ReleaseAsset> assets = release.Assets;
            ReleaseAsset? asset = assets[0];

            if (assets.Count > 1)
                asset = assets.FirstOrDefault(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && a.Name.Contains(release.TagName, StringComparison.OrdinalIgnoreCase));

            if (asset is null)
                return false;

            MainProgram.PrintfDebugMessage("debug.update.downloading", asset, asset.BrowserDownloadUrl, asset.Size / 1048576d);

            using WebClient wc = new();

            await wc.DownloadFileTaskAsync(asset.BrowserDownloadUrl, download_target.FullName);

            using FileStream fs = download_target.OpenRead();
            using ZipArchive zip = new(fs, ZipArchiveMode.Read, false);

            MainProgram.PrintfDebugMessage("debug.update.extracting", download_target);

            foreach (ZipArchiveEntry entry in zip.Entries)
            {
                FileInfo path = new(MainProgram.ASM_DIR + "/" + entry.FullName);

                if (entry.FullName[^1] is '/' or '\\')
                {
                    if (!Directory.Exists(path.FullName))
                        Directory.CreateDirectory(path.FullName);

                    continue;
                }

                if (path.Exists)
                {
                    uint crc32 = DataStream.FromFile(path.FullName).Hash<CRC32Hash>().ToUnmanaged<uint>();

                    if (crc32 == entry.Crc32)
                    {
                        MainProgram.PrintfDebugMessage("debug.update.skipping", path);

                        continue;
                    }
                    else
                    {
                        MainProgram.PrintfDebugMessage("debug.update.replacing", path, crc32, entry.Crc32);

                        File.Move(path.FullName, MainProgram.ASM_DIR + "/" + prefix + path.Name, true);
                    }
                }
                else
                    MainProgram.PrintfDebugMessage("debug.update.creating", path);

                entry.ExtractToFile(path.FullName, false);
            }

            MainProgram.PrintfDebugMessage("debug.update.finished_extraction");

            zip.Dispose();
            fs.Close();

            await fs.DisposeAsync().ConfigureAwait(true);

            MainProgram.PrintfDebugMessage("debug.update.starting_updater");

            ProcessStartInfo psi = new()
            {
                FileName = "dotnet",
                UseShellExecute = true,
            };

            foreach (object obj in new object[]
            {
                MainProgram.UPDATER.FullName,
                true,
                MainProgram.ASM_DIR.FullName,
                prefix,
                Environment.ProcessId,
                MainProgram.ASM_FILE.FullName,
            }.Concat(MainProgram.RawCMDLineArguments))
            {
                string arg = obj as string ?? obj?.ToString() ?? "";

                psi.ArgumentList.Add(arg);
            }

            using Process? process = Process.Start(psi);

            Environment.Exit(0);

            return true;
        }
        catch
        {
        }

        return false;
    }).ConfigureAwait(true);
}
