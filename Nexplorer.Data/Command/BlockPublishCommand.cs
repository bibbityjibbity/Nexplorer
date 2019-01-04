﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nexplorer.Config;
using Nexplorer.Core;
using Nexplorer.Domain.Dtos;

namespace Nexplorer.Data.Command
{
    public class BlockPublishCommand
    {
        private readonly ILogger<BlockPublishCommand> _logger;
        private readonly RedisCommand _redisCommand;

        public BlockPublishCommand(ILogger<BlockPublishCommand> logger, RedisCommand redisCommand)
        {
            _logger = logger;
            _redisCommand = redisCommand;
        }

        public async Task PublishAsync(int blockHeight)
        {
            if (blockHeight == 0)
                return;

            var block = await _redisCommand.GetAsync<BlockDto>(Settings.Redis.BuildCachedBlockKey(blockHeight));

            if (block == null)
            {
                _logger.LogWarning($"Block {blockHeight} is missing from the cache");

                throw new NullReferenceException($"Block {blockHeight} is missing from the cache");
            }

            var blockLite = new BlockLiteDto(block);

            await _redisCommand.PublishAsync(Settings.Redis.NewBlockPubSub, blockLite);

            if (block.Transactions == null || !block.Transactions.Any())
                return;

            foreach (var tx in block.Transactions)
                await _redisCommand.PublishAsync(Settings.Redis.NewTransactionPubSub, new TransactionLiteDto(tx, block.Height, 1));

            _logger.LogInformation($"Published block {block.Height}");
        }
    }
}