using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Google.Protobuf;
using Acorle.Core;
using Acorle.Models;
using Acorle.Models.Contexts;
using static Acorle.Models.RequestPacket.Types;
using static Acorle.Models.ResponsePacket.Types;


namespace Acorle.Controllers
{
    // 业务控制器
    public static class ServiceController
    {
        /*
         * 业务入口
         */
        public static async Task<byte[]> ServiceMain(RequestPacket requestPacket, HttpContext httpContext, RpcHttpClient httpClient, ILogger<ApiController> logger)
        {
            ServiceRequest request;
            try
            {
                request = ServiceRequest.Parser.ParseFrom(requestPacket.Data);
            }
            catch
            {
                return PacketHandler.MakeResponse(ResponseCodeType.BadRequest);
            }

            if (!PacketHandler.ValidateServiceRequest(request))
            {
                // Bad Request 不记录日志，以防垃圾信息堆积。
                return PacketHandler.MakeResponse(ResponseCodeType.BadRequest);
            }

            return (await ServiceMain(requestPacket.Zone, request.Key, request.Data, httpContext, httpClient, logger).ConfigureAwait(false)).ToByteArray();
        }


        public static async Task<ResponsePacket> ServiceMain(string requestZone, string requestKey, ByteString data, HttpContext httpContext, RpcHttpClient httpClient,
                                                               ILogger<ApiController> logger)
        {
            ResponsePacket response = PacketHandler.MakeEmptyResponsePacket();
            if (!Registry.Zones.TryGetValue(requestZone, out Zone zone))
            {
                // zone 找不到就不记录日志。否则数据库中会充满这些未注册的 zone 的垃圾数据。
                response.Code = ResponseCodeType.SvcInvalidZone;
                return response;
            }

            // 获取并处理最终用户的 User-Agent 头部。因为 HTTP 允许有多个相同的头，而 UA 正常情况下只发送一个，因此只取一个值
            string userAgentSingle = httpContext.Request.Headers.TryGetValue("user-agent", out var uaHeaders) && uaHeaders.Any() ? uaHeaders.ElementAt(0) : string.Empty;

            // 初始化请求计时器并开始计时处理时间
            DateTime requestBegin = DateTime.UtcNow;
            Stopwatch requestWatch = null;
            ServiceLog serviceLog = null;
            if (zone.LogUserRequest)
            {
                requestWatch = new();
                requestWatch.Start();
                serviceLog = new ServiceLog
                {
                    RequestBegin = requestBegin,
                    Zone = requestZone,
                    Key = requestKey,
                    Ip = httpContext.Connection.RemoteIpAddress.ToString(),
                    Port = httpContext.Connection.RemotePort,
                    UA = userAgentSingle,
                    ResponseCode = response.Code
                };
            }

            var sessions = Registry.GetZoneSessions(requestZone, requestKey);
            if (!Registry.LoadBalancers.TryGetValue(zone.Key, out var serviceLoadBalancers)
                || !serviceLoadBalancers.TryGetValue(requestKey, out LoadBalancerType loadBalancerType))
            {
                loadBalancerType = LoadBalancerType.NoLoadBalance;
            }

            ServiceSession hitSession = Core.LoadBalancing.LoadBalancer.Lease(loadBalancerType, sessions, httpContext.Connection.RemoteIpAddress.GetHashCode());
            if (hitSession == null || hitSession.Service.IsPrivate) // 负载均衡器返回无可用业务备选 (业务已过期或不存在)
            {
                response.Code = ResponseCodeType.SvcNotFoundOrUnavailable;
                if (zone.LogUserRequest)
                {
                    serviceLog.ResponseCode = response.Code;
                    LogRequest(logger, serviceLog, requestWatch);
                }
                return response;
            }

            hitSession.IncrementCurrentRequests();
            try
            {
                List<HeaderKVPair> headers = new();
                foreach (var header in httpContext.Request.Headers)
                {
                    HeaderKVPair headerKVPair = new() { Key = header.Key };
                    headerKVPair.Values.Add(header.Value.AsEnumerable());
                    headers.Add(headerKVPair);
                }

                HeaderKVPair requestMethodKVPair = new() { Key = "x-http-method" };
                requestMethodKVPair.Values.Add(httpContext.Request.Method);
                headers.Add(requestMethodKVPair);

                var rpcResponse = await httpClient.SendRpcRequest(new Uri(hitSession.Service.Url), data, zone.RpcTimeoutSeconds, requestZone, zone.Secret, requestKey, httpContext.Connection.RemoteIpAddress.ToString(), httpContext.Connection.RemotePort, headers).ConfigureAwait(false);
                if (rpcResponse == null || rpcResponse.Code != ResponseCodeType.Ok)
                {
                    hitSession.IncrementFailedRequests();
                    response.Code = ResponseCodeType.RpcResponseError;
                    if (zone.LogUserRequest)
                    {
                        serviceLog.ResponseCode = response.Code;
                        LogRequest(logger, serviceLog, requestWatch);
                    }
                    return response;
                }
                if (zone.LogUserRequest)
                {
                    serviceLog.ResponseCode = rpcResponse.Code;
                    LogRequest(logger, serviceLog, requestWatch);
                }
                return rpcResponse;
            }
            catch (TaskCanceledException)
            {
                hitSession.IncrementFailedRequests();
                response.Code = ResponseCodeType.RpcResponseTimedout;
                if (zone.LogUserRequest)
                {
                    serviceLog.ResponseCode = response.Code;
                    LogRequest(logger, serviceLog, requestWatch);
                }
                return response;
            }
            catch (HttpRequestException)
            {
                hitSession.IncrementFailedRequests();
                response.Code = ResponseCodeType.RpcNetworkException;
                if (zone.LogUserRequest)
                {
                    serviceLog.ResponseCode = response.Code;
                    LogRequest(logger, serviceLog, requestWatch);
                }
                return response;
            }
            catch (Exception exception)
            {
                hitSession.IncrementFailedRequests();
                LoggingWrapper.LogError(logger, "sending rpc request", exception);
                response.Code = ResponseCodeType.ServerException;
                if (zone.LogUserRequest)
                {
                    serviceLog.ResponseCode = response.Code;
                    LogRequest(logger, serviceLog, requestWatch);
                }
                return response;
            }
            finally
            {
                hitSession.DecrementCurrentRequests();
                hitSession.IncrementFinishedRequests();
            }
        }


        private static void LogRequest(ILogger<ApiController> logger, ServiceLog requestLog, Stopwatch requestWatch)
        {
            requestWatch.Stop();
            requestLog.ElapsedMs = requestWatch.ElapsedMilliseconds;
            LoggingWrapper.LogAccess(logger, requestLog.ElapsedMs, requestLog.Zone, requestLog.Key, requestLog.ResponseCode, requestLog.Ip, requestLog.UA);
        }

    }
}
