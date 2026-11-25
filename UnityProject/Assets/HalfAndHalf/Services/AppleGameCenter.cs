using System;
using System.Threading.Tasks;
#if UNITY_IOS && !UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Apple.GameKit;
using Apple.GameKit.Leaderboards;
using HalfAndHalf.Core;
#endif

namespace HalfAndHalf.Services
{
    // Native calls live here. Editor and unsupported platforms remain honest guest sessions.
    public sealed class AppleGameCenter : IGameCenter
    {
#if UNITY_IOS && !UNITY_EDITOR
        readonly bool enabled;
        readonly string overallBoard;
        readonly int overallCount;
        readonly Func<string,long,string,bool> submissionCheck;
        Task<GKLocalPlayer> authentication;
        public AppleGameCenter(bool enableOnline,string aggregateBoard=null,Func<string,long,string,bool> validateSubmission=null,int aggregateCount=29)
        {enabled=enableOnline;overallBoard=aggregateBoard;overallCount=aggregateCount;submissionCheck=validateSubmission;}
        public bool Supported => enabled;
        public string PlayerId=>Supported&&GKLocalPlayer.Local.IsAuthenticated?GKLocalPlayer.Local.GamePlayerId:"";
        public async Task<LoginResult> Authenticate()
        {
            if(!Supported)return new LoginResult{Outcome=LoginOutcome.Unsupported};
            try
            {
                if(GKLocalPlayer.Local.IsAuthenticated)return new LoginResult{Outcome=LoginOutcome.SignedIn,PlayerId=PlayerId};
                if(authentication==null || authentication.IsCompleted)authentication=GKLocalPlayer.Authenticate();
                var local=await Bounded(authentication);
                return new LoginResult{Outcome=local.IsAuthenticated?LoginOutcome.SignedIn:LoginOutcome.Failed,PlayerId=local.IsAuthenticated?local.GamePlayerId:""};
            }
            catch(GameKitException e){return new LoginResult{Outcome=e.GKErrorCode==GKErrorCode.Cancelled?LoginOutcome.Cancelled:LoginOutcome.Failed};}
            catch(Exception){return new LoginResult{Outcome=LoginOutcome.Failed};}
        }
        public async Task Submit(string boardId,long score,string expectedPlayerId)
        {
            if(!(boardId==overallBoard?RankingScore.TryDecodeOverall(score,out _,out _,overallCount):RankingScore.TryDecodeAttempt(score,out _,out _)))throw new ArgumentOutOfRangeException(nameof(score));
            CheckPlayer(expectedPlayerId);
            CheckSubmission(boardId,score,expectedPlayerId);
            using(var boards=await Bounded(GKLeaderboard.LoadLeaderboards(boardId)))
            using(var board=boards?.FirstOrDefault(b=>b.BaseLeaderboardId==boardId))
            {
                if(board==null)throw new InvalidOperationException("Configured Game Center board is unavailable.");
                CheckPlayer(expectedPlayerId);
                CheckSubmission(boardId,score,expectedPlayerId);
                await Bounded(board.SubmitScore(score,0,GKLocalPlayer.Local));
            }
        }
        public async Task<RankingPage> Load(string boardId)
        {
            string player=PlayerId;CheckPlayer(player);
            using(var boards=await Bounded(GKLeaderboard.LoadLeaderboards(boardId)))
            using(var board=boards?.FirstOrDefault(b=>b.BaseLeaderboardId==boardId))
            {
                if(board==null)throw new InvalidOperationException("Configured Game Center board is unavailable.");
                CheckPlayer(player);
                var response=await Bounded(board.LoadEntries(GKLeaderboard.PlayerScope.Global,GKLeaderboard.TimeScope.AllTime,1,10));
                using(response.Entries)
                using(response.LocalPlayerEntry)
                {
                    CheckPlayer(player);var entries=new List<RankingEntry>();
                    if(response.Entries!=null)foreach(var e in response.Entries)using(e){var entry=Convert(e,boardId==overallBoard);if(entry!=null)entries.Add(entry);}
                    var page=new RankingPage{Entries=entries.AsReadOnly(),LocalPlayer=Convert(response.LocalPlayerEntry,boardId==overallBoard),TotalPlayers=response.TotalPlayerCount};
                    var budget=System.Diagnostics.Stopwatch.StartNew();
                    return await SharedRanking.Normalize(page,async rank=>
                    {
                        CheckPlayer(player);
                        long remaining=30000-budget.ElapsedMilliseconds;
                        if(remaining<=0)throw new TimeoutException();
                        // Apple.GameKit accepts inclusive rankMin/rankMax, not a range length.
                        var sample=await Bounded(board.LoadEntries(GKLeaderboard.PlayerScope.Global,GKLeaderboard.TimeScope.AllTime,rank,rank),(int)remaining);
                        using(sample.Entries)
                        using(sample.LocalPlayerEntry)
                        {
                            CheckPlayer(player);SharedRanking.EnsureSnapshot(page,sample.TotalPlayerCount,Convert(sample.LocalPlayerEntry,boardId==overallBoard));
                            RankingEntry match=null;
                            if(sample.Entries!=null)foreach(var e in sample.Entries)using(e){if(e.Rank==rank)match=Convert(e,boardId==overallBoard);}
                            return match;
                        }
                    });
                }
            }
        }
        RankingEntry Convert(GKLeaderboard.Entry entry,bool overall)
        {
            if(entry==null || entry.Rank<1 || !(overall?RankingScore.TryDecodeOverall(entry.Score,out _,out _,overallCount):RankingScore.TryDecodeAttempt(entry.Score,out _,out _)))return null;
            using(var player=entry.Player)return new RankingEntry{Rank=entry.Rank,Score=entry.Score,PlayerName=player?.DisplayName??"Player"};
        }
        void CheckPlayer(string expected)
        {if(string.IsNullOrEmpty(expected)||PlayerId!=expected)throw new InvalidOperationException("Game Center player changed or signed out.");}
        void CheckSubmission(string board,long score,string player)
        {if(submissionCheck==null||!submissionCheck(board,score,player))throw new InvalidOperationException("Submission evidence is missing or inconsistent.");}
        // Never hang the gameplay UI indefinitely on a native callback. Late success is safe to retry on an ascending best-score board.
        static async Task<T> Bounded<T>(Task<T> operation,int timeoutMs=30000)
        {
            if(await Task.WhenAny(operation,Task.Delay(timeoutMs))!=operation){Observe(operation);throw new TimeoutException();}
            return await operation;
        }
        static async Task Bounded(Task operation)
        {
            if(await Task.WhenAny(operation,Task.Delay(30000))!=operation){Observe(operation);throw new TimeoutException();}
            await operation;
        }
        static void Observe(Task task){task.ContinueWith(t=>{var ignored=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);}
#else
        public AppleGameCenter(bool enableOnline,string aggregateBoard=null,Func<string,long,string,bool> validateSubmission=null,int aggregateCount=29) { }
        public bool Supported => false;
        public string PlayerId => "";
        public Task<LoginResult> Authenticate() => Task.FromResult(new LoginResult { Outcome = LoginOutcome.Unsupported });
        public Task Submit(string boardId,long score,string expectedPlayerId) => Task.FromException(new PlatformNotSupportedException());
        public Task<RankingPage> Load(string boardId) => Task.FromResult<RankingPage>(null);
#endif
    }
}
