//
// Copyright (c) 2026 7Bpencil
//
// This source code is licensed under the MIT license found in the
// LICENSE file in the root directory of this source tree.
//

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using Diz.Jobs;
using DG.Tweening;
using EFT;
using EFT.AssetsManager;
using EFT.Animations;
using EFT.Ballistics;
using EFT.Development;
using EFT.Interactive;
using EFT.InventoryLogic;
using EFT.Hideout;
using Newtonsoft.Json;
using HarmonyLib;
using SevenBoldPencil.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// TODO despawn all of them on hideout exit
// TODO test all timings

namespace SevenBoldPencil.TargetMannequins
{
	// TODO add all of them
	// TODO mannequin with player equipment can be just "Mannequin" type
	public enum MannequinType
	{
		Scav,
		Reshala,
		Killa,
		Sanitar,
		Gluhar,
		Partisan,
		Rogue,
		Knight,
		BigPipe,
		BirdEye,
	}

	public readonly record struct MannequinData
	(
		Vector3 Position,
		ConfigEntry<MannequinType> Type
	);

	// TODO rename to TargetDummies
    [BepInPlugin("7Bpencil.TargetMannequins", "7Bpencil.TargetMannequins", "0.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance;
		public ManualLogSource LoggerInstance;

		public ConfigEntry<MannequinType> CloseLeftMannequinType;
		public ConfigEntry<MannequinType> CloseMiddleMannequinType;
		public ConfigEntry<MannequinType> CloseRightMannequinType;

		public ConfigEntry<MannequinType> FarLeftMannequinType;
		public ConfigEntry<MannequinType> FarMiddleMannequinType;
		public ConfigEntry<MannequinType> FarRightMannequinType;

		public Dictionary<LocalPlayer, MannequinData> Mannequins;

        private void Awake()
        {
            Instance = this;
			LoggerInstance = Logger;

			CloseLeftMannequinType = Config.Bind<MannequinType>("Close", "Left Mannequin Type", MannequinType.Scav, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 3 }));
			CloseMiddleMannequinType = Config.Bind<MannequinType>("Close", "Middle Mannequin Type", MannequinType.Scav, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 2 }));
			CloseRightMannequinType = Config.Bind<MannequinType>("Close", "Right Mannequin Type", MannequinType.Scav, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 1 }));

			FarLeftMannequinType = Config.Bind<MannequinType>("Far", "Left Mannequin Type", MannequinType.Scav, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 3 }));
			FarMiddleMannequinType = Config.Bind<MannequinType>("Far", "Middle Mannequin Type", MannequinType.Scav, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 2 }));
			FarRightMannequinType = Config.Bind<MannequinType>("Far", "Right Mannequin Type", MannequinType.Scav, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 1 }));

			Mannequins = new();

			new Patch_HideoutController_HideoutAwake().Enable();
			new Patch_GameWorld_DestroyAllLoot().Enable();
			new Patch_CorpseRagdoll_Start().Enable();
        }

		public async Task SpawnBot(MannequinData data)
		{
			try
			{

    		if (!TarkovApplication.Exist(out var tarkovApplication))
            {
                return;
            }

			var hideoutController = tarkovApplication.HideoutControllerAccess;
			var hideoutGame = hideoutController.task_0.Result.Value;
			var hideoutGameWorld = hideoutGame.GameWorld;
			var localPlayer = hideoutGame.LocalPlayer;
			var localPlayerPosition = new Vector3(-2.5263f, 0f, 9.3481f);

			var session = tarkovApplication.Session;
			var profilesRequest = new List<CountTypeBotWave>()
			{
				GetBotType(data.Type.Value)
			};
			var profiles = await session.LoadBots(profilesRequest);
			var botPlayerProfile = profiles[0];

			await Singleton<ObjectsFactory>.Instance.LoadBundlesAndCreatePools
			(
				ObjectsFactory.PoolsCategory.Raid,
				ObjectsFactory.AssemblyType.Local,
				botPlayerProfile.GetAllPrefabPaths(true).ToArray<ResourceKey>(),
				JobYieldPriority.General,
				null,
				ObjectsFactory.DefaultCancellationToken
			);

			var botPlayerId = hideoutGame.NextPlayerId();
			var rotation = Quaternion.LookRotation((localPlayerPosition - data.Position).normalized);

			var botPlayer = await LocalPlayer.Create
			(
				gameWorld: hideoutGameWorld,
				playerId: botPlayerId,
				position: data.Position,
				rotation: rotation,
				layerName: "Player",
				prefix: "",
				pointOfView: EPointOfView.ThirdPerson,
				profile: botPlayerProfile,
				aiControl: true,
				updateQueue: hideoutGame.UpdateQueue,
				armsUpdateMode: Player.EUpdateMode.Auto,
				bodyUpdateMode: Player.EUpdateMode.Auto,
				characterControllerMode: AppEnvironment.Config.CharacterController.BotPlayerMode,
				getSensitivity: new Func<float>(LocalGame.CG_Class1642.CG_Class1642.method_4),
				getAimingSensitivity: new Func<float>(LocalGame.CG_Class1642.CG_Class1642.method_5),
				statisticsManager: new DumbStatisticsManager(),
				filter: ThirdPersonCustomizationFilter.Default,
				session: null,
				localMode: ELocalMode.TRAINING,
				isYourPlayer: false,
				isBot: true
			);

			// TODO for some reason clothes skinned mesh renderers have enabled forceRenderingOff,
			// I guess culling component thinks that they are not in camera view because
			// something is not initalized properly, so for now just force rendering on

			// TODO have to manually update player culling toggle
			var playerCulling = botPlayer.GetField<LocalPlayer, OfflinePlayerCulling>("botPlayerCulling");
			playerCulling.SetMode(BasePlayerCulling.EMode.Visible);

			// take weapon in hands
			botPlayer.SetSlotItem(EquipmentSlot.FirstPrimaryWeapon, (_) => {});

			Mannequins[botPlayer] = data;

			}
			catch (Exception e)
			{
				Logger.LogError(e);
			}
		}

		public static CountTypeBotWave GetBotType(MannequinType mannequinType)
		{
			// TODO figure out correct bot difficulty
			return mannequinType switch
			{
				MannequinType.Scav => new(1, WildSpawnType.assault, BotDifficulty.normal),
				MannequinType.Reshala => new(1, WildSpawnType.bossBully, BotDifficulty.normal),
				MannequinType.Killa => new(1, WildSpawnType.bossKilla, BotDifficulty.normal),
				MannequinType.Gluhar => new(1, WildSpawnType.bossGluhar, BotDifficulty.normal),
				MannequinType.Sanitar => new(1, WildSpawnType.bossSanitar, BotDifficulty.normal),
				MannequinType.Partisan => new(1, WildSpawnType.bossPartisan, BotDifficulty.normal),
				MannequinType.Rogue => new(1, WildSpawnType.exUsec, BotDifficulty.normal),
				MannequinType.Knight => new(1, WildSpawnType.bossKnight, BotDifficulty.normal),
				MannequinType.BigPipe => new(1, WildSpawnType.followerBigPipe, BotDifficulty.normal),
				MannequinType.BirdEye => new(1, WildSpawnType.followerBirdEye, BotDifficulty.normal),
				_ => throw new ArgumentException($"Unknown mannequin type: {mannequinType}"),
			};
		}

		// TODO this is true for level 3, what about other levels?
		public static string[] ShootingRangeTargets =
		[
			"Rail_targets/01_rail_target/Shooting_range_rails_02/Shooting_range_target_rails",
			"Rail_targets/02_rail_target/Shooting_range_rails_02 (1)/Shooting_range_target_rails",
			"Rail_targets/03_rail_target/Shooting_range_rails_02 (2)/Shooting_range_target_rails",
			"Popper_targets",
			"Target_stand_changed (1)",
			"Target_stand_changed (2)",
			"Target_stand_changed (3)",
		];

		public void HideShootingRangeTargets(HideoutController __instance)
		{
			if (!__instance.Areas.TryGetValue(EAreaType.ShootingRange, out var shootingRange))
			{
				return;
			}

			var areaLevel = shootingRange.CurrentLevel;
			if (!areaLevel)
			{
				return;
			}

			StartCoroutine(FindAndDisableTargets(areaLevel.HighlightTransform));
			StartCoroutine(SpawnInitialBots());
		}

		public IEnumerator FindAndDisableTargets(Transform targetsRoot)
		{
			foreach (var targetPath in ShootingRangeTargets)
			{
				var targetTransform = targetsRoot.Find(targetPath);
				if (targetTransform)
				{
					targetTransform.gameObject.SetActive(false);
					yield return null;
				}
			}
		}

		public IEnumerator SpawnInitialBots()
		{
			yield return new WaitForSeconds(1f);

			var closeLeft = new MannequinData(new(-4f, 0.01f, 16.2f), CloseLeftMannequinType);
			var closeMiddle = new MannequinData(new(-2.9f, 0.01f, 23.75f), CloseMiddleMannequinType);
			var closeRight = new MannequinData(new(-1.65f, 0.01f, 30.22f), CloseRightMannequinType);

			var farLeft = new MannequinData(new(-4.95f, 0.01f, 57.48f), FarLeftMannequinType);
			var farMiddle = new MannequinData(new(-2.75f, 0.01f, 57.47f), FarMiddleMannequinType);
			var farRight = new MannequinData(new(-0.56f, 0.01f, 57.47f), FarRightMannequinType);

			SpawnBot(closeLeft);
			SpawnBot(closeMiddle);
			SpawnBot(closeRight);

			SpawnBot(farLeft);
			SpawnBot(farMiddle);
			SpawnBot(farRight);
		}

		public void OnBotDeath(LocalPlayer bot)
		{
			StartCoroutine(DespawnBotSpawnAnotherOne(bot));
		}

		public IEnumerator DespawnBotSpawnAnotherOne(LocalPlayer bot)
		{
			if (!Mannequins.Remove(bot, out var mannequinData))
			{
				yield break;
			}

			yield return new WaitForSeconds(1f);

			bot.Dispose();
			AssetPoolObject.ReturnToPool(bot.gameObject, true);

			yield return new WaitForSeconds(1f);

			SpawnBot(mannequinData);
		}
    }

    public static class R
    {
        public static V GetField<T, V>(this T instance, string fieldName)
        {
            return (V)AccessTools.Field(typeof(T), fieldName).GetValue(instance);
        }

        public static V GetField<T, V>(string fieldName)
        {
            return (V)AccessTools.Field(typeof(T), fieldName).GetValue(null);
        }
    }

}
