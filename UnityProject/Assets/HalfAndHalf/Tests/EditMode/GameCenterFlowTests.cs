using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HalfAndHalf.Core;
using HalfAndHalf.Services;
using NUnit.Framework;
namespace HalfAndHalf.Tests
{
    public sealed class GameCenterFlowTests
    {
        string folder;
        SubmissionQueue queue;
        ObjectDefinition definition;
        FakeService service;
        object flow,catalog;
        Type FlowType=>Required("GameCenterFlow");
        static Type Required(string name)
        {var t=typeof(LocalProgressStore).Assembly.GetType("HalfAndHalf.Services."+name);Assert.That(t,Is.Not.Null,"Task4 needs "+name);return t;}
        object Call(string name,params object[] args)=>FlowType.GetMethod(name).Invoke(flow,args);
        Task Login()=> (Task)Call("Login");
        Task Flush()=> (Task)Call("Flush");
        [SetUp] public void Setup()
        {
            folder=Path.Combine(Path.GetTempPath(),"half-task4-flow-"+Guid.NewGuid());Directory.CreateDirectory(folder);
            definition=new ObjectDefinition("B01","v1",new MassMap(new[]{new MassCell(0,1,0,1,100)}),"");
            queue=new SubmissionQueue(Path.Combine(folder,"q.json"),new[]{definition});service=new FakeService();
        }
        [TearDown] public void Cleanup(){if(Directory.Exists(folder))Directory.Delete(folder,true);}
        void Create()
        {
            catalog=Activator.CreateInstance(Required("LeaderboardCatalog"),new object[]{new[]{new LeaderboardMapping{objectId="B01",contentVersion="v1",leaderboardId="test.apple.v1"}}});
            flow=Activator.CreateInstance(FlowType,new[]{(object)service,queue,catalog});
        }
        AttemptResult Result(int accuracy=9800)
        {decimal x=accuracy/20000m;return Scoring.Evaluate(definition,new CutSnapshot("B01","v1",x,2000,TransformState.Identity,true));}
        [Test] public async Task OfflineFailureSurvivesDuplicateRetryAndSuccessAcknowledgesOnce()
        {
            Create();queue.Offer(Result(),"");queue.Persist();await Login();service.Fail=true;await Flush();Assert.That(queue.Entries.Single().pending,Is.True);
            service.Fail=false;await Flush();await Flush();Assert.That(queue.Entries.Single().pending,Is.False);Assert.That(service.Submissions,Is.EqualTo(2));
        }
        [TestCase(LoginOutcome.Cancelled)] [TestCase(LoginOutcome.Failed)] [TestCase(LoginOutcome.Unsupported)]
        public async Task LoginFailureAndCancelKeepGuestCandidateAndNeverSubmit(LoginOutcome outcome)
        {Create();service.Outcome=outcome;queue.Offer(Result(),"");await Login();await Flush();Assert.That(service.Submissions,Is.Zero);Assert.That(queue.Entries.Single().playerId,Is.Empty);Assert.That(queue.Entries.Single().pending,Is.True);}
        [Test] public async Task AccountSwitchDoesNotSubmitThePreviousPlayersQueue()
        {Create();queue.Offer(Result(),"");await Login();service.Player="p2";await Flush();Assert.That(service.Submissions,Is.Zero);await Login();await Flush();Assert.That(service.Submissions,Is.Zero);Assert.That(queue.Entries.Single().playerId,Is.EqualTo("p1"));}
        [Test] public async Task DuplicateConcurrentFlushDoesNotIssueParallelSubmissions()
        {Create();queue.Offer(Result(),"");await Login();service.Gate=new TaskCompletionSource<bool>();var first=Flush();await Flush();Assert.That(service.Submissions,Is.EqualTo(1));service.Gate.SetResult(true);await first;Assert.That(queue.Entries.Single().pending,Is.False);}
        [Test] public async Task BetterAttemptDuringSubmissionStaysPendingAfterOldCallback()
        {Create();queue.Offer(Result(),"");await Login();service.Gate=new TaskCompletionSource<bool>();var first=Flush();queue.Offer(Result(9900),"p1");queue.Persist();service.Gate.SetResult(true);await first;Assert.That(queue.Entries.Single().pending,Is.True);service.Gate=null;await Flush();Assert.That(service.Scores.Last(),Is.EqualTo(1000002000));}
        [Test] public async Task GuestSelectionDuringLoginDiscardsLateAuthentication()
        {Create();queue.Offer(Result(),"");service.LoginGate=new TaskCompletionSource<LoginResult>();var login=Login();Call("UseGuest");service.LoginGate.SetResult(new LoginResult{Outcome=LoginOutcome.SignedIn,PlayerId="p1"});await login;await Flush();Assert.That(service.Submissions,Is.Zero);Assert.That(queue.Entries.Single().playerId,Is.Empty);}
        [Test] public async Task NativeLogoutDiscardsHeldPageEvenIfSameAccountReturns()
        {
            Create();await Login();
            service.PageGate=new TaskCompletionSource<RankingPage>();
            var pending=(Task<RankingPage>)Call("Load","B01","v1");
            service.Player="";Call("RefreshAccount");
            Assert.That(((GameCenterFlow)flow).ActivePlayerId,Is.Empty);
            service.Player="p1";await Login();
            service.PageGate.SetResult(new RankingPage());
            Assert.That(await pending,Is.Null);
        }
        [Test] public async Task UnpersistedQueueMustNotSubmit()
        {Create();queue.Offer(Result(),"");await Login();Directory.CreateDirectory(Path.Combine(folder,"q.json.tmp"));await Flush();Assert.That(service.Submissions,Is.Zero);Assert.That(queue.Entries.Single().pending,Is.True);}
        [Test] public async Task WrongContentVersionAndNonMvpBoardNeverSubmit()
        {Create();queue.Offer(Result(),"");await Login();catalog=Activator.CreateInstance(Required("LeaderboardCatalog"),new object[]{new[]{new LeaderboardMapping{objectId="B01",contentVersion="v2",leaderboardId="test.apple.v2"}}});flow=Activator.CreateInstance(FlowType,new[]{(object)service,queue,catalog});await Login();await Flush();Assert.That(service.Submissions,Is.Zero);}
        sealed class FakeService:IGameCenter
        {
            public bool Supported=>true;
            public string Player="p1";
            public string PlayerId=>Player;
            public LoginOutcome Outcome=LoginOutcome.SignedIn;
            public bool Fail;public int Submissions;
            public System.Collections.Generic.List<long> Scores=new System.Collections.Generic.List<long>();
            public TaskCompletionSource<bool> Gate;
            public TaskCompletionSource<LoginResult> LoginGate;
            public TaskCompletionSource<RankingPage> PageGate;
            public Task<LoginResult> Authenticate()=>LoginGate?.Task??Task.FromResult(new LoginResult{Outcome=Outcome,PlayerId=Outcome==LoginOutcome.SignedIn?Player:null});
            public async Task Submit(string board,long score,string expected)
            {Assert.That(expected,Is.EqualTo(Player));Submissions++;Scores.Add(score);if(Gate!=null)await Gate.Task;if(Fail)throw new IOException("offline");}
            public Task<RankingPage> Load(string board)=>PageGate?.Task??Task.FromResult(new RankingPage{Entries=new RankingEntry[0]});
        }
    }
}
