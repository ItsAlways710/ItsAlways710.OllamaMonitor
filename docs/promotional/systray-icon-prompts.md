# System Tray Icon Prompts

Use these prompts to generate the tray icons for each **ElBruno.OllamaMonitor** runtime state.

## Global Art Direction

```text
Create a tiny Windows system tray icon for a developer tool that monitors a local Ollama runtime. The icon must be minimal, geometric, readable at 16x16, and based on a circular status indicator with a subtle telemetry pulse or line chart inside. Flat design, no text, no background scene, no 3D effect, no mascot, no extra decoration.
```

## Shared Constraints

```text
- Windows tray icon style
- Transparent background
- Flat design
- High contrast
- Must remain recognizable at 16x16, 20x20, and 32x32
- No words, letters, or captions
- No brand logos
- No photorealism
```

## 1. Not Reachable / Offline

```text
Create a gray Windows system tray icon for a local Ollama monitor in the offline or not reachable state. Use a circular status ring with a muted gray tone and a subtle telemetry line inside. The icon should feel inactive but still clean and intentional. Minimal, flat, transparent background, optimized for 16x16 tray usage.
```

## 2. Running

```text
Create a green Windows system tray icon for a local Ollama monitor in the healthy running state. Use a circular status ring with a bright green tone and a tiny telemetry pulse or line chart inside. The icon should feel alive, responsive, and lightweight. Minimal, flat, transparent background, optimized for 16x16 tray usage.
```

## 3. Model Loaded

```text
Create a blue Windows system tray icon for a local Ollama monitor in the model loaded state. Use a circular status ring with a strong blue tone and a subtle telemetry line or tiny signal mark inside. The icon should suggest that a model is available and ready. Minimal, flat, transparent background, optimized for 16x16 tray usage.
```

## 4. High Usage / Active

```text
Create an orange Windows system tray icon for a local Ollama monitor in the active or high usage state. Use a circular status ring with a vivid orange tone and a more energetic telemetry pulse or upward trend inside. The icon should suggest active work without looking noisy. Minimal, flat, transparent background, optimized for 16x16 tray usage.
```

## 5. Error

```text
Create a red Windows system tray icon for a local Ollama monitor in the error state. Use a circular status ring with a strong red tone and a subtle warning-like telemetry interruption inside. The icon should communicate failure clearly while staying clean and readable. Minimal, flat, transparent background, optimized for 16x16 tray usage.
```

## Output Files

Save generated reference images under:

```text
docs/promotional/images/
```

Suggested filenames:

```text
tray-state-gray.png
tray-state-green.png
tray-state-blue.png
tray-state-orange.png
tray-state-red.png
```

The app-ready tray assets should be exported as `.ico` files under:

```text
src/ItsAlways710.OllamaMonitor/Assets/TrayIcons/
```
