using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.ID;
using Terraria.ModLoader;

namespace ModDemoUtils {
	public class GreendDesign : ModItem {
		public override void SetDefaults() {
			Item.CloneDefaults(ItemID.WireKite);
		}
	}
}
