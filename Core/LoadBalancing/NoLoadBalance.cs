using System;
using System.Collections.Concurrent;
using System.Linq;
using Acorle.Models;


namespace Acorle.Core.LoadBalancing
{
    public static partial class LoadBalancer
    {
        public static ServiceSession NoLoadBalance(ConcurrentDictionary<string, ServiceSession> sessions)
        {
            var result = sessions.Where(e => e.Value.Service.ExpireTime > DateTime.UtcNow).FirstOrDefault();
            if (string.IsNullOrEmpty(result.Key))
            {
                return null;
            }
            return result.Value;
        }
    }
}
