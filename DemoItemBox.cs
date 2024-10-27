using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.Audio;
using Terraria.ID;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;
using PegasusLib;
using Terraria.ObjectData;
using Terraria.DataStructures;
using Terraria.ModLoader.IO;
using Newtonsoft.Json.Linq;
using Microsoft.Xna.Framework;
using static ModDemoUtils.DemoBoxContents;

namespace ModDemoUtils {
	public class DemoItemBox : GlobalTile {
		public override void RightClick(int i, int j, int type) {
			if (!Main.tileFrameImportant[type] || !Main.tileContainer[type]) return;
			int style = TileObjectData.GetTileStyle(Main.tile[i, j]);
			if (style < 0) return;
			PegasusLib.PegasusLib.GetMultiTileTopLeft(i, j, TileObjectData.GetTileData(type, style), out int x, out int y);
			if (Main.LocalPlayer.HeldItem.type == ModContent.ItemType<GreendDesign>()) {
				Main.LocalPlayer.SetTalkNPC(-1);
				Main.npcChatCornerItem = 0;
				SoundEngine.PlaySound(SoundID.MenuOpen);
				IngameFancyUI.OpenUIState(new DemoItemBoxSetupUI((short)x, (short)y));
				Main.LocalPlayer.chest = -1;
			} else if (ModContent.GetInstance<DemoItemBoxSystem>().tileEntities.ContainsKey(new((short)x, (short)y))) {
				Main.LocalPlayer.chest = -6;
				Main.playerInventory = true;
				Main.editChest = false;
				Main.npcChatText = "";
				Main.ClosePlayerChat();
				Main.chatText = "";
				ModContent.GetInstance<DemoItemBoxSystem>().itemBoxUI.SetState(new DemoItemBoxUI((short)x, (short)y));
			}
		}
	}
	public class DemoItemBoxSystem : ModSystem {
		public UserInterface itemBoxUI = new();
		public override void UpdateUI(GameTime gameTime) {
			if (itemBoxUI.CurrentState is not null && (!Main.playerInventory || Main.LocalPlayer.chest != -6)) {
				itemBoxUI.SetState(null);
			}
			itemBoxUI.Update(gameTime);
		}
		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
			int inventoryIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Inventory"));
			if (inventoryIndex != -1) {//error prevention & null check
				layers.Insert(inventoryIndex + 1, new LegacyGameInterfaceLayer(
					"ModDemoUtils: Demo Item Box UI",
					delegate {
						itemBoxUI?.Draw(Main.spriteBatch, Main._drawInterfaceGameTime);
						return true;
					},
					InterfaceScaleType.UI) { Active = Main.playerInventory }
				);
			}
		}

		public Dictionary<Point16, DemoBoxContents> tileEntities = [];
		public static void ToggleTileEntity(short x, short y) {
			Point16 pos = new(x, y);
			if (ModContent.GetInstance<DemoItemBoxSystem>().tileEntities.ContainsKey(pos)) {
				RemoveTileEntity(pos);
			} else {
				AddTileEntity(pos);
			}
		}
		public static void AddTileEntity(Point16 pos) {
			if (Main.netMode == NetmodeID.SinglePlayer) {
				ModContent.GetInstance<DemoItemBoxSystem>().tileEntities.Add(pos, new());
			} else {
				ModPacket packet = ModContent.GetInstance<ModDemoUtils>().GetPacket();
				packet.Write((byte)ModDemoUtils.NetMessageType.PlaceDemoBox);
				packet.Write((short)pos.X);
				packet.Write((short)pos.Y);
				packet.Send();
			}
		}
		public static void RemoveTileEntity(Point16 pos) {
			if (Main.netMode == NetmodeID.SinglePlayer) {
				ModContent.GetInstance<DemoItemBoxSystem>().tileEntities.Remove(pos);
			} else {
				ModPacket packet = ModContent.GetInstance<ModDemoUtils>().GetPacket();
				packet.Write((byte)ModDemoUtils.NetMessageType.RemoveDemoBox);
				packet.Write((short)pos.X);
				packet.Write((short)pos.Y);
				packet.Send();
			}
		}
		public static void UpdateTileEntity(Point16 pos, string data, SortType sortType, int toPlayer = -1) {
			if (Main.netMode == NetmodeID.SinglePlayer) {
				ModContent.GetInstance<DemoItemBoxSystem>().tileEntities[pos] = new(data, sortType);
			} else {
				ModPacket packet = ModContent.GetInstance<ModDemoUtils>().GetPacket();
				packet.Write((byte)ModDemoUtils.NetMessageType.UpdateDemoBox);
				packet.Write((short)pos.X);
				packet.Write((short)pos.Y);
				packet.Write((string)data);
				packet.Write((byte)sortType);
				packet.Send();
			}
		}
		public void SyncToPlayer(int player) {
			foreach (var item in tileEntities) {
				UpdateTileEntity(item.Key, item.Value.text, item.Value.sortType, player);
			}
		}
		public override void SaveWorldData(TagCompound tag) {
			tag[$"{nameof(tileEntities)}"] = tileEntities.Select(kvp => new TagCompound {
				["key"] = kvp.Key,
				["data"] = kvp.Value.text,
				["sortType"] = kvp.Value.sortType.ToString(),
			}).ToList();
		}
		public override void LoadWorldData(TagCompound tag) {
			tileEntities = tag
				.SafeGet<List<TagCompound>>($"{nameof(tileEntities)}", [])
				.Select(t => (t.SafeGet<Point16>("key"), new DemoBoxContents(t.SafeGet("data", string.Empty), Enum.TryParse(t.SafeGet("sortType", string.Empty), out SortType sortType) ? sortType : SortType.ID)))
				.ToDictionary();
		}
	}
	public class DemoBoxContents(string text, SortType sortType) {
		public readonly string text = text ?? string.Empty;
		public readonly SortType sortType = sortType;
		List<Item> items;
		public DemoBoxContents() : this(null, SortType.ID) { }
		public List<Item> GetItems() {
			if (items is null) {
				items = [];
				Filter filter = CreateFilter(text);
				ModDemoUtils instance = ModContent.GetInstance<ModDemoUtils>();
				for (int i = ItemID.Count; i < ItemLoader.ItemCount; i++) {
					if (instance.stats.TryGetValue(i, out JObject stats) && filter.Matches(stats)) items.Add(ContentSamples.ItemsByType[i]);
				}
				switch (sortType) {
					case SortType.Rarity:
					items.Sort((a, b) => a.rare - b.rare);
					break;
					case SortType.Damage:
					items.Sort((a, b) => a.damage - b.damage);
					break;
				}
			}
			return items;
		}
		public enum SortType : byte{
			ID,
			Rarity,
			Damage
		}
		public static Filter CreateFilter(string text) {
			return CreateFilter(text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
		}
		public static Filter CreateFilter(string[] tags) {
			bool isOr = false;
			Filter currentFilter = null;
			for (int i = 0; i < tags.Length; i++) {
				string tag = tags[i];
				bool isNot = false;
				Filter newFilter = null;
				reparse:
				if (tag == "") continue;
				switch (tag[0]) {
					case '|':
					isOr = true;
					tag = tag[1..];
					goto reparse;
					case '&':
					tag = tag[1..];
					goto reparse;

					case '-':
					isNot ^= true;
					tag = tag[1..];
					goto reparse;

					case '(':
					int oldI = i;
					for (i++; i < tags.Length; i++) {
						if (tags[i][^1] == ')') {
							string[] test = new string[i - oldI + 1];
							for (int k = oldI + 1; k < i; k++) {
								test[k - oldI] = tags[k];
							}
							test[0] = tag[1..];
							test[^1] = tags[i][..^1];
							newFilter = CreateFilter(test);
							break;
						}
					}
					break;

					default:
					newFilter = ParseSingleFilter(tag);
					break;
				}
				if (newFilter is null) throw new Exception("you formatted something wrong and a null got in here");

				if (isNot) newFilter = new NotFilter(newFilter);
				if (currentFilter is null) {
					currentFilter = newFilter;
				} else {
					currentFilter = isOr ? new OrFilter(currentFilter, newFilter) : new AndFilter(currentFilter, newFilter);
				}
				isOr = false;
			}
			return currentFilter;
		}
		public static Filter ParseSingleFilter(string text) {
			string[] sections = text.Split('.');
			bool valid = false;
			Filter filter = null;
			string[] subsections = sections[^1].Split("<", 2);
			if (subsections.Length == 2) {
				valid = true;
				filter = new ChildMatchesFilter(subsections[0], new HasFilter(subsections[1].Split(',').Select(
					s => s[0] == '.' ? ParseSingleFilter(s[1..]) : new IsFilter(s)
				).ToArray()));
			} else {
				subsections = sections[^1].Split("=");
				if (subsections.Length == 2) {
					valid = true;
					filter = new ChildMatchesFilter(subsections[0], new IsFilter(subsections[1]));
				}
			}
			if (!valid) throw new Exception($"{text} is not a valid filter string, a valid filter string must end in exactly one \"is\" (__=__) or \"has\" (__<__,__) clause");
			for (int i = 2; i < sections.Length + 1; i++) {
				filter = new ChildMatchesFilter(sections[^i], filter);
			}
			return filter;
		}
		public abstract class Filter {
			public abstract bool Matches(JToken data);
		}
		public class ChildMatchesFilter(string name, Filter filter) : Filter {
			public string Name => name;
			public Filter Filter => filter;
			public override bool Matches(JToken data) => data is JObject obj && obj.TryGetValue(name, out JToken child) && filter.Matches(child);
			public override string ToString() => $"{name}.{filter}";
		}
		public class IsFilter(string value) : Filter {
			public string Value => value;
			public override bool Matches(JToken data) => data.ToString() == value;
			public override string ToString() => $"={value}";
		}
		public class HasFilter(params Filter[] filters) : Filter {
			public Filter[] Filters => filters;
			public override bool Matches(JToken data) {
				bool ret;
				if (data is JArray array) {
					ret = !filters.Any(filter => !array.Any(filter.Matches));
					return ret;
				}
				if (filters.Any(f => f is not IsFilter)) throw new Exception($"I don't even know how I'd go about supporting \"has\" clauses like {this} on things other than arrays");
				string dataString = data.ToString();
				ret = !filters.Any(filter => !dataString.Contains(((IsFilter)filter).Value));
				return ret;
			}
			public override string ToString() => $"<{string.Join<Filter>(',', filters)}";
		}
		public class NotFilter(Filter a) : Filter {
			public Filter A => a;
			public override bool Matches(JToken data) => !a.Matches(data);
			public override string ToString() => $"-{a}";
		}
		public class AndFilter(Filter a, Filter b) : Filter {
			public Filter A => a;
			public Filter B => b;
			public override bool Matches(JToken data) => a.Matches(data) && b.Matches(data);
			public override string ToString() => $"{a} & {b}";
		}
		public class OrFilter(Filter a, Filter b) : Filter {
			public Filter A => a;
			public Filter B => b;
			public override bool Matches(JToken data) => a.Matches(data) || b.Matches(data);
			public override string ToString() => $"{a} | {b}";
		}
		public class HiddenFilter : Filter {
			public override bool Matches(JToken data) => data is JObject obj && obj.TryGetValue("Types", out JToken child) && child is JArray array && array.Any(v => v.ToString() == "Hidden");
			public override string ToString() => $"hidden";
		}
		public class CategoryCast {
			public string name;
			public string page;
			public string items;
		}
		public class CategoryObject {
			public string name;
			public string page;
			public string[] items;
		}
	}
}
