//
// Copyright (c) 2026 7Bpencil
//
// This source code is licensed under the MIT license found in the
// LICENSE file in the root directory of this source tree.
//

using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using Diz.Jobs;
using EFT;
using EFT.AssetsManager;
using EFT.InventoryLogic;
using EFT.Hideout;
using Newtonsoft.Json;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

// TODO test all timings

namespace SevenBoldPencil.TargetDummies
{
	public enum MannequinType
	{
		Player,

		Mannequin1,
		Mannequin2,
		Mannequin3,

		Custom1,

		Scav,
		ScavSniper,
		Raider,

		BEAR,
		USEC,

		Reshala,
		ReshalaGuard,

		Shturman,
		ShturmanGuard,

		Sanitar,
		SanitarGuard,

		Gluhar,
		GluharGuardAssault,
		GluharGuardSecurity,
		GluharGuardScout,
		GluharGuardSnipe,

		Killa,
		KillaLabyrinth,

		Tagilla,
		TagillaLabyrinth,

		Rogue,
		Knight,
		BigPipe,
		BirdEye,

		CultistWarrior,
		CultistPriest,

		Zryachiy,
		ZryachiyGuard,

		Kaban,
		KabanGuardBasmach,
		KabanGuardGus,
		KabanGuard,
		KabanGuardSniper,

		Kolontay,
		KolontayGuardAssault,
		KolontayGuardSecurity,

		Partisan,

		BlackDivisionAssault,
		BlackDivisionWedge,
		BlackDivisionIcebreakerRaider,

        RuafRifleman,
        RuafSeniorRifleman,
        RuafAutorifleman,
        RuafMarksman,
        RuafMachinegunner,
        RemnantRifleman,

        UntarRifleman,
        UntarSquadLeader,
        UntarOfficer,
	}

	public readonly record struct MannequinData
	(
		Vector3 Position,
		ConfigEntry<MannequinType> Type
	);

    [BepInPlugin("7Bpencil.TargetDummies", "7Bpencil.TargetDummies", "0.3.0")]
	[BepInDependency("com.morebotsapi.tacticaltoaster", BepInDependency.DependencyFlags.SoftDependency)]
	[BepInDependency(BlackDivisionGUID, BepInDependency.DependencyFlags.SoftDependency)]
	[BepInDependency(RuafGUID, BepInDependency.DependencyFlags.SoftDependency)]
	[BepInDependency(UntarGUID, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
		public const string BlackDivisionGUID = "com.blackdiv.tacticaltoaster";
		public const string RuafGUID = "com.ruafcomehome.tacticaltoaster";
		public const string UntarGUID = "com.untargh.tacticaltoaster";

        public static Plugin Instance;
		public ManualLogSource LoggerInstance;

		public ConfigEntry<MannequinType> CloseLeftMannequinType;
		public ConfigEntry<MannequinType> CloseMiddleMannequinType;
		public ConfigEntry<MannequinType> CloseRightMannequinType;

		public ConfigEntry<MannequinType> FarLeftMannequinType;
		public ConfigEntry<MannequinType> FarMiddleMannequinType;
		public ConfigEntry<MannequinType> FarRightMannequinType;

		public ConfigEntry<WildSpawnType> CustomType1;

		public ConfigEntry<float> Mannequin_Health_Head;
		public ConfigEntry<float> Mannequin_Health_Chest;
		public ConfigEntry<float> Mannequin_Health_Stomach;
		public ConfigEntry<float> Mannequin_Health_Arm;
		public ConfigEntry<float> Mannequin_Health_Leg;

		public HashSet<MannequinType> MissingTypes;
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

			CustomType1 = Config.Bind<WildSpawnType>("Custom Mannequin Types", "Custom1", WildSpawnType.infectedLaborant, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 1 }));

			Mannequin_Health_Head = Config.Bind<float>("Mannequin Settings", "Health Head", 35, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 5 }));
			Mannequin_Health_Chest = Config.Bind<float>("Mannequin Settings", "Health Chest", 85, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 4 }));
			Mannequin_Health_Stomach = Config.Bind<float>("Mannequin Settings", "Health Stomach", 70, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 3 }));
			Mannequin_Health_Arm = Config.Bind<float>("Mannequin Settings", "Health Arm", 60, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 2 }));
			Mannequin_Health_Leg = Config.Bind<float>("Mannequin Settings", "Health Leg", 65, new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = 1 }));

			MissingTypes = new();

			if (!Chainloader.PluginInfos.ContainsKey(BlackDivisionGUID))
			{
				MissingTypes.Add(MannequinType.BlackDivisionAssault);
				MissingTypes.Add(MannequinType.BlackDivisionWedge);
				MissingTypes.Add(MannequinType.BlackDivisionIcebreakerRaider);
			}
			if (!Chainloader.PluginInfos.ContainsKey(RuafGUID))
			{
		        MissingTypes.Add(MannequinType.RuafRifleman);
		        MissingTypes.Add(MannequinType.RuafSeniorRifleman);
		        MissingTypes.Add(MannequinType.RuafAutorifleman);
		        MissingTypes.Add(MannequinType.RuafMarksman);
		        MissingTypes.Add(MannequinType.RuafMachinegunner);
		        MissingTypes.Add(MannequinType.RemnantRifleman);
			}
			if (!Chainloader.PluginInfos.ContainsKey(UntarGUID))
			{
		        MissingTypes.Add(MannequinType.UntarRifleman);
		        MissingTypes.Add(MannequinType.UntarSquadLeader);
		        MissingTypes.Add(MannequinType.UntarOfficer);
			}

			Mannequins = new();

			new Patch_HideoutController_HideoutAwake().Enable();
			new Patch_GameWorld_DestroyAllLoot().Enable();
			new Patch_CorpseRagdoll_Start().Enable();
			new Patch_HideoutAreaTrigger_OnTriggerExit().Enable();
			new Patch_TarkovApplication_HideoutController_UnloadHideout().Enable();
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
			var localPlayerPosition = new Vector3(-2.5263f, 0f, 9.3481f);

			var botPlayerProfile = await GenerateProfile(tarkovApplication.Session, hideoutGame.Profile, data.Type.Value);

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

			// TODO have to manually update player culling toggle?
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

		public async Task<Profile> GenerateProfile(IEftSession session, Profile playerProfile, MannequinType mannequinType)
		{
			if (mannequinType == MannequinType.Player)
			{
				return GenerateProfileWithPlayerEquipmentAndStats(playerProfile);
			}
			if (mannequinType == MannequinType.Mannequin1)
			{
				return GenerateProfileWithMannequinEquipment(playerProfile, 0);
			}
			if (mannequinType == MannequinType.Mannequin2)
			{
				return GenerateProfileWithMannequinEquipment(playerProfile, 1);
			}
			if (mannequinType == MannequinType.Mannequin3)
			{
				return GenerateProfileWithMannequinEquipment(playerProfile, 2);
			}
			if (MissingTypes.Contains(mannequinType))
			{
				return new(GenerateMannequinProfile());
			}
			if (mannequinType == MannequinType.Custom1)
			{
				var botType = CustomType1.Value;
				return await GetBotProfile(session, botType);
			}
			{
				var botType = GetBotType(mannequinType);
				return await GetBotProfile(session, botType);
			}
		}

		public Profile GenerateProfileWithPlayerEquipmentAndStats(Profile playerProfile)
		{
			var profileDescriptor = new ProfileDescriptor()
			{
				Id = MongoID.Generate(true),
				Info = new(),
				Customization = CloneCustomization(playerProfile),
				Health = CloneHealth(playerProfile),
				Inventory = GenerateDefaultInventory(),
			};

			var profile = new Profile(profileDescriptor);
			CloneEquipment(profile.Inventory.Equipment.Slots, playerProfile.Inventory.Equipment.Slots);

			return profile;
		}

		public void CloneEquipment(Slot[] targetSlots, Slot[] sourceSlots)
		{
			for (var i = 0; i < targetSlots.Length && i < sourceSlots.Length; i++)
			{
				var originalItem = sourceSlots[i].ContainedItem;
				if (originalItem != null)
				{
					var clonedItem = originalItem.CloneItem();
					targetSlots[i].ChangeContainedItemDirectly(clonedItem);
				}
			}
		}

		public Profile GenerateProfileWithMannequinEquipment(Profile playerProfile, int mannequinIndex)
		{
			var profileDescriptor = GenerateMannequinProfile();

			if (!playerProfile.Inventory.HideoutAreaStashes.TryGetValue(EAreaType.EquipmentPresetsStand, out var equipmentPresetsStand))
			{
				return new(profileDescriptor);
			}

			var mannequinItem = equipmentPresetsStand.Slots[mannequinIndex].ContainedItem;
			if (mannequinItem == null || mannequinItem is not CompoundItem mannequin)
			{
				return new(profileDescriptor);
			}

			// TODO use pants player assigned in mannequin customization option

			// default mannequin pants don't have holster, so pistols will fly
			// in the air somewhere near mannequin, so use pants from player

			profileDescriptor.Customization[EBodyModelPart.Feet] = playerProfile.Customization[EBodyModelPart.Feet];

			var profile = new Profile(profileDescriptor);
			CloneEquipment(profile.Inventory.Equipment.Slots, mannequin.Slots);

			return profile;
		}

		public ProfileDescriptor GenerateMannequinProfile()
		{
			return new()
			{
				Id = MongoID.Generate(true),
				Info = new(),
				Customization = GenerateDefaultCustomization(),
				Health = GenerateDefaultHealth(),
				Inventory = GenerateDefaultInventory(),
			};
		}

		public static Dictionary<EBodyModelPart, MongoID> CloneCustomization(Profile profile)
		{
			// probably no need to clone profile.Customization,
			// we could just return it itself, but lets be safe

			var source = profile.Customization;
			return new()
			{
			    { EBodyModelPart.Head, source[EBodyModelPart.Head] },
			    { EBodyModelPart.Body, source[EBodyModelPart.Body] },
			    { EBodyModelPart.Feet, source[EBodyModelPart.Feet] },
			    { EBodyModelPart.Hands, source[EBodyModelPart.Hands] },
			    { EBodyModelPart.Voice, source[EBodyModelPart.Voice] },
			};
		}

		public static Dictionary<EBodyModelPart, MongoID> GenerateDefaultCustomization()
		{
			return new()
			{
			    { EBodyModelPart.Head, "6644d2da35d958070c02642c" },
			    { EBodyModelPart.Body, "6644d2ffd85107e63500a61c" },
			    { EBodyModelPart.Feet, "6644d32235d958070c02642e" },
			    { EBodyModelPart.Hands, "5cc2e68f14c02e28b47de290" },
			    { EBodyModelPart.Voice, "5fc613c80b735e7b024c76e2" },
			};
		}

		public Profile.HealthInfo CloneHealth(Profile profile)
		{
			// some people run with custom health, this is for them

			var source = profile.Health;
			var sourceBodyParts = source.BodyParts;

			return new()
			{
				BodyParts = new()
				{
					{ EBodyPart.Head, NewBodyPartInfo(sourceBodyParts[EBodyPart.Head]) },
					{ EBodyPart.Chest, NewBodyPartInfo(sourceBodyParts[EBodyPart.Chest]) },
					{ EBodyPart.Stomach, NewBodyPartInfo(sourceBodyParts[EBodyPart.Stomach]) },
					{ EBodyPart.LeftArm, NewBodyPartInfo(sourceBodyParts[EBodyPart.LeftArm]) },
					{ EBodyPart.RightArm, NewBodyPartInfo(sourceBodyParts[EBodyPart.RightArm]) },
					{ EBodyPart.LeftLeg, NewBodyPartInfo(sourceBodyParts[EBodyPart.LeftLeg]) },
					{ EBodyPart.RightLeg, NewBodyPartInfo(sourceBodyParts[EBodyPart.RightLeg]) },
				},
				Energy = NewHealthValueInfo(source.Energy.Maximum),
				Hydration = NewHealthValueInfo(source.Hydration.Maximum),
				Temperature = NewHealthValueInfo(36.6f, 28, 40),
				Poison = NewHealthValueInfo(0, 0, 100),
			};
		}

		public Profile.HealthInfo GenerateDefaultHealth()
		{
			return new()
			{
				BodyParts = new()
				{
					{ EBodyPart.Head, NewBodyPartInfo(Mannequin_Health_Head.Value) },
					{ EBodyPart.Chest, NewBodyPartInfo(Mannequin_Health_Chest.Value) },
					{ EBodyPart.Stomach, NewBodyPartInfo(Mannequin_Health_Stomach.Value) },
					{ EBodyPart.LeftArm, NewBodyPartInfo(Mannequin_Health_Arm.Value) },
					{ EBodyPart.RightArm, NewBodyPartInfo(Mannequin_Health_Arm.Value) },
					{ EBodyPart.LeftLeg, NewBodyPartInfo(Mannequin_Health_Leg.Value) },
					{ EBodyPart.RightLeg, NewBodyPartInfo(Mannequin_Health_Leg.Value) },
				},
				Energy = NewHealthValueInfo(100),
				Hydration = NewHealthValueInfo(100),
				Temperature = NewHealthValueInfo(36.6f, 28, 40),
				Poison = NewHealthValueInfo(0, 0, 100),
			};
		}

		public static Profile.HealthInfo.BodyPartInfo NewBodyPartInfo(Profile.HealthInfo.BodyPartInfo source)
		{
			return new() { Health = NewHealthValueInfo(source.Health.Maximum) };
		}

		public static Profile.HealthInfo.BodyPartInfo NewBodyPartInfo(float maxHealthValue)
		{
			return new() { Health = NewHealthValueInfo(maxHealthValue) };
		}

		public static Profile.HealthInfo.ValueInfo NewHealthValueInfo(float maxValue)
		{
			return NewHealthValueInfo(maxValue, 0, maxValue);
		}

		public static Profile.HealthInfo.ValueInfo NewHealthValueInfo(float currentValue, float minValue, float maxValue)
		{
			return new()
			{
				Current = currentValue,
				Minimum = minValue,
				Maximum = maxValue,
			};
		}

		public static InventoryDescriptor GenerateDefaultInventory()
		{
		    var equipment = MongoID.Generate(true);
			return new()
			{
				_items =
				[
					new() { _id = equipment, _tpl = "55d7217a4bdc2d86028b456d" },
				],
				_equipmentId = equipment,
			};
		}

		public async Task<Profile> GetBotProfile(IEftSession session, WildSpawnType botType)
		{
			var botProfileRequest = new CountTypeBotWave(1, botType, BotDifficulty.normal);
			var profilesRequest = new List<CountTypeBotWave>(1) { botProfileRequest };
			var profiles = await session.LoadBots(profilesRequest);
			if (profiles.Length > 0)
			{
				return profiles[0];
			}

			// happens when user selects broken bot type for Custom type
			return new(GenerateMannequinProfile());
		}

		public static WildSpawnType GetBotType(MannequinType mannequinType)
		{
			return mannequinType switch
			{
				MannequinType.Scav => WildSpawnType.assault,
				MannequinType.ScavSniper => WildSpawnType.marksman,
				MannequinType.Raider => WildSpawnType.pmcBot,

				MannequinType.BEAR => WildSpawnType.pmcBEAR,
				MannequinType.USEC => WildSpawnType.pmcUSEC,

				MannequinType.Reshala => WildSpawnType.bossBully,
				MannequinType.ReshalaGuard => WildSpawnType.followerBully,

				MannequinType.Shturman => WildSpawnType.bossKojaniy,
				MannequinType.ShturmanGuard => WildSpawnType.followerKojaniy,

				MannequinType.Sanitar => WildSpawnType.bossSanitar,
				MannequinType.SanitarGuard => WildSpawnType.followerSanitar,

				MannequinType.Gluhar => WildSpawnType.bossGluhar,
				MannequinType.GluharGuardAssault => WildSpawnType.followerGluharAssault,
				MannequinType.GluharGuardSecurity => WildSpawnType.followerGluharSecurity,
				MannequinType.GluharGuardScout => WildSpawnType.followerGluharScout,
				MannequinType.GluharGuardSnipe => WildSpawnType.followerGluharSnipe,

				MannequinType.Killa => WildSpawnType.bossKilla,
				MannequinType.KillaLabyrinth => WildSpawnType.bossKillaAgro,

				MannequinType.Tagilla => WildSpawnType.bossTagilla,
				MannequinType.TagillaLabyrinth => WildSpawnType.bossTagillaAgro,

				MannequinType.Rogue => WildSpawnType.exUsec,
				MannequinType.Knight => WildSpawnType.bossKnight,
				MannequinType.BigPipe => WildSpawnType.followerBigPipe,
				MannequinType.BirdEye => WildSpawnType.followerBirdEye,

				MannequinType.CultistWarrior => WildSpawnType.sectantWarrior,
				MannequinType.CultistPriest => WildSpawnType.sectantPriest,

				MannequinType.Zryachiy => WildSpawnType.bossZryachiy,
				MannequinType.ZryachiyGuard => WildSpawnType.followerZryachiy,

				MannequinType.Kaban => WildSpawnType.bossBoar,
				MannequinType.KabanGuardBasmach => WildSpawnType.followerBoarClose1,
				MannequinType.KabanGuardGus => WildSpawnType.followerBoarClose2,
				MannequinType.KabanGuard => WildSpawnType.followerBoar,
				MannequinType.KabanGuardSniper => WildSpawnType.bossBoarSniper,

				MannequinType.Kolontay => WildSpawnType.bossKolontay,
				MannequinType.KolontayGuardAssault => WildSpawnType.followerKolontayAssault,
				MannequinType.KolontayGuardSecurity => WildSpawnType.followerKolontaySecurity,

				MannequinType.Partisan => WildSpawnType.bossPartisan,

				MannequinType.BlackDivisionAssault => (WildSpawnType)848421,
				MannequinType.BlackDivisionWedge => (WildSpawnType)848424,
				MannequinType.BlackDivisionIcebreakerRaider => (WildSpawnType)848426,

		        MannequinType.RuafRifleman => (WildSpawnType)848400,
		        MannequinType.RuafSeniorRifleman => (WildSpawnType)848401,
		        MannequinType.RuafAutorifleman => (WildSpawnType)848402,
		        MannequinType.RuafMarksman => (WildSpawnType)848404,
		        MannequinType.RuafMachinegunner => (WildSpawnType)848405,
		        MannequinType.RemnantRifleman => (WildSpawnType)848406,

		        MannequinType.UntarRifleman => (WildSpawnType)1170,
		        MannequinType.UntarSquadLeader => (WildSpawnType)1171,
		        MannequinType.UntarOfficer => (WildSpawnType)1173,

				_ => throw new ArgumentException($"Unknown mannequin type: {mannequinType}"),
			};
		}

		public static string[] ShootingRangeTargets =
		[
			"Rail_targets/01_rail_target/Shooting_range_rails_02/Shooting_range_target_rails",
			"Rail_targets/02_rail_target/Shooting_range_rails_02 (1)/Shooting_range_target_rails",
			"Rail_targets/03_rail_target/Shooting_range_rails_02 (2)/Shooting_range_target_rails",
			"Popper_targets",
			"Target_stand_changed (1)",
			"Target_stand_changed (2)",
			"Target_stand_changed (3)",
			"Target_stand_changed (4)",
			"metal_target (1)",
			"metal_target (2)",
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

			if (areaLevel == shootingRange.AreaLevels[0])
			{
				// level 0, no shooting range
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

			yield return new WaitForSeconds(0.5f);

			bot.Dispose();
			AssetPoolObject.ReturnToPool(bot.gameObject, true);

			yield return new WaitForSeconds(0.5f);

			SpawnBot(mannequinData);
		}

		public void ClearBots()
		{
			foreach (var mannequin in Mannequins.Keys)
			{
				mannequin.Dispose();
				AssetPoolObject.ReturnToPool(mannequin.gameObject, true);
			}
			Mannequins.Clear();
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
