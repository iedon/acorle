using System;
using System.Collections.Concurrent;
using System.Linq;
using Acorle.Models;


namespace Acorle.Core.LoadBalancing
{
    public static partial class LoadBalancer
    {
        public static ServiceSession SourceIpHash(int sourceIpAddressHashCode,
                                           ConcurrentDictionary<string, ServiceSession> sessions)
        {
            var availableEntries = sessions.Where(e => e.Value.Service.ExpireTime > DateTime.UtcNow).AsEnumerable();
            
            if (!availableEntries.Any()) return null;

            int count = availableEntries.Count();
            int hashCode = Math.Abs(sourceIpAddressHashCode);
            int index = hashCode % count;
            return availableEntries.ElementAt(index).Value;
        }
    }
}
