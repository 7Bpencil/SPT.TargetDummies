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
using DG.Tweening;
using EFT;
using EFT.Ballistics;
using EFT.Interactive;
using EFT.InventoryLogic;
using EFT.Hideout;
using Newtonsoft.Json;
using SevenBoldPencil.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
	public readonly record struct MannequinData
	(
		string SpawnPointName,
		Vector3 LocalPosition,
		float LocalEulerAnglesY
	);

	public class MannequinPlayer
	{
		public readonly Transform RotationPivot;
		public readonly Dictionary<EBodyPart, float> Health;
		public bool IsAlive;

		public MannequinPlayer(Transform rotationPivot)
		{
			RotationPivot = rotationPivot;
			Health = new(7);
			ResetHealth();
		}

		public void ResetHealth()
		{
			Health[EBodyPart.Head] = Plugin.Instance.Health_Head;
			Health[EBodyPart.Chest] = Plugin.Instance.Health_Chest;
			Health[EBodyPart.Stomach] = Plugin.Instance.Health_Stomach;
			Health[EBodyPart.LeftArm] = Plugin.Instance.Health_Arm;
			Health[EBodyPart.RightArm] = Plugin.Instance.Health_Arm;
			Health[EBodyPart.LeftLeg] = Plugin.Instance.Health_Leg;
			Health[EBodyPart.RightLeg] = Plugin.Instance.Health_Leg;
			IsAlive = true;
		}

		public float GetTotalHealth()
		{
			var sum = 0f;
			foreach (var bodyPartHealth in Health.Values)
			{
				sum += bodyPartHealth;
			}
			return sum;
		}
	}

    [BepInPlugin("7Bpencil.TargetMannequins", "7Bpencil.TargetMannequins", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
		// TODO scale them down a little to match player height
		public static readonly MannequinData[] MannequinsData =
		[
			new
			(
				SpawnPointName: "Stand1",
				LocalPosition: new(-9.6436f, 0.9881f, 11.0927f),
				LocalEulerAnglesY: 180
			),
			new
			(
				SpawnPointName: "Stand2",
				LocalPosition: new(-11.4582f, 0.9883f, 3.3036f),
				LocalEulerAnglesY: 135
			),
			new
			(
				SpawnPointName: "Stand3",
				LocalPosition: new(-8.3382f, 0.9881f, -3.5891f),
				LocalEulerAnglesY: 90
			),
		];

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

		public void PatchMannequins(HideoutAreaStashController __instance)
		{
			// TODO this one is called every time mannequin equipment is changed,
			// so track when shit is needed and when not,
			// TODO we probably should make our own mannequins and dont touch bsg ones
			// TODO setup all body part colliders correctly

			foreach (var mannequinData in MannequinsData)
			{
				var spawnPoint = __instance._stashObjectsSpawnPoints[mannequinData.SpawnPointName];
				var _spawnPoint = new StashItemModelSpawnPoint_Proxy(spawnPoint);
				var items = _spawnPoint._items;
				if (items.Count == 0)
				{
					Logger.LogWarning($"{mannequinData.SpawnPointName} no items");
					continue;
				}

				var item = items[0];
				var _item = new StashItemModel_Proxy(item);
				var prefab = _item._prefab;
				var rotationPivot = _item._rotationPivot;
				if (!prefab.TryGetComponent<PlayerBody>(out var playerBody))
				{
					Logger.LogWarning($"{mannequinData.SpawnPointName} no PlayerBody");
					continue;
				}

				rotationPivot.localPosition = mannequinData.LocalPosition;
				rotationPivot.localEulerAngles = new(0, mannequinData.LocalEulerAnglesY, 0);

				var player = new MannequinPlayer(prefab.transform);

				var playerBones = playerBody.PlayerBones;
				var colliders = playerBones.BodyPartColliders;
				foreach (var collider in colliders)
				{
					collider.gameObject.layer = LayersMaskController.HitColliderLayer;
					// collider.InitColliderSettings(); // TODO setup correct settings before this
					collider.playerBridge = new MannequinPlayerBridge(player);
				}

				Logger.LogWarning($"{mannequinData.SpawnPointName} done!");
			}
		}
    }

	public class MannequinPlayerBridge : BodyPartCollider.IObserverToPlayerBridge
	{
		public readonly MannequinPlayer MannequinPlayer;

		public MannequinPlayerBridge(MannequinPlayer mannequinPlayer)
		{
			MannequinPlayer = mannequinPlayer;
		}

		public IPlayer iPlayer
		{
			get
			{
				Plugin.Instance.LoggerInstance.LogWarning("iPlayer");
				return null;
			}
		}

		public float WorldTime
		{
			get
			{
				Plugin.Instance.LoggerInstance.LogWarning("WorldTime");
				return 0;
			}
		}

		public bool UsingSimplifiedSkeleton
		{
			get
			{
				Plugin.Instance.LoggerInstance.LogWarning("UsingSimplifiedSkeleton");
				return false;
			}
		}

		public PlayerHitInfo ApplyShot(DamageInfo damageInfo, EBodyPart bodyPart, EBodyPartColliderType bodyPartCollider, EArmorPlateCollider armorPlateCollider, ShotId shotId)
		{
			// TODO finish damage spread and commit that implementing damage+armor system ourselves is a bad idea,
			// just try to spawn bot, then make custom one
			if (MannequinPlayer.IsAlive)
			{
				var health = MannequinPlayer.Health;

				Plugin.Instance.LoggerInstance.LogWarning($"ApplyShot: part: {bodyPart}, health: {health[bodyPart]}, total: {MannequinPlayer.GetTotalHealth()}, damage: {damageInfo.Damage}");

				if (health[bodyPart] > 0)
				{
					health[bodyPart] = Math.Max(health[bodyPart] - damageInfo.Damage, 0);
					if (health[bodyPart] == 0)
					{
						if (bodyPart == EBodyPart.Head || bodyPart == EBodyPart.Chest)
						{
							Kill();
						}
						// TODO spread damage over non destroyed body parts
					}
				}
				else
				{
					// TODO spread damage over non destroyed body parts
				}
			}
			return null;
		}

		private void Kill()
		{
			MannequinPlayer.IsAlive = false;

			// TODO make animations more sexy
			var sequence = DOTween.Sequence();
			sequence.Append(MannequinPlayer.RotationPivot.DOLocalRotate(new(-90, 0, 0), 0.5f));
			sequence.AppendInterval(3);
			sequence.Append(MannequinPlayer.RotationPivot.DOLocalRotate(new(0, 0, 0), 0.5f));
			sequence.AppendCallback(() =>
			{
				MannequinPlayer.ResetHealth();
			});
		}


		public void ApplyDamageInfo(DamageInfo damageInfo, EBodyPart bodyPartType, EBodyPartColliderType bodyPartCollider, float absorbed)
		{
			Plugin.Instance.LoggerInstance.LogWarning("ApplyDamageInfo");
		}

		public bool TryGetArmorResistData(BodyPartCollider bodyPart, float penetrationPower, out ArmorResistanceData armorResistanceData)
		{
			Plugin.Instance.LoggerInstance.LogWarning("TryGetArmorResistData");
			armorResistanceData = default;
			return false;
		}

		public bool SetShotStatus(BodyPartCollider bodypart, Shot shot, Vector3 hitpoint, Vector3 shotNormal, Vector3 shotDirection)
		{
			Plugin.Instance.LoggerInstance.LogWarning("SetShotStatus");
			// either deflected by armor, or set
			return false;
		}

		public bool CheckArmorHitByDirection(BodyPartCollider bodypart, Vector3 hitpoint, Vector3 shotNormal, Vector3 shotDirection)
		{
			Plugin.Instance.LoggerInstance.LogWarning("CheckArmorHitByDirection");
			return false;
		}

		public bool IsShotDeflectedByHeavyArmor(EBodyPartColliderType colliderType, EArmorPlateCollider armorPlateCollider, int shotSeed)
		{
			Plugin.Instance.LoggerInstance.LogWarning("IsShotDeflectedByHeavyArmor");
			return false;
		}
	}

}
