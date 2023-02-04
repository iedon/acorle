using System;
using System.Net;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Acorle.Core;
using Acorle.Models.Contexts;
using Microsoft.EntityFrameworkCore;


[assembly: CLSCompliant(false)]
namespace Acorle
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            // 读取并注入允许的反向代理主机配置
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                var allowedProxyNetworks = Configuration.GetSection("AllowedProxyNetworks").AsEnumerable().Where(cidr => cidr.Value != null).Select(cidr => cidr);
                foreach (var cidr in allowedProxyNetworks)
                {
                    string[] split = cidr.Value.Split('/');
                    if (split.Length == 2)
                    {
                        options.KnownNetworks.Add(new IPNetwork(IPAddress.Parse(split[0].Trim()), int.Parse(split[1].Trim())));
                    }
                }
                options.ForwardLimit = Configuration.GetValue("XForwardedForLimit", 1);
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            });

            var dbSettings = Configuration.GetSection("DbSettings");
            string connectionString = dbSettings.GetValue<string>("ConnectionString");
            services.AddDbContextPool<DatabaseContext>(options => options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

            // 配置其他信息
            Constants.RpcServiceDefaultWeight = Configuration.GetValue("RpcServiceDefaultWeight", 1);
            Constants.RegistrySyncIntervalSeconds = Configuration.GetValue("RegistrySyncIntervalSeconds", 3600U);
            Constants.EnableSessionContextStat = Configuration.GetValue("EnableSessionContextStat", false);
            Constants.MaxBodyLengthBytes = Configuration.GetValue("MaxBodyLengthBytes", 4U * 1024U * 1024U);
            Constants.RpcAntiReplaySeconds = Configuration.GetValue("RpcAntiReplaySeconds", 600U);
            Constants.AppName = Assembly.GetEntryAssembly().GetName().Name;
            Constants.AppVersion = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
            Constants.RpcHttpClientUserAgent = Constants.AppName + "/" + Constants.AppVersion;
            Constants.RpcHttpClientLifeTimeSeconds = Configuration.GetValue("RpcHttpClientLifeTimeSeconds", 300D);

            // 注入子节点访问客户端
            services.AddHttpClient<RpcHttpClient>().SetHandlerLifetime(TimeSpan.FromSeconds(Constants.RpcHttpClientLifeTimeSeconds));

            // 注入路由和控制器
            services.AddRouting();
            services.AddControllers()
                .AddJsonOptions(options => {
                    options.JsonSerializerOptions.WriteIndented = Constants.JsonSerializerOptionsGlobal.WriteIndented;
                    options.JsonSerializerOptions.PropertyNamingPolicy = Constants.JsonSerializerOptionsGlobal.PropertyNamingPolicy;
                    options.JsonSerializerOptions.DictionaryKeyPolicy = Constants.JsonSerializerOptionsGlobal.DictionaryKeyPolicy;
                })
                .ConfigureApiBehaviorOptions(options => options.SuppressModelStateInvalidFilter = true);

            // 注入后台计划任务
            if (Constants.RegistrySyncIntervalSeconds != 0) services.AddHostedService<RegistrySync>();
        }

        public static void Configure(IApplicationBuilder app)
        {
            app.UseForwardedHeaders();

            // 设置全局异常捕获与统一错误处理
            app.UseExceptionHandler(ErrorHandler.commonErrorHandler);
            app.UseStatusCodePages(ErrorHandler.commonErrorHandler);

            // 设置路由与 Endpoint
            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        }
    }
}
