# NoFocusLoss · WPF / .NET 10

A Windows desktop utility that prevents supported offline games from pausing or muting when unfocused.

## Current desktop application

The application is WPF on **.NET 10**, with a redesigned searchable two-pane window list, light/dark appearance, keyboard focus indicators, and English / Simplified Chinese / Traditional Chinese resources. The old .NET Framework project and settings scaffolding are removed. SharpestInjector's pinned source is built by `NoFocusLoss.Core` directly for .NET 10; the upstream .NET Standard project is not part of the build.

Install **.NET 10 Desktop Runtime x64**, then run `NoFocusLossGUI.exe`. Keep `NoFocusLoss.dll` and `NoFocusLoss64.dll` beside the executable. The GUI is x64; the two native payloads still support the respective 32-bit and 64-bit target processes. Native hooks remain C++ / MinHook, not managed code inside the game. A .NET runtime upgrade does not change the injection safety limitations.

## Build

Requires .NET 10 SDK, Visual Studio C++ tools (v143), and Windows SDK. Clone with `--recurse-submodules` and run:

```powershell
./build.ps1 -Configuration Release
```

The complete framework-dependent application is in `artifacts/app`. There is no old-runtime target or alternate legacy UI. `--ui-smoke <directory>` renders the actual light/dark WPF window for CI without enumerating or injecting into any process. This is a UI/build check, not an injection compatibility test.

## Safety and behavior

This utility uses process injection. Only use it in offline games that actually need it. Do not use it with multiplayer or anti-cheat protected games. Selecting the wrong process can leave that application unresponsive and require a restart.

Choose a window in the left panel and select **Inject**. The cursor option applies at injection time. Injected windows appear on the right; select one and choose **Unload** to remove the hook. Search matches window titles and executable names. Closing the GUI does not silently unload another process.

The native component filters focus messages and hooks relevant foreground/cursor functions. It does not guarantee support for every engine, overlays, controller API or secondary window. Targeting elevated applications still requires sufficient privileges; this upgrade does not bypass UAC or process protection.

## Verification

Windows CI builds both native payloads and the .NET 10 application, then renders the real WPF window in light and dark mode. This does not claim real-game injection, timeout, or unload compatibility has been tested. Those paths retain the existing implementation and require Windows validation with benign test applications before release.

## Third-party code

Original project attribution and licensing are retained in `LICENSE.txt`. MinHook and SharpestInjector remain pinned submodules with their original license files; the publish directory includes those notices. The SharpestInjector source is not a separate legacy-runtime application.
