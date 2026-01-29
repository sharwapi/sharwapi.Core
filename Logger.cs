using Microsoft.Extensions.Configuration;
using Serilog;

namespace sharwapi.Core;

public static class Logger
{
    /// <summary>
    /// 初始化全局 Serilog 配置
    /// </summary>
    /// <param name="configuration">应用程序配置对象</param>
    public static void Initialize(IConfiguration configuration)
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();
    }
}
