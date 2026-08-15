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

// TargetMannequins
//
// EAreaType.EquipmentPresetsStand
// walk around model, get all body part collider, and change go layer to HitCollider
// also need to attach other colliders to prevent gun
// maneq is missing some colliders (at least head colliders)
//
// TODO add body part health settings in F12 menu, head: 35, chest: 85, etc
// TODO use BSG calculations, to support all weird bullets people use

namespace SevenBoldPencil.TargetMannequins
{
    [BepInPlugin("7Bpencil.TargetMannequins", "7Bpencil.TargetMannequins", "0.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance;
		public ManualLogSource LoggerInstance;

        private void Awake()
        {
            Instance = this;
			LoggerInstance = Logger;
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
				new(1, WildSpawnType.bossZryachiy, BotDifficulty.normal),
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
