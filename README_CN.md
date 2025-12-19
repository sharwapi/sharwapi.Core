# SharwAPI

[简体中文](/README_CN.md) | [English](/README.md)

[![Stars](https://img.shields.io/github/stars/sharwapi/sharwapi.core?label=Stars)](https://github.com/sharwapi/sharwapi.core)
[![Github release](https://img.shields.io/github/v/tag/sharwapi/sharwapi.core)](https://github.com/sharwapi/sharwapi.core/releases)
[![GitHub](https://img.shields.io/github/license/sharwapi/sharwapi.core)](https://github.com/sharwapi/sharwapi.core/blob/main/LICENSE)
[![GitHub last commit](https://img.shields.io/github/last-commit/sharwapi/sharwapi.core)](https://github.com/sharwapi/sharwapi.core/commits/main)
[![GitHub issues](https://img.shields.io/github/issues/sharwapi/sharwapi.core)](https://github.com/sharwapi/sharwapi.core/issues)

SharwAPI (又称Sharw's API) 是一款基于.NET开发的模块化API框架，轻量、高性能、可扩展，且简单易用。

**[文档](https://sharwapi.hope-now.top)** | **[下载](https://github.com/sharwapi/sharwapi.core/releases)** | 🧩 **[插件市场](https://sharwapi.hope-now.top/market)**

## 特性

- **功能插件化**: 将实际功能分为独立的 **插件(Plugin)** ，**API本体(CoreAPI)** 仅负责插件加载、路由注册等底层任务，没有任何的业务代码
- **轻量化**: 相较于传统API框架，本项目可以让你像搭积木一样只加载你需要的插件，而不必分去大量的资源和时间给用不到的功能
- **简单易用**: 基础框架已经打好，你无须处理繁琐的底层工作，可以专注开发你想要的功能
- **跨平台**: 依托于.NET的强大能力，本项目可以运行到 **几乎** 任何平台上

## 快速开始

在开始之前，请先确保你的设备满足以下推荐要求：

- **系统**：Windows x64 / Linux x64
- **CPU**：1 核或更高
- **内存**：1GB 或更高
- **硬盘**：5GB 可用空间
- **运行时**：[ASP.NET Core 9 Runtime](https://dotnet.microsoft.com/zh-cn/download/dotnet/9.0)

你可以在 [Github Releases](https://github.com/sharwapi/sharwapi.core/releases) 中下载软件并运行。

插件的载入请参阅 [文档中插件部分](https://sharwapi.hope-now.top/getting-started.html#插件-plugin)

## 许可证

本项目基于 [GNU General Public License v3.0](LICENSE) 获得许可