# Custom Rooms

A mod for Potion Craft that lets you add custom rooms

A custom room can be provided to the mod in the form of a text file (describing a few details of the room) and a prefab stored within an Unity asset bundle.

## Installation

1. Download the latest BepinEx package corresponding to your operating system from [here](https://github.com/BepInEx/BepInEx/releases) and extract all files from the zip into your Potion Craft installation
2. Run the game once for BepinEx to generate its file system
3. Download latest from [releases](https://github.com/TommySoucy/PotionCraft-CustomRooms/releases)
4. Put all files from the .zip of this mod into BepinEx/Plugins folder

## Development

### Building

1. Clone repo
2. Open solution
3. Ensure all references are there
4. Build
5. DLL is now ready for install as explained in **Installation** section

### Making a custom room

See example room asset Unity (2020.2.1f1) project [here](https://github.com/TommySoucy/PotionCraft-CustomRooms-Example)

This room asset is used by and included with my [Storage Cellar mod](https://github.com/TommySoucy/PotionCraft-StorageCellar)

Using those two for guidance should be enough for someone to add their own room

## Used libraries

- Harmony
- BepinEx
