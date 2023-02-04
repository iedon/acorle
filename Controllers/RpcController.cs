using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Google.Protobuf;
using Acorle.Core;
using Acorle.Models;
using Acorle.Models.Contexts;
using static Acorle.Models.RequestPacket.Types;
using static Acorle.Models.RequestPacket.Types.RpcRequest.Types;
using static Acorle.Models.ResponsePacket.Types;


namespace Acorle.Controllers
{
    /* RPC 业务控制器 */
    public static class RpcController
    {
        /*
         * 子节请求互入口
         */
        public static async Task<byte[]> RpcMain(RequestPacket requestPacket, DatabaseContext databaseContext, HttpContext httpContext, ILogger<ApiController> logger)
        {
            RpcRequest RpcRequest;
            try
            {
                RpcRequest = RpcRequest.Parser.ParseFrom(requestPacket.Data);
            }
            catch
            {
                return PacketHandler.MakeResponse(ResponseCodeType.BadRequest);
            }

            if (!PacketHandler.ValidateRpcRequest(RpcRequest))
                return PacketHandler.MakeResponse(ResponseCodeType.BadRequest);

            Zone zone = await databaseContext.Zones.Where(z => z.Key == requestPacket.Zone).FirstOrDefaultAsync().ConfigureAwait(false);
            if (zone == null)
            {
                // zone 找不到就不记录日志。否则数据库中会充满这些未注册的 zone 的垃圾数据。
                return PacketHandler.MakeResponse(ResponseCodeType.RpcInvalidZone);
            }

            byte[] rawPayload = PacketHandler.GetRpcPayload(RpcRequest, requestPacket.Zone, zone.Secret);
            if (rawPayload == null) return PacketHandler.MakeResponse(ResponseCodeType.InvalidBody);

            switch (requestPacket.Action) {
                case ActionType.RpcRegister:
                {
                    RpcRegisterServiceRequest payload;
                    try
                    {
                        payload = RpcRegisterServiceRequest.Parser.ParseFrom(rawPayload);
                    }
                    catch
                    {
                        return PacketHandler.MakeResponse(ResponseCodeType.InvalidBody);
                    }
                    return await RegisterService(databaseContext, zone, payload, logger).ConfigureAwait(false);
                }
                case ActionType.RpcList:
                    return ListServices(zone);
                case ActionType.RpcGet:
                {
                    RpcGetServiceRequest payload;
                    try
                    {
                        payload = RpcGetServiceRequest.Parser.ParseFrom(rawPayload);
                    }
                    catch
                    {
                        return PacketHandler.MakeResponse(ResponseCodeType.InvalidBody);
                    }
                    return GetService(zone, payload);
                }
                case ActionType.RpcCall:
                {
                    RpcCallServiceRequest payload;
                    try
                    {
                        payload = RpcCallServiceRequest.Parser.ParseFrom(rawPayload);
                    }
                    catch
                    {
                        return PacketHandler.MakeResponse(ResponseCodeType.InvalidBody);
                    }
                    return CallService(httpContext, zone, payload);
                }
                case ActionType.RpcDestroy:
                {
                    RpcDestroyServiceRequest payload;
                    try
                    {
                        payload = RpcDestroyServiceRequest.Parser.ParseFrom(rawPayload);
                    }
                    catch
                    {
                        return PacketHandler.MakeResponse(ResponseCodeType.InvalidBody);
                    }
                    return await DestroyService(databaseContext, zone, payload, logger).ConfigureAwait(false);
                }
                case ActionType.RpcConfigGet:
                {
                    RpcGetConfigRequest payload;
                    try
                    {
                        payload = RpcGetConfigRequest.Parser.ParseFrom(rawPayload);
                    }
                    catch
                    {
                        return PacketHandler.MakeResponse(ResponseCodeType.InvalidBody);
                    }
                    return await GetConfig(databaseContext, zone, payload).ConfigureAwait(false);
                }
                case ActionType.RpcConfigSet:
                {
                    RpcSetConfigRequest payload;
                    try
                    {
                        payload = RpcSetConfigRequest.Parser.ParseFrom(rawPayload);
                    }
                    catch
                    {
                        return PacketHandler.MakeResponse(ResponseCodeType.InvalidBody);
                    }
                    return await SetConfig(databaseContext, zone, payload, logger).ConfigureAwait(false);
                }
                default: return PacketHandler.MakeResponse(ResponseCodeType.BadRequest);
            }
        }


        /*
         * 子节点业务注册与续命
         */
        private static async Task<byte[]> RegisterService(DatabaseContext databaseContext, Zone zone, RpcRegisterServiceRequest data, ILogger<ApiController> logger)
        {
            if (data == null || data.Services == null)
                return PacketHandler.MakeResponse(ResponseCodeType.InvalidBody);

            foreach (var serviceToRegister in data.Services) // 用户提交的 Service
            {
                if (string.IsNullOrEmpty(serviceToRegister.Key) || string.IsNullOrEmpty(serviceToRegister.Name) || string.IsNullOrEmpty(serviceToRegister.Url))
                    return PacketHandler.MakeResponse(ResponseCodeType.InvalidBody);

                var entries = Registry.Contexts.GetOrAdd(zone.Key, key => new ConcurrentDictionary<string, ConcurrentDictionary<string, ServiceSession>>());
                var sessions = entries.GetOrAdd(serviceToRegister.Key, key => new ConcurrentDictionary<string, ServiceSession>());

                // 自定义业务超出数量限制(0 代表无限制)
                if (zone.MaxServices != 0 && sessions.Count >= zone.MaxServices)
                    return PacketHandler.MakeResponse(ResponseCodeType.RpcRegLimit);

                string serviceHash = Utils.Sha1Hash(serviceToRegister.Key + serviceToRegister.Url);
                var session = sessions.GetOrAdd(serviceHash, hash => {
                    Service service = new()
                    {
                        Zone = zone.Key,
                        Hash = hash,
                        Key = serviceToRegister.Key,
                        Url = serviceToRegister.Url,
                        AddedTime = DateTime.UtcNow,
                    };
                    return new ServiceSession(service);
                });

                // 更新业务信息
                session.Service.Name = serviceToRegister.Name;
                session.Service.Weight = serviceToRegister.Weight == 0 ? Constants.RpcServiceDefaultWeight : serviceToRegister.Weight;
                session.Service.IsPrivate = serviceToRegister.IsPrivate;

                // 此业务已经注册且本次存活 / 新注册的业务，为其更新过期时间
                session.Service.ExpireTime = DateTime.UtcNow.AddSeconds(zone.RegIntervalSeconds);

                var serviceInDb = await databaseContext.Services.FirstOrDefaultAsync(s => s.Hash == serviceHash).ConfigureAwait(false);
                if (serviceInDb != null)
                {
                    if (serviceInDb.Name != session.Service.Name
                        || serviceInDb.Weight != session.Service.Weight
                        || serviceInDb.IsPrivate != session.Service.IsPrivate)
                    {
                        serviceInDb.Name = session.Service.Name;
                        serviceInDb.Weight = session.Service.Weight;
                        serviceInDb.IsPrivate = session.Service.IsPrivate;
                    }
                    serviceInDb.ExpireTime = session.Service.ExpireTime;
                }
                else
                {
                    await databaseContext.Services.AddAsync(session.Service).ConfigureAwait(false);
                }
            }
            try
            {
                if (data.Services.Any()) await databaseContext.SaveChangesAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                LoggingWrapper.LogError(logger, "saving to database", exception);
                return PacketHandler.MakeResponse(ResponseCodeType.RpcOperationFailed);
            }
            return PacketHandler.MakeResponse(ResponseCodeType.Ok);
        }


        /*
         *  子节点销毁业务
         */
        private static async Task<byte[]> DestroyService(DatabaseContext databaseContext, Zone zone, RpcDestroyServiceRequest data, ILogger<ApiController> logger)
        {
            if (data == null || data.Services == null)
                return PacketHandler.MakeResponse(ResponseCodeType.InvalidBody);

            bool dbNeedsSave = false;
            foreach (var serviceToDestroy in data.Services) // 用户提交的 Service
            {
                if (string.IsNullOrEmpty(serviceToDestroy.Key) || string.IsNullOrEmpty(serviceToDestroy.Url))
                    return PacketHandler.MakeResponse(ResponseCodeType.InvalidBody);

                // 在注册中心中已经存在的 Service
                if (!Registry.Contexts.TryGetValue(zone.Key, out var entries))
                    return PacketHandler.MakeResponse(ResponseCodeType.RpcInvalidZone);

                var sessions = entries.GetOrAdd(serviceToDestroy.Key, key => new ConcurrentDictionary<string, ServiceSession>());
                string serviceHash = Utils.Sha1Hash(serviceToDestroy.Key + serviceToDestroy.Url);

                sessions.TryRemove(serviceHash, out _);
                Service serviceToRemove = await databaseContext.Services.Where(s => s.Zone == zone.Key && s.Hash == serviceHash).FirstOrDefaultAsync().ConfigureAwait(false);
                if (serviceToRemove != null)
                {
                    databaseContext.Services.Remove(serviceToRemove);
                    dbNeedsSave = true;
                }
            }
            try
            {
                if (dbNeedsSave) await databaseContext.SaveChangesAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                LoggingWrapper.LogError(logger, "saving to database", exception);
                return PacketHandler.MakeResponse(ResponseCodeType.RpcOperationFailed);
            }
            return PacketHandler.MakeResponse(ResponseCodeType.Ok);
        }


        /*
         *  子节点获取当前所注册的业务信息
         */
        private static byte[] ListServices(Zone zone)
        {
            // 在注册中心中已经存在的 Service
            if (!Registry.Contexts.TryGetValue(zone.Key, out var entries))
                return PacketHandler.MakeResponse(ResponseCodeType.RpcInvalidZone);

            ServiceProto serviceProto = new();
            foreach (var entry in entries)
            {
                foreach (var session in entry.Value)
                {
                    serviceProto.Services.Add(new ServiceProto.Types.ServiceMessage()
                    {
                        Hash = session.Key,
                        Key = session.Value.Service.Key,
                        Name = session.Value.Service.Name,
                        Url = session.Value.Service.Url,
                        Weight = session.Value.Service.Weight,
                        IsPrivate = session.Value.Service.IsPrivate,
                        AddedTimestamp = Utils.ToUnixTimeMilliseconds(session.Value.Service.AddedTime),
                        ExpireTimestamp = Utils.ToUnixTimeMilliseconds(session.Value.Service.ExpireTime),
                    });
                }
            }
            return PacketHandler.MakeResponse(ResponseCodeType.Ok, serviceProto.ToByteString());
        }


        /*
         *  子节点获取某业务的信息
         */
        private static byte[] GetService(Zone zone, RpcGetServiceRequest data)
        {
            if (data == null || string.IsNullOrEmpty(data.Key))
                return PacketHandler.MakeResponse(ResponseCodeType.InvalidBody);

            // 在注册中心中已经存在的 Service
            if (!Registry.Contexts.TryGetValue(zone.Key, out var entries))
                return PacketHandler.MakeResponse(ResponseCodeType.RpcInvalidZone);

            ServiceProto serviceProto = new();
            if (entries.TryGetValue(data.Key, out var sessions))
            {
                foreach (var session in sessions)
                {
                    serviceProto.Services.Add(new ServiceProto.Types.ServiceMessage()
                    {
                        Hash = session.Key,
                        Key = session.Value.Service.Key,
                        Name = session.Value.Service.Name,
                        Url = session.Value.Service.Url,
                        Weight = session.Value.Service.Weight,
                        IsPrivate = session.Value.Service.IsPrivate,
                        AddedTimestamp = Utils.ToUnixTimeMilliseconds(session.Value.Service.AddedTime),
                        ExpireTimestamp = Utils.ToUnixTimeMilliseconds(session.Value.Service.ExpireTime),
                    });
                }
            }
            return PacketHandler.MakeResponse(ResponseCodeType.Ok, serviceProto.ToByteString());
        }


        /*
         *  子节点调用同节点下其他业务，返回经过负载均衡器选择后的 URL
         */
        private static byte[] CallService(HttpContext httpContext, Zone zone, RpcCallServiceRequest data)
        {
            if (data == null || string.IsNullOrEmpty(data.Key))
                return PacketHandler.MakeResponse(ResponseCodeType.InvalidBody);

            var sessions = Registry.GetZoneSessions(zone.Key, data.Key);
            if (!Registry.LoadBalancers.TryGetValue(zone.Key, out var serviceLoadBalancers)
                || !serviceLoadBalancers.TryGetValue(data.Key, out LoadBalancerType loadBalancerType))
            {
                loadBalancerType = LoadBalancerType.NoLoadBalance;
            }

            ServiceSession hitSession = Core.LoadBalancing.LoadBalancer.Lease(loadBalancerType, sessions, httpContext.Connection.RemoteIpAddress.GetHashCode());
            if (hitSession == null) // 负载均衡器返回无可用业务备选 (业务已过期或不存在)
            {
                return PacketHandler.MakeResponse(ResponseCodeType.SvcNotFoundOrUnavailable);
            }
            hitSession.IncrementFinishedRequests();

            return PacketHandler.MakeResponse(ResponseCodeType.Ok, ByteString.CopyFromUtf8(hitSession.Service.Url));
        }


        /*
         *  子节点获取配置
         */
        private static async Task<byte[]> GetConfig(DatabaseContext databaseContext, Zone zone, RpcGetConfigRequest data)
        {
            if (data == null || string.IsNullOrEmpty(data.Key))
                return PacketHandler.MakeResponse(ResponseCodeType.InvalidBody);

            ServiceConfig config = await databaseContext.Configs.Where(c => c.Zone == zone.Key && c.Key == data.Key).FirstOrDefaultAsync().ConfigureAwait(false);
            if (config == null) return PacketHandler.MakeResponse(ResponseCodeType.RpcConfigNotFound);

            if (!string.IsNullOrEmpty(data.Hash) && data.Hash == config.Hash) return PacketHandler.MakeResponse(ResponseCodeType.Ok);

            RpcConfigProto rpcConfigProto = new() {
                Zone = zone.Key,
                Key = data.Key,
                Hash = config.Hash,
                Context = config.Context,
                LastModifiedTimestamp = Utils.ToUnixTimeMilliseconds(config.LastModified),
            };
            return PacketHandler.MakeResponse(ResponseCodeType.Ok, rpcConfigProto.ToByteString());
        }


        /*
         *  子节点设置配置
         */
        private static async Task<byte[]> SetConfig(DatabaseContext databaseContext, Zone zone, RpcSetConfigRequest data, ILogger<ApiController> logger)
        {
            if (data == null || string.IsNullOrEmpty(data.Key))
                return PacketHandler.MakeResponse(ResponseCodeType.InvalidBody);

            ServiceConfig config = await databaseContext.Configs.Where(c => c.Zone == zone.Key && c.Key == data.Key).FirstOrDefaultAsync().ConfigureAwait(false);
            if (string.IsNullOrEmpty(data.Context))
            {
                if (config == null) return PacketHandler.MakeResponse(ResponseCodeType.Ok);
                databaseContext.Configs.Remove(config);
            }
            else
            {
                bool needsAdd = config == null;
                if (needsAdd) config = new ServiceConfig();

                string hash = Utils.Sha1Hash(data.Context);
                if (hash == config.Hash) return PacketHandler.MakeResponse(ResponseCodeType.Ok);

                config.Zone = zone.Key;
                config.Key = data.Key;
                config.Context = data.Context;
                config.Hash = hash;
                config.LastModified = DateTime.UtcNow;

                if (needsAdd) databaseContext.Configs.Add(config);
            }
            try
            {
                await databaseContext.SaveChangesAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                LoggingWrapper.LogError(logger, "saving to database", exception);
                return PacketHandler.MakeResponse(ResponseCodeType.RpcOperationFailed);
            }
            return PacketHandler.MakeResponse(ResponseCodeType.Ok);
        }
    }
}
