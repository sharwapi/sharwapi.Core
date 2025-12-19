# SharwAPI

[简体中文](/README_CN.md) | [English](/README.md)

[![Stars](https://img.shields.io/github/stars/sharwapi/sharwapi.core?label=Stars)](https://github.com/sharwapi/sharwapi.core)
[![Github release](https://img.shields.io/github/v/tag/sharwapi/sharwapi.core)](https://github.com/sharwapi/sharwapi.core/releases)
[![GitHub](https://img.shields.io/github/license/sharwapi/sharwapi.core)](https://github.com/sharwapi/sharwapi.core/blob/main/LICENSE)
[![GitHub last commit](https://img.shields.io/github/last-commit/sharwapi/sharwapi.core)](https://github.com/sharwapi/sharwapi.core/commits/main)
[![GitHub issues](https://img.shields.io/github/issues/sharwapi/sharwapi.core)](https://github.com/sharwapi/sharwapi.core/issues)

SharwAPI (also known as Sharw's API) is a modular API framework built on .NET. It is lightweight, high-performance, extensible, and easy to use.

**[Documentation](https://sharwapi.hope-now.top)** | **[Download](https://github.com/sharwapi/sharwapi.core/releases)** | 🧩 **[Plugin Market](https://sharwapi.hope-now.top/market)**

## Features

- **Plugin-Based Architecture**: Actual functionality is split into independent **Plugins**. The **CoreAPI** is only responsible for low-level tasks such as plugin loading and route registration, containing absolutely no business logic code.
- **Lightweight**: Compared to traditional API frameworks, SharwAPI allows you to assemble features like LEGO blocks. You only need to load the plugins you require, without wasting resources and time on unused functions.
- **Easy to Use**: The underlying infrastructure is already built for you. You don't need to deal with tedious low-level details—just focus on developing the features you want.
- **Cross-Platform**: Powered by the robust capabilities of .NET, this project can run on **almost** any platform.

## Quick Start

Before you start, please ensure your device meets the following recommended requirements:

- **OS**: Windows x64 / Linux x64
- **CPU**: 1 Core or higher
- **RAM**: 1GB or higher
- **Disk**: 5GB available space
- **Runtime**: [ASP.NET Core 9 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)

You can download and run the software from [Github Releases](https://github.com/sharwapi/sharwapi.core/releases).

For instructions on loading plugins, please refer to the [Plugin section in the documentation](https://sharwapi.hope-now.top/getting-started.html#插件-plugin).

## License

The API framework in this project is licensed under the [GNU General Public License v3.0](LICENSE).