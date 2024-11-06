### Guide Block:
These are non-solid blocks which are invisible unless the player is using a **Guide Lens**, be sure to give your players one if you use these

### Waypoint Block:
When toggled on by wiring, these will display a marker showing their direction and distance, the blocks themselves are invisible without a **Demo Config Tool**

### Demo Config Tool:
This item allows you to configure demo item boxes, functions as a **Guide Lens**, and generally aids in creating demo worlds

### Demo Item Box:
This is not a block, but a functionality which can be added to any container
Players will be able to take an unlimited amount of the items contained within
#### To use this with your mod's items, you must set up a stat provider with the AddStatProvider call, the additional parameters should be your mod (as a `Terraria.ModLoader.Mod`), followed by a `Func<Item, Newtonsoft.Json.Linq.JObject>`
To set an item box up, open the container you want to add it to while holding a **Demo Config Tool**, this will open a UI showing the settings for the item box
The "enabled" toggle controls whether or not the container is a demo item box at all
The dropdown menu on the right side allows you to change how the items in the box are sorted
The text input field allows you to specify which items should be in the item box with the following syntax:

#### filter clauses:
`x=y` (is): matches a token named `x` which equals `y`
`x=y0,y1…` (has): matches an array token named `x` which contains every `y`
`x0.x1…` (child matches): matches a token named `x0` with a child token named `x1` which matches the continuation

#### filter modifiers:
`&` (and): makes the following filter combine with the existing filter to require that both are true, this is the default behavior
`|` (or): makes the following filter combine with the existing filter to only require that one be true
`-` (negate): inverts the following filter
`(x y)` (group): groups the filters within the parenthenses, to allow for more control over complex logic

For example: a=bees b.c.d<ham,cheese (c=seven | c=7)
matches the following example objects:
```json
{
	"a": "bees",
	"b": {
		"c": {
			"d": [
				"ham",
				"cheese"
			]
		} 
	},
	"c": "seven"
}
{
	"a": "bees",
	"b": {
		"c": {
			"d": [
				"ham",
				"cheese"
			]
		} 
	},
	"c": 7
}```
