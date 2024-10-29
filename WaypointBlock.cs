using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader.IO;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria;
using PegasusLib;
using Microsoft.Xna.Framework.Graphics;
using Terraria.Localization;
using Terraria.Chat;
using Terraria.UI.Chat;
using Terraria.GameContent;
using ReLogic.Graphics;

namespace ModDemoUtils {
	public class Waypoint_Block : ModTile {
		public static int ID { get; private set; }
		public override void SetStaticDefaults() {
			TileID.Sets.CanPlaceNextToNonSolidTile[Type] = true;
			TileID.Sets.CanBeSloped[Type] = true;
			Main.tileFrameImportant[Type] = true;
			ID = Type;
		}
		public override void HitWire(int i, int j) {
			Framing.GetTileSafely(i, j).TileFrameX ^= 18;
			NetMessage.SendTileSquare(-1, i, j);
		}
		public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
			return Main.LocalPlayer.GetModPlayer<Guide_Lens_Player>().hasGreenDesign;
		}
		public override void PlaceInWorld(int i, int j, Item item) {
			WaypointSystem.AddTileEntity(new(i, j));
		}
	}
	public class Waypoint_Block_Item : ModItem {
		public override string Texture => "Terraria/Images/Item_" + ItemID.PixelBox;
		public override void SetDefaults() {
			Item.DefaultToPlaceableTile(ModContent.TileType<Waypoint_Block>());
		}
	}
	public class WaypointSystem : ModSystem {
		public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
			int inventoryIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: MP Player Names"));
			if (inventoryIndex != -1) {//error prevention & null check
				layers.Insert(inventoryIndex + 1, new LegacyGameInterfaceLayer(
					"ModDemoUtils: Waypoints",
					delegate {
						DynamicSpriteFont font = FontAssets.MouseText.Value;
						Rectangle screenRect = new(0, 0, Main.screenWidth, Main.screenHeight);
						screenRect.Inflate(-8, -8);
						static Vector2 GetProjectedPoint(Rectangle rect, Vector2 a, Vector2 b) {
							float s = (a.Y - b.Y) / (a.X - b.X);
							float v = s * rect.Width / 2;
							if (-rect.Height / 2 <= v && v <= rect.Height / 2) {
								if (a.X > b.X) {
									return new Vector2(rect.Right, b.Y + v).Clamp(rect);
								} else {
									return new Vector2(rect.Left, b.Y - v).Clamp(rect);
								}
							} else {
								v = (rect.Height / 2) / s;
								if (a.Y > b.Y) {
									return new Vector2(b.X + v, rect.Bottom).Clamp(rect);
								} else {
									return new Vector2(b.X - v, rect.Top).Clamp(rect);
								}
							}
						}
						foreach (Point16 pos in tileEntities.ToList()) {
							Tile tile = Framing.GetTileSafely(pos);
							if (!tile.HasTile || tile.TileType != Waypoint_Block.ID) {
								tileEntities.Remove(pos);
								continue;
							}
							if (tile.TileFrameX != 0) {
								Vector2 offset = pos.ToWorldCoordinates() - Main.LocalPlayer.Center;
								float dist = offset.Length();
								string text = Language.GetTextValue("GameUI.PlayerDistance", (int)(dist / 16f * 2f));
								Vector2 size = font.MeasureString(text);
								Vector2 screenPosition = offset + Main.ScreenSize.ToVector2() * 0.5f;
								Rectangle _screenRect = screenRect;
								_screenRect.Inflate((int)(size.X * -0.5f), (int)(size.Y * -0.5f));
								Vector2 clampedScreenPosition = screenPosition;
								if (!screenRect.Contains(screenPosition.ToPoint())) {
									_screenRect.Inflate(-26, -26);
									clampedScreenPosition = GetProjectedPoint(_screenRect, screenPosition, Main.LocalPlayer.Center - Main.screenPosition);
									_screenRect = screenRect;
									_screenRect.Inflate(-13, -13);
									Vector2 cursorPosition = (clampedScreenPosition + (offset / dist) * 42).Clamp(_screenRect);
									Main.spriteBatch.Draw(
										TextureAssets.Cursors[12].Value,
										cursorPosition,
										null,
										Colors.RarityAmber.MultiplyRGB(Color.Gray),
										offset.ToRotation() + MathHelper.PiOver4 * 3,
										new(9),
										1,
										SpriteEffects.None,
									0);
									Main.spriteBatch.Draw(
										TextureAssets.Cursors[1].Value,
										cursorPosition,
										null,
										Colors.RarityAmber,
										offset.ToRotation() + MathHelper.PiOver4 * 3,
										new(7),
										1,
										SpriteEffects.None,
									0);
									ChatManager.DrawColorCodedStringWithShadow(
										Main.spriteBatch,
										font,
										text,
										clampedScreenPosition,
										Colors.RarityAmber,
										0,
										size * 0.5f,
										Vector2.One
									);
								}
							}
						}
						return true;
					},
					InterfaceScaleType.Game)
				);
			}
		}
		public HashSet<Point16> tileEntities = [];
		public static void AddTileEntity(Point16 pos) {
			if (Main.netMode == NetmodeID.SinglePlayer) {
				ModContent.GetInstance<WaypointSystem>().tileEntities.Add(pos);
			} else {
				ModPacket packet = ModContent.GetInstance<ModDemoUtils>().GetPacket();
				packet.Write((byte)ModDemoUtils.NetMessageType.PlaceWaypoint);
				packet.Write((short)pos.X);
				packet.Write((short)pos.Y);
				packet.Send();
			}
		}
		public static void RemoveTileEntity(Point16 pos) {
			if (Main.netMode == NetmodeID.SinglePlayer) {
				ModContent.GetInstance<WaypointSystem>().tileEntities.Remove(pos);
			} else {
				ModPacket packet = ModContent.GetInstance<ModDemoUtils>().GetPacket();
				packet.Write((byte)ModDemoUtils.NetMessageType.RemoveWaypoint);
				packet.Write((short)pos.X);
				packet.Write((short)pos.Y);
				packet.Send();
			}
		}
		public void SyncToPlayer(int player) {
			ModPacket packet = ModContent.GetInstance<ModDemoUtils>().GetPacket();
			packet.Write((byte)ModDemoUtils.NetMessageType.PlaceWaypoint);
			packet.Write((short)tileEntities.Count);
			foreach (Point16 pos in tileEntities) {
				packet.Write((short)pos.X);
				packet.Write((short)pos.Y);
			}
			packet.Send(toClient: player);
		}
		public override void SaveWorldData(TagCompound tag) {
			tag[$"{nameof(tileEntities)}"] = tileEntities.ToList();
		}
		public override void LoadWorldData(TagCompound tag) {
			tileEntities = tag.SafeGet<List<Point16>>($"{nameof(tileEntities)}", []).ToHashSet();
		}
	}
}
