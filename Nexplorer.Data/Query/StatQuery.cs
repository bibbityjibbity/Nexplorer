using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Nexplorer.Config;
using Nexplorer.Core;
using Nexplorer.Data.Cache.Services;
using Nexplorer.Domain.Dtos;
using Nexplorer.Domain.Enums;

namespace Nexplorer.Data.Query
{
    public class StatQuery
    {
        private readonly RedisCommand _redisCommand;
        private readonly BlockCacheService _blockCache;

        public StatQuery(RedisCommand redisCommand, BlockCacheService blockCache)
        {
            _redisCommand = redisCommand;
            _blockCache = blockCache;
        }

        public async Task<List<ChannelStatDto>> GetChannelStatsAsync()
        {
            const string sqlQ = @"SELECT 
                                  b.Channel,
                                  COUNT(*) AS Height
                                  FROM Block b
                                  GROUP BY b.Channel";

            using (var sqlCon = await DbConnectionFactory.GetNexusDbConnectionAsync())
            {
                var channelResult = (await sqlCon.QueryAsync(sqlQ)).ToList();

                var miningInfo = await _redisCommand.GetAsync<MiningInfoDto>(Settings.Redis.MiningInfoLatest);

                if (miningInfo == null)
                    return null;

                var channelStatTasks = channelResult
                    .Select(async x =>
                    {
                        var channel = (BlockChannels)x.Channel;
                        double diff = 0;
                        double reward = 0;
                        long rateSec = 0;
                        double reserve = 0;

                        switch (channel)
                        {
                            case BlockChannels.PoS:
                                diff = await _blockCache.GetChannelDifficultyAsync(channel);
                                reward = await _blockCache.GetPosBlockRewardAsync();
                                break;
                            case BlockChannels.Prime:
                                diff = miningInfo.PrimeDifficulty;
                                reward = miningInfo.PrimeValue;
                                rateSec = miningInfo.PrimesPerSecond;
                                reserve = miningInfo.PrimeReserve;
                                break;
                            case BlockChannels.Hash:
                                diff = miningInfo.HashDifficulty;
                                reward = miningInfo.HashValue;
                                rateSec = miningInfo.HashPerSecond;
                                reserve = miningInfo.HashReserve;
                                break;
                        }

                        return new ChannelStatDto
                        {
                            Channel = ((BlockChannels)x.Channel).ToString(),
                            Height = (int)x.Height + await _blockCache.GetChannelHeightAsync((BlockChannels)x.Channel),
                            Difficulty = diff,
                            Reward = reward,
                            RatePerSecond = rateSec,
                            Reserve = reserve,
                            CreatedOn = miningInfo.CreatedOn
                        };
                    });

                return (await Task.WhenAll(channelStatTasks)).ToList();
            }
        }
    }
}