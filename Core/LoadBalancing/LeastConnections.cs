using System;
using System.Collections.Concurrent;
using System.Linq;
using Acorle.Models;


namespace Acorle.Core.LoadBalancing
{
    public static partial class LoadBalancer
    {
        public static ServiceSession LeastConnections(ConcurrentDictionary<string, ServiceSession> sessions)
        {
            ServiceSession hitSession = null;
            var availableEntries = sessions.Where(e => e.Value.Service.ExpireTime > DateTime.UtcNow).AsEnumerable();
            if (availableEntries.Any())
            {
                int leastConnections = int.MaxValue;
                foreach (var entry in availableEntries)
                {
                    ServiceSession currentSession = entry.Value;
                    int currentRequests = currentSession.CurrentRequests;
                    if (currentRequests < leastConnections)
                    {
                        hitSession = currentSession;
                        leastConnections = currentRequests;
                    }
                }
            }
            return hitSession;
        }
    }
}
