using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Ionic.Zip;
using Newtonsoft.Json.Linq;
using Terraria;
using Terraria.ModLoader;

namespace ModDemoUtils {
	public class DemoWorldDownloader : ILoadable {
		public const string demo_world_prefix = "ModDemoUtils_";
		readonly string baseCachePath = Path.Combine(Main.SavePath, nameof(ModDemoUtils), "DemoWorlds");
		HttpClient client = new(new HttpClientHandler() { UseDefaultCredentials = true });
		public void ProcessDemoCall(Mod mod, string path) {
			string cachePath = Path.Combine(baseCachePath, mod.Name);
			Directory.CreateDirectory(cachePath);
			string versionFile = Path.Combine(cachePath, "version");
			if (File.Exists(versionFile) && File.ReadAllText(versionFile) == mod.Version.ToString()) {

			} else {
				return;
				client.SendAsync(new HttpRequestMessage(HttpMethod.Get, new Uri($"https://api.github.com/repos/{path}/releases")).WithUserAgent())
				.ContinueWith(async task => {
					task.Result.EnsureSuccessStatusCode();
					List<(string, string)> thisModDemos = [];
					ModContent.GetInstance<ModDemoUtils>().demos.Add(mod.Name, thisModDemos);
					foreach (JObject release in JArray.Parse(await task.Result.Content.ReadAsStringAsync()).Where(r => r is JObject).Cast<JObject>()) {
						HttpResponseMessage response = client.Send(new HttpRequestMessage(HttpMethod.Get, release["assets_url"].ToString()).WithUserAgent());
						response.EnsureSuccessStatusCode();
						bool foundDemos = false;
						foreach (JObject artifact in JArray.Parse(await response.Content.ReadAsStringAsync()).Where(r => r is JObject).Cast<JObject>()) {
							string artifactName = artifact["name"].ToString();
							if (artifactName.EndsWith(".zip") && artifactName.StartsWith(demo_world_prefix)) {
								thisModDemos.Add((artifact["name"].ToString()[demo_world_prefix.Length..], artifact["browser_download_url"].ToString()));
								foundDemos = true;
							}
						}
						if (foundDemos) {
							File.WriteAllText(versionFile, mod.Version.ToString());
							return;
						}
					}
				});
			}
		}

		public void Load(Mod mod) {}

		public void Unload() {
			client.Dispose();
			client = null;
		}
	}
	internal static class HttpRequestMessageExt {
		internal static HttpRequestMessage WithUserAgent(this HttpRequestMessage message) {
			message.Headers.UserAgent.Add(new("Tyfyer.ModDemoUtils", ModContent.GetInstance<ModDemoUtils>().Version.ToString()));
			return message;
		}
	}
}
