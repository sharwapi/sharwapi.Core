using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using sharwapi.Contracts.Core;

namespace sharwapi.Core.Modules.Routing;

/// <summary>
/// 路由前缀解析器
/// 负责解析插件的路由前缀
/// </summary>
public static class RoutePrefixResolver
{
    /// <summary>
    /// 解析插件的路由前缀
    /// 根据插件配置和应用程序配置确定路由前缀
    /// </summary>
    /// <param name="plugin">插件实例</param>
    /// <param name="configuration">应用程序配置</param>
    /// <param name="app">Web 应用程序</param>
    /// <returns>路由构建器（可能是分组路由）</returns>
    public static IEndpointRouteBuilder Resolve(IApiPlugin plugin, IConfiguration configuration, WebApplication app)
    {
        // 如果插件不启用自动路由前缀，直接返回 app
        if (!plugin.UseAutoRoutePrefix)
        {
            return app;
        }

        // 默认使用插件名称作为路由前缀
        string routePrefix = plugin.Name;

        // 检查是否存在路由前缀覆盖配置
        var overrideRoute = configuration.GetValue<string>($"RouteOverride:{plugin.Name}");
        if (!string.IsNullOrEmpty(overrideRoute))
        {
            // 验证覆盖值只包含字母和数字
            if (System.Text.RegularExpressions.Regex.IsMatch(overrideRoute, "^[a-zA-Z0-9]+$"))
            {
                routePrefix = overrideRoute;
                app.Logger.LogInformation("Route prefix for plugin '{PluginName}' overridden to '{RoutePrefix}'", plugin.Name, routePrefix);
            }
            else
            {
                // 无效的覆盖值，回退到插件名称
                app.Logger.LogWarning("Invalid route override '{OverrideRoute}' for plugin '{PluginName}'. Only alphanumeric characters (A-Z, a-z, 0-9) are allowed. Falling back to default.", overrideRoute, plugin.Name);
            }
        }

        // 返回带有前缀的路由组
        return app.MapGroup($"/{routePrefix.TrimStart('/')}");
    }
}
