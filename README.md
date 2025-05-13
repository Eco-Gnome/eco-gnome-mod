# Eco Gnome Mod

## Project is officially released !

This repository contains the Data Extractor part of [Eco Gnome](https://eco-gnome.com).   
It allows to extract your specific server configuration (skills, recipes, items, ...) to visualize and calculate their price in [Eco Gnome](https://eco-gnome.com) website.  
Extracted file will be created in your server file root folder: `eco_gnome_data.json`

It allows to synchronize prices between your shops and EcoGnome website thanks to three commands:
- /EcoGnome registerserver **{JoinCode}**   :   To be launched by admin during the setup of the server
- /EcoGnome registeruser **{SecretId}**     :   To be launched by all users only one time
- /EcoGnome open                            :   Opens Eco Gnome in your default browser. If the server is registered and you have joined it, it switches you to this server.
- /EcoGnome join                            :   Opens Eco Gnome in your default browser, and joins the server if it has been registered.
- /EcoGnome syncshop _{ContextName}_        :   Apply your EcoGnome prices on your targeted shop. It doesn't add or remove items, only edit prices of matching items. You can specify a Context Name to retrieve a specific context, or leave it blank to retrieve the default one.
- /EcoGnome syncshopsell _{ContextName}_    :   Same as syncshop, but for sell offers only.
- /EcoGnome syncshopbuy _{ContextName}_     :   Same as syncshop, but for buy offers only
- /EcoGnome createshop _{ContextName}_      :   Add offers for all items in Eco Gnome, grouped in categories by skills. You can specify a context name if you don't want to retrieve the default context.
- /EcoGnome createshopsell _{ContextName}_  :   Same as CreateShop, but creates only the sell offers.
- /EcoGnome createshopbuy _{ContextName}_   :   Same as CreateShop, but creates only the buy offers.

  
## Installation

Download the latest EcoGnomeMod.dll file from [Release page](https://github.com/Eco-Gnome/eco-gnome-mod/releases) and paste it in the folder Mods/ in your server

## Contact
Zangdar (Discord: #zangdar1111)  
Joridan (Discord: #joridan)
