# Archipelago mod for *Kingdom Two Crowns*

### What is randomized? 

In K2C, the checks are:
- things you can find in the world, like chests or gems
- destroying portals
- unlocking hermits & mounts

The items are:
- different tier upgrades, for castles and walls
- the ability to use stone & iron age related stuff
- unlocking statues, hermits, and mounts

What is the GOAL
- Kill the greed on all islands

Are DLC supported ?
- Not yet (shogun might work but I can't guarantee that)

### How to install the mod? 

Sadly, there is no mod loader that support K2C at this time, so you'll have to do it yourself, here's a quick guide:

First, you will need to download BepinEx from here: https://builds.bepinex.dev/projects/bepinex_be, and search for the version `BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.735+5fef357.zip` (replace `win-x64` with your operating system)
*Make sure the last number is 735. It could/should work with the latest version, but just to be sure.*
*Make also sure to download the IL2CPP version, and not Mono, as it will straight up not work.*

Then, unzip the file at your game's root folder (if you use steam, you can right click on the game, and into Manage > Browse local files. It should bring you to `[Steam]/steamapps/common/Kingdom Two Crowns`)

Follow the same procedure for CPP2IL.Patch (from here: https://github.com/abevol/KingdomMod/releases/download/2.4.0/Cpp2IL.Patch.zip)

The directory should now look like this:
<img width="714" height="461" alt="K2CDirectoryWithBepin" src="https://github.com/user-attachments/assets/386df788-973a-4457-84c4-489d2313895c" />
*(the zip files don't have to stay here)*

Then, simply run the game once to initialize BepinEx, you can then close the game

Finally, download the latest dll file from here (https://github.com/Catalyss/Archipelago-Kingdom2Crown/releases) and put it into BepInEx/plugins/

And you can now run the game! Go into the pause menu to change your connection info and connect to the archipelago server.






