using Microsoft.Extensions.Configuration;

namespace sharwapi.Core.Modules.Configuration;

/// <summary>
/// 应用程序配置构建器
/// 负责构建和管理应用程序的配置信息
/// </summary>
public static class AppConfiguration
{
    /// <summary>
    /// 构建应用程序配置
    /// </summary>
    /// <returns>配置构建器实例</returns>
    public static IConfigurationBuilder Build()
    {
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
    }

    /// <summary>
    /// 获取 API 名称
    /// </summary>
    /// <param name="configuration">应用程序配置</param>
    /// <returns>API 名称</returns>
    public static string GetApiName(IConfiguration configuration)
    {
        return configuration.GetValue<string>("ApiInfo:Name") ?? "CoreAPI";
    }

    /// <summary>
    /// 获取 API 版本
    /// </summary>
    /// <param name="configuration">应用程序配置</param>
    /// <returns>API 版本</returns>
    public static string GetApiVersion(IConfiguration configuration)
    {
        return configuration.GetValue<string>("ApiInfo:Version") ?? "0.0.0";
    }

    /// <summary>
    /// 获取路由前缀重写配置值
    /// </summary>
    /// <param name="configuration">应用程序配置</param>
    /// <param name="pluginName">插件名称</param>
    /// <returns>路由前缀重写值</returns>
    public static string? GetRouteOverride(IConfiguration configuration, string pluginName)
    {
        return configuration.GetValue<string>($"RouteOverride:{pluginName}");
    }
}
