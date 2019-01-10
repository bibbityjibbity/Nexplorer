using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Nexplorer.Config;
using Nexplorer.Core;
using Nexplorer.Data.Services;
using Nexplorer.Domain.Dtos;

namespace Nexplorer.Web.Hubs
{
    public class AdminMessenger
    {
        public readonly List<string> RecentSyncOutputs;

        private readonly IHubContext<AdminHub> _adminContext;
        private readonly CacheService _cache;

        public AdminMessenger(RedisCommand redisCommand, IHubContext<AdminHub> adminContext, CacheService cache)
        {
            _adminContext = adminContext;
            _cache = cache;

            redisCommand.Subscribe<string>(Settings.Redis.SyncOutputPubSub, OnSyncOutputAsync);

            RecentSyncOutputs = new List<string>();
        }

        private Task OnSyncOutputAsync(string syncOutput)
        {
            RecentSyncOutputs.Add(syncOutput);

            if (RecentSyncOutputs.Count > 500)
                RecentSyncOutputs.RemoveAt(0);

            return _adminContext.Clients.All.SendAsync("SyncOutput", syncOutput);
        }
    }
}