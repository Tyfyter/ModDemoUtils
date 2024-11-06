using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace ModDemoUtils {
	public class Greend_Design : ModItem {
		public override void SetDefaults() {
			Item.CloneDefaults(ItemID.WireKite);
			Item.shoot = ProjectileID.None;
		}
		public override void UpdateInventory(Player player) {
			player.GetModPlayer<Guide_Lens_Player>().hasGreenDesign = true;
		}
	}
}
