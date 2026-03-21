# HitlessMode

This mod adds two game modes for hitless:
- Fragile Soul: Hitless but you can still respawn.
- Glass Soul: Hitless with Steel Soul content. Death is permanent.

Fractured Mask does not work under these modes.

Existing saves are not affected. Only new saves started with these modes are affected.

## Translation

New translation are welcomed! [Follow this guide](https://github.com/silksong-modding/Silksong.I18N), add the appropriate file under [HitlessMode/languages](HitlessMode/languages) and [create a pull request](https://docs.github.com/en/pull-requests/collaborating-with-pull-requests/proposing-changes-to-your-work-with-pull-requests/creating-a-pull-request-from-a-fork).

## Build

.NET 10 is required.

Download [SkongGamemodes](https://thunderstore.io/c/hollow-knight-silksong/p/DerVorce/SkongGamemodes/). The downloaded archive contains `Skonggamemodes.dll`. Create a new directory `deps` under `HitlessMode` and put `Skonggamemodes.dll`.

Create `SilksongPath.props` under `HitlessMode`. Copy and paste the following text and edit as needed.

```xml
<Project>
  <PropertyGroup>
    <SilksongFolder>SilksongInstallPath</SilksongFolder>
    <!-- If you use a mod manager rather than manually installing BepInEx, this should be a profile directory for that mod manager. -->
    <SilksongPluginsFolder>$(SilksongFolder)/BepInEx/plugins</SilksongPluginsFolder>
  </PropertyGroup>
</Project>
```

```
dotnet build -c Release
```
