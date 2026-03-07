

using Services.AIViewModel;

namespace Services.Interaces
{
    public interface IUserTrajectoryService
    {
        /// <summary>
        /// Analyzes the wellness trajectory for a user.
        /// Uses cached results when available; calls Claude API only when
        /// new responses exist since the last analysis.
        /// </summary>
        /// <param name="userEmail">The user's email to look up responses</param>
        /// <returns>Complete trajectory analysis</returns>
        Task<UserTrajectoryAnalysis> GetOrAnalyzeTrajectoryAsync(string userEmail);

        /// <summary>
        /// Forces a fresh analysis regardless of cache state.
        /// Useful when admin wants to re-analyze.
        /// </summary>
        Task<UserTrajectoryAnalysis> ForceReanalyzeTrajectoryAsync(string userEmail);

        /// <summary>
        /// Checks if a cached analysis exists and whether it's stale
        /// (i.e., new responses exist since last analysis).
        /// </summary>
        Task<(bool HasCache, bool IsStale, int CachedCount, int CurrentCount)> CheckCacheStatusAsync(string userEmail);
    }
}