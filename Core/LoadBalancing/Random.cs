using System;
using System.Collections.Concurrent;
using System.Linq;
using Acorle.Models;


namespace Acorle.Core.LoadBalancing
{
    public static partial class LoadBalancer
    {
        private static readonly Random random = new();
        public static ServiceSession Random(ConcurrentDictionary<string, ServiceSession> sessions)
        {
            var availableEntries = sessions.Where(e => e.Value.Service.ExpireTime > DateTime.UtcNow).AsEnumerable();
            if (!availableEntries.Any()) return null;
            return availableEntries.ElementAt(random.Next(availableEntries.Count())).Value;
        }
    }
}
