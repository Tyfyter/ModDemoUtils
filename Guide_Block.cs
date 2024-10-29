using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace ModDemoUtils {
	public class Guide_Block : ModTile {
		public override void SetStaticDefaults() {
			TileID.Sets.CanPlaceNextToNonSolidTile[Type] = true;
			TileID.Sets.CanBeSloped[Type] = true;
		}
		public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
			if (Main.tile[i, j].IsTileFullbright) return true;
			Player player = Main.LocalPlayer;
			return player.HeldItem.type == ModContent.ItemType<Guide_Lens>() || (player.GetModPlayer<Guide_Lens_Player>().HasGuideLens && !player.hideInfo[ModContent.GetInstance<Guide_Lens_Info>().Type]);
		}
		public override void DrawEffects(int i, int j, SpriteBatch spriteBatch, ref TileDrawInfo drawData) {
			drawData.tileLight = Color.White;
		}
	}
	public class Guide_Block_Item : ModItem {
		public override string Texture => "Terraria/Images/Item_" + ItemID.PixelBox;
		public override void SetDefaults() {
			Item.DefaultToPlaceableTile(ModContent.TileType<Guide_Block>());
		}
	}
	public class Guide_Lens : ModItem {
		public override void UpdateInventory(Player player) {
			player.GetModPlayer<Guide_Lens_Player>().hasGuideLens = true;
		}
	}
	public class Guide_Lens_Info : InfoDisplay {
		public override bool Active() => Main.LocalPlayer.GetModPlayer<Guide_Lens_Player>().HasGuideLens;
		public override string DisplayValue(ref Color displayColor, ref Color displayShadowColor) => this.GetLocalizedValue("Display");
	}
	public class Guide_Lens_Player : ModPlayer {
		public bool hasGuideLens = false;
		public bool hasGreenDesign = false;
		public bool HasGuideLens {
			get => hasGuideLens || hasGreenDesign;
			set {
				if (value) {
					hasGuideLens = true;
				} else {
					hasGuideLens = false;
					hasGreenDesign = false;
				}
			}
		}
		public override void ResetEffects() {
			hasGuideLens = false;
			hasGreenDesign = false;
		}
	}
}
