using MonoMod.Cil;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace ModDemoUtils {
	// Please read https://github.com/tModLoader/tModLoader/wiki/Basic-tModLoader-Modding-Guide#mod-skeleton-contents for more information about the various files in a mod.
	public class ModDemoUtils : Mod {
		public Dictionary<int, JObject> stats = [];
		internal Dictionary<Mod, Func<Item, JObject>> statProviders = [];
		internal Dictionary<string, DemoDownloadData> demos = [];
		public override object Call(params object[] args) {
			switch (((string)args[0]).ToUpperInvariant()) {
				case "ADDSTATPROVIDER":
				statProviders.Add((Mod)args[1], (Func<Item, JObject>)args[2]);
				break;
				case "REGISTERDEMO":
				ModContent.GetInstance<DemoWorldDownloader>().ProcessDemoCall((Mod)args[1], (string)args[2]);
				break;
			}
			return null;
		}
		public override void Load() {
			On_ItemSorting.SetupWhiteLists += On_ItemSorting_SetupWhiteLists;
			IL_ChestUI.Draw += IL_ChestUI_Draw;
			On_Recipe.FindRecipes += On_Recipe_FindRecipes;
		}
		private static void On_ItemSorting_SetupWhiteLists(On_ItemSorting.orig_SetupWhiteLists orig) {
			orig();
			ModDemoUtils instance = ModContent.GetInstance<ModDemoUtils>();
			for (int i = ItemID.Count; i < ItemLoader.ItemCount; i++) {
				ContentSamples.ItemsByType.TryGetValue(i, out Item item);
				if (item?.ModItem?.Mod is not null && instance.statProviders.TryGetValue(item.ModItem?.Mod, out Func<Item, JObject> statProvider)) {
					JObject itemStats = (JObject)statProvider(item).DeepClone();
					itemStats["Mod"] = item.ModItem.Mod.Name;
					instance.stats.Add(i, itemStats);
				}
			}
		}
		private static void IL_ChestUI_Draw(ILContext il) {
			ILCursor c = new(il);
			ILLabel label = default;
			c.GotoNext(MoveType.After,
				i => i.MatchLdfld<Player>(nameof(Player.chest)),
				i => i.MatchLdcI4(-1),
				i => i.MatchBeq(out label)
			);
			c.EmitDelegate(() => Main.LocalPlayer.chest == -6);
			c.EmitBrtrue(label);
		}
		private static void On_Recipe_FindRecipes(On_Recipe.orig_FindRecipes orig, bool canDelayCheck) {
			if (Main.LocalPlayer.chest != -6) orig(canDelayCheck);
		}
		public override void HandlePacket(BinaryReader reader, int whoAmI) {
			switch ((NetMessageType)reader.ReadByte()) {
				case NetMessageType.PlaceDemoBox: {
					Point16 pos = new(reader.ReadInt16(), reader.ReadInt16());
					ModContent.GetInstance<DemoItemBoxSystem>().tileEntities.Add(pos, new());
					if (Main.netMode != NetmodeID.MultiplayerClient) DemoItemBoxSystem.AddTileEntity(pos);
					break;
				}
				case NetMessageType.RemoveDemoBox: {
					Point16 pos = new(reader.ReadInt16(), reader.ReadInt16());
					ModContent.GetInstance<DemoItemBoxSystem>().tileEntities.Remove(pos);
					if (Main.netMode != NetmodeID.MultiplayerClient) DemoItemBoxSystem.RemoveTileEntity(pos);
					break;
				}
				case NetMessageType.UpdateDemoBox: {
					Point16 pos = new(reader.ReadInt16(), reader.ReadInt16());
					string data = reader.ReadString();
					DemoBoxContents.SortType sortType = (DemoBoxContents.SortType)reader.ReadByte();
					ModContent.GetInstance<DemoItemBoxSystem>().tileEntities[pos] = new(data, sortType);
					if (Main.netMode != NetmodeID.MultiplayerClient) DemoItemBoxSystem.UpdateTileEntity(pos, data, sortType);
					break;
				}
			}
		}
		public enum NetMessageType : byte {
			PlaceDemoBox,
			RemoveDemoBox,
			UpdateDemoBox,
		}
	}
	public class DemoSyncPlayer : ModPlayer {
		bool dummyInitialize = false;
		bool netInitialized = false;
		public override void SyncPlayer(int toWho, int fromWho, bool newPlayer) {
			if (Main.netMode == NetmodeID.Server) {
				if (!netInitialized) {
					Mod.Logger.Info($"NetInit {netInitialized}, {Player.name}, dummyInitialize: {dummyInitialize}");
					netInitialized = dummyInitialize;
					ModContent.GetInstance<DemoItemBoxSystem>().SyncToPlayer(Player.whoAmI);
				}
				if (!dummyInitialize) dummyInitialize = true;
			}
		}
	}
}
