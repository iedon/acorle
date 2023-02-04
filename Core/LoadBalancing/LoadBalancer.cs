using System.Collections.Concurrent;
using Acorle.Models;


namespace Acorle.Core.LoadBalancing
{
    public static partial class LoadBalancer
    {
        public static ServiceSession Lease(LoadBalancerType type,
								    ConcurrentDictionary<string, ServiceSession> sessions,
								    int sourceIpAddressHashCode)
        => type switch
            {
                LoadBalancerType.NoLoadBalance => NoLoadBalance(sessions),
                LoadBalancerType.SmoothWeightRoundRobin => SmoothWeightRoundRobin(sessions),
                LoadBalancerType.LeastConnection => LeastConnections(sessions),
                LoadBalancerType.Random => Random(sessions),
                LoadBalancerType.SourceIpHash => SourceIpHash(sourceIpAddressHashCode, sessions),
                _ => NoLoadBalance(sessions),
            };
    }
}
