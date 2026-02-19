using Microsoft.Extensions.Configuration;
using Serilog;

namespace sharwapi.Core.Modules.Configuration;

/// <summary>
/// Serilog 日志配置
/// 负责初始化和管理全局 Serilog 日志配置
/// </summary>
public static class SerilogSetup
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

    /// <summary>
    /// 配置日志输出到控制台
    /// </summary>
    /// <param name="loggerConfiguration">日志配置构建器</param>
    /// <returns>日志配置构建器</returns>
    public static LoggerConfiguration AddConsole(this LoggerConfiguration loggerConfiguration)
    {
        return loggerConfiguration.WriteTo.Console();
    }

    /// <summary>
    /// 配置日志输出到文件
    /// </summary>
    /// <param name="loggerConfiguration">日志配置构建器</param>
    /// <param name="path">日志文件路径</param>
    /// <returns>日志配置构建器</returns>
    public static LoggerConfiguration AddFile(this LoggerConfiguration loggerConfiguration, string path)
    {
        return loggerConfiguration.WriteTo.File(
            path: path,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            fileSizeLimitBytes: 10485760,
            rollOnFileSizeLimit: true
        );
    }
}
