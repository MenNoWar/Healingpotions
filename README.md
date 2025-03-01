
# Healingpotions

## What..
Healingpotions adds four different Healing-, Stamina- and Eitr-Potions that will add different percent values of your current maximal stat 
and are crafted in a cauldron.

As the only alternative for vanilla meads (at least i currently found) only adds absolute values this one adds 
a percentage of your current base stat. You will get the difference when having around 300 of a stat :)

## Potions
Each of the potion recipes need by default Dandelion as base item (configurable).<br />

The **second** required item specifies the quality of the potion.<br />
- Mushroom = Small Potion = gives 25% of the stat back<br />
- Thistle = Medium Potion = gives 50% of the stat back<br />
- Obsidian = Huge Potion = gives 75% of the stat back<br />
- Flax = Godly Potion = gives 100% of the stat back<br />

The **3rd** item specifies the type of the potion.<br />
- Raspberry = Healing Potion<br />
- Blueberries = Stamina Potion<br />
- MushroomYellow = Eitr/Mana Potion<br />

#### Hint: 
- the potions look nice as decoration on an itemstand (depending on taste)
- each potion has a configurable cooldown before next use
- the item(s) to be used and amount are totally configurable

## ConfigurationManager
[Official BepInEx ConfigurationManager by Azumat](https://valheim.thunderstore.io/package/Azumatt/Official_BepInEx_ConfigurationManager/) is supported. <br />The only quirk which requires restarting the game is when enabling/disabling bottles, 
as my Mod is not able to add/remove the items dynamically.<br /> All other settings like amounts/item names are updated on the fly.<br/> Other ConfigurationManagers like from cjayride may or may not work as i have not tested them.

## Versions
### 1.0.4
+ changed Jotunn Version Dependency to 2.23.2

### 1.0.3
+ changed Jotunn Version Dependency to 2.10.4
+ updated/added icons for the bottles
+ added ability to configure most settings (ServerSync enabled)
+ added Stamina potions
+ added Eitr (Mana) potions
+ changed default requirements for healing potions
+ updated unity asset-bundle
+ updated assets

### 1.0.2
+ added better looking models for the potions
+ added fancy pointlight to bottle, when in itemstand
+ fixed a bug which threw an error when logging out of valheim (Thx to Margmas!)

### 1.0.1
+ removed falsely assigned "pickable" script from asset, which in some cases prevented picking up a dropped potion
+ fine tuned the clay-part texture to look more like Valheim item

### 1.0.0
Initial Version

## Thanks
go to beloved "<b>schattentraum</b>" for her supervision of my stuff and ideas for improvement <br />
and to the great, supporting [community of the J&ouml;tunn Mod](https://discord.gg/DdUt6g7gyA) - you are awesome!