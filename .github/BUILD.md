### Build Instructions
This document provides a step-by-step guide on how to build the Cartel Enforcer mod from the source code.

### Prerequisites
Before you can build the project, you need to have the following software installed:

Visual Studio 2022 (or newer). **.NET desktop development workload* is required.

MelonLoader: This mod requires MelonLoader to be installed in your game. Follow the official MelonLoader installation guide for your specific game version.

### Getting the Source Code

Clone the Repository

```bash
git clone https://github.com/XOWithSauce/schedule-cartelenforcer.git
```

### Project Structure

- Mono and IL2Cpp folders have following files:
    1. **.csproj**: The main project file for the mod. Has the Build configurations MONO or IL2CPP + Debug/Release + Beta for each
    2. **.sln**: Preset Solution file with IL2CPP or MONO configuration ready.

- Source folder contains the shared source code between the 2 build types. In source code build differences are marked with conditional `#if MONO` expressions.

### Building the Mod

- First you need to get the required assembly files from the game installation:
1. **Mono**: Opt in to the Alternate or Alternate Beta branch for Schedule I in Steam and wait for it to finish installation.
    - Then you must navigate to C:\Program Files (x86)\Steam\steamapps\common\Schedule I\Schedule I_Data\Managed
    - From here you will need to copy all the files specified in the **CartelEnforcer.csproj** file ItemGroup References to the libs-mono directory.
2. **IL2Cpp**: Opt in to the default (none) or beta branch for Schedule I in Steam and wait for it to finish installattion.
    - Start your game once and let MelonLoader build the il2cpp assemblies. After this is done the game will start and then close the game.
    - Then you must navigate to the following directory: C:\Program Files (x86)\Steam\steamapps\common\Schedule I\MelonLoader\Il2CppAssemblies
    - From here you will need to copy all the files specified in the **CartelEnforcer-IL2Cpp.csproj** file ItemGroup References to the libs-il2cpp directory.
    - Additionally you will need the **Il2CppInteropRuntime.dll** from the C:\Program Files (x86)\Steam\steamapps\common\Schedule I\MelonLoader\net6 directory. Copy it to the libs-il2cpp directory.


#### Set the Build Configuration:

Open the Project: Open the **.sln** solution file with Visual Studio.

In the Visual Studio toolbar, locate the "Solution Configurations" dropdown. By default, it's set to "Debug."

For testing and development, use the Debug configuration. This build will include all debug logs and messages.

For the final release, you must switch to the Release configuration. The Release build is optimized for performance and will automatically strip out all calls to DebugModule.Log.

If you want to build Beta compatible version you must make a new configuration with Release_Beta or Debug_Beta and set it as active configuration. This will cause the mod to revert to using any code regions marked with `#if BETA ... #endif`