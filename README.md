# Eco Gnome Mod

## Optional server mod of [Eco Gnome](https://eco-gnome.com)

This mod connects an Eco server to [Eco Gnome](https://eco-gnome.com), the price calculator. There is no chat command: everything is done from the **Eco Gnome** tab of the stores (`StoreObject`, `WoodShopCartObject`), and confirmed in the browser.

- The server sends its configuration (skills, recipes, items, ...) to Eco Gnome by itself at each start, only when it changed. The same data is still written to `eco_gnome_data.json` in the server root folder for a manual upload.
- Eco Gnome reads the average prices of the server's market (store offers and recent sales) from the server's web server, through a route this mod adds (`GET /api/ecognome/v1/market`, signed requests only). It suggests them next to the buy prices players type.
- Store owners update their prices and offers from Eco Gnome, and send their store buy prices to Eco Gnome.
- Players carry their Eco Gnome shopping lists in game and buy them in one go.

Requires the Eco 14.2 client: the tab uses the button grids (`ButtonGrid` in label mode).

## Eco Gnome tab

Each player only sees what they can use.

**Server not registered yet**
- Admins see **Register this server on Eco Gnome**. A page opens in the browser: create an Eco Gnome server or pick one you run, then confirm.
- Other players read who to ask: an admin registers it from this tab.

**Account not connected** — **Connect to Eco Gnome**: a page opens in the browser, confirm, and the account joins the Eco Gnome server by itself. No code or key to copy.

**Connected, customer** — the shopping list buttons: **Get a shopping list** and **Buy my shopping list** (greyed out while you carry no list with lines left to buy).

**Connected, store owner**
- The status line: the Eco Gnome account and the context the buttons use.
- **Prices**
  - **Update prices from Eco Gnome** — updates the prices of the offers already in the store. Does not add or remove offers. An item Eco Gnome tracks without a price leaves its offer unpriced (not tradable); the result popup lists them.
  - **Rebuild offers from Eco Gnome** — adds the items you buy and sell, updates prices, removes offers Eco Gnome no longer tracks. Shows what will be added and removed first, and asks to confirm. New offers start unpriced (Eco 14.2+) until Eco Gnome gives them a price.
  - **Send buy prices to Eco Gnome** — the prices of the store's buy offers become the prices you pay for your ingredients in Eco Gnome. Sell prices are never sent: Eco Gnome calculates them.
- **Shopping list** — the same buttons as customers.
- **Settings**
  - **Context: ...** — picks which of your Eco Gnome contexts this store uses (one per store).
  - **Open Eco Gnome** — opens the website on this server.
  - **Disconnect** — removes your connection from this server.
- **Advanced settings** — shows:
  - **Offers synced** — `All`, `Buy` only or `Sell` only.
  - **Sync tag offers** — `No` leaves tag offers untouched.
  - **Group categories by** / **Only these skills** — how **Rebuild offers** groups and filters the categories it creates.
  - **For-sale radius** / **Reprice my for-sale objects** — updates the price of every for-sale object you own around the store.

Categories whose name contains `[NS]` are never touched. **Shift + Right-click** on a store runs **Update prices from Eco Gnome**.

Connection tokens are kept in `Storage/EcoGnome.tokens.json` on the server; a link removed on the website brings the matching button back.

## Shopping lists

A shopping list from Eco Gnome becomes a **Shopping List** paper in your inventory, one paper per list. It holds the "Items to buy" of the list, with a progress per line (`250 / 500`).

- **Get a shopping list** in the Eco Gnome tab of a store gives the paper (or updates the one you carry).
- **Right-click** the paper in your inventory to open it: progress per line, the items a tag line accepts, a **Done** checkbox per line, **Reset purchases** (to buy the same list again, e.g. every day) and **Reload from Eco Gnome** (fetches the list again and keeps what was already bought).
- **Store purchases are counted** for whoever carries the paper. A line is ticked automatically once its quantity is reached. When an item is on several carried lists, the oldest list is filled first.
- **Selected in the toolbar**, the paper shows its lines in the bottom-right corner. Hovering an item (in a store too) shows how many each carried list still needs.
- **Buy my shopping list** buys from the cheapest offers first, never more than what is left on a line or on the shelf. A store offer by tag is only used when every item it may deliver counts for the line, since the store picks which item is delivered; other lines sold only by tag are listed in the recap as "buy these yourself".

Known limits:
- Purchases from for-sale objects (signs) are not counted: tick the line by hand.
- The paper is a copy: when the list changes on Eco Gnome, use **Reload from Eco Gnome**.
- Remove every paper before removing the mod, or the save may fail to load.

## Configuration

The plugin creates `Configs/EcoGnome.eco`:

- `EcoGnomeUrl` — Default `https://eco-gnome.com`. URL of the Eco Gnome API.
- `EcoGnomeUrlReverseProxy` — Optional. Used for the pages opened in the players' browser (when Eco Gnome is behind a reverse proxy).
- `AutoUploadData` — Default `true`. Send the server data at start when it changed.
- `ShareMarketPrices` — Default `true`. Let Eco Gnome read the market average prices. Requests are signed (HMAC) with a secret only this server and Eco Gnome know, kept in `Storage/EcoGnome.tokens.json`.
- `MarketPublicUrl` — Optional. Address Eco Gnome uses to reach the web server (e.g. behind a reverse proxy). Empty: the network config's `WebServerUrl`, else the server's public IP and `WebServerPort`. The web server port must be reachable from the internet; Eco Gnome's Server Management page shows whether it is, and lets an admin set another address.
- `MarketWindowDays` — Default `7`. Days of sales the traded average covers.
- `MarketCurrency` — Empty by default: the currency that moved the most money.

## Installation

Download the latest `EcoGnomeMod.dll` and `StoreObject.cs` from the [Releases page](https://github.com/Eco-Gnome/eco-gnome-mod/releases) and drop them in `Mods/UserCode` in your server. Both files must come from the same release.

For the French texts, also copy `Translations/EcoGnome.csv` to `Mods/Translations` in your server.

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
