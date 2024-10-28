using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Ionic.Zip;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using MonoMod.Cil;
using Newtonsoft.Json.Linq;
using ReLogic.Content;
using ReLogic.OS;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.IO;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.UI;
using Terraria.UI;

namespace ModDemoUtils {
	public class DemoWorldDownloader : ILoadable {
		readonly string baseCachePath = Path.Combine(Main.SavePath, nameof(ModDemoUtils), "DemoWorlds");
		HttpClient client = new(new HttpClientHandler() { UseDefaultCredentials = true });
		public void ProcessDemoCall(Mod mod, string path) {
			string cachePath = Path.Combine(baseCachePath, mod.Name);
			Directory.CreateDirectory(cachePath);
			string versionFile = Path.Combine(cachePath, "version");
			string pathFile = Path.Combine(cachePath, "download_path");
			if (File.Exists(versionFile) && File.ReadAllText(versionFile) == mod.Version.ToString()) {
				DemoDownloadData data = File.Exists(pathFile) ? DemoDownloadData.From(File.ReadAllText(pathFile)) : DemoDownloadData.Downloaded;
				ModContent.GetInstance<ModDemoUtils>().demos.Add(mod.Name, data);
			} else {
				Directory.Delete(cachePath);
				Directory.CreateDirectory(cachePath);
				client.SendAsync(new HttpRequestMessage(HttpMethod.Get, new Uri($"https://api.github.com/repos/{path}/releases")).WithUserAgent())
				.ContinueWith(async task => {
					task.Result.EnsureSuccessStatusCode();
					foreach (JObject release in JArray.Parse(await task.Result.Content.ReadAsStringAsync()).Where(r => r is JObject).Cast<JObject>()) {
						HttpResponseMessage response = client.Send(new HttpRequestMessage(HttpMethod.Get, release["assets_url"].ToString()).WithUserAgent());
						response.EnsureSuccessStatusCode();
						foreach (JObject artifact in JArray.Parse(await response.Content.ReadAsStringAsync()).Where(r => r is JObject).Cast<JObject>()) {
							string artifactName = artifact["name"].ToString();
							if (artifactName == "ModDemoUtils_Demos.zip") {
								string path = artifact["browser_download_url"].ToString();
								ModContent.GetInstance<ModDemoUtils>().demos.Add(mod.Name, DemoDownloadData.From(path));
								File.WriteAllText(pathFile, path);
								File.WriteAllText(versionFile, mod.Version.ToString());
								return;
							}
						}
					}
				});
			}
		}

		public void Load(Mod mod) {
			On_Main.LoadPlayers += On_Main_LoadPlayers;
			On_Main.LoadWorlds += On_Main_LoadWorlds;
			On_UICharacterListItem.ctor += On_UICharacterListItem_ctor;
			On_UIWorldListItem.ctor += On_UIWorldListItem_ctor;
			On_Main.DrawMenu += On_Main_DrawMenu;
			MonoModHooks.Modify(typeof(UICommon).Assembly.GetType("Terraria.ModLoader.UI.UIModItem").GetMethod(nameof(UIElement.OnInitialize)), IL_UIModItem_ctor);
			DemoButton = mod.Assets.Request<Texture2D>("DemoWorlds");
		}
		Asset<Texture2D> DemoButton;
		private void On_Main_DrawMenu(On_Main.orig_DrawMenu orig, Main self, GameTime gameTime) {
			orig(self, gameTime);
			if (Main.menuMode != 888 && SelectedModDemo is not null) SelectedModDemo = null;
		}
		public void IL_UIModItem_ctor(ILContext il) {
			ILCursor c = new(il);
			ILLabel end = default;
			int result = -1;
			int bottomRightRowOffset = -1;
			c.GotoNext(MoveType.After,
				i => i.MatchLdarg0(), i => i.MatchCall(out MethodReference md) && md.Name == "get_ModName", i => i.MatchLdloca(out result), i => i.MatchCall(typeof(ModLoader), nameof(ModLoader.TryGetMod)), i => i.MatchBrfalse(out end),
				i => i.MatchLdsfld(typeof(ConfigManager), "Configs"), i => i.MatchLdloc(result), i => i.MatchCallOrCallvirt(out MethodReference call) && call.Name == "ContainsKey", i => i.MatchBrfalse(out ILLabel other) && other.Target == end.Target,
				i => i.MatchLdloc(out bottomRightRowOffset), i => i.MatchLdcI4(36), i => i.MatchSub(), i => i.MatchStloc(bottomRightRowOffset)
			);
			c.GotoLabel(end, MoveType.AfterLabel);
			c.EmitLdarg0();
			c.EmitBox(typeof(UIElement));
			c.EmitLdloc(result);
			c.EmitLdloca(bottomRightRowOffset);
			Dictionary<string, DemoDownloadData> demos = ModContent.GetInstance<ModDemoUtils>().demos;
			c.EmitDelegate<AddModItemButton>((UIElement parent, Mod mod, ref int bottomRightRowOffset) => {
				if (mod is null || !demos.ContainsKey(mod.Name)) return;
				bottomRightRowOffset -= 36;
				UITooltipImage demoButton = new(DemoButton, Language.GetOrRegister("Mods.ModDemoUtils.DemoButton")) {
					RemoveFloatingPointsFromDrawPosition = true,
					Width = {
						Pixels = 36f
					},
					Height = {
						Pixels = 36f
					},
					Left = {
						Pixels = bottomRightRowOffset - 10f,
						Precent = 1f
					},
					Top = {
						Pixels = 40f
					}
				};
				demoButton.OnLeftClick += async (_, _) => {
					await DownloadDemo(mod);
					SelectedModDemo = Path.Combine(baseCachePath, mod.Name);
					Main.OpenCharacterSelectUI();
				};
				parent.Append(demoButton);
			});
		}
		class UITooltipImage(Asset<Texture2D> texture, LocalizedText tooltip) : UIImage(texture) {
			bool hovered = false;
			public override void MouseOver(UIMouseEvent evt) => hovered = true;
			public override void MouseOut(UIMouseEvent evt) => hovered = false;
			protected override void DrawSelf(SpriteBatch spriteBatch) {
				base.DrawSelf(spriteBatch);
				if (hovered) UICommon.TooltipMouseText(tooltip.Value);
			}
		}
		delegate void AddModItemButton(UIElement parent, Mod mod, ref int bottomRightRowOffset);
		private void On_UICharacterListItem_ctor(On_UICharacterListItem.orig_ctor orig, UICharacterListItem self, PlayerFileData data, int snapPointIndex) {
			orig(self, data, snapPointIndex);
			if (isInDemoSelect) {
				List<UIElement> children = self.Children.Reverse().ToList();
				for (int i = 0; i < children.Count; i++) {
					if (children[i].GetSnapPoint(out SnapPoint snapPoint) && snapPoint.Name is "Favorite" or "Cloud" or "Rename" or "Delete") self.RemoveChild(children[i]);
				}
			}
		}
		private void On_UIWorldListItem_ctor(On_UIWorldListItem.orig_ctor orig, UIWorldListItem self, WorldFileData data, int orderInList, bool canBePlayed) {
			orig(self, data, orderInList, canBePlayed);
			if (isInDemoSelect) {
				List<UIElement> children = self.Children.Reverse().ToList();
				for (int i = 0; i < children.Count; i++) {
					if (children[i].GetSnapPoint(out SnapPoint snapPoint) && snapPoint.Name is "Favorite" or "Cloud" or "Seed" or "Rename" or "Delete") self.RemoveChild(children[i]);
				}
			}
		}

		public void Unload() {
			client.Dispose();
			client = null;
		}
		public string SelectedModDemo { get; internal set; }
		bool isInDemoSelect = false;
		private void On_Main_LoadPlayers(On_Main.orig_LoadPlayers orig) {
			if (SelectedModDemo is null || !Directory.CreateDirectory(SelectedModDemo).EnumerateFiles().Any(fi => fi.Extension == ".plr")) {
				isInDemoSelect = false;
				orig();
			} else {
				isInDemoSelect = true;
				Main.PlayerList.Clear();
				string[] files = Directory.GetFiles(SelectedModDemo, "*.plr");
				int maxPlayers = Math.Min(1000, files.Length);
				for (int i = 0; i < maxPlayers; i++) {
					PlayerFileData fileData = Player.GetFileData(files[i], cloudSave: false);
					if (fileData != null) {
						Main.PlayerList.Add(fileData);
					}
				}
				Main.PlayerList.Sort((a, b) => a.Name.CompareTo(b.Name));
			}
		}
		private void On_Main_LoadWorlds(On_Main.orig_LoadWorlds orig) {
			if (SelectedModDemo is null || !Directory.CreateDirectory(SelectedModDemo).EnumerateFiles().Any(fi => fi.Extension == ".wld")) {
				isInDemoSelect = false;
				orig();
			} else {
				isInDemoSelect = true;
				Main.WorldList.Clear();
				string[] files = Directory.GetFiles(SelectedModDemo, "*.wld");
				int maxWorlds = Math.Min(files.Length, 1000);
				for (int j = 0; j < maxWorlds; j++) {
					WorldFileData allMetadata2 = WorldFile.GetAllMetadata(files[j], cloudSave: false);
					if (allMetadata2 != null) {
						Main.WorldList.Add(allMetadata2);
					} else {
						Main.WorldList.Add(WorldFileData.FromInvalidWorld(files[j], cloudSave: false));
					}
				}
				Main.WorldList.Sort((a, b) => a.Name.CompareTo(b.Name));
			}
		}
		async Task DownloadDemo(Mod mod) {
			if (!ModContent.GetInstance<ModDemoUtils>().demos.TryGetValue(mod.Name, out DemoDownloadData downloadData)) return;
			if (downloadData.IsDownloaded) return;
			await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, downloadData.DownloadPath).WithUserAgent()).ContinueWith(download => {
				download.Result.EnsureSuccessStatusCode();
				string cachePath = Path.Combine(baseCachePath, mod.Name);
				using (ZipFile zipped = ZipFile.Read(download.Result.Content.ReadAsStream())) {
					zipped.ExtractAll(cachePath);
				}
				File.Delete(Path.Combine(cachePath, "download_path"));
			});
		}
	}
	public record struct DemoDownloadData(string DownloadPath) {
		public bool IsDownloaded { get; init; }
		public static DemoDownloadData Downloaded => new(null) {
			IsDownloaded = true
		};
		public static DemoDownloadData From(string path) => new(path);
	}
	internal static class HttpRequestMessageExt {
		internal static HttpRequestMessage WithUserAgent(this HttpRequestMessage message) {
			message.Headers.UserAgent.Add(new("Tyfyer.ModDemoUtils", ModContent.GetInstance<ModDemoUtils>().Version.ToString()));
			return message;
		}
	}
}
