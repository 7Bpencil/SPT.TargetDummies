//
// Copyright (c) 2026 7Bpencil
//
// This source code is licensed under the MIT license found in the
// LICENSE file in the root directory of this source tree.
//

using Comfort.Common;
using EFT;
using EFT.Interactive;
using EFT.Hideout;
using System.Linq;
using System.Reflection;
using SPT.Reflection.Patching;
using HarmonyLib;
using UnityEngine;

namespace SevenBoldPencil.TargetDummies
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

	public class Patch_GameWorld_DestroyAllLoot : ModulePatch
	{
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(GameWorld), nameof(GameWorld.DestroyAllLoot));
        }

        [PatchPrefix]
        public static bool Prefix(GameWorld __instance)
		{
			if (__instance is not HideoutGameWorld)
			{
				return true;
			}

			// quitting shooting range destroys all loot and corpses are considered loot too
			foreach (var lootItem in __instance.LootList.OfType<LootItem>().ToArray<LootItem>())
			{
				if (lootItem is not Corpse)
				{
					__instance.DestroyLoot(lootItem);
				}
			}

			return false;
		}
	}

	public class Patch_CorpseRagdoll_Start : ModulePatch
	{
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(CorpseRagdoll), nameof(CorpseRagdoll.Start));
        }

        [PatchPrefix]
        public static void Prefix(CorpseRagdoll __instance)
		{
			var gameWorld = Singleton<GameWorld>.Instance;
			if (gameWorld is not HideoutGameWorld)
			{
				return;
			}

			__instance._onRigidbodyStopped += () =>
			{
				if (__instance._owner.TryGetComponent<LocalPlayer>(out var localPlayer))
				{
					Plugin.Instance.OnBotDeath(localPlayer);
				}
			};
		}
	}

	// BSG doesnt check which collider exited shooting range, which means respawning
	// bots force player to exit shooting range, so add check if collider actually belongs to player
	public class Patch_HideoutAreaTrigger_OnTriggerExit : ModulePatch
	{
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(HideoutAreaTrigger), nameof(HideoutAreaTrigger.OnTriggerExit));
        }

        [PatchPrefix]
        public static bool Prefix(HideoutArea ____area, Collider col)
		{
			if (____area == null)
			{
				return false;
			}

			var gameWorld = Singleton<GameWorld>.Instance;
			var colliderOwner = gameWorld.GetPlayerByCollider(col);
			if (colliderOwner == null)
			{
				return false;
			}
			if (colliderOwner != gameWorld.MainPlayer)
			{
				return false;
			}

			____area.Data.Template.AreaBehaviour.OnExitLocation();
			return false;
		}
	}
}
