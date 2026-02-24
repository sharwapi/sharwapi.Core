using Microsoft.Extensions.Configuration;
using Serilog;

namespace sharwapi.Core.Modules.Logging;

/// <summary>
/// 日志服务
/// 提供应用程序的日志记录功能，通过 appsettings.json 中的 Serilog 节点进行配置。
/// </summary>
public static class Logger
{
    /// <summary>
    /// 初始化全局 Serilog 配置。
    /// 日志输出目标（Console、File 等）均从 appsettings.json 的 Serilog 节点读取，无需硬编码。
    /// </summary>
    /// <param name="configuration">应用程序配置对象</param>
    public static void Initialize(IConfiguration configuration)
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();
    }
}
