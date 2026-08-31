# Eco Gnome Mod

## Optional server mod of [Eco Gnome](https://eco-gnome.com)

This repository contains the Data Extractor part of [Eco Gnome](https://eco-gnome.com).
It allows to extract your specific server configuration (skills, recipes, items, ...) to visualize and calculate their prices on the [Eco Gnome](https://eco-gnome.com) website.
The extracted file is created in your server root folder: `eco_gnome_data.json`.

It also lets users synchronize prices between their Eco shops and Eco Gnome, either through chat commands or directly from the in-game Store UI.

## Chat commands

### Setup

- `/EcoGnome registerserver {JoinCode}` _(admin, alias `egserver`)_ — Register the server on Eco Gnome. To be launched once during server setup.
- `/EcoGnome registeruser {SecretId}` _(alias `eguser`)_ — Register your Eco Gnome account on this server. To be launched once per user.
- `/EcoGnome export` _(admin)_ — Re-export the server data to `eco_gnome_data.json`. The export also runs automatically on server start.

### Open Eco Gnome

- `/EcoGnome open` _(alias `egopen`)_ — Open the Eco Gnome website in your default browser. If the server is registered and you have joined it, you will be switched to this server.
- `/EcoGnome join` _(alias `egjoin`)_ — Open Eco Gnome and join this server (if it has been registered).

### For-sale objects bulk sync

- `/EcoGnome syncroom {ContextName}` _(alias `egsyncroom`)_ — Update the price of every authorized for-sale world object in the room you are standing in, using your Eco Gnome prices. `ContextName` is optional; leave it blank to use the default context.
- `/EcoGnome syncdeed {ContextName}` _(alias `egsyncdeed`)_ — Same as `syncroom`, but applied to every authorized for-sale object on the deed you are standing on.

> Shop sync commands (`syncshop`, `createshop`, ...) have been replaced by the in-game **Eco Gnome** component on stores — see below.

## In-game Eco Gnome component

Stores (`StoreObject`, `WoodShopCartObject`) now expose an **Eco Gnome** tab with the following options and buttons:

- **Context Name** — Name of the Eco Gnome context to pull prices from. Leave empty to use the default context.
- **Scope** — Which side of the offers the buttons act on: `All` (buys + sells), `Buy` only, or `Sell` only.
- **Sync Tags** — When set to `Yes`, tag-based offers are also created/updated. Set to `No` to leave existing tag offers untouched.
- **Sync Prices** _(button)_ — Update the prices of offers already present in the store from your Eco Gnome prices. Does not add or remove offers. Honors **Scope** and **Sync Tags**.
- **Group By** — How offers are grouped into categories when new ones are created by **Sync Offers** (`None`, `Margin`, `Skill`).
- **Filter Skills** — Restrict **Sync Offers** to items tied to these skills. Leave empty to include every skill.
- **Sync Offers** _(button)_ — Full sync: adds missing items/tags, updates prices of existing ones, and removes offers no longer tracked in Eco Gnome. Honors **Scope** and **Sync Tags**.
- **For Sale Sync Radius** — Radius (in blocks) used by the **Sync For Sale Area** button to find for-sale objects around the store.
- **Sync For Sale Area** _(button)_ — Update the prices of every for-sale world object around this store (within the configured radius) from your Eco Gnome prices.

A shortcut interaction is also available: **Shift + Right-click** on a store triggers **Sync Prices with Eco Gnome** using the component's current settings.

## Configuration

The plugin creates a config file `Configs/EcoGnome.eco` with:

- `EcoGnomeUrl` — Default `https://eco-gnome.com`. URL used to talk to the Eco Gnome API.
- `EcoGnomeUrlReverseProxy` — Optional. When set, this URL is used for the user-facing links opened by `/EcoGnome open` and `/EcoGnome join` (useful when Eco Gnome is hosted behind a reverse proxy).

## Installation

Download the latest `EcoGnomeMod.dll` and `StoreObject.cs` from the [Releases page](https://github.com/Eco-Gnome/eco-gnome-mod/releases) and drop them in `Mods/UserCode` in your server.

## Development build

The project references the Eco server sources (`..\..\Eco.Core`, `..\..\Eco.Gameplay`, …), so the clone has to sit two levels below `Eco\Server`. With the Eco source tree at `Eco\` and this repository cloned elsewhere, on Windows:

```powershell
New-Item -ItemType Directory Eco\Server\ServerMods
New-Item -ItemType Junction -Path Eco\Server\ServerMods\eco-gnome-mod -Target <path-to-this-clone>
# StoreObject.cs is compiled by the server together with the other mods (Eco.Mods), so make it visible to the Eco.Mods dev build too:
New-Item -ItemType Directory Eco\Server\Mods\__core__\EcoGnomeDev
New-Item -ItemType HardLink -Path Eco\Server\Mods\__core__\EcoGnomeDev\StoreObject.cs -Target <path-to-this-clone>\StoreObject.cs

dotnet build Eco\Server\ServerMods\eco-gnome-mod\EcoGnomeMod.csproj -c Debug "-p:SolutionDir=<absolute-path-to>\Eco\Server\"
```

Notes:
- `SolutionDir` must be passed (absolute, trailing backslash) because the tech tree generator invoked by `Eco.Mods` relies on it.
- Do not pass `-p:Platform=x64`: the Eco solution maps the library projects (and `Eco.Fody`) to *Any CPU*, and the Fody weaver path is hard-coded to the Any CPU output.
- Warnings are errors (inherited from `Eco\Directory.Build.props`).
- The first build compiles the whole Eco server library chain (several minutes); if it fails on `EcoVersion` not found, simply run it again (the version stamp is generated by the first pass).

Output: `bin\Debug\net10.0-windows\EcoGnomeMod.dll`.

## Contact
Zangdar (Discord: `#zangdar1111`)
Joridan (Discord: `#joridan`)
