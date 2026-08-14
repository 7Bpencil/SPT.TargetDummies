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
using System.IO;
using System.Reflection;
using UnityEngine;

// TargetMannequins
//
// EAreaType.EquipmentPresetsStand
// walk around model, get all body part collider, and change go layer to HitCollider
// also need to attach other colliders to prevent gun
// maneq is missing some colliders (at least head colliders)

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
		public float Health;
		public Transform RotationPivot;
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

				var player = new MannequinPlayer();
				player.Health = 440;
				player.RotationPivot = prefab.transform;

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
			Plugin.Instance.LoggerInstance.LogWarning("ApplyShot");
			var shotDamage = 60;
			if (MannequinPlayer.Health > 0)
			{
				MannequinPlayer.Health = Math.Max(MannequinPlayer.Health - shotDamage, 0);
				if (MannequinPlayer.Health == 0)
				{
					// TODO make animations more sexy
					var sequence = DOTween.Sequence();
					sequence.Append(MannequinPlayer.RotationPivot.DOLocalRotate(new(-90, 0, 0), 0.5f));
					sequence.AppendInterval(3);
					sequence.Append(MannequinPlayer.RotationPivot.DOLocalRotate(new(0, 0, 0), 0.5f));
					sequence.AppendCallback(() =>
					{
						MannequinPlayer.Health = 440;
					});
				}
			}
			return null;
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
