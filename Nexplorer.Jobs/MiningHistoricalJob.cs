﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nexplorer.Config;
using Nexplorer.Core;
using Nexplorer.Data.Query;
using Nexplorer.Domain.Dtos;
using Nexplorer.Jobs.Service;

namespace Nexplorer.Jobs
{
    public class MiningHistoricalJob : HostedService
    {
        private readonly NexusQuery _nexusQuery;
        private readonly RedisCommand _redisCommand;

        public MiningHistoricalJob(NexusQuery nexusQuery, RedisCommand redisCommand)
            : base(10)
        {
            _nexusQuery = nexusQuery;
            _redisCommand = redisCommand;
        }

        protected override async Task ExecuteAsync()
        {
            var miningInfo = await _nexusQuery.GetMiningInfoAsync();

            var recentMiningInfos = await _redisCommand.GetAsync<List<MiningInfoDto>>(Settings.Redis.MiningInfo10Mins) ??
                                    new List<MiningInfoDto>();

            if (recentMiningInfos.All(x => x.CreatedOn != miningInfo.CreatedOn))
            {
                recentMiningInfos.Add(miningInfo);
                recentMiningInfos.RemoveAll(x => x.CreatedOn < DateTime.UtcNow.AddMinutes(-10));

                await _redisCommand.SetAsync(Settings.Redis.MiningInfo10Mins, recentMiningInfos);
            }

            await _redisCommand.SetAsync(Settings.Redis.MiningInfoLatest, miningInfo);
        }
    }
}