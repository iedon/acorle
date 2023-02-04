using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Google.Protobuf;
using Acorle.Core;
using Acorle.Models;
using Acorle.Models.Contexts;
using static Acorle.Models.ResponsePacket.Types;
using static Acorle.Models.RequestPacket.Types;


namespace Acorle.Controllers
{
    /* API 控制器 */
    [ApiController]
    public class ApiController : ControllerBase
    {
        private readonly DatabaseContext databaseContext;
        private readonly RpcHttpClient httpClient;
        private readonly ILogger<ApiController> logger;

        public ApiController(DatabaseContext databaseContext, RpcHttpClient httpClient, ILogger<ApiController> logger)
        {
            this.databaseContext = databaseContext;
            this.httpClient = httpClient;
            this.logger = logger;
        }


        protected IActionResult ProtobufResponse(byte[] data) => File(data, Constants.ProtobufContentType);


        [Route("~/")]
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Produces(Constants.JsonContentType)]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult StatisticsMain()
        {
            var response = new List<Statistics>();
            if (Constants.EnableSessionContextStat)
            {
                foreach (var zone in Registry.Contexts)
                {
                    var element = new Statistics
                    {
                        Zone = zone.Key,
                        Services = new List<ServiceElement>(),
                    };
                    foreach (var entries in zone.Value)
                    {
                        foreach (var hash in entries.Value)
                        {
                            element.Services.Add(new ServiceElement
                            {
                                Hash = hash.Key,
                                Key = entries.Key,
                                Name = hash.Value.Service.Name,
                                Url = hash.Value.Service.Url,
                                Weight = hash.Value.Service.Weight,
                                IsPrivate = hash.Value.Service.IsPrivate,
                                AddedTime = hash.Value.Service.AddedTime,
                                ExpireTime = hash.Value.Service.ExpireTime,
                                CurrentRequests = hash.Value.CurrentRequests,
                                FailedRequests = hash.Value.FailedRequests,
                                FinishedRequests = hash.Value.FinishedRequests,
                            });
                        }
                    }
                    response.Add(element);
                }
            }
            return new JsonResult(response);
        }


        private ResponseCodeType PreProcessRequest(out RequestPacket requestPacket)
        {
            requestPacket = null;

            if (Request.ContentLength != null && Request.ContentLength > Constants.MaxBodyLengthBytes)
                return ResponseCodeType.InvalidBody;
            
            try
            {
                using var stream = Request.BodyReader.AsStream();
                requestPacket = RequestPacket.Parser.ParseFrom(stream);
            }
            catch (InvalidProtocolBufferException)
            {
                return ResponseCodeType.BadRequest;
            }
            catch (Exception exception)
            {
                LoggingWrapper.LogError(logger, "reading body", exception);
                return ResponseCodeType.ServerException;
            }

            if (!PacketHandler.ValidateRequestPacket(requestPacket))
            {
                return ResponseCodeType.BadRequest;
            }
            return ResponseCodeType.Ok;
        }


        /*
         * POST - RPC 请求路由
         */
        [Route("~/rpc")]
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Produces(Constants.ProtobufContentType)]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> RpcMain()
        {
            ResponseCodeType status = PreProcessRequest(out RequestPacket requestPacket);
            if (status != ResponseCodeType.Ok)
                return ProtobufResponse(PacketHandler.MakeResponse(status));

            return requestPacket.Action switch
            {
                ActionType.RpcList or
                ActionType.RpcGet or
                ActionType.RpcCall or
                ActionType.RpcRegister or
                ActionType.RpcDestroy or
                ActionType.RpcConfigGet or
                ActionType.RpcConfigSet =>
                    ProtobufResponse(await RpcController.RpcMain(requestPacket, databaseContext, HttpContext, logger).ConfigureAwait(false)),
                _ => ProtobufResponse(PacketHandler.MakeResponse(ResponseCodeType.MethodNotAllowed)),
            };
        }


        /*
         * POST - 业务请求路由，通过 Acorle 协议
         */
        [Route("~/")]
        [HttpPost]
        [HttpPut]
        [HttpPatch]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [Produces(Constants.ProtobufContentType)]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> SvcMain()
        {
            ResponseCodeType status = PreProcessRequest(out RequestPacket requestPacket);
            if (status != ResponseCodeType.Ok)
                return ProtobufResponse(PacketHandler.MakeResponse(status));

            return requestPacket.Action switch
            {
                ActionType.SvcRequest => ProtobufResponse(await ServiceController.ServiceMain(requestPacket, HttpContext, httpClient, logger).ConfigureAwait(false)),
                _ => ProtobufResponse(PacketHandler.MakeResponse(ResponseCodeType.MethodNotAllowed)),
            };
        }


        /*
         * GET - 业务请求路由，直接通过 HTTP 访问，用于无数据提交的直接访问
         */
        [Route("~/service/{zone}/{key}")]
        [HttpGet]
        [HttpHead]
        [HttpOptions]
        [HttpDelete]
        public async Task<IActionResult> SvcDirectGet(string zone, string key)
            => await SvcDirect(zone, key, ByteString.Empty).ConfigureAwait(false);


        /*
         * POST - 业务请求路由，直接通过 HTTP 访问，并向后端提交数据
         */
        [Route("~/service/{zone}/{key}")]
        [HttpPost]
        public async Task<IActionResult> SvcDirectPost(string zone, string key)
        {
            ByteString body;
            try
            {
                using var bodyStream = Request.BodyReader.AsStream();
                body = await ByteString.FromStreamAsync(bodyStream).ConfigureAwait(false);
            }
            catch
            {
                return PlainError(ResponseCodeType.BadRequest);
            }
            return await SvcDirect(zone, key, body).ConfigureAwait(false);
        }


        private async Task<IActionResult> SvcDirect(string zone, string key, ByteString data)
        {
            ResponsePacket responsePacket = await ServiceController.ServiceMain(zone, key, data, HttpContext, httpClient, logger).ConfigureAwait(false);
            if (responsePacket.Code != ResponseCodeType.Ok)
            {
                return PlainError(responsePacket.Code);
            }

            string contentType = Constants.StreamContentType;
            int statusCode = Constants.DefaultHttpResponseCode;
            foreach (var header in responsePacket.Headers)
            {
                if (header.Key.ToLower() == "content-type" && header.Values.Count != 0)
                {
                    contentType = header.Values[0];
                    continue;
                }
                if (header.Key.ToLower() == "status" && header.Values.Count != 0)
                {
                    if (!int.TryParse(header.Values[0].TrimStart().Split('\x20')[0], out statusCode) || statusCode == 0)
                    {
                        statusCode = Constants.DefaultHttpResponseCode;
                    }
                    continue;
                }
                string[] values = new string[header.Values.Count];
                header.Values.CopyTo(values, 0);
                HttpContext.Response.Headers.Add(header.Key, values);
            }
            HttpContext.Response.StatusCode = statusCode;

            return File(responsePacket.Data.ToByteArray(), contentType);
        }

        private IActionResult PlainError(ResponseCodeType responseCode)
        {
            HttpContext.Response.Headers.Add("Cache-Control", "no-store,no-cache");
            HttpContext.Response.Headers.Add("Pragma", "no-cache");
            HttpContext.Response.StatusCode = responseCode switch
            {
                ResponseCodeType.RpcResponseError or ResponseCodeType.ServerException => StatusCodes.Status500InternalServerError,
                ResponseCodeType.InvalidBody or ResponseCodeType.BadRequest => StatusCodes.Status400BadRequest,
                ResponseCodeType.Forbidden => StatusCodes.Status403Forbidden,
                ResponseCodeType.SvcNotFoundOrUnavailable or ResponseCodeType.RpcInvalidZone or ResponseCodeType.SvcInvalidZone or ResponseCodeType.NotFound => StatusCodes.Status404NotFound,
                ResponseCodeType.MethodNotAllowed => StatusCodes.Status405MethodNotAllowed,
                ResponseCodeType.BadGateway => StatusCodes.Status502BadGateway,
                _ => StatusCodes.Status503ServiceUnavailable,
            };
            return new JsonResult(PacketHandler.MakePlainErrorObject(responseCode));
        }

    }
}
