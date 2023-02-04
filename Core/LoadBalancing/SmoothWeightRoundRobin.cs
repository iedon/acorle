using System;
using System.Collections.Concurrent;
using System.Linq;
using Acorle.Models;


namespace Acorle.Core.LoadBalancing
{
    public static partial class LoadBalancer
    {
        public static ServiceSession SmoothWeightRoundRobin(ConcurrentDictionary<string, ServiceSession> sessions)
        {
            ServiceSession hitSession = null;
            var availableEntries = sessions.Where(e => e.Value.Service.ExpireTime > DateTime.UtcNow).AsEnumerable();
            if (availableEntries.Any())
            {
                // 调整权重
                foreach (var entry in availableEntries)
                {
                    ServiceSession currentSession = entry.Value;
                    currentSession.SetCurrentLoadBalanceWeight(currentSession.CurrentLoadBalanceWeight + currentSession.Service.Weight);
                }

                // 原始权重之和
                int weightSum = 0;
                // 最大当前权重
                int maxCurrentWeight = 0;
                foreach (var entry in availableEntries)
                {
                    ServiceSession currentSession = entry.Value;
                    weightSum += currentSession.Service.Weight;
                    int currentWeight = currentSession.CurrentLoadBalanceWeight;
                    if (currentWeight >= maxCurrentWeight)
                    {
                        hitSession = currentSession;
                        maxCurrentWeight = currentWeight;
                    }
                }

                // 对选中的后端再次设置权重
                hitSession.SetCurrentLoadBalanceWeight(maxCurrentWeight - weightSum);
            }
            return hitSession;
        }
    }
}
