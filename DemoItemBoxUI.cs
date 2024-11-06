using Humanizer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using PegasusLib;
using PegasusLib.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ModLoader.UI;
using Terraria.UI;

namespace ModDemoUtils {
	public class UISearchBarRef : ReflectionLoader {
		public static FastFieldInfo<UISearchBar, string> actualContents;
	}
	public class DemoItemBoxSetupUI(short x, short y) : UIState {
		public static SoundStyle ClickSound => SoundID.MenuTick;
		public override void OnInitialize() {
			UIOverflowablePanel panel = new();
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
			UITextBox textBox = (UITextBox)input.Children.First();
			//textBox.BackgroundColor = new Color(63, 82, 151) * 0.7f;
			//textBox.BorderColor = Color.Black;
			textBox.SetTextMaxLength(100);
			UIInteractablePanel inputPanel = new();
			inputPanel.Width.Set(-252, 1f);
			inputPanel.Height.Set(-36, 1f);
			inputPanel.OnLeftClick += (_, _) => {
				if (!textBox.ShowInputTicker) {
					input.ToggleTakingText();
				}
			};
			inputPanel.Append(input);
			DemoBoxContents.SortType sortType = DemoBoxContents.SortType.ID;
			if (ModContent.GetInstance<DemoItemBoxSystem>().tileEntities.TryGetValue(new(x, y), out DemoBoxContents contents)) {
				input.SetContents(contents.text);
				sortType = contents.sortType;
			}
			UIDropdown<DemoBoxContents.SortType> sortPanel = new(
				value => {
					sortType = value;
					UpdateContents();
				},
				() => sortType,
				Enum.GetValues<DemoBoxContents.SortType>()
			);
			sortPanel.Left.Set(-248, 1f);
			sortPanel.Width.Set(250, 0f);
			sortPanel.Height.Set(-36, 1f);
			void SetEnabledMode(bool enabled) {
				inputPanel.IgnoresMouseInteraction = !enabled;
				sortPanel.IgnoresMouseInteraction = !enabled;
				if (!enabled) {
					if (input.IsWritingText) input.ToggleTakingText();
					sortPanel.OverflowHidden = true;
				}
			}
			SetEnabledMode(ModContent.GetInstance<DemoItemBoxSystem>().tileEntities.ContainsKey(new(x, y)));
			//sortPanel.Height.Set(-36, 1f);

			UIToggleSwitch enableButton = new(
				() => {
					DemoItemBoxSystem.ToggleTileEntity(x, y);
					SetEnabledMode(ModContent.GetInstance<DemoItemBoxSystem>().tileEntities.ContainsKey(new(x, y)));
				},
				() => ModContent.GetInstance<DemoItemBoxSystem>().tileEntities.ContainsKey(new(x, y)),
				Language.GetOrRegister("Mods.ModDemoUtils.DemoItemBox.Enabled"),
				Language.GetOrRegister("Mods.ModDemoUtils.DemoItemBox.Disabled")
			);
			enableButton.Width.Set(-10, 1f);
			enableButton.Height.Set(32, 0f);
			enableButton.Top.Set(-32, 1f);
			panel.Append(enableButton);

			panel.Append(inputPanel);
			panel.Append(sortPanel);

			void UpdateContents() {
				DemoItemBoxSystem.UpdateTileEntity(new(x, y), UISearchBarRef.actualContents.GetValue(input), sortType);
			}
			input.OnContentsChanged += _ => {
				UpdateContents();
			};
		}
	}
	public class UIInteractablePanel : UIPanel {
		public new Color BorderColor = Color.Black;
		public new Color BackgroundColor = new Color(63, 82, 151) * 0.7f;
		public Color HoverBorderColor = new Color(33, 33, 33) * 0.9f;
		public Color HoverBackgroundColor = new Color(93, 113, 187) * 0.9f;
		public Color DisabledBorderColor = Color.Black;
		public Color DisabledBackgroundColor = new Color(73, 82, 111) * 0.7f;
		bool hovered = false;
		public override void MouseOver(UIMouseEvent evt) {
			hovered = true;
		}
		public override void MouseOut(UIMouseEvent evt) {
			hovered = false;
		}
		protected override void DrawSelf(SpriteBatch spriteBatch) {
			if (IgnoresMouseInteraction) {
				base.BorderColor = DisabledBorderColor;
				base.BackgroundColor = DisabledBackgroundColor;
			} else if (hovered) {
				base.BorderColor = HoverBorderColor;
				base.BackgroundColor = HoverBackgroundColor;
			} else {
				base.BorderColor = BorderColor;
				base.BackgroundColor = BackgroundColor;
			}
			base.DrawSelf(spriteBatch);
		}
		public override bool ContainsPoint(Vector2 point) {
			if (!OverflowHidden) {
				foreach (UIElement child in Children) {
					if (child.ContainsPoint(point)) return true;
				}
			}
			return base.ContainsPoint(point);
		}
	}
	public class UIOverflowablePanel : UIPanel {
		public override bool ContainsPoint(Vector2 point) {
			if (!OverflowHidden) {
				foreach (UIElement child in Children) {
					if (child.ContainsPoint(point)) return true;
				}
			}
			return base.ContainsPoint(point);
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
		public Color DisabledBorderColor = Color.Black;
		public Color DisabledBackgroundColor = new Color(73, 82, 111) * 0.7f;
		bool hovered = false;
		public override void MouseOver(UIMouseEvent evt) {
			hovered = true;
		}
		public override void MouseOut(UIMouseEvent evt) {
			hovered = false;
		}
		protected override void DrawSelf(SpriteBatch spriteBatch) {
			Rectangle bounds = GetDimensions().ToRectangle();
			if (IgnoresMouseInteraction) {
				base.BorderColor = DisabledBorderColor;
				base.BackgroundColor = DisabledBackgroundColor;
			} else if (hovered) {
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
	public class UIDropdown<T>(Action<T> set, Func<T> getState, T[] values) : UITextPanel<T>(getState()) {
		public override void OnInitialize() {
			PaddingBottom = 0;
			PaddingLeft = 0;
			PaddingRight = 0;
			MarginBottom = 0;
			MarginLeft = 0;
			MarginRight = 0;
			for (int i = 0; i < values.Length; i++) {
				T value = values[i];
				UIDropdownItem<T> panel = new(value);
				panel.Top.Set(40 * i, 1);
				panel.Width.Set(0, 1);
				panel.Height.Set(0, 1);
				panel.OnLeftClick += (_, _) => {
					set(value);
					SetText(getState());
					SoundEngine.PlaySound(DemoItemBoxSetupUI.ClickSound);
				};
				Append(panel);
			}
			OverflowHidden = true;
		}
		public override void LeftClick(UIMouseEvent evt) {
			OverflowHidden ^= true;
		}
		public new Color BorderColor = Color.Black;
		public new Color BackgroundColor = new Color(63, 82, 151) * 0.7f;
		public Color HoverBorderColor = new Color(33, 33, 33) * 0.9f;
		public Color HoverBackgroundColor = new Color(93, 113, 187) * 0.9f;
		public Color DisabledBorderColor = Color.Black;
		public Color DisabledBackgroundColor = new Color(73, 82, 111) * 0.7f;
		bool hovered = false;
		public override void MouseOver(UIMouseEvent evt) {
			hovered = true;
		}
		public override void MouseOut(UIMouseEvent evt) {
			hovered = false;
		}
		protected override void DrawSelf(SpriteBatch spriteBatch) {
			if (IgnoresMouseInteraction) {
				base.BorderColor = DisabledBorderColor;
				base.BackgroundColor = DisabledBackgroundColor;
			} else if (hovered) {
				base.BorderColor = HoverBorderColor;
				base.BackgroundColor = HoverBackgroundColor;
			} else {
				base.BorderColor = BorderColor;
				base.BackgroundColor = BackgroundColor;
			}
			base.DrawSelf(spriteBatch);
		}
		public override bool ContainsPoint(Vector2 point) {
			if (!OverflowHidden) {
				foreach (UIElement child in Children) {
					if (child.ContainsPoint(point)) return true;
				}
			}
			return base.ContainsPoint(point);
		}
	}
	public class UIDropdownItem<T>(T text, float textScale = 1, bool large = false) : UITextPanel<T>(text, textScale, large) {
		public T Value => _text;
		public new Color BorderColor = Color.Black;
		public new Color BackgroundColor = new Color(63, 82, 151) * 0.7f;
		public Color HoverBorderColor = new Color(33, 33, 33) * 0.9f;
		public Color HoverBackgroundColor = new Color(93, 113, 187) * 0.9f;
		public Color DisabledBorderColor = Color.Black;
		public Color DisabledBackgroundColor = new Color(73, 82, 111) * 0.7f;
		bool hovered = false;
		public override void MouseOver(UIMouseEvent evt) {
			hovered = true;
		}
		public override void MouseOut(UIMouseEvent evt) {
			hovered = false;
		}
		protected override void DrawSelf(SpriteBatch spriteBatch) {
			if (IgnoresMouseInteraction) {
				base.BorderColor = DisabledBorderColor;
				base.BackgroundColor = DisabledBackgroundColor;
			} else if (hovered) {
				base.BorderColor = HoverBorderColor;
				base.BackgroundColor = HoverBackgroundColor;
			} else {
				base.BorderColor = BorderColor;
				base.BackgroundColor = BackgroundColor;
			}
			base.DrawSelf(spriteBatch);
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
		UIScrollbar scrollbar;
		public override void OnInitialize() {
			Left.Set(51f, 0);
			Top.Set(Main.instance.invBottom, 0);
			Width.Set(560f * 0.755f + 22, 0);
			Height.Set(224f * 0.755f, 0);

			if (items.Count <= 40) return;
			scrollbar = new();
			scrollbar.Left.Set(0, 0);
			scrollbar.Top.Set(10, 0);
			scrollbar.Height.Set(-20, 1);
			scrollbar.SetView(56 * 4, MathF.Ceiling(items.Count / 10f) * 56);
			Append(scrollbar);
		}
		protected override void DrawSelf(SpriteBatch spriteBatch) {
			float inventoryScale = Main.inventoryScale;
			Main.inventoryScale = 0.755f;
			Player player = Main.LocalPlayer;
			int viewPos = scrollbar is null ? 0 : (int)(scrollbar.ViewPosition / 56);
			for (int i = 0; i < 10; i++) {
				for (int j = 0; j < 4; j++) {
					int slot = i + (j + viewPos) * 10;
					int xPos = (int)(73f + (i * 56) * Main.inventoryScale);
					int yPos = (int)(Main.instance.invBottom + (j * 56) * Main.inventoryScale);
					Item item = slot >= items.Count ? new() : items[slot].Clone();
					if (!PlayerInput.IgnoreMouseInterface && Utils.FloatIntersect(Main.mouseX, Main.mouseY, 0f, 0f, xPos, yPos, TextureAssets.InventoryBack.Width() * Main.inventoryScale, TextureAssets.InventoryBack.Height() * Main.inventoryScale)) {
						player.mouseInterface = true;
						ItemSlot.OverrideHover(ref item, ItemSlot.Context.ChestItem);
						bool isLeft = Main.mouseLeftRelease && Main.mouseLeft;
						if (!player.ItemAnimationActive && (isLeft || (Main.mouseRightRelease && Main.mouseRight))) {
							bool didSomething = false;
							if (Main.cursorOverride == 8) {
								Item newItem = item.Clone();
								if (isLeft) newItem.stack = newItem.maxStack;
								newItem.OnCreated(new JourneyDuplicationItemCreationContext());
								Main.LocalPlayer.GetItem(Main.myPlayer, newItem, new GetItemSettings(StepAfterHandlingSlotNormally: static item => item.newAndShiny = true));
								didSomething = true;
							} else if (Main.mouseItem?.IsAir ?? true) {
								Main.mouseItem = item.Clone();
								if (isLeft) Main.mouseItem.stack = Main.mouseItem.maxStack;
								Main.mouseItem.OnCreated(new JourneyDuplicationItemCreationContext());
								didSomething = true;
							} else if (Main.mouseItem.type == item.type && Main.mouseItem.stack < Main.mouseItem.maxStack && ItemLoader.TryStackItems(Main.mouseItem, item, out _, true)) {
								didSomething = true;
							}
							if (didSomething) {
								Recipe.FindRecipes();
								SoundEngine.PlaySound(SoundID.Grab);
							}
						}
						ItemSlot.MouseHover(ref item, ItemSlot.Context.ChestItem);
					}
					ItemSlot.Draw(spriteBatch, ref item, ItemSlot.Context.GoldDebug, new Vector2(xPos, yPos));//slot >= items.Count ? ItemSlot.Context.VoidItem : 
				}
			}
			Main.inventoryScale = inventoryScale;
		}
		public override void ScrollWheel(UIScrollWheelEvent evt) {
			if (scrollbar is null) return;
			int viewPos = (int)(scrollbar.ViewPosition / 56);
			scrollbar.ViewPosition = (viewPos - evt.ScrollWheelValue / 120) * 56;
		}
	}
}
