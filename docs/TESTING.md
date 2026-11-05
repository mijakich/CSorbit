# Testing CSorbit

The core test runner has no external test framework dependency. It runs the same 28 scenarios in `Delegate` and `Reflection` modes, for 56 checks in total.

## Core tests

With .NET SDK 8 or newer, run from the repository root:

```shell
dotnet run --project tests/CSorbit.Tests -c Release
```

The runner exits with a nonzero status on failure. Coverage includes exact runtime type dispatch, priorities, cancellation, handler discovery, invalid signatures, duplicate subscriptions, reference identity, exception propagation, subscription changes during dispatch, nested posts, clearing subscriptions, and concurrent operations.

To build all library targets and release packages with the tests enabled:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1
```

PowerShell 7 users can run `pwsh -NoProfile -File scripts/build.ps1` instead. GitHub Actions runs this script for branch pushes, pull requests, and version tag releases.

## Godot examples

Build the library first. Open either `examples/Godot3/project.godot` in Godot 3.6.3 Mono or `examples/Godot4/project.godot` in Godot 4.6.3 .NET, build the C# project, and run the scene. Each scene checks that damage 10 is delivered once and damage 100 is cancelled before the next handler.

For an automated scene check, replace the executable paths with your installed editors:

```powershell
& 'C:/path/to/godot3-mono.exe' --no-window --audio-driver Dummy --path examples/Godot3 --quit -- --csorbit-smoke-test
& 'C:/path/to/godot4-mono-console.exe' --headless --audio-driver Dummy --path examples/Godot4 --quit-after 5 -- --csorbit-smoke-test
```

Successful checks print `CSORBIT_GODOT3_OK` or `CSORBIT_GODOT4_OK` and exit with code 0. Prepare Godot 3 C# script metadata by building in the editor, or with `--build-solutions --quit`, before running its scene check.

## Platform validation

The core tests run on .NET. Godot examples and the [Unity example](../examples/Unity/README.md) require their respective editors. Check exported applications on their target platforms, including IL2CPP preservation settings; the core tests do not exercise engine exports or Unity Player builds.
