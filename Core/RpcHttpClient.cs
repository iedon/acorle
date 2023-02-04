using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Google.Protobuf;
using Acorle.Models;


namespace Acorle.Core
{
    public class RpcHttpClient
    {
        public HttpClient Client { get; }

        public RpcHttpClient(HttpClient client)
        {
            client.DefaultRequestHeaders.Add("user-agent", Constants.RpcHttpClientUserAgent);
            Client = client;
        }

        /* 如果任务超时，会由 PostAsync Task 发出 TaskCanceledException */
        public async Task<ResponsePacket> SendRpcRequest(Uri serviceUrl, ByteString data, uint timeout, string zone, string secret, string key, string remoteIp, int remotePort, IEnumerable<HeaderKVPair> headers)
        {
            Client.Timeout = TimeSpan.FromSeconds(timeout);
            RpcRequestOut packetOutbound = PacketHandler.MakeRpcRequestOut(data, zone, secret, key, remoteIp, remotePort, headers);

            using var content = new ByteArrayContent(packetOutbound.ToByteArray());
            content.Headers.ContentType = new MediaTypeHeaderValue(Constants.ProtobufContentType);
            using var response = await Client.PostAsync(serviceUrl, content).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            try
            {
                using var httpBody = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                // 这里使用流解析响应数据包，提高性能
                return ResponsePacket.Parser.ParseFrom(httpBody);
            }
            catch
            {
                return null;
            }
        }
    }
}
