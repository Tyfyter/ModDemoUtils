using Humanizer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PegasusLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.UI;
using Terraria.UI;

namespace ModDemoUtils {
	public class DemoItemBoxSetupUI(short x, short y) : UIState {
		public static SoundStyle ClickSound => SoundID.MenuTick;
		public override void OnInitialize() {
			UIPanel panel = new UIPanel();
			panel.Width.Set(0f, 0.875f);
			panel.MaxWidth.Set(900f, 0f);
			panel.MinWidth.Set(700f, 0f);
			panel.Top.Set(-50f, 0.5f);
			panel.Height.Set(100f, 0f);
			panel.HAlign = 0.5f;
			Append(panel);

			UISearchBar input = new(LocalizedText.Empty, 1);
			input.Width.Set(0, 1f);
			input.Height.Set(0, 1f);
			if (ModContent.GetInstance<DemoItemBoxSystem>().tileEntities.TryGetValue(new(x, y), out DemoBoxContents contents)) {
				input.SetContents(contents.text);
			}
			input.OnContentsChanged += (data) => {
				DemoItemBoxSystem.UpdateTileEntity(new(x, y), data);
			};
			UITextBox textBox = (UITextBox)input.Children.First();
			//textBox.BackgroundColor = new Color(63, 82, 151) * 0.7f;
			//textBox.BorderColor = Color.Black;
			textBox.SetTextMaxLength(100);
			UIPanel inputPanel = new UIPanel();
			inputPanel.Width.Set(0, 1f);
			inputPanel.Height.Set(-36, 1f);
			inputPanel.OnLeftClick += (_, _) => {
				if (!textBox.ShowInputTicker) {
					input.ToggleTakingText();
				}
			};
			void SetTextFieldMode(bool enabled) {
				inputPanel.IgnoresMouseInteraction = !enabled;
				if (enabled) {
					inputPanel.BackgroundColor = new Color(63, 82, 151) * 0.7f;
				} else {
					inputPanel.BackgroundColor = new Color(73, 82, 111) * 0.7f;
					if (input.IsWritingText) input.ToggleTakingText();
				}
			}
			SetTextFieldMode(ModContent.GetInstance<DemoItemBoxSystem>().tileEntities.ContainsKey(new(x, y)));
			inputPanel.Append(input);
			panel.Append(inputPanel);

			UIToggleSwitch enableButton = new(
				() => {
					DemoItemBoxSystem.ToggleTileEntity(x, y);
					SetTextFieldMode(ModContent.GetInstance<DemoItemBoxSystem>().tileEntities.ContainsKey(new(x, y)));
				},
				() => ModContent.GetInstance<DemoItemBoxSystem>().tileEntities.ContainsKey(new(x, y)),
				Language.GetOrRegister("Mods.ModDemoUtils.DemoItemBox.Enabled"),
				Language.GetOrRegister("Mods.ModDemoUtils.DemoItemBox.Disabled")
			);
			enableButton.Width.Set(-10, 1f);
			enableButton.Height.Set(32, 0f);
			enableButton.Top.Set(-32, 1f);
			panel.Append(enableButton);
		}
	}
	public class UIToggleSwitch(Action toggle, Func<bool> getState, LocalizedText on, LocalizedText off) : UIPanel {
		static AutoLoadingAsset<Texture2D> toggleTexture = "Terraria/Images/UI/Settings_Toggle";
		bool state = getState();
		public override void LeftClick(UIMouseEvent evt) {
			toggle();
			state = getState();
			SoundEngine.PlaySound(DemoItemBoxSetupUI.ClickSound);
		}
		public new Color BorderColor = Color.Black;
		public new Color BackgroundColor = new Color(63, 82, 151) * 0.7f;
		public Color HoverBorderColor = new Color(33, 33, 33) * 0.9f;
		public Color HoverBackgroundColor = new Color(93, 113, 187) * 0.9f;
		protected override void DrawSelf(SpriteBatch spriteBatch) {
			Rectangle bounds = GetDimensions().ToRectangle();
			if (bounds.Contains(Main.mouseX, Main.mouseY)) {
				base.BorderColor = HoverBorderColor;
				base.BackgroundColor = HoverBackgroundColor;
			} else {
				base.BorderColor = BorderColor;
				base.BackgroundColor = BackgroundColor;
			}
			base.DrawSelf(spriteBatch);

			Utils.DrawBorderString(spriteBatch, (state ? on : off).Value, bounds.Left() + Vector2.UnitX * 8, Color.White, 1, 0f, 0.4f, -1);
			Texture2D value = toggleTexture;
			Rectangle sourceRectangle = new(state ? ((value.Width - 2) / 2 + 2) : 0, 0, (value.Width - 2) / 2, value.Height);
			spriteBatch.Draw(toggleTexture, bounds.Right() - Vector2.UnitX * 8 - sourceRectangle.Size() * new Vector2(1, 0.5f), sourceRectangle, Color.White);
		}
	}
	public class DemoItemBoxUI(short x, short y) : UIState {
		public override void OnInitialize() {
			if (ModContent.GetInstance<DemoItemBoxSystem>().tileEntities.TryGetValue(new(x, y), out DemoBoxContents contents)) {
				Append(new DemoItemBoxItems(contents.GetItems()));
			}
		}
	}
	public class DemoItemBoxItems(List<Item> items) : UIElement {
		public override void OnInitialize() {
			Left.Set(73f, 0);
			Left.Set(Main.instance.invBottom, 0);
			Left.Set(560f * 0.755f, 0);
			Left.Set(224f * 0.755f, 0);
		}
		protected override void DrawSelf(SpriteBatch spriteBatch) {
			float inventoryScale = Main.inventoryScale;
			Main.inventoryScale = 0.755f;
			Player player = Main.LocalPlayer;
			for (int i = 0; i < 10; i++) {
				for (int j = 0; j < 4; j++) {
					int slot = i + j * 10;
					int num = (int)(73f + (i * 56) * Main.inventoryScale);
					int num2 = (int)(Main.instance.invBottom + (j * 56) * Main.inventoryScale);
					Item item = slot >= items.Count ? new() : items[slot].Clone();
					if (!PlayerInput.IgnoreMouseInterface && Utils.FloatIntersect(Main.mouseX, Main.mouseY, 0f, 0f, num, num2, TextureAssets.InventoryBack.Width() * Main.inventoryScale, TextureAssets.InventoryBack.Height() * Main.inventoryScale)) {
						player.mouseInterface = true;
						//ItemSlot.OverrideHover(ref item, ItemSlot.Context.ChestItem);
						ItemSlot.LeftClick(ref item, ItemSlot.Context.ChestItem);
						ItemSlot.RightClick(ref item, ItemSlot.Context.ChestItem);
						ItemSlot.MouseHover(ref item, ItemSlot.Context.ChestItem);
					}
					ItemSlot.Draw(spriteBatch, ref item, ItemSlot.Context.GoldDebug, new Vector2(num, num2));//slot >= items.Count ? ItemSlot.Context.VoidItem : 
				}
			}
			Main.inventoryScale = inventoryScale;
		}
	}
}
