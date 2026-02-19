using Microsoft.Extensions.Logging;
using NuGet.Versioning;
using sharwapi.Contracts.Core;

namespace sharwapi.Core.Modules.PluginManagement;

/// <summary>
/// 插件依赖检查器
/// 负责检查插件的依赖关系是否满足
/// </summary>
public class PluginDependencyChecker
{
    private readonly ILogger _logger;

    /// <summary>
    /// 初始化依赖检查器
    /// </summary>
    /// <param name="logger">日志记录器</param>
    public PluginDependencyChecker(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 检查所有插件的依赖关系
    /// </summary>
    /// <param name="plugins">所有候选插件列表</param>
    /// <returns>通过依赖检查的有效插件列表</returns>
    public List<IApiPlugin> CheckDependencies(List<IApiPlugin> plugins)
    {
        // 构建插件名称到版本号的映射，用于依赖检查
        var allCandidatePluginsMap = plugins.ToDictionary(p => p.Name, p => p.Version);
        var validPlugins = new List<IApiPlugin>();

        // 遍历每个插件，检查其依赖是否满足
        foreach (var plugin in plugins)
        {
            if (CheckPluginDependencies(plugin, allCandidatePluginsMap))
            {
                validPlugins.Add(plugin);
            }
        }

        // 如果有插件因依赖问题被移除，记录警告日志
        if (validPlugins.Count < plugins.Count)
        {
            var removedCount = plugins.Count - validPlugins.Count;
            _logger.LogWarning("{Count} plugins were unloaded due to missing or incompatible dependencies.", removedCount);
        }

        return validPlugins;
    }

    /// <summary>
    /// 检查单个插件的依赖关系
    /// </summary>
    /// <param name="plugin">要检查的插件</param>
    /// <param name="allCandidatePluginsMap">所有候选插件的映射</param>
    /// <returns>如果依赖满足返回 true，否则返回 false</returns>
    private bool CheckPluginDependencies(IApiPlugin plugin, Dictionary<string, string> allCandidatePluginsMap)
    {
        // 第一阶段：声明式强依赖检查
        foreach (var dependency in plugin.Dependencies)
        {
            if (!CheckSingleDependency(plugin, dependency, allCandidatePluginsMap))
            {
                return false;
            }
        }

        // 第二阶段：自定义验证逻辑
        try
        {
            if (!plugin.ValidateDependency(allCandidatePluginsMap))
            {
                _logger.LogWarning("Plugin '{PluginName}' rejected loading during validation check.", plugin.Name);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Plugin '{PluginName}' threw an exception during dependency validation.", plugin.Name);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 检查单个依赖
    /// </summary>
    private bool CheckSingleDependency(IApiPlugin plugin, KeyValuePair<string, string> dependency, Dictionary<string, string> allCandidatePluginsMap)
    {
        // 提取依赖名称和版本范围
        string depName = dependency.Key;
        string depRangeStr = dependency.Value ?? string.Empty;

        // 检查依赖插件是否存在
        if (!allCandidatePluginsMap.TryGetValue(depName, out var loadedVersionStr))
        {
            _logger.LogError("Plugin '{PluginName}' failed to load. Missing dependency: '{DepName}'.", plugin.Name, depName);
            return false;
        }

        // 解析已加载的插件版本号
        if (!NuGetVersion.TryParse(loadedVersionStr, out var loadedVersion))
        {
            _logger.LogError("Plugin '{PluginName}' depends on '{DepName}', but the loaded version '{LoadedVer}' of dependency '{DepName}' has an invalid format.", 
                plugin.Name, depName, loadedVersionStr, depName);
            return false;
        }

        // 解析依赖要求的版本范围
        if (!VersionRange.TryParse(depRangeStr, out var requiredRange))
        {
            // 尝试处理浮动版本（如 1.*）
            if (FloatRange.TryParse(depRangeStr, out var floatRange))
            {
                // 检查浮动版本是否满足
                if (!floatRange.Satisfies(loadedVersion))
                {
                    _logger.LogError("Plugin '{PluginName}' requires '{DepName}' version '{DepRange}' (Floating), but loaded version '{LoadedVer}' is incompatible.", 
                        plugin.Name, depName, depRangeStr, loadedVersionStr);
                    return false;
                }
            }
            else
            {
                // 无法解析的版本要求格式
                _logger.LogError("Plugin '{PluginName}' has an invalid dependency version format for '{DepName}': '{DepRange}'.", 
                    plugin.Name, depName, depRangeStr);
                return false;
            }
        }
        else if (!requiredRange!.Satisfies(loadedVersion))
        {
            // 标准版本范围检查
            _logger.LogError("Plugin '{PluginName}' requires '{DepName}' version '{DepRange}', but loaded version '{LoadedVer}' is incompatible.", 
                plugin.Name, depName, depRangeStr, loadedVersionStr);
            return false;
        }

        return true;
    }
}
