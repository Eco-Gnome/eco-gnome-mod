# Eco Gnome Mod

## Optional server mod of [Eco Gnome](https://eco-gnome.com)

This mod connects an Eco server to [Eco Gnome](https://eco-gnome.com), the price calculator. There is no chat command and no code to copy: everything starts from the **Eco Gnome** tab of the stores (`StoreObject`, `WoodShopCartObject`) and is confirmed in the browser.

- Store owners fill and price their stores from Eco Gnome, and send their buy prices back to it.
- Players carry their Eco Gnome shopping lists in game and buy them in one go.
- Once registered, the server sends its data (skills, recipes, items, ...) to Eco Gnome by itself, and lets Eco Gnome read its market prices.

Requires the Eco 14.2 client: the tab uses the button grids (`ButtonGrid` in label mode).

## Getting started

1. **Install** the mod (see [Installation](#installation)) and restart the server.
2. **An admin registers the server.** In the Eco Gnome tab of any store, **Register this server on Eco Gnome** opens a page in the browser: create an Eco Gnome server or pick one you run, then confirm. Other players see the tab ask them to find an admin.
3. **Each player connects their account.** **Connect to Eco Gnome** opens a page in the browser: confirm, and the account joins the Eco Gnome server by itself.
4. **Use the tab.** Store owners get the price tools, everyone gets the shopping list buttons.

While a page waits in the browser, the mod checks every few seconds until it is confirmed, cancelled or expired. Clicking the button again starts over. If no page opened, the chat gives its link.

Tokens are kept in `Storage/EcoGnome.tokens.json` on the server. A link removed on the website (server or account) is noticed at the next use, and the matching button comes back.

## Eco Gnome tab

Each player only sees what they can use.

| Who | What they see |
|---|---|
| Anyone, server not registered | What Eco Gnome does, then **Register this server on Eco Gnome** (admins) or who to ask (others) |
| Account not connected | **Connect to Eco Gnome** |
| Connected customer | The shopping list buttons |
| Connected store owner | The status line, **Prices**, **Shopping list**, **Settings**, **Advanced settings** |

### Store owner

The status line shows the Eco Gnome account and the context the buttons use. Every action reports in a popup.

**Prices**
- **Update prices from Eco Gnome** — updates the prices of the offers already in the store. Adds and removes nothing. An item Eco Gnome tracks without a price leaves its offer unpriced (not tradable); the popup lists them. Also on **Shift + Right-click** on the store.
- **Rebuild offers from Eco Gnome** — adds the items you buy and sell, updates prices, removes the offers Eco Gnome no longer tracks. Shows what will be added and removed first and asks to confirm. New offers stay unpriced until Eco Gnome gives them a price.
- **Send buy prices to Eco Gnome** — the prices of the store's buy offers become the prices you pay for your ingredients in Eco Gnome. Items no recipe of yours uses are ignored and listed. Sell prices are never sent: Eco Gnome calculates them.

**Shopping list** — the same buttons as customers.

**Settings**
- **Context: ...** — picks which of your Eco Gnome contexts this store uses. Each store keeps its own; empty uses your default context.
- **Open Eco Gnome** — opens the website on this server.
- **Disconnect** — removes your connection from this server.

**Advanced settings** (checkbox) shows:
- **Offers synced** — `All`, `Buy` only or `Sell` only.
- **Sync tag offers** — `No` leaves tag offers untouched.
- **Group categories by** (`None`, `Margin`, `Skill`) / **Only these skills** — how **Rebuild offers** groups and filters the categories it creates.
- **For-sale radius** / **Reprice my for-sale objects** — prices every for-sale object you own within the radius around the store. Those Eco Gnome has no price for keep their own.

Categories whose name contains `[NS]` ("no sync") are never touched.

## Shopping lists

A shopping list from Eco Gnome becomes a **Shopping List** paper in your inventory, one paper per list. It holds the "Items to buy" of the list, with a progress per line (`250 / 500`).

1. **Get a shopping list** (store tab) — pick one of your Eco Gnome lists. You get its paper, or the paper you already carry for that list is updated.
2. **Shop.** Store purchases are counted for whoever carries the paper, and a line is ticked once its quantity is reached. When an item is on several carried lists, the oldest list is filled first.
3. **Buy my shopping list** (store tab, greyed out while you carry no list with lines left) — buys in one go what this store sells of the list. With several lists, pick one; with several bank accounts holding the store's currency, pick one. A recap shows what will be bought, the total and what is missing, and asks to confirm.
   **Ctrl + Right-click** on a store with a paper selected in the toolbar does the same for that list.

How the purchase is planned:
- Cheapest offers first, never more than what is left on a line or on the shelf.
- When the store can't deliver the whole list (shared stock, storage rules), it buys what goes through and says why.
- A store offer by tag is only used when every item it may deliver counts for the line, since the store picks which item is delivered. Other lines sold only by tag are listed in the recap as "buy these yourself".
- The trade goes through the store's own trade: laws, taxes and the journal apply.

The paper:
- **Right-click** it in your inventory to open it: progress per line, the items a tag line accepts, a **Done** checkbox per line, **Reset purchases** (to buy the same list again, e.g. every day) and **Reload from Eco Gnome** (fetches the list again and keeps what was already bought).
- **Selected in the toolbar**, it shows its lines in the bottom-right corner. Hovering an item (in a store too) shows how many each carried list still needs.
- It can't be sold in stores. It burns or recycles like paper.

Known limits:
- Purchases from for-sale objects (signs) are not counted: tick the line by hand.
- The paper is a copy: when the list changes on Eco Gnome, use **Reload from Eco Gnome**.
- Remove every paper before removing the mod, or the save may fail to load.

## Server data and market prices

Once the server is registered, at each start (one minute after, so the world and web server are up) and right after registering:
- **Server data** — sent to Eco Gnome only when it changed since the last upload. It is also written to `eco_gnome_data.json` in the server root folder for a manual upload (`eco_gnome_error.txt` if the export fails).
- **Market prices** — the server tells Eco Gnome where to reach its web server. Eco Gnome then reads the average prices of the market (store offers and recent sales) through a route this mod adds (`GET /api/ecognome/v1/market`, HMAC-signed requests only) and suggests them next to the buy prices players type. The web server port must be reachable from the internet; Eco Gnome's Server Management page shows whether it is, and lets an admin set another address.

Failures are logged with an `[EcoGnome]` prefix and never retried in a loop.

## Configuration

The plugin creates `Configs/EcoGnome.eco`:

- `EcoGnomeUrl` — Default `https://eco-gnome.com`. URL of the Eco Gnome API.
- `EcoGnomeUrlReverseProxy` — Optional. Used for the pages opened in the players' browser (when Eco Gnome is behind a reverse proxy).
- `AutoUploadData` — Default `true`. Send the server data at start when it changed.
- `ShareMarketPrices` — Default `true`. Let Eco Gnome read the market average prices. Requests are signed with a secret only this server and Eco Gnome know, kept in `Storage/EcoGnome.tokens.json`.
- `MarketPublicUrl` — Optional. Address Eco Gnome uses to reach the web server (e.g. behind a reverse proxy). Empty: the network config's `WebServerUrl`, else the server's public IP and `WebServerPort`.
- `MarketWindowDays` — Default `7`. Days of sales the traded average covers.
- `MarketCurrency` — Empty by default: the currency that moved the most money.

The plugin status (server admin panel) says whether the server is registered on Eco Gnome.

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

`StoreObject.cs` and the DLL talk through `IEcoGnomeService`: the server mods can't see the DLL, so the DLL registers itself in `EcoGnomeServiceRegistry` when it loads. A change to that interface needs both files rebuilt and shipped together.

## Contact
Zangdar (Discord: `#zangdar1111`)
Joridan (Discord: `#joridan`)
