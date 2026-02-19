using Microsoft.AspNetCore.Builder;
using sharwapi.Contracts.Core;

namespace sharwapi.Core.Modules.Middleware;

/// <summary>
/// 插件中间件管道配置
/// </summary>
public static class MiddlewarePipeline
{
    /// <summary>
    /// 配置所有插件的中间件
    /// </summary>
    /// <param name="app">Web 应用程序</param>
    /// <param name="plugins">已加载的插件列表</param>
    public static void Configure(WebApplication app, List<IApiPlugin> plugins)
    {
        app.Logger.LogInformation("Configuring plugin middleware...");
        
        foreach (var plugin in plugins)
        {
            plugin.Configure(app);
        }
    }
}
