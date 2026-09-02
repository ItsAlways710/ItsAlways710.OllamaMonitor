# ItsAlways710.OllamaMonitor

A Windows system tray monitor for your local Ollama runtime — status, loaded models, live context tracking, and the real resource cost of inference, right out of the tray.

It started as a fork of [ElBruno.OllamaMonitor](https://github.com/elbruno/ElBruno.OllamaMonitor) by [Bruno Capuano](https://github.com/elbruno) and has grown into its own project on that foundation.

[![NuGet](https://img.shields.io/nuget/v/ItsAlways710.OllamaMonitor.svg?style=flat-square&logo=nuget)](https://www.nuget.org/packages/ItsAlways710.OllamaMonitor)
[![NuGet Downloads](https://img.shields.io/nuget/dt/ItsAlways710.OllamaMonitor.svg?style=flat-square&logo=nuget)](https://www.nuget.org/packages/ItsAlways710.OllamaMonitor)
[![Publish to NuGet](https://github.com/ItsAlways710/ItsAlways710.OllamaMonitor/actions/workflows/publish.yml/badge.svg)](https://github.com/ItsAlways710/ItsAlways710.OllamaMonitor/actions/workflows/publish.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)

A tiny Windows system tray tool to monitor your local Ollama runtime.

> Quick visual feedback about your Ollama status, resource usage, and models—right from your Windows system tray.

## What's New

- **Detailed logging toggle (off by default)** — a verbose-level log gate (incl. topmost-guard forensics) via Settings (0.12.0)
- **Live context-window tracking** — optional "Context" line in the mini monitor (off by default): per-task tokens used, slot size, tokens/second, with runner-to-model attribution parsed from the Ollama server log (0.11.0)
- **Launch at Windows Startup** — optional sign-in autostart toggle (off by default) (0.11.0)
- **System-wide CPU and Memory** — "(System)" figures alongside the Ollama process metrics (0.11.0)
- **Optional Mini Monitor display controls** — CPU, memory, and log panel toggles, applied live (0.10.0)
- **Model lifecycle controls** — Stop selected/all, Pull, Remove, Copy, plus Start Ollama and **Auto / Cli / Api** unload strategy

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
dotnet tool install --global ItsAlways710.OllamaMonitor
```

Then launch anytime:

```bash
ollamamon
```

### From Source

```bash
git clone https://github.com/ItsAlways710/ItsAlways710.OllamaMonitor.git
cd ItsAlways710.OllamaMonitor
dotnet build src/ItsAlways710.OllamaMonitor/
dotnet run --project src/ItsAlways710.OllamaMonitor/
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
- ✅ **Standard Details Window** — A normal Windows window that keeps the app in the tray when closed
- ✅ **Mini Monitor Window** — A semi-transparent always-on-top compact view for CPU, RAM, GPU, and model status
- ✅ **Live Context Tracking** — Optional per-task context usage line (tokens, slot size, tokens/second, model attribution)
- ✅ **System-wide Metrics** — Machine CPU/memory alongside the Ollama process metrics
- ✅ **Settings Window** — Notifications, general behavior, and mini monitor display, applied live
- ✅ **Windows Notifications** — Toast alerts for status changes and model events
- ✅ **Themed UI** — Dark, light, or system theme
- ✅ **Local Configuration** — Endpoint, refresh rate, thresholds, via `settings.json`
- ✅ **CLI Commands** — Scriptable configuration and status
- ✅ **GPU Metrics** — Best-effort NVIDIA GPU tracking (if nvidia-smi is available)
- ✅ **Model Management** — Stop selected/all models, pull/remove/copy models
- ✅ **CLI-Based Stop Strategy** — `ollama stop` for local endpoints with API fallback for remote
- ✅ **Start Ollama** — Trigger `ollama serve` from the UI for local setups
- ✅ **Copy to Clipboard, Manual Refresh, Open Ollama URL**

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

## Support

Found a bug or have a feature request? Open an issue on [GitHub](https://github.com/ItsAlways710/ItsAlways710.OllamaMonitor/issues).

Questions about Ollama? Check the [Ollama documentation](https://github.com/ollama/ollama).

## License

This project is licensed under the MIT License — see [LICENSE](LICENSE) for details.

This project started as a fork of [ElBruno.OllamaMonitor](https://github.com/elbruno/ElBruno.OllamaMonitor) by [Bruno Capuano](https://github.com/elbruno) — thanks to him for the foundation.

- 📝 **Blog**: [elbruno.com](https://elbruno.com)
- 📺 **YouTube**: [youtube.com/elbruno](https://youtube.com/elbruno)
- 🔗 **LinkedIn**: [linkedin.com/in/elbruno](https://linkedin.com/in/elbruno)
- 𝕏 **Twitter**: [twitter.com/elbruno](https://twitter.com/elbruno)
- 🎙️ **Podcast**: [notienenombre.com](https://notienenombre.com)
