using Dissonance;
using GameNetcodeStuff;
using HarmonyLib;
using LethalNetworkAPI;
using System.Collections;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

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

namespace LCMyMango.Patches
{
	[HarmonyPatch(typeof(PlayerControllerB))]
	public class PlayerControllerBPatch
	{
		//static float _voiceAmplitudeThreshold = 0.2f;
		//static float _timeUntilExplode = 0.5f;
		//static float _explodeCooldownSeconds = 2f;

		static bool _explodeCooldown = false;
		static float _timeSinceScreaming = 0f;

		static LNetworkMessage<ulong>? _explosionMessage;
		static LNetworkMessage<MangoConfigPacket>? _syncConfigMessage;

		[HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.Awake))]
		[HarmonyPostfix]
		static void OnClientJoined()
		{
			_explosionMessage ??= LNetworkMessage<ulong>.Create("ExplodeRequest");
			LCMyMango.Logger.LogInfo("Created client/server connection.");

			_syncConfigMessage ??= LNetworkMessage<MangoConfigPacket>.Create("SyncConfig");
			LCMyMango.Logger.LogInfo("Created config sync connection.");
		}

		static void SetupConnections()
		{
			_explosionMessage!.OnServerReceived += OnServerExplosionRequest;
			_syncConfigMessage!.OnClientReceived += OnClientConfigReceived;
			_syncConfigMessage!.OnServerReceived += OnServerConfigRequest;
		}

		[HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.ConnectClientToPlayerObject))]
		[HarmonyPostfix]
		static void OnPlayerReady()
		{
			SetupConnections();

			if (!NetworkManager.Singleton.IsHost)
			{
				_syncConfigMessage?.SendServer(new MangoConfigPacket() { Type = MangoConfigPacket.PacketType.Request });
			}
			else
			{
				SetHostConfigToLocal();
			}
		}

		[HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.StartDisconnect))]
		[HarmonyPostfix]
		static void OnClientStartDisconnecting()
		{
			// destroy explosion message
			_explosionMessage?.ClearSubscriptions();
			//_explosionMessage = null;

			// destroy sync message
			_syncConfigMessage?.ClearSubscriptions();
			//_syncConfigMessage = null;

			// destroy host config
			LCMyMango.HostConfig = null!;
			
			LCMyMango.Logger.LogInfo("Destroyed client/server connection.");
		}

		static void OnClientConfigReceived(MangoConfigPacket packet)
		{
			if (NetworkManager.Singleton.IsHost) return;

			if (packet.Type != MangoConfigPacket.PacketType.Receive) return;

			// unsubscribe
			_syncConfigMessage?.ClearSubscriptions();
			//_syncConfigMessage = null;

			MangoConfigPrimitive config = packet.Config!;
            if (config is null)
            {
				LCMyMango.Logger.LogError("Failed to fetch/deserialize host config! Using client values!");

				SetHostConfigToLocal();

				return;
            }

			LCMyMango.Logger.LogDebug($"CLIENT {config.TimeUntilExplode}, {config.ExplodeCooldown}");

			LCMyMango.HostConfig = config;
			LCMyMango.Logger.LogInfo("Successfuly fetched host config!");

			StartOfRound.Instance.localPlayerController.StartCoroutine(DisplaySyncNotice());
		}

		static IEnumerator DisplaySyncNotice()
		{
			yield return new WaitForSeconds(1.5f);
			HUDManager.Instance.DisplayTip(
				"LCMyMango",
				$"Successfully synced with host's config!\nTimeUntilExplode: {LCMyMango.HostConfig.TimeUntilExplode} seconds\nExplodeCooldown: {LCMyMango.HostConfig.ExplodeCooldown} seconds"
			);
		}

		static void OnServerConfigRequest(MangoConfigPacket packet, ulong id)
		{
			LCMyMango.Logger.LogInfo($"playerId: {id} requested a host config sync");

			if (packet.Type != MangoConfigPacket.PacketType.Request) return;

			LCMyMango.Logger.LogDebug($"SERVER {LCMyMango.MangoConfig.TimeUntilExplode}, {LCMyMango.MangoConfig.ExplodeCooldown}");

			MangoConfigPrimitive mangoConfig = new()
			{
				TimeUntilExplode = LCMyMango.MangoConfig.TimeUntilExplode,
				ExplodeCooldown = LCMyMango.MangoConfig.ExplodeCooldown,
			};

			_syncConfigMessage?.SendClient(
				new MangoConfigPacket() { Config = mangoConfig, Type = MangoConfigPacket.PacketType.Receive },
				id
			);
		}

		static void OnServerExplosionRequest(ulong _, ulong clientID)
		{
			#pragma warning disable Harmony003 // Harmony non-ref patch parameters modified
			PlayerControllerB? senderController = clientID.GetPlayerController();
			#pragma warning restore Harmony003 // Harmony non-ref patch parameters modified
			if (senderController is null || senderController.isPlayerDead) return;
			if (!senderController.IsHost)
			{
				LCMyMango.Logger.LogError($"{senderController.name ?? "Unknown"} tried to call server only method!");
				return;
			}

			// spawn landmine

			// check if we're actually the host
			LCMyMango.Logger.LogInfo($"clientID: {clientID}");

			// Landmine spawning method:
			// https://github.com/legoandmars/MetalRecharging/tree/master
			SpawnableMapObject? landmine = StartOfRound.Instance?.levels?.SelectMany(x => x.spawnableMapObjects).FirstOrDefault(x => x.prefabToSpawn.name == "Landmine");
			if (landmine is null)
			{
				LCMyMango.Logger.LogError("Failed to find landmine spawnable map object!");
				return;
			}

			Vector3 playerPos = senderController.gameObject.transform.position - new Vector3(0, 0.25f, 0);

			GameObject landmineObject = UnityEngine.Object.Instantiate(landmine.prefabToSpawn, playerPos, Quaternion.identity);

			Landmine landmineComponent = landmineObject.GetComponentInChildren<Landmine>();
			landmineObject.GetComponent<NetworkObject>().Spawn(true);
			landmineComponent.ExplodeMineServerRpc();
		}

		[HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.Update))]
		[HarmonyPostfix]
		static void OnUpdatePatch(PlayerControllerB __instance)
		{
			if (_explodeCooldown || __instance.isPlayerDead) return;

			StartOfRound startOfRound = StartOfRound.Instance;

			VoicePlayerState voiceState = startOfRound.voiceChatModule.FindPlayer(startOfRound.voiceChatModule.LocalPlayerName);

			bool isSpeaking = voiceState.IsSpeaking; // && voiceState.Amplitude > 0.005f;

			// this whole part was made just to stop
			// me from exploding even if I was not holding down
			// the push to talk button
			// why????
			if (IngamePlayerSettings.Instance.settings?.pushToTalk == true)
			{
				// checking if action is pressed
				// https://github.com/taffyko/LCNameplateTweaks/tree/master
				bool? isPressingButton = IngamePlayerSettings.Instance.playerInput?.actions?.FindAction("VoiceButton", false)?.IsPressed();
				if (isPressingButton is not null && !(bool)isPressingButton)
				{
					isSpeaking = false;
				} 
			}

			//if (!isSpeaking || startOfRound.averageVoiceAmplitude < _voiceAmplitudeThreshold)
			if (!isSpeaking || startOfRound.averageVoiceAmplitude < LCMyMango.MangoConfig.VoiceThreshold)
			{
				_timeSinceScreaming = 0f;
				return;
			}

			_timeSinceScreaming += Time.deltaTime;

			if (!(_timeSinceScreaming >= LCMyMango.HostConfig.TimeUntilExplode)) return;

			_timeSinceScreaming = 0f;
			_explodeCooldown = true;
			__instance.StartCoroutine(ExplosionCooldownCoroutine());

			// send server rpc

			LCMyMango.Logger.LogInfo("Send server rpc");

			_explosionMessage?.SendServer(__instance.actualClientId);
		}

		static IEnumerator ExplosionCooldownCoroutine()
		{
			yield return new WaitForSeconds(LCMyMango.HostConfig.ExplodeCooldown);
			_explodeCooldown = false;
			LCMyMango.Logger.LogInfo("Waited");
		}

		static void SetHostConfigToLocal()
		{
			MangoConfigPrimitive defaultConfig = new()
			{
				TimeUntilExplode = LCMyMango.MangoConfig.TimeUntilExplode,
				ExplodeCooldown = LCMyMango.MangoConfig.ExplodeCooldown,
			};
			LCMyMango.HostConfig = defaultConfig;
		}
	}
}
