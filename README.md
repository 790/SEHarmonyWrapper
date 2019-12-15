# SEHarmonyWrapper

A plugin for the game Space Engineers that provides an instance of the [Harmony library](https://github.com/pardeike/Harmony) to other plugins.

This tool modifies the game in unsupported ways and should be used with caution. Expect bugs, crashes, broken updates.

## Install

Extract `0Harmony.dll` and `SEHarmonyWrapper.dll` to your `steam\steamapps\common\SpaceEngineers\Bin64` directory.
In Steam, go to properties of the game, Set Launch Options and add `-plugin SEHarmonyWrapper.dll`

## Installing Plugins

Copy a plugin .dlls to a directory inside `SpaceEngineers\Bin64\seharmonywrapper` to enable them.

If a new SEHarmonyWrapper using workshop mod is detected the game will prompt on launch whether to enable it. Enabled workshop plugins can be edited from `SpaceEngineers\Bin64\seharmonywrapper\modlist.txt`
