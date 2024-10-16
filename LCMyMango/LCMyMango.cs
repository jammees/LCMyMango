using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;

/*
 * LCMyMango - jammees
 * 
 * Ever wanted to get your revenge with your last
 * breath? Now you can!
 * 
 * Under GNU General Public License v3.0
 * https://www.gnu.org/licenses/
 * 
 * MetalRecharging - legoandmars
 * 
 * Spawning landmine functionality.
 * 
 * Under GNU General Public License v3.0
 * https://www.gnu.org/licenses/
 * 
 * LCNameplateTweaks - taffyko
 * 
 * Detecting if push to talk button is pressed.
 * 
 * MIT License
 * 
 * Copyright (c) 2023 taffyko
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

namespace LCMyMango
{
	[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
	[BepInDependency("BMX.LobbyCompatibility", BepInDependency.DependencyFlags.SoftDependency)]
	[BepInDependency("LethalNetworkAPI", BepInDependency.DependencyFlags.HardDependency)]
	public class LCMyMango : BaseUnityPlugin
	{
		public static LCMyMango Instance { get; private set; } = null!;

		internal new static ManualLogSource Logger { get; private set; } = null!;
		internal static Harmony Harmony { get; set; } = null!;
		internal static MangoConfig MangoConfig { get; private set; } = null!;
		internal static MangoConfigPrimitive HostConfig { get; set; } = null!;

		private void Awake()
		{
			Logger = base.Logger;
			Instance = this;

			MangoConfig = new MangoConfig(Config);

			Patch();
			
			if( RegisterLobbyCompatibility.HasLobbyCompatibility ) RegisterLobbyCompatibility.RegisterSelf();

			Logger.LogInfo($"{MyPluginInfo.PLUGIN_GUID} v{MyPluginInfo.PLUGIN_VERSION} has loaded!");
		}

		internal static void Patch()
		{
			Harmony ??= new Harmony(MyPluginInfo.PLUGIN_GUID);

			Logger.LogDebug("Patching...");

			Harmony.PatchAll();

			Logger.LogDebug("Finished patching!");
		}
	}

	static class RegisterLobbyCompatibility
	{
		public static bool HasLobbyCompatibility => BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("BMX.LobbyCompatibility");

		public static void RegisterSelf()
		{
			LobbyCompatibility.Features.PluginHelper.RegisterPlugin(
				MyPluginInfo.PLUGIN_GUID,
				new Version(MyPluginInfo.PLUGIN_VERSION),
				LobbyCompatibility.Enums.CompatibilityLevel.ClientOptional,
				LobbyCompatibility.Enums.VersionStrictness.Minor
			);
			LCMyMango.Logger.LogDebug("Registered mod for LobbyCompatibility");
		}
	}
}
