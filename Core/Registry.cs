using System.Collections.Concurrent;
using Acorle.Models;
using Acorle.Models.Contexts;


namespace Acorle.Core
{
    public static class Registry
    {
        // 业务会话上下文结构
        public static ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentDictionary<string, ServiceSession>>> Contexts = new();

        // 通过区域 ID，业务键获取业务会话上下文，没有则会新建
        public static ConcurrentDictionary<string, ServiceSession> GetZoneSessions(string zone, string serviceKey)
        {
            var entries = Contexts.GetOrAdd(zone, key => new ConcurrentDictionary<string, ConcurrentDictionary<string, ServiceSession>>());
            return entries.GetOrAdd(serviceKey, key => new ConcurrentDictionary<string, ServiceSession>());
        }

        // 区域上下文结构
        public static ConcurrentDictionary<string, Zone> Zones = new();

        // 负载均衡上下文结构
        public static ConcurrentDictionary<string, ConcurrentDictionary<string, LoadBalancerType>> LoadBalancers = new();
    }
}
