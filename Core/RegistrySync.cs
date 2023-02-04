using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Acorle.Models.Contexts;
using Acorle.Models;


namespace Acorle.Core
{
    public class RegistrySync : IHostedService, IDisposable
    {
        private Timer timer;
        private readonly IServiceScopeFactory scopeFactory;
        private readonly ILogger<RegistrySync> logger;

        public RegistrySync(IServiceScopeFactory scopeFactory, ILogger<RegistrySync> logger)
        {
            this.scopeFactory = scopeFactory;
            this.logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            timer = new Timer(async state => await DoRegistrySyncAsync(scopeFactory).ConfigureAwait(false), null, 0, ((int)Constants.RegistrySyncIntervalSeconds) * 1000);
            return Task.CompletedTask;
        }

        // 回收无用的会话上下文
        // 上下文结构 <zone, <service, <hash, 会话信息> > >
        //            ^ 领域, ^ 业务键  ^ 候选服务的 hash
        // 一个 Key 下可能有多个 yrl 可供使用。使用负载均衡器找到此时应该使用的 url。
        private async Task DoRegistrySyncAsync(IServiceScopeFactory scopeFactory)
        {
            using var scope = scopeFactory.CreateScope();
            var dataBaseContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

            Zone[] allZones = await SyncZones(dataBaseContext).ConfigureAwait(false);
            await SyncContexts(allZones, dataBaseContext).ConfigureAwait(false);
            await SyncLoadBalancers(dataBaseContext).ConfigureAwait(false);
        }

        private async Task SyncContexts(Zone[] allZones, DatabaseContext dataBaseContext)
        {
            try
            {
                // 从注册表中找到并删除在可用 Zone 中不再存在的服务，并同步上下文
                if (allZones != null)
                {
                    foreach (Zone zone in allZones)
                    {
                        var allServices = await dataBaseContext.Services.Where(s => s.Zone == zone.Key).ToArrayAsync().ConfigureAwait(false);
                        if (Registry.Contexts.TryGetValue(zone.Key, out var entries))
                        {
                            foreach (var entry in entries)
                            {
                                string serviceKey = entry.Key;
                                if (allServices.Where(s => s.Key == serviceKey).Any())
                                {
                                    // 从注册表中找到并删除在可用业务中不再存在的负载均衡节点(即同业务键名不同业务URL)
                                    foreach (var session in entry.Value)
                                    {
                                        if (!allServices.Where(s => s.Key == serviceKey && s.Hash == session.Key).Any())
                                        {
                                            entry.Value.TryRemove(session.Key, out _);
                                        }
                                    }
                                }
                                else
                                {
                                    entries.TryRemove(serviceKey, out _);
                                }
                            }
                        }


                        foreach (Service service in allServices)
                        {
                            var registryEntries = Registry.Contexts.GetOrAdd(service.Zone, key => new ConcurrentDictionary<string, ConcurrentDictionary<string, ServiceSession>>());
                            var sessions = registryEntries.GetOrAdd(service.Key, key => new ConcurrentDictionary<string, ServiceSession>());
                            sessions.AddOrUpdate(service.Hash, hash => new ServiceSession(new Service()
                            {
                                Zone = service.Zone,
                                Hash = hash,
                                Key = service.Key,
                                Name = service.Name,
                                Url = service.Url,
                                Weight = service.Weight,
                                IsPrivate = service.IsPrivate,
                                AddedTime = service.AddedTime,
                                ExpireTime = service.ExpireTime,
                            }), (hash, session) => {
                                if (session.Service.Name != service.Name) session.Service.Name = service.Name;
                                if (session.Service.Url != service.Url) session.Service.Url = service.Url;
                                if (session.Service.Weight != service.Weight) session.Service.Weight = service.Weight;
                                if (session.Service.IsPrivate != service.IsPrivate) session.Service.IsPrivate = service.IsPrivate;
                                // 由于数据库中持久化的时间精度与程序直接获取的精度不一致，这里舍弃精度以毫秒(Unix毫秒时间戳)进行比较
                                if (Utils.ToUnixTimeMilliseconds(session.Service.AddedTime) != Utils.ToUnixTimeMilliseconds(service.AddedTime)) session.Service.AddedTime = service.AddedTime;
                                if (Utils.ToUnixTimeMilliseconds(session.Service.ExpireTime) != Utils.ToUnixTimeMilliseconds(service.ExpireTime)) session.Service.ExpireTime = service.ExpireTime;
                                return session;
                            });
                        }
                    }
                }

                // 删除所有 Zone 中的所有业务中的已过期的会话
                foreach (var zones in Registry.Contexts)
                {
                    foreach (var entries in zones.Value)
                    {
                        foreach (var session in entries.Value)
                        {
                            if (session.Value.Service.ExpireTime < DateTime.UtcNow)
                            {
                                var service = zones.Value.Where(s => s.Key == session.Value.Service.Key).FirstOrDefault();
                                service.Value.TryRemove(session.Value.Service.Hash, out _);
                                if (service.Value.IsEmpty) zones.Value.TryRemove(session.Value.Service.Key, out _);
                            }
                        }
                        if (zones.Value.IsEmpty) Registry.Contexts.TryRemove(zones.Key, out _);
                    }
                }
            }
            catch (Exception exception)
            {
                LoggingWrapper.LogError(logger, "synchronizing Contexts", exception);
            }
        }

        private async Task<Zone[]> SyncZones(DatabaseContext dataBaseContext)
        {
            try
            {
                var allZones = await dataBaseContext.Zones.ToArrayAsync().ConfigureAwait(false);
                foreach (Zone zoneInRegistry in Registry.Zones.Values)
                {
                    if (!allZones.Where(z => z.Key == zoneInRegistry.Key).Any())
                    {
                        Registry.Zones.TryRemove(zoneInRegistry.Key, out _);
                    }
                }
                foreach (Zone zoneInDb in allZones)
                {
                    Registry.Zones.AddOrUpdate(zoneInDb.Key, key => new Zone()
                    {
                        Key = zoneInDb.Key,
                        Name = zoneInDb.Name,
                        Description = zoneInDb.Description,
                        Secret = zoneInDb.Secret,
                        MaxServices = zoneInDb.MaxServices,
                        RegIntervalSeconds = zoneInDb.RegIntervalSeconds,
                        RpcTimeoutSeconds = zoneInDb.RpcTimeoutSeconds,
                        LogUserRequest = zoneInDb.LogUserRequest,
                    }, (key, zone) => {
                        if (zoneInDb.Name != zone.Name) zone.Name = zoneInDb.Name;
                        if (zoneInDb.Description != zone.Description) zone.Description = zoneInDb.Description;
                        if (zoneInDb.Secret != zone.Secret) zone.Secret = zoneInDb.Secret;
                        if (zoneInDb.MaxServices != zone.MaxServices) zone.MaxServices = zoneInDb.MaxServices;
                        if (zoneInDb.RegIntervalSeconds != zone.RegIntervalSeconds) zone.RegIntervalSeconds = zoneInDb.RegIntervalSeconds;
                        if (zoneInDb.RpcTimeoutSeconds != zone.RpcTimeoutSeconds) zone.RpcTimeoutSeconds = zoneInDb.RpcTimeoutSeconds;
                        if (zoneInDb.LogUserRequest != zone.LogUserRequest) zone.LogUserRequest = zoneInDb.LogUserRequest;
                        return zone;
                    });
                }

                return allZones;
            }
            catch (Exception exception)
            {
                LoggingWrapper.LogError(logger, "synchronizing Zones", exception);
            }
            return null;
        }

        private async Task SyncLoadBalancers(DatabaseContext dataBaseContext)
        {
            try
            {
                var allLoadBalancers = await dataBaseContext.LoadBalancers.ToArrayAsync().ConfigureAwait(false);
                foreach (var serviceLoadBalancers in Registry.LoadBalancers)
                {
                    foreach (var serviceLoadBalancer in serviceLoadBalancers.Value)
                    {
                        if (!allLoadBalancers.Where(l => l.Zone == serviceLoadBalancers.Key && l.Service == serviceLoadBalancer.Key).Any())
                        {
                            serviceLoadBalancers.Value.TryRemove(serviceLoadBalancer.Key, out _);
                            if (serviceLoadBalancers.Value.IsEmpty) Registry.LoadBalancers.TryRemove(serviceLoadBalancers.Key, out _);
                        }
                    }
                }
                foreach (LoadBalancer loadBalancerInDb in allLoadBalancers)
                {
                    var serviceLoadBalancers = Registry.LoadBalancers.GetOrAdd(loadBalancerInDb.Zone, key => new ConcurrentDictionary<string, LoadBalancerType>());
                    serviceLoadBalancers.AddOrUpdate(loadBalancerInDb.Service, key => (LoadBalancerType)loadBalancerInDb.Type, (key, loadBalancer) => (LoadBalancerType)loadBalancerInDb.Type);
                }
            }
            catch (Exception exception)
            {
                LoggingWrapper.LogError(logger, "synchronizing LoadBalancers", exception);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose() => timer?.Dispose();
        
    }
}
