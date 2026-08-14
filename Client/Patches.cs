//
// Copyright (c) 2026 7Bpencil
//
// This source code is licensed under the MIT license found in the
// LICENSE file in the root directory of this source tree.
//

using DG.Tweening;
using EFT;
using EFT.Ballistics;
using EFT.Interactive;
using EFT.InventoryLogic;
using EFT.Hideout;
using SevenBoldPencil.Common;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Reflection;
using SPT.Reflection.Patching;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SevenBoldPencil.TargetMannequins
{
    public struct StashItemModelSpawnPoint_Proxy(StashItemModelSpawnPoint instance)
    {
        private readonly StashItemModelSpawnPoint __instance = instance;

        private static TypedFieldInfo<StashItemModelSpawnPoint, List<StashItemModel>> __items = new("_items");

        public List<StashItemModel> _items { get { return __items.Get(__instance); } set { __items.Set(__instance, value); } }
    }

	public struct StashItemModel_Proxy(StashItemModel instance)
	{
        private readonly StashItemModel __instance = instance;

        private static TypedFieldInfo<StashItemModel, GameObject> __prefab = new("_prefab");
        private static TypedFieldInfo<StashItemModel, Transform> __rotationPivot = new("_rotationPivot");

        public GameObject _prefab { get { return __prefab.Get(__instance); } set { __prefab.Set(__instance, value); } }
        public Transform _rotationPivot { get { return __rotationPivot.Get(__instance); } set { __rotationPivot.Set(__instance, value); } }
	}

	public class Patch_HideoutAreaStashController_UpdateStash : ModulePatch
	{
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(HideoutAreaStashController), nameof(HideoutAreaStashController.UpdateStash));
        }

        [PatchPostfix]
        public static void Postfix(ref Task __result, HideoutAreaStashController __instance, bool clearCache = false)
		{
			if (__instance._areaType != EAreaType.EquipmentPresetsStand)
			{
				return;
			}
	        __result = __result.ContinueWith(t =>
	        {
	            if (t.IsCompletedSuccessfully)
	            {
					Plugin.Instance.PatchMannequins(__instance);
	            }
	            return t;
	        }).Unwrap();
		}
	}
}
