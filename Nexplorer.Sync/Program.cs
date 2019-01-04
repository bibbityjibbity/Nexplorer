﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nexplorer.Config;
using Nexplorer.Core;
using Nexplorer.Data.Cache.Block;
using Nexplorer.Data.Cache.Services;
using Nexplorer.Data.Command;
using Nexplorer.Data.Context;
using Nexplorer.Data.Map;
using Nexplorer.Data.Publish;
using Nexplorer.Data.Query;
using Nexplorer.Infrastructure.Bittrex;
using Nexplorer.Infrastructure.Geolocate;
using Nexplorer.Jobs;
using Nexplorer.Jobs.Catchup;
using Nexplorer.Jobs.Service;
using Nexplorer.NexusClient;
using Nexplorer.NexusClient.Core;
using NLog.Extensions.Logging;
using StackExchange.Redis;
using NLog;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Nexplorer.Sync
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            var serviceCollection = new ServiceCollection();

            ConfigureServices(serviceCollection);
            
            var serviceProvider = serviceCollection.BuildServiceProvider();

            // Attach Config
            Settings.AttachConfig(serviceProvider);
            
            // Configure NLog
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            loggerFactory.AddNLog(new NLogProviderOptions { CaptureMessageTemplates = true, CaptureMessageProperties = true });
            LogManager.LoadConfiguration("nlog.config");

            // Clear Redis
            var endpoints = serviceProvider.GetService<ConnectionMultiplexer>().GetEndPoints(true);
            foreach (var endpoint in endpoints)
            {
                var redis = serviceProvider.GetService<ConnectionMultiplexer>().GetServer(endpoint);
                redis.FlushAllDatabases();
            }

            // Migrate EF
            serviceProvider.GetService<NexusDb>().Database.Migrate();

            JobService.Start(serviceProvider.GetService<IEnumerable<IHostedService>>());

            Task.Run(serviceProvider.GetService<App>().StartAsync);

            Console.Read();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<ILoggerFactory, LoggerFactory>();
            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
            services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Trace));

            var configuration = Settings.BuildConfig(services);
                
            services.AddDbContext<NexusDb>(x => x.UseSqlServer(configuration.GetConnectionString("NexusDb"), y => { y.MigrationsAssembly("Nexplorer.Data"); }), ServiceLifetime.Transient);

            services.AddSingleton(ConnectionMultiplexer.Connect(configuration.GetConnectionString("Redis")));
            services.AddSingleton<RedisCommand>();

            services.AddSingleton<AutoMapperConfig>();
            services.AddSingleton(x => x.GetService<AutoMapperConfig>().GetMapper());
            
            services.AddSingleton<BlockCacheService>();
            services.AddSingleton<GeolocationService>();

            services.AddSingleton<BlockCacheCommand>();
            services.AddSingleton<BlockPublishCommand>();

            services.AddScoped<NexusQuery>();
            services.AddScoped<BlockQuery>();
            services.AddScoped<AddressQuery>();
            services.AddScoped<StatQuery>();
            services.AddScoped<AddressAggregator>();

            services.AddScoped<INxsClient, NxsClient>();
            services.AddScoped<INxsClient, NxsClient>(x => new NxsClient(configuration.GetConnectionString("Nexus")));
            services.AddScoped<NexusBlockStream>();
            services.AddScoped<BittrexClient>();
            services.AddScoped<GeolocateIpClient>();

            services.AddSingleton<App>();

            services.AddSingleton<BlockSyncCatchup>();
            services.AddSingleton<AddressAggregateCatchup>();
            services.AddSingleton<BlockRewardCatchup>();
            
            services.AddScoped<App>();

            JobService.Init(services);
        }
    }
}
