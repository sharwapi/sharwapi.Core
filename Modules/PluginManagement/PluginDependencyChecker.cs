using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;
using sharwapi.Contracts.Core;

namespace sharwapi.Core.Modules.PluginManagement;

/// <summary>
/// 插件依赖检查器
/// 负责检查插件的依赖关系是否满足，并进行拓扑排序以确定加载顺序
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
    /// <returns>通过依赖检查的有效插件列表（已拓扑排序）</returns>
    public List<IApiPlugin> CheckDependencies(List<IApiPlugin> plugins)
    {
        if (plugins.Count == 0)
            return plugins;

        // 步骤1：构建依赖图
        var dependencyGraph = BuildDependencyGraph(plugins);

        // 步骤2：检测循环依赖（使用 Kahn 算法）
        var sortedPlugins = TopologicalSort(dependencyGraph, plugins);
        
        if (sortedPlugins == null)
        {
            // 循环依赖已被 Kahn 算法检测并记录，返回空列表
            return new List<IApiPlugin>();
        }

        // 步骤3：阶段一 - 声明式强依赖检查（基于拓扑排序结果）
        var stageOneValid = new Dictionary<string, IApiPlugin>();
        foreach (var plugin in sortedPlugins)
        {
            if (CheckDeclarativeDependencies(plugin, dependencyGraph, stageOneValid))
            {
                stageOneValid[plugin.Name] = plugin;
            }
            else
            {
                _logger.LogWarning("Plugin '{PluginName}' failed stage one (declarative) dependency check and was removed.", plugin.Name);
            }
        }

        // 步骤4：阶段二 - 自定义验证（只对阶段一通过的插件调用）
        // 传入的是已通过阶段一的"有效候选"，而非全部候选
        var stageTwoValid = new Dictionary<string, IApiPlugin>();
        foreach (var plugin in stageOneValid.Values)
        {
            try
            {
                // 传入阶段一的有效插件列表
                if (!plugin.ValidateDependency(new ReadOnlyDictionary<string, string>(
                    stageOneValid.ToDictionary(p => p.Key, p => p.Value.Version))))
                {
                    _logger.LogWarning("Plugin '{PluginName}' rejected loading during stage two (ValidateDependency) check.", plugin.Name);
                }
                else
                {
                    stageTwoValid[plugin.Name] = plugin;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Plugin '{PluginName}' threw an exception during stage two dependency validation.", plugin.Name);
            }
        }

        // 步骤5：级联剔除 - 如果某个插件被剔除，依赖它的插件也需要重新检查
        var finalValid = ApplyCascadeRemoval(stageTwoValid, dependencyGraph);

        // 记录结果
        if (finalValid.Count < plugins.Count)
        {
            var removedCount = plugins.Count - finalValid.Count;
            _logger.LogWarning("{Count} plugins were unloaded due to missing or incompatible dependencies.", removedCount);
        }

        // 返回拓扑排序后的有效插件列表
        return sortedPlugins.Where(p => finalValid.ContainsKey(p.Name)).ToList();
    }

    /// <summary>
    /// 构建依赖图
    /// </summary>
    private Dictionary<string, HashSet<string>> BuildDependencyGraph(List<IApiPlugin> plugins)
    {
        var graph = new Dictionary<string, HashSet<string>>();

        foreach (var plugin in plugins)
        {
            if (!graph.ContainsKey(plugin.Name))
            {
                graph[plugin.Name] = new HashSet<string>();
            }

            foreach (var dep in plugin.Dependencies)
            {
                graph[plugin.Name].Add(dep.Key);
            }
        }

        return graph;
    }

    /// <summary>
    /// 使用 Kahn 算法进行拓扑排序，同时检测循环依赖
    /// </summary>
    private List<IApiPlugin>? TopologicalSort(Dictionary<string, HashSet<string>> graph, List<IApiPlugin> plugins)
    {
        var pluginMap = plugins.ToDictionary(p => p.Name);
        
        // 计算每个插件的入度（被依赖数）
        var inDegree = new Dictionary<string, int>();
        foreach (var plugin in plugins)
        {
            inDegree[plugin.Name] = 0;
        }

        foreach (var plugin in plugins)
        {
            foreach (var dep in graph[plugin.Name])
            {
                // 如果被依赖的插件存在于候选列表中，增加其入度
                if (inDegree.ContainsKey(dep))
                {
                    inDegree[dep]++;
                }
            }
        }

        // 入度为0的插件可以首先加载（没有插件依赖它）
        var queue = new Queue<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var sorted = new List<string>();

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            sorted.Add(current);

            // 找到所有依赖当前插件的插件
            foreach (var plugin in plugins)
            {
                if (graph[plugin.Name].Contains(current))
                {
                    inDegree[plugin.Name]--;
                    if (inDegree[plugin.Name] == 0)
                    {
                        queue.Enqueue(plugin.Name);
                    }
                }
            }
        }

        // 如果排序后的数量不等于插件数量，说明存在循环依赖
        if (sorted.Count != plugins.Count)
        {
            // 找出循环依赖的插件
            var cyclicPlugins = inDegree.Where(kv => kv.Value > 0).Select(kv => kv.Key);
            _logger.LogError("Circular dependency detected among plugins: {CyclicPlugins}. Loading aborted.", 
                string.Join(", ", cyclicPlugins));
            return null;
        }

        // 按排序结果返回插件列表
        return sorted.Select(name => pluginMap[name]).ToList();
    }

    /// <summary>
    /// 阶段一：声明式强依赖检查
    /// 只检查插件声明的 Dependencies 是否满足
    /// </summary>
    private bool CheckDeclarativeDependencies(IApiPlugin plugin, 
        Dictionary<string, HashSet<string>> graph, 
        Dictionary<string, IApiPlugin> validPlugins)
    {
        foreach (var dependency in plugin.Dependencies)
        {
            if (!CheckSingleDependency(plugin, dependency, validPlugins))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// 检查单个依赖（使用当前有效插件列表）
    /// </summary>
    private bool CheckSingleDependency(IApiPlugin plugin, KeyValuePair<string, string> dependency, Dictionary<string, IApiPlugin> validPlugins)
    {
        string depName = dependency.Key;
        string depRangeStr = dependency.Value ?? string.Empty;

        // 检查依赖插件是否在有效列表中
        if (!validPlugins.TryGetValue(depName, out var depPlugin))
        {
            _logger.LogError("Plugin '{PluginName}' failed to load. Missing dependency: '{DepName}'.", plugin.Name, depName);
            return false;
        }

        // 解析版本
        if (!NuGetVersion.TryParse(depPlugin.Version, out var loadedVersion))
        {
            _logger.LogError("Plugin '{PluginName}' depends on '{DepName}', but the loaded version '{LoadedVer}' has an invalid format.", 
                plugin.Name, depName, depPlugin.Version);
            return false;
        }

        // 解析版本范围
        if (!VersionRange.TryParse(depRangeStr, out var requiredRange))
        {
            if (FloatRange.TryParse(depRangeStr, out var floatRange))
            {
                if (!floatRange.Satisfies(loadedVersion))
                {
                    _logger.LogError("Plugin '{PluginName}' requires '{DepName}' version '{DepRange}' (Floating), but loaded version '{LoadedVer}' is incompatible.", 
                        plugin.Name, depName, depRangeStr, loadedVersion);
                    return false;
                }
            }
            else
            {
                _logger.LogError("Plugin '{PluginName}' has an invalid dependency version format for '{DepName}': '{DepRange}'.", 
                    plugin.Name, depName, depRangeStr);
                return false;
            }
        }
        else if (!requiredRange!.Satisfies(loadedVersion))
        {
            _logger.LogError("Plugin '{PluginName}' requires '{DepName}' version '{DepRange}', but loaded version '{LoadedVer}' is incompatible.", 
                plugin.Name, depName, depRangeStr, loadedVersion);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 级联剔除：当某个插件被剔除时，依赖它的插件也需要重新检查
    /// </summary>
    private Dictionary<string, IApiPlugin> ApplyCascadeRemoval(Dictionary<string, IApiPlugin> validPlugins, 
        Dictionary<string, HashSet<string>> graph)
    {
        var result = new Dictionary<string, IApiPlugin>(validPlugins);
        bool changed;

        do
        {
            changed = false;
            var toRemove = new List<string>();

            foreach (var plugin in result.Values)
            {
                // 检查这个插件依赖的所有插件是否都还在结果中
                foreach (var depName in graph[plugin.Name])
                {
                    if (!result.ContainsKey(depName))
                    {
                        // 依赖缺失，需要剔除此插件
                        toRemove.Add(plugin.Name);
                        _logger.LogWarning("Plugin '{PluginName}' is being removed due to missing dependency '{DepName}'.", 
                            plugin.Name, depName);
                        break;
                    }
                }
            }

            foreach (var name in toRemove)
            {
                result.Remove(name);
                changed = true;
            }

        } while (changed);

        return result;
    }
}
