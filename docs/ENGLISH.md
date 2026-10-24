# AdofaiDeeplink

[KOREAN](https://github.com/Pro203S/AdofaiDeeplink#AdofaiDeeplink) [TROBLESHOOTING](./TROBLESHOOTING_EN.md) [Report Bugs](https://github.com/Pro203S/AdofaiDeeplink/issues)

A mod that allows you to open A Dance of Fire and Ice-related files and `adofai://` links.

> [!IMPORTANT]
> This mod only works with A Dance of Fire and Ice installed through Steam.
> This mod is only available on Windows.

## Installation

1. Download `AdofaiDeeplink.exe` from [Releases](https://github.com/Pro203S/AdofaiDeeplink/releases). (You don't need to download the mod ZIP file.)
2. Run `AdofaiDeeplink.exe` to automatically install the mod.

## Usage

### Opening `adofai://` Links

1. Use `adofai://open?url=<level URL>` to open a level.
2. Use `adofai://updateMod` to update the mod.
3. Use `adofai://uninstall` to uninstall the mod.

> [!IMPORTANT]
> The level URL must point to a level that can be opened using the **Open by URL** feature.
> Example: Links from `cdn.discordapp.com`, etc.

### Opening `*.adofai` and `*.adozip` Files

1. Find a file with the `.adofai` extension in your level folder.
2. Double-click the file to automatically launch A Dance of Fire and Ice.

> [!TIP]
> If A Dance of Fire and Ice doesn't launch automatically, right-click the `.adofai` file and select **Open with > A Dance of Fire and Ice**.

### Opening `*.zip` Files

1. Right-click a `.zip` archive.
2. Click **Open with ADOFAI**.

> [!NOTE]
> Windows 11 users may need to click **Show more options** first.

## Uninstalling the Mod

1. Press `Win + R` to open the Run dialog.
2. Enter `%localappdata%\Pro203S\AdofaiDeeplink\` and press Enter.
3. Run `AdofaiDeeplink.exe`.
4. Click **Remove** to uninstall the mod.

## Important Notes

- If you have previously set default applications for `.adofai` or `.adozip` files, AdofaiDeeplink may overwrite those file associations.
- Installing the mod alone will not enable its features. You must install it using `AdofaiDeeplink.exe`.
