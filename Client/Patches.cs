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
	public class Patch_HideoutController_HideoutAwake : ModulePatch
	{
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(HideoutController), nameof(HideoutController.HideoutAwake));
        }

        [PatchPostfix]
        public static void Postfix(HideoutController __instance)
		{
			Plugin.Instance.HideShootingRangeTargets(__instance);
		}
	}
}
