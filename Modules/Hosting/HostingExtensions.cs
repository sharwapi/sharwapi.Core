using Microsoft.Extensions.Hosting;
using Serilog;

namespace sharwapi.Core.Modules.Hosting;

/// <summary>
/// 主机扩展方法
/// </summary>
public static class HostingExtensions
{
    /// <summary>
    /// 使用 Serilog 接管系统日志
    /// </summary>
    /// <param name="hostBuilder">主机构建器</param>
    /// <returns>主机构建器</returns>
    public static IHostBuilder UseSerilogLogging(this IHostBuilder hostBuilder)
    {
        return hostBuilder.UseSerilog();
    }

    /// <summary>
    /// 配置主机选项
    /// </summary>
    /// <param name="builder">Web 应用程序构建器</param>
    /// <param name="shutdownTimeoutSeconds">关闭超时时间（秒）</param>
    public static void ConfigureHostOptions(this WebApplicationBuilder builder, int shutdownTimeoutSeconds = 30)
    {
        builder.Services.Configure<HostOptions>(opts =>
        {
            opts.ShutdownTimeout = TimeSpan.FromSeconds(shutdownTimeoutSeconds);
        });
    }
}
