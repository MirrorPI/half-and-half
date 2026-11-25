using System.Collections.Generic;
using System.Threading.Tasks;

namespace HalfAndHalf.Services
{
    public enum LoginOutcome { SignedIn, Cancelled, Failed, Unsupported }
    public sealed class LoginResult
    {
        public LoginOutcome Outcome;
        public string PlayerId;
    }
    [System.Serializable]
    public sealed class RankingEntry
    {
        public long Rank,Score;
        public string PlayerName;
    }
    public sealed class RankingPage
    {
        public IReadOnlyList<RankingEntry> Entries;
        public RankingEntry LocalPlayer;
        public long TotalPlayers;
    }
    public interface IGameCenter
    {
        bool Supported { get; }
        string PlayerId { get; }
        Task<LoginResult> Authenticate();
        Task Submit(string leaderboardId,long score,string expectedPlayerId);
        Task<RankingPage> Load(string leaderboardId);
    }
    internal interface IRankingSessionReset
    {
        void EndSession();
    }
    [System.Serializable]
    public sealed class QueuedScore
    {
        public string objectId,contentVersion,playerId;
        public long score;
        public bool pending;
        public SubmissionAttempt[] evidence;
        public string heldReason;
        public bool needsEvidence;
    }
}
