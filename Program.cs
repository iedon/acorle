using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Web;


namespace Acorle
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            // 首先初始化一个 NLOG 以便捕捉并记录所有启动过程中的错误，程序退出时销毁
            var logger = NLogBuilder.ConfigureNLog("nlog.config").GetCurrentClassLogger();
            try
            {
                await CreateHostBuilder(args).Build().RunAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // NLOG: 捕捉初始化错误
                logger.Error(ex, "Application stopped due to exception.");
                throw;
            }
            finally
            {
                // 确保在程序退出之前清理并停止内部定时器和线程 (避免在 Linux 上发生 segmentation fault)
                NLog.LogManager.Shutdown();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                })
                .ConfigureLogging(logging => // 清除所有默认的日志提供方，以便只使用 NLOG
                {
                    logging.ClearProviders();
                })
                .UseNLog();  // 初始化 NLOG 注入
    }
}
