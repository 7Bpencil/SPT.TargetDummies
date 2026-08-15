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
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace SevenBoldPencil.TargetMannequins
{
	// TODO add all of them
	public enum MannequinType
	{
		Scav,
		Partisan,
		Knight,
		BigPipe,
		BirdEye,
	}

    [BepInPlugin("7Bpencil.TargetMannequins", "7Bpencil.TargetMannequins", "0.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance;
		public ManualLogSource LoggerInstance;

		public static ConfigEntry<MannequinType> CenterMannequinType;

        private void Awake()
        {
            Instance = this;
			LoggerInstance = Logger;

			CenterMannequinType = Config.Bind<MannequinType>("Main", "Mannequin Type", MannequinType.Scav);
        }

		public void Update()
		{
			if (Input.GetKeyDown(KeyCode.F3))
			{
				SpawnBot();
			}
		}

		public async Task SpawnBot()
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

			var session = tarkovApplication.Session;
			var profilesRequest = new List<CountTypeBotWave>()
			{
				GetBotType(CenterMannequinType.Value)
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
			var position = new Vector3(-3f, 0.01f, 19);
			var rotation = Quaternion.Euler(0, 180, 0);

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
