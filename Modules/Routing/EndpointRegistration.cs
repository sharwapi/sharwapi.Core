using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using sharwapi.Contracts.Core;

namespace sharwapi.Core.Modules.Routing;

/// <summary>
/// 端点注册
/// 负责注册插件路由和根路径端点
/// </summary>
public static class EndpointRegistration
{
    /// <summary>
    /// 注册所有插件的路由
    /// </summary>
    /// <param name="app">Web 应用程序</param>
    /// <param name="plugins">已加载的插件列表</param>
    /// <param name="configuration">应用程序配置</param>
    public static void RegisterPluginRoutes(WebApplication app, List<IApiPlugin> plugins, IConfiguration configuration)
    {
        app.Logger.LogInformation("Registering plugin routes...");

        // 遍历所有已加载的插件，为每个插件注册路由
        foreach (var plugin in plugins)
        {
            try
            {
                // 解析插件的路由前缀（可能来自配置覆盖）
                var routeBuilder = RoutePrefixResolver.Resolve(plugin, configuration, app);

                // 调用插件的路由注册方法
                plugin.RegisterRoutes(routeBuilder, configuration);

                app.Logger.LogInformation("Loaded Plugin: {PluginName} v{PluginVersion}", plugin.Name, plugin.Version);
            }
            catch (Exception ex)
            {
                app.Logger.LogError(ex, "Plugin '{PluginName}' threw an exception during RegisterRoutes().", plugin.Name);
            }
        }
    }

    /// <summary>
    /// 注册根路径端点
    /// </summary>
    /// <param name="app">Web 应用程序</param>
    /// <param name="apiName">API 名称</param>
    /// <param name="apiVersion">API 版本</param>
    /// <param name="startTime">应用启动时间</param>
    public static void RegisterRootEndpoint(WebApplication app, string apiName, string apiVersion, DateTime startTime)
    {
        app.MapGet("/", () =>
        {
            var uptime = DateTime.UtcNow - startTime;
            return new { apiName, version = apiVersion, runningTime = uptime, message = "Core API running." };
        });
    }
}
