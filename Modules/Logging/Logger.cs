using Serilog;

namespace sharwapi.Core.Modules.Logging;

/// <summary>
/// 日志服务
/// 提供应用程序的日志记录功能
/// </summary>
public static class Logger
{
    /// <summary>
    /// 初始化全局 Serilog 配置
    /// </summary>
    /// <param name="configuration">应用程序配置对象</param>
    public static void Initialize(Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();
    }
}
