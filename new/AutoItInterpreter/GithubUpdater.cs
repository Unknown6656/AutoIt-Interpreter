using System.Threading.Tasks;

using Octokit;

namespace Unknown6656.AutoIt3
{
    public static class GithubUpdater
    {
        public static async Task<bool?> TryCheckForUpdatesAsync(Telemetry telemetry) =>
            await telemetry.MeasureAsync<bool?>(TelemetryCategory.GithubUpdater, async delegate
            {
                try
                {
                    GitHubClient client = new GitHubClient(new ProductHeaderValue(__module__.Author));
                    Release? release = await client.Repository.Release.GetLatest(__module__.Author, __module__.RepositoryName);

                    return release is Release && (release.PublishedAt ?? release.CreatedAt) > __module__.DateBuilt;
                }
                catch
                {
                    return null;
                }
            });

        // TODO : auto fetch / install update ?
    }
}
