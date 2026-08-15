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
    [BepInPlugin("7Bpencil.TargetMannequins", "7Bpencil.TargetMannequins", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance;
		public ManualLogSource LoggerInstance;

		// TODO make it a setting
		public float Health_Head = 35;
		public float Health_Chest = 85;
		public float Health_Stomach = 70;
		public float Health_Arm = 60;
		public float Health_Leg = 65;

        private void Awake()
        {
            Instance = this;
			LoggerInstance = Logger;

			new Patch_HideoutAreaStashController_UpdateStash().Enable();
        }

		public void Update()
		{
			if (Input.GetKeyDown(KeyCode.F3))
			{
				SpawnBot();
			}
			if (Input.GetKeyDown(KeyCode.F13))
			{
				DumpAllPlayersProfiles();
			}
		}

		public void DumpAllPlayersProfiles()
		{
			var gameWorld = Singleton<GameWorld>.Instance;
			var players = gameWorld.GetField<GameWorld, Dictionary<string, Player>>("_allPlayersEverExisted");
			foreach (var (playerName, player) in players)
			{
				var profileDescriptor = new ProfileDescriptor(player.Profile, FullySearchedSearchController.Instance);
				var prettyJson = profileDescriptor.ToPrettyJson(Array.Empty<JsonConverter>());
				File.WriteAllText($"dumped_profiles/{playerName}.json", prettyJson);
			}
		}

		public static ProfileDescriptor LoadDumpedProfile(string id)
		{
			var text = File.ReadAllText($"dumped_profiles/{id}.json");
			return text.ParseJsonTo<ProfileDescriptor>(Array.Empty<JsonConverter>());
		}

		public static Profile.HealthInfo.ValueInfo GetHealthValueInfo(float currentValue, float minValue, float maxValue)
		{
			return new()
			{
				Current = currentValue,
				Minimum = minValue,
				Maximum = maxValue,
			};
		}

		public static Profile.HealthInfo.ValueInfo GetHealthValueInfo(float maxValue)
		{
			return GetHealthValueInfo(maxValue, 0, maxValue);
		}

		public static Profile.HealthInfo.BodyPartInfo GetBodyPartInfo(float maxHealthValue)
		{
			return new()
			{
				Health = GetHealthValueInfo(maxHealthValue),
			};
		}

		public static InventoryDescriptor MakeDefaultInventory()
		{
			// TODO copy equipment from mannequin (not entire inventory)
		    var equipment = MongoID.Generate(true);
			return new()
			{
				_items =
				[
					new()
					{
						_id = equipment,
						_tpl = "55d7217a4bdc2d86028b456d",
					},
				],
				_equipmentId = equipment,
				_hideoutAreaStashesIds = new(),
			};
		}

		public ProfileDescriptor GenerateProfile()
		{
			return new()
			{
				Id = MongoID.Generate(true),
				AccountId = "0",
				Info = new()
				{
					Nickname = "Mannequin",
					Side = EPlayerSide.Savage,
					GameVersion = "",
					Type = EProfileType.Eft,
					Level = 1,
				},
				Customization = new()
				{
					// TODO different voice
				    { EBodyModelPart.Head, "6644d2da35d958070c02642c" },
				    { EBodyModelPart.Body, "6644d2ffd85107e63500a61c" },
				    { EBodyModelPart.Feet, "6644d32235d958070c02642e" },
				    { EBodyModelPart.Hands, "5cc2e68f14c02e28b47de290" },
				    { EBodyModelPart.Voice, "5fc613c80b735e7b024c76e2" },
				},
				Health = new()
				{
					BodyParts = new()
					{
						{ EBodyPart.Head, GetBodyPartInfo(Health_Head) },
						{ EBodyPart.Chest, GetBodyPartInfo(Health_Chest) },
						{ EBodyPart.Stomach, GetBodyPartInfo(Health_Stomach) },
						{ EBodyPart.LeftArm, GetBodyPartInfo(Health_Arm) },
						{ EBodyPart.RightArm, GetBodyPartInfo(Health_Arm) },
						{ EBodyPart.LeftLeg, GetBodyPartInfo(Health_Leg) },
						{ EBodyPart.RightLeg, GetBodyPartInfo(Health_Leg) },
					},
					Energy = GetHealthValueInfo(100),
					Hydration = GetHealthValueInfo(100),
					Temperature = GetHealthValueInfo(36.6f, 28, 40),
					Poison = GetHealthValueInfo(0, 0, 100),
					UpdateTime = 0,
				},
				Inventory = MakeDefaultInventory(),
			};
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

			// var localPlayer = hideoutGame.LocalPlayer;
			// var botPlayerProfile = localPlayer.Profile.Clone();

			// var botPlayerProfile = await ProfileStorage.GenerateProfile(EPlayerSide.Savage, DebugBotProfileChooser.Rifle);

			var profileDescriptor = GenerateProfile();
			var botPlayerProfile = new Profile(profileDescriptor);

			// var profileDescriptor = LoadDumpedProfile("6a7f8f1aaf5c0f120081d6d9");
			// var botPlayerProfile = new Profile(profileDescriptor);

			{
				// var profileDescriptor = new ProfileDescriptor(botPlayerProfile, FullySearchedSearchController.Instance);
				// var prettyJson = profileDescriptor.ToPrettyJson(Array.Empty<JsonConverter>());
				// File.WriteAllText($"bot.json", prettyJson);
			}

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

			// TODO make them hold weapon if there is one
			// botPlayer.ProceduralWeaponAnimation.Mask = EProceduralAnimationMask.Aiming;

			}
			catch (Exception e)
			{
				Logger.LogError(e);
			}
		}

		public void PatchMannequins(HideoutAreaStashController __instance)
		{
			// TODO this one is called every time mannequin equipment is changed,
			// so track when shit is needed and when not,
			// TODO we probably should make our own mannequins and dont touch bsg ones
			// TODO setup all body part colliders correctly

			// TODO get equipment from mannequin

			var spawnPointName = "Stand1";
			var spawnPoint = __instance._stashObjectsSpawnPoints[spawnPointName];
			var _spawnPoint = new StashItemModelSpawnPoint_Proxy(spawnPoint);
			var items = _spawnPoint._items;
			if (items.Count == 0)
			{
				Logger.LogWarning($"{spawnPointName} no items");
				return;
			}

			var item = items[0];
			var _item = new StashItemModel_Proxy(item);
			var prefab = _item._prefab;
			var rotationPivot = _item._rotationPivot;
			if (!prefab.TryGetComponent<PlayerBody>(out var playerBody))
			{
				Logger.LogWarning($"{spawnPointName} no PlayerBody");
				return;
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
