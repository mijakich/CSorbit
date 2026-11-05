# Third-party notices

## Orbit

CSorbit is a C# port of [Orbit](https://github.com/MeteorDevelopment/orbit), a Java event bus by Meteor Development.

The reference source declares version `0.3.0` in `build.gradle`. Its exact upstream Git commit is unknown, so this project does not claim correspondence to a specific commit or release tag.

Orbit is distributed under the MIT License. The original notice, `Copyright (c) 2021 Meteor Development`, and the full license text are retained in the root [LICENSE](../LICENSE). The license is also included in CSorbit's release ZIP files and NuGet package. Retain it when redistributing copies or substantial portions of the library.

The original Java implementation is available from the upstream repository linked above.

## Godot and Unity

Godot and Unity are separate engines with their own licenses. CSorbit does not bundle their runtimes, APIs, or SDKs in `CSorbit.dll`. Engine examples require an installed compatible engine; Godot SDK packages are used only to build the Godot examples.
