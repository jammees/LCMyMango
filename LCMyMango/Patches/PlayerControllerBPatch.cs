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
		static float _voiceAmplitudeThreshold = 0.2f;
		static float _timeUntilExplode = 0.5f;
		static float _explodeCooldownSeconds = 2f;

		static bool _explodeCooldown = false;
		static float _timeSinceScreaming = 0f;

		static LNetworkMessage<ulong>? _clientMessage;

		[HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.Awake))]
		[HarmonyPostfix]
		static void OnNetworkSpawn()
		{
			_clientMessage = LNetworkMessage<ulong>.Create("ExplodeRequest", onServerReceived: OnReceivedExplosionRequest);
			LCMyMango.Logger.LogInfo("Created client/server connection.");
		}

		[HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.OnLocalDisconnect))]
		[HarmonyPostfix]
		static void OnNetworkDespawn()
		{
			_clientMessage?.ClearSubscriptions();
			_clientMessage = null;
			LCMyMango.Logger.LogInfo("Destroyed client/server connection.");
		}

		static void OnReceivedExplosionRequest(ulong _, ulong clientID)
		{
			PlayerControllerB? senderController = clientID.GetPlayerController();
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

			// problem here
			GameObject landmineObject = Object.Instantiate(landmine.prefabToSpawn, playerPos, Quaternion.identity);

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

			if (!isSpeaking || startOfRound.averageVoiceAmplitude < _voiceAmplitudeThreshold)
			{
				_timeSinceScreaming = 0f;
				return;
			}

			_timeSinceScreaming += Time.deltaTime;

			if (!(_timeSinceScreaming >= _timeUntilExplode)) return;

			_timeSinceScreaming = 0f;
			_explodeCooldown = true;
			__instance.StartCoroutine(ExplosionCooldownCoroutine());

			// send server rpc

			LCMyMango.Logger.LogInfo("Send server rpc");

			_clientMessage?.SendServer(__instance.actualClientId);
		}

		static IEnumerator ExplosionCooldownCoroutine()
		{
			yield return new WaitForSeconds(_explodeCooldownSeconds);
			_explodeCooldown = false;
			LCMyMango.Logger.LogInfo("Waited");
		}
	}
}
