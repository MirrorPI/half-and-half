using System;
using System.Linq;
using System.Threading.Tasks;

namespace HalfAndHalf.Services
{
    public sealed class GameCenterFlow
    {
        readonly IGameCenter service;
        readonly SubmissionQueue queue;
        readonly LeaderboardCatalog catalog;
        int loginGeneration;
        bool loggingIn,flushing;
        public string ActivePlayerId { get; private set; }="";
        public LoginOutcome LastLogin { get; private set; }=LoginOutcome.Unsupported;
        public bool IsSignedIn=>ActivePlayerId!="" && service.PlayerId==ActivePlayerId;
        public bool IsLoggingIn=>loggingIn;
        public bool IsFlushing=>flushing;
        public int SessionVersion=>loginGeneration;
        public bool SubmissionFailed { get; private set; }
        public GameCenterFlow(IGameCenter adapter,SubmissionQueue scores,LeaderboardCatalog boards)
        {service=adapter;queue=scores;catalog=boards;}
        public async Task Login()
        {
            if(loggingIn)return;
            if(!service.Supported){LastLogin=LoginOutcome.Unsupported;return;}
            RefreshAccount();
            int generation=++loginGeneration;loggingIn=true;ActivePlayerId="";
            try
            {
                var result=await service.Authenticate();
                if(generation!=loginGeneration)return;
                LastLogin=result.Outcome;
                ActivePlayerId=result.Outcome==LoginOutcome.SignedIn ? result.PlayerId??"" : "";
                if(IsSignedIn){queue.BindGuest(ActivePlayerId);queue.Persist();}
            }
            catch(Exception){if(generation==loginGeneration){ActivePlayerId="";LastLogin=LoginOutcome.Failed;}}
            finally{loggingIn=false;}
        }
        public void UseGuest()
        {
            loginGeneration++;ActivePlayerId="";LastLogin=LoginOutcome.Unsupported;SubmissionFailed=false;
            (service as IRankingSessionReset)?.EndSession();
        }
        public void RefreshAccount()
        {
            if(ActivePlayerId!="" && !IsSignedIn)UseGuest();
        }
        public async Task Flush()
        {
            if(flushing || !IsSignedIn)return;
            flushing=true;SubmissionFailed=false;
            string player=ActivePlayerId;
            int generation=loginGeneration;
            try
            {
                // No side effect reaches Game Center until the queue is durable.
                if(!queue.Persist()){SubmissionFailed=true;return;}
                foreach(var item in queue.Entries.Where(e=>e.pending&&e.playerId==player))
                {
                    if(generation!=loginGeneration || !IsSignedIn || ActivePlayerId!=player)return;
                    var board=catalog.Resolve(item.objectId,item.contentVersion);if(board==null)continue;
                    if(!queue.CanSubmit(item,player)){queue.Persist();continue;}
                    try
                    {
                        await service.Submit(board,item.score,player);
                        if(generation!=loginGeneration || !IsSignedIn || ActivePlayerId!=player)return;
                        queue.Acknowledge(item);
                        if(!queue.Persist()){SubmissionFailed=true;return;}
                    }
                    catch(Exception){if(generation==loginGeneration)SubmissionFailed=true;return;}
                }
            }
            finally{flushing=false;}
        }
        public async Task<RankingPage> Load(string id,string version)
        {
            if(!IsSignedIn)return null;
            var board=catalog.Resolve(id,version);if(board==null)return null;
            int generation=loginGeneration;
            string player=ActivePlayerId;var page=await service.Load(board);
            return generation==loginGeneration&&IsSignedIn&&ActivePlayerId==player ? page : null;
        }
    }
}
