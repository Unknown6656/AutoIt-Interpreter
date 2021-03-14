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
using Unknown6656.Common;
using Unknown6656.IO;

namespace Unknown6656.AutoIt3
{
    public enum GithubUpdaterMode
    {
        ReleaseOnly,
        IncludeBetaVersions,
    }

    public sealed class GithubUpdater
    {
        private Release[] _releases;


        public Telemetry Telemetry { get; }

        public GitHubClient Client { get; }

        public string RepositoryAuthor { get; }

        public string RepositoryName { get; }

        public GithubUpdaterMode UpdaterMode { get; set; }

        public bool UpdatesAvailable => _releases.Length > 0;

        public Release? LatestReleaseAvailable => _releases.FirstOrDefault();


        public GithubUpdater(Telemetry telemetry)
            : this(telemetry, __module__.Author, __module__.RepositoryName)
        {
        }

        public GithubUpdater(Telemetry telemetry, string repo_author, string repo_name)
        {
            Client = new GitHubClient(new ProductHeaderValue($"{__module__.Author}.{__module__.RepositoryName}", __module__.InterpreterVersion?.ToString()));
            RepositoryAuthor = repo_author;
            RepositoryName = repo_name;
            Telemetry = telemetry;
            _releases = Array.Empty<Release>();
        }

        public async Task<bool> FetchReleaseInformationAsync() => await Telemetry.MeasureAsync(TelemetryCategory.GithubUpdater, async delegate
        {
            try
            {
                MainProgram.PrintfDebugMessage("debug.update.searching");

                _releases = (from release in await Client.Repository.Release.GetAll(__module__.Author, __module__.RepositoryName).ConfigureAwait(true)
                             let date = release.PublishedAt ?? release.CreatedAt
                             where !release.Draft
                             where date > __module__.DateBuilt
                             where UpdaterMode is GithubUpdaterMode.IncludeBetaVersions || !release.Prerelease
                             where release.Assets.Count > 0
                             orderby date descending
                             select release).ToArray();

                if (_releases.Length == 0)
                    MainProgram.PrintfDebugMessage("debug.update.no_releases");
                else
                    MainProgram.PrintfDebugMessage("debug.update.new_releases", _releases.Length, _releases.Select(r => $"\n\t- 0x{r.Id:x8}: {r.Name} ({r.PublishedAt})").StringConcat());

                return true;
            }
            catch
            {
                return false;
            }
        }).ConfigureAwait(true);

        public async Task<bool> TryUpdateToLatestAsync()
        {
            if (LatestReleaseAvailable is Release latest)
                return await TryUpdateTo(latest).ConfigureAwait(true);

            return false;
        }

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
                    asset = assets.FirstOrDefault(a => a.Name.EndsWith(".zip", StringComparison.InvariantCultureIgnoreCase) && a.Name.Contains(release.TagName, StringComparison.InvariantCultureIgnoreCase));

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
                        uint crc32 = From.File(path.FullName).Hash<CRC32Hash>().ToUnmanaged<uint>();

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
}
