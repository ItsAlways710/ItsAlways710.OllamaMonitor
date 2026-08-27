# ItsAlways710.OllamaMonitor

A fork of [ElBruno.OllamaMonitor](https://github.com/elbruno/ElBruno.OllamaMonitor) by [Bruno Capuano](https://github.com/elbruno), maintained by [ItsAlways710](https://github.com/ItsAlways710).

[![NuGet](https://img.shields.io/nuget/v/ElBruno.OllamaMonitor.svg?style=flat-square&logo=nuget)](https://www.nuget.org/packages/ElBruno.OllamaMonitor)
[![NuGet Downloads](https://img.shields.io/nuget/dt/ElBruno.OllamaMonitor.svg?style=flat-square&logo=nuget)](https://www.nuget.org/packages/ElBruno.OllamaMonitor)
[![Publish to NuGet](https://github.com/ItsAlways710/ElBruno.OllamaMonitor/actions/workflows/publish.yml/badge.svg)](https://github.com/ItsAlways710/ElBruno.OllamaMonitor/actions/workflows/publish.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)

A tiny Windows system tray tool to monitor your local Ollama runtime.

> Quick visual feedback about your Ollama status, resource usage, and models—right from your Windows system tray.

## What's New

- Added optional Mini Monitor display controls: **Show CPU**, **Show Memory**, and **Show Ollama logs** — disabled by default, configure from Settings
- Collapsible logs panel in Mini Monitor shows the last 5 lines of Ollama server output in real-time
- All mini-monitor toggles apply live (no restart needed)
- Added full model lifecycle controls in the monitor UI: **Stop selected**, **Stop all**, **Pull**, **Remove**, **Copy**
- Added **Start Ollama** action from the main window
- Upgraded unload logic with strategy support: **Auto / Cli / Api**
- `Auto` strategy now prefers `ollama stop` for local endpoints and API fallback for remote endpoints
- Added verification after stop requests to confirm models are no longer running
- Added automated tests for unload strategy behavior and new settings defaults

## What It Does

ItsAlways710.OllamaMonitor sits in your Windows system tray and tells you:

- **Is Ollama running?** A glance at the tray icon shows you the status.
- **Is a model loaded?** See what's currently active.
- **How much CPU, RAM, and GPU is it using?** Real-time resource metrics from the Ollama process.
- **Any errors?** Get instant visual feedback if something's wrong.
- **Need model actions?** Stop running models, pull new models, remove old ones, and copy model tags from the monitor window.

Perfect for:
- Local AI developers who need quick visibility into Ollama
- Demo presenters who want to know resource impact in real-time
- Anyone running large models locally who's curious about the overhead

## Demo

![ItsAlways710.OllamaMonitor demo](images/ollamanitor-demo01.gif)

## Installation

### Via NuGet (Recommended)

```bash
dotnet tool install --global ElBruno.OllamaMonitor
```

Then launch anytime:

```bash
ollamamon
```

### From Source

```bash
git clone https://github.com/ItsAlways710/ElBruno.OllamaMonitor.git
cd ElBruno.OllamaMonitor
dotnet build src/ElBruno.OllamaMonitor/
dotnet run --project src/ElBruno.OllamaMonitor/
```

## Quick Start

1. **Launch the app:**
   ```bash
   ollamamon
   ```
   The app starts minimized to the tray. Click the icon to open the details window or mini monitor.

2. **Check your status:**
   Look at the tray icon color—it tells you Ollama's status at a glance.

3. **Configure (optional):**
   See [Configuration Guide](docs/configuration.md) for endpoint, refresh rate, and threshold settings.

## System Tray Status

The tray icon color tells you the status at a glance:

| Color  | Meaning |
|--------|---------|
| 🟤 Gray  | Ollama is not reachable |
| 🟢 Green | Ollama is running, no model loaded |
| 🔵 Blue  | A model is currently loaded |
| 🟠 Orange | A model is running or high resource usage |
| 🔴 Red   | Error or Ollama unavailable |

Click the icon to open the full details window for diagnostics, or open the mini monitor from the tray menu to keep resource usage visible on top of other windows.

## Features

- ✅ **System Tray Integration** — Runs in the background, always visible
- ✅ **Visual Status Indicators** — Color-coded icons for quick status checks
- ✅ **Standard Details Window** — A normal Windows window with minimize/close behavior that keeps the app in the tray when closed
- ✅ **Mini Monitor Window** — A semi-transparent always-on-top compact view for CPU, RAM, GPU, and model status
- ✅ **Local Configuration** — Customize endpoint, refresh rate, thresholds
- ✅ **CLI Commands** — Fully scriptable configuration
- ✅ **GPU Metrics** — Best-effort NVIDIA GPU tracking (if nvidia-smi is available)
- ✅ **Copy to Clipboard** — Quickly share diagnostics
- ✅ **Manual Refresh** — Force an immediate check
- ✅ **Open Ollama URL** — Quick link to the Ollama API
- ✅ **Model Management** — Stop selected/all models, pull/remove/copy models
- ✅ **CLI-Based Stop Strategy** — Uses `ollama stop` for local endpoints with API fallback support
- ✅ **Start Ollama** — Trigger `ollama serve` from the UI for local setups

## Requirements

- **Windows 10 / Windows 11** (requires .NET 10 runtime, which can be downloaded from [dotnet.microsoft.com](https://dotnet.microsoft.com))
- **Ollama** running locally (download from [ollama.ai](https://ollama.ai))
- **.NET 10 SDK** to build from source

Optional:
- **nvidia-smi** (NVIDIA GPU drivers) for GPU metrics

## Configuration

See [Configuration Guide](docs/configuration.md) for detailed setup, CLI commands, custom thresholds, and advanced options like remote Ollama monitoring.

## Documentation

- **[Architecture Guide](docs/architecture.md)** — How the app is built and organized
- **[Configuration Guide](docs/configuration.md)** — Detailed configuration, CLI commands, and advanced setup
- **[Development Guide](docs/development-guide.md)** — Building from source, folder structure, debugging
- **[Publishing Guide](docs/publishing.md)** — NuGet publishing with GitHub Releases and OIDC
- **[Troubleshooting](docs/troubleshooting.md)** — Common issues and solutions
- **[Release Notes](docs/release-notes.md)** — Version history and changelog

### Promotional Materials

If you'd like to share this project:
- **[Blog Post](docs/promotional/blog-post.md)** — Full-length article
- **[LinkedIn Post](docs/promotional/linkedin-post.md)** — Social media ready
- **[Twitter Post](docs/promotional/twitter-post.md)** — X-ready snippets
- **[Image Prompts](docs/promotional/image-prompts.md)** — AI image generation prompts

## License

This project is licensed under the MIT License — see [LICENSE](LICENSE) for details.

## Support

Found a bug or have a feature request? Open an issue on [GitHub](https://github.com/ItsAlways710/ElBruno.OllamaMonitor/issues).

Questions about Ollama? Check the [Ollama documentation](https://github.com/ollama/ollama).

## About the Original Project

**Made with ❤️ by [Bruno Capuano (ElBruno)](https://github.com/elbruno)**

- 📝 **Blog**: [elbruno.com](https://elbruno.com)
- 📺 **YouTube**: [youtube.com/elbruno](https://youtube.com/elbruno)
- 🔗 **LinkedIn**: [linkedin.com/in/elbruno](https://linkedin.com/in/elbruno)
- 𝕏 **Twitter**: [twitter.com/elbruno](https://twitter.com/elbruno)
- 🎙️ **Podcast**: [notienenombre.com](https://notienenombre.com)

## About This Fork

Forked and maintained by [ItsAlways710](https://github.com/ItsAlways710) — [github.com/ItsAlways710/ElBruno.OllamaMonitor](https://github.com/ItsAlways710/ElBruno.OllamaMonitor).
