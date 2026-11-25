#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.Threading.Tasks;
using HalfAndHalf.Core;
using UnityEngine;
using UnityEngine.Scripting;

namespace HalfAndHalf.Services
{
    sealed class GooglePlayRankings : IGameCenter, IRankingSessionReset
    {
        const string Bridge = "com.halfandhalf.ranking.GoogleRankBridge";
        readonly string project;
        readonly Func<string,long,string,bool> validSubmission;
        public bool Supported => true;
        public string PlayerId { get; private set; } = "";
        int session;
        bool checking;

        public GooglePlayRankings(string projectId, Func<string,long,string,bool> validSubmission)
        { project = projectId; this.validSubmission = validSubmission; }

        [Serializable] sealed class ResponsePage
        {
            public RankingEntry[] entries = null;
            public RankingEntry local = null;
        }

        sealed class Callback : AndroidJavaProxy
        {
            public readonly TaskCompletionSource<string> Result = new TaskCompletionSource<string>();
            public Callback() : base(Bridge + "$Callback") {}
            [Preserve] public void complete(string value, string error)
            {
                if (string.IsNullOrEmpty(error)) Result.TrySetResult(value);
                else Result.TrySetException(new InvalidOperationException(error));
            }
        }

        async Task<string> Request(string operation, string board="", long score=0)
        {
            int version = session;
            var callback = new Callback();
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var bridge = new AndroidJavaClass(Bridge))
                bridge.CallStatic("request", activity, project, operation, PlayerId, board, score, callback);
            try
            {
                var result = await callback.Result.Task;
                if (session != version) throw new InvalidOperationException("Session changed");
                return result;
            }
            catch (InvalidOperationException e) when (e.Message == "account")
            {
                if (session == version) PlayerId = "";
                throw;
            }
            finally { GC.KeepAlive(callback); }
        }

        public async Task<LoginResult> Authenticate()
        {
            PlayerId = await Request("login");
            return new LoginResult { Outcome = string.IsNullOrEmpty(PlayerId) ? LoginOutcome.Failed : LoginOutcome.SignedIn, PlayerId = PlayerId };
        }
        public async Task Submit(string board, long score, string expected)
        {
            if (string.IsNullOrEmpty(expected) || expected != PlayerId || !RankingScore.TryDecodeAttempt(score,out _,out _)
                || !validSubmission(board,score,expected)) throw new InvalidOperationException("Invalid submission");
            await Request("submit", board, score);
        }
        public async Task<RankingPage> Load(string board)
        {
            var page = JsonUtility.FromJson<ResponsePage>(await Request("load",board));
            return new RankingPage { Entries = page.entries ?? Array.Empty<RankingEntry>(), LocalPlayer = page.local };
        }
        public async void RefreshIdentity()
        {
            if (checking || PlayerId == "") return;
            int version = session;
            checking = true;
            try { if (await Request("identity") != PlayerId && version == session) EndSession(); }
            catch (Exception) { if (version == session) EndSession(); }
            finally { checking = false; }
        }
        public void EndSession()
        {
            session++;
            PlayerId = "";
            using (var bridge = new AndroidJavaClass(Bridge)) bridge.CallStatic("endSession");
        }
    }
}
#endif
