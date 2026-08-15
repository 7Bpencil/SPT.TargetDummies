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

// TODO spawn bots when player enters shooting range,
// so let it clean bodies automatically
// TODO on demostration video shoot bots with different guns,
// use mod to bind weapons from backpack (use items anywhere)
// TODO rotate bots to face player on spawn

namespace SevenBoldPencil.TargetMannequins
{
	// TODO add all of them
	// TODO mannequin with player equipment can be just "Mannequin" type
	public enum MannequinType
	{
		Scav,
		Partisan,
		Knight,
		BigPipe,
		BirdEye,
	}

	// TODO rename to TargetDummies
    [BepInPlugin("7Bpencil.TargetMannequins", "7Bpencil.TargetMannequins", "0.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance;
		public ManualLogSource LoggerInstance;

		public ConfigEntry<MannequinType> LeftMannequinType;
		public ConfigEntry<MannequinType> CenterMannequinType;
		public ConfigEntry<MannequinType> RightMannequinType;

        private void Awake()
        {
            Instance = this;
			LoggerInstance = Logger;

			LeftMannequinType = Config.Bind<MannequinType>("Main", "Left Mannequin Type", MannequinType.Scav);
			CenterMannequinType = Config.Bind<MannequinType>("Main", "Center Mannequin Type", MannequinType.Scav);
			RightMannequinType = Config.Bind<MannequinType>("Main", "Right Mannequin Type", MannequinType.Scav);

			new Patch_HideoutController_HideoutAwake().Enable();
        }

		public void Update()
		{
			if (Input.GetKeyDown(KeyCode.F3))
			{
				var closeLeftPosition = new Vector3(-4f, 0.01f, 16.2f);
				var closeCenterPosition = new Vector3(-2.9f, 0.01f, 23.75f);
				var closeRightPosition = new Vector3(-1.65f, 0.01f, 30.22f);

				var farLeftPosition = new Vector3(-4.95f, 0.01f, 57.48f);
				var farCenterPosition = new Vector3(-2.75f, 0.01f, 57.47f);
				var farRightPosition = new Vector3(-0.56f, 0.01f, 57.47f);

				SpawnBot(closeLeftPosition, LeftMannequinType.Value);
				SpawnBot(closeCenterPosition, CenterMannequinType.Value);
				SpawnBot(closeRightPosition, RightMannequinType.Value);

				// TODO should far row targets have different bot types?
				SpawnBot(farLeftPosition, LeftMannequinType.Value);
				SpawnBot(farCenterPosition, CenterMannequinType.Value);
				SpawnBot(farRightPosition, RightMannequinType.Value);
			}
		}

		public async Task SpawnBot(Vector3 position, MannequinType mannequinType)
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
			var localPlayerPosition = localPlayer.Transform.position;

			var session = tarkovApplication.Session;
			var profilesRequest = new List<CountTypeBotWave>()
			{
				GetBotType(mannequinType)
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
			var rotation = Quaternion.LookRotation((localPlayerPosition - position).normalized);

			var botPlayer = await LocalPlayer.Create
			(
				gameWorld: hideoutGameWorld,
				playerId: botPlayerId,
				position: position,
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

			var playerCulling = botPlayer.GetField<LocalPlayer, OfflinePlayerCulling>("botPlayerCulling");
			playerCulling.SetMode(BasePlayerCulling.EMode.Visible);

			// take weapon in hands
			botPlayer.SetSlotItem(EquipmentSlot.FirstPrimaryWeapon, (_) => {});

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
				MannequinType.Partisan => new(1, WildSpawnType.bossPartisan, BotDifficulty.normal),
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
