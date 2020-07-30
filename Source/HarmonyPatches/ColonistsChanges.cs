﻿using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;

namespace Puppeteer
{
	[HarmonyPatch(typeof(Game))]
	[HarmonyPatch(nameof(Game.LoadGame))]
	static class Game_LoadGame_Patch
	{
		[HarmonyPriority(Priority.First)]
		public static void Postfix()
		{
			Controller.instance.SetEvent(PuppeteerEvent.ColonistsChanged);
			Tools.GameInit();
		}
	}

	[HarmonyPatch(typeof(Thing))]
	[HarmonyPatch(nameof(Thing.SplitOff))]
	static class Thing_SplitOff_Patch
	{
		public static bool inSplitOff = false;

		[HarmonyPriority(Priority.First)]
		static void Prefix()
		{
			inSplitOff = true;
		}

		[HarmonyPriority(Priority.First)]
		static void Postfix()
		{
			inSplitOff = false;
		}
	}

	[HarmonyPatch(typeof(Pawn))]
	[HarmonyPatch(nameof(Pawn.SpawnSetup))]
	static class Pawn_SpawnSetup_Patch
	{
		[HarmonyPriority(Priority.First)]
		public static void Postfix(Pawn __instance, bool respawningAfterLoad)
		{
			if (respawningAfterLoad == false && __instance.Spawned && __instance.IsColonist)
				Controller.instance.PawnAvailable(__instance);
		}
	}

	[HarmonyPatch(typeof(GenSpawn))]
	[HarmonyPatch(nameof(GenSpawn.Spawn))]
	[HarmonyPatch(new[] { typeof(Thing), typeof(IntVec3), typeof(Map), typeof(Rot4), typeof(WipeMode), typeof(bool) })]
	static class GenSpawn_Spawn_Patch
	{
		public static void Postfix(Thing newThing)
		{
			if (newThing is Pawn pawn && pawn.IsColonist)
				Controller.instance.UpdateAvailability(pawn);
		}
	}

	[HarmonyPatch(typeof(Pawn))]
	[HarmonyPatch(nameof(Pawn.DeSpawn))]
	static class Pawn_DeSpawn_Patch
	{
		public static bool manualDespawn = false;
		static AccessTools.FieldRef<bool> manualDespawnRef = null;

		public static void Prepare(MethodBase original)
		{
			if (original != null) return;
			var type = AccessTools.TypeByName("ZLevels.JobManagerPatches") ?? typeof(Pawn_DeSpawn_Patch);
			manualDespawnRef = AccessTools.StaticFieldRefAccess<bool>(AccessTools.Field(type, "manualDespawn"));
		}

		[HarmonyPriority(Priority.First)]
		public static void Postfix(Pawn __instance)
		{
			if (__instance.IsColonist && Thing_SplitOff_Patch.inSplitOff == false && manualDespawnRef() == false)
				Controller.instance.UpdateAvailability(__instance);
		}
	}

	[HarmonyPatch(typeof(Pawn))]
	[HarmonyPatch(nameof(Pawn.Kill))]
	static class Pawn_Kill_Patch
	{
		[HarmonyPriority(Priority.First)]
		public static void Postfix(Pawn __instance)
		{
			if (__instance.IsColonist)
				Controller.instance.PawnUnavailable(__instance);
		}
	}

	[HarmonyPatch(typeof(WorldPawns))]
	[HarmonyPatch(nameof(WorldPawns.PassToWorld))]
	static class WorldPawns_PassToWorld_Patch
	{
		[HarmonyPriority(Priority.First)]
		public static void Postfix(Pawn pawn)
		{
			if (pawn.IsColonist)
				Controller.instance.SetEvent(PuppeteerEvent.ColonistsChanged);
		}
	}

	[HarmonyPatch(typeof(ColonistBar))]
	[HarmonyPatch("Reorder")]
	static class ColonistBar_Reorder_Patch
	{
		[HarmonyPriority(Priority.First)]
		public static void Postfix()
		{
			Controller.instance.SetEvent(PuppeteerEvent.ColonistsChanged);
		}
	}

	[HarmonyPatch(typeof(Pawn_WorkSettings))]
	[HarmonyPatch(nameof(Pawn_WorkSettings.SetPriority))]
	static class Pawn_WorkSettings_SetPriority_Patch
	{
		[HarmonyPriority(Priority.First)]
		public static void Postfix()
		{
			Controller.instance.SetEvent(PuppeteerEvent.PrioritiesChanged);
		}
	}

	[HarmonyPatch(typeof(Pawn_WorkSettings))]
	[HarmonyPatch(nameof(Pawn_WorkSettings.Notify_UseWorkPrioritiesChanged))]
	static class Pawn_WorkSettings_Notify_UseWorkPrioritiesChanged_Patch
	{
		[HarmonyPriority(Priority.First)]
		public static void Postfix()
		{
			Controller.instance.SetEvent(PuppeteerEvent.PrioritiesChanged);
		}
	}

	[HarmonyPatch(typeof(Pawn_TimetableTracker))]
	[HarmonyPatch(nameof(Pawn_TimetableTracker.SetAssignment))]
	static class Pawn_TimetableTracker_SetPriority_Patch
	{
		[HarmonyPriority(Priority.First)]
		public static void Postfix()
		{
			Controller.instance.SetEvent(PuppeteerEvent.SchedulesChanged);
		}
	}

	[HarmonyPatch(typeof(Pawn_TimetableTracker), MethodType.Constructor)]
	[HarmonyPatch(new Type[] { typeof(Pawn) })]
	static class Pawn_TimetableTracker_Constructor_Patch
	{
		[HarmonyPriority(Priority.First)]
		public static void Postfix()
		{
			Controller.instance.SetEvent(PuppeteerEvent.SchedulesChanged);
		}
	}

	[HarmonyPatch(typeof(ColonistBarColonistDrawer))]
	[HarmonyPatch(nameof(ColonistBarColonistDrawer.DrawColonist))]
	static class ColonistBarColonistDrawer_DrawColonist_Patch
	{
		[HarmonyPriority(Priority.First)]
		public static void Prefix(Rect rect, Pawn colonist)
		{
			Drawing.DrawAssignmentStatus(colonist, rect);
		}
	}

	[HarmonyPatch(typeof(ColonistBarColonistDrawer))]
	[HarmonyPatch(nameof(ColonistBarColonistDrawer.HandleClicks))]
	static class ColonistBarColonistDrawer_HandleClicks_Patch
	{
		[HarmonyPriority(Priority.First)]
		public static void Prefix(Rect rect, Pawn colonist)
		{
			Drawing.AssignFloatMenu(colonist, rect);
		}
	}

	[HarmonyPatch(typeof(ColonistBarDrawLocsFinder))]
	[HarmonyPatch("GetDrawLoc")]
	static class ColonistBarDrawLocsFinder_GetDrawLoc_Patch
	{
		const float BaseSpaceBetweenColonistsVertical = 32f;
		const float extraVerticalOffset = 15f;
		const float extraDevModeOffset = 20f;

		[HarmonyPriority(Priority.First)]
		public static void Postfix(ref Vector2 __result, float scale)
		{
			var offset = extraVerticalOffset + (Prefs.DevMode ? extraDevModeOffset : 0f);
			__result.y += offset * scale;
		}

		[HarmonyPriority(Priority.First)]
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			foreach (var instr in instructions)
			{
				var scale = Find.UIRoot == null || Find.MapUI == null ? 1f : (Find.ColonistBar?.Scale ?? 1f);
				if (instr.OperandIs(BaseSpaceBetweenColonistsVertical))
				{
					var offset = extraVerticalOffset + (Prefs.DevMode ? extraDevModeOffset : 0f);
					instr.operand = BaseSpaceBetweenColonistsVertical + offset * scale;
				}
				yield return instr;
			}
		}
	}
}