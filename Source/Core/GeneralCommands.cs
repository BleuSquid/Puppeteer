﻿using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Profile;

namespace Puppeteer
{
	public static class GeneralCommands
	{
		public static void CheckVersionRequired(Welcome info)
		{
			var minimumVersion = new Version(info.minVersion);
			var currentVersion = new Version(Tools.GetModVersionString());
			if (currentVersion < minimumVersion)
			{
				OperationQueue.Add(OperationType.Log, () =>
				{
					var note = $"The Puppeteer server needs v{minimumVersion} of Puppeteer but you are running v{currentVersion}. " +
									"Please make sure the Puppeteer Mod is updated.\n\n" +
									"Thank you.";
					if (Current.ProgramState == ProgramState.Playing)
					{
						GameDataSaveLoader.SaveGame($"Puppeteer-Upgrade-Save-{DateTime.Now:yyyyMMdd-HHmmss}");
						Find.WindowStack.Add(new NoteDialog(note, "SaveAndQuitToMainMenu".Translate(), null, null, null, "Puppeteer")
						{
							closeAction = () =>
							{
								LongEventHandler.QueueLongEvent(delegate ()
								{
									MemoryUtility.ClearAllMapsAndWorld();
								}, "Entry", "SavingLongEvent", false, null, false);
							}
						});
						return;
					}
					var note2 = new NoteDialog(note);
					Find.WindowStack.Add(note2);
				});
			}
		}

		public static void SendAllColonists(Connection connection)
		{
			if (connection == null) return;
			var colonists = Tools.AllColonists(false)
				.Select(pawn =>
				{
					var puppet = State.Instance.PuppetForPawn(pawn);
					return new ColonistInfo()
					{
						id = pawn.thingIDNumber,
						name = pawn.OriginalName(),
						controller = puppet?.puppeteer?.vID,
					};
				})
				.ToList();
			connection.Send(new AllColonists() { colonists = colonists });
		}

		public static void Join(Connection connection, ViewerID vID)
		{
			if (connection == null || vID.IsValid == false) return;
			Tools.LogWarning($"{vID.name} joined");
			State.Instance.SetConnected(vID, true);
			var pawn = State.Instance.PuppeteerForViewer(vID)?.puppet?.pawn;
			if (pawn?.Map != null) Tools.SetColonistNickname(pawn, vID.name);
			State.Save();
			SendAllState(connection, vID);
			TwitchToolkitMod.RefreshViewers();
		}

		public static void Leave(ViewerID vID)
		{
			if (vID.IsValid)
			{
				Tools.LogWarning($"{vID.name} left");
				State.Instance.SetConnected(vID, false);
				State.Save();
			}
		}

		public static void Availability(Connection connection, Pawn pawn)
		{
			if (connection == null || pawn == null) return;
			var pawnID = pawn.ThingID;
			var puppeteer = State.Instance.ConnectedPuppeteers().FirstOrDefault(p => p.puppet?.pawn?.ThingID == pawnID);
			if (puppeteer != null)
				connection.Send(new ColonistAvailable() { viewer = puppeteer.vID, state = pawn.Spawned });
		}

		public static void SendChatMessage(Connection connection, ViewerID vID, string message)
		{
			if (connection == null || message == null || message.Length == 0) return;
			connection.Send(new OutgoingChat() { viewer = vID, message = message });
		}

		public static void SendToolkitCommands(Connection connection, ViewerID vID)
		{
			if (connection == null) return;
			var commands = TwitchToolkitMod.GetAllCommands();
			connection.Send(new ToolkitCommands() { viewer = vID, commands = commands });
		}

		public static void Assign(Connection connection, Pawn pawn, ViewerID vID)
		{
			void SendAssignment(ViewerID v, bool state) => connection.Send(new Assignment() { viewer = v, state = state });

			if (vID == null)
			{
				if (pawn == null || pawn.Spawned == false) return;
				// Tools.SetColonistNickname(pawn, null);
				Tools.LogWarning($"{pawn.OriginalName()} lost control");
				vID = State.Instance.PuppetForPawn(pawn)?.puppeteer?.vID;
				State.Instance.Unassign(vID);
				if (vID != null) SendAssignment(vID, false);
				State.Save();
				return;
			}

			var oldPawn = State.Instance.PuppeteerForViewer(vID)?.puppet?.pawn;
			// Tools.SetColonistNickname(oldPawn, null);
			State.Instance.Unassign(vID);

			var oldPuppeteer = State.Instance.PuppetForPawn(pawn)?.puppeteer;
			State.Instance.Unassign(oldPuppeteer?.vID);

			State.Instance.Assign(vID, pawn);
			Tools.SetColonistNickname(pawn, vID.name);

			Tools.LogWarning($"{pawn.OriginalName()} is now controlled by {vID.name}");
			SendAssignment(vID, true);
			State.Save();
		}

		static void SendGameInfo(Connection connection, ViewerID vID)
		{
			var puppeteer = State.Instance.PuppeteerForViewer(vID);
			var pawn = puppeteer?.puppet?.pawn;

			var features = new List<string>();
			if (ModLister.RoyaltyInstalled)
				features.Add("royalty");
			if (TwitchToolkitMod.Exists)
			{
				features.Add("twitch-toolkit");
				// TwitchToolkit.SendMessage(vID.id, vID.name, "bal");
			}

			var info = new GameInfo.Info()
			{
				version = Tools.GetModVersionString(),
				mapFreq = PuppeteerMod.Settings.mapUpdateFrequency,
				hairStyles = Customizer.AllHairStyle,
				bodyTypes = Customizer.AllBodyTypes,
				features = features.ToArray(),
				style = pawn == null ? null : Customizer.GetStyle(pawn)
			};
			connection.Send(new GameInfo() { viewer = vID, info = info });
		}

		static void SendTimeInfo(Connection connection, ViewerID vID)
		{
			if (connection == null) return;

			var map = Find.CurrentMap;
			if (map == null) return;

			var tickManager = Find.TickManager;
			if (tickManager == null) return;

			var worldGrid = Find.WorldGrid;
			if (worldGrid == null) return;

			var vector = worldGrid.LongLatOf(map.Tile);
			var dateStr = GenDate.DateFullStringWithHourAt(tickManager.TicksAbs, vector);
			connection.Send(new TimeInfo() { viewer = vID, info = new TimeInfo.Info() { time = dateStr, speed = (int)Find.TickManager.CurTimeSpeed } });
		}

		public static void SendCoinsToAll(Connection connection)
		{
			var puppeteers = State.Instance.ConnectedPuppeteers();
			puppeteers.Do(puppeteer => SendCoins(connection, puppeteer));
		}

		public static void SendCoins(Connection connection, State.Puppeteer puppeteer)
		{
			if (puppeteer == null) return;
			var coins = TwitchToolkitMod.GetCurrentCoins(puppeteer.vID.name);
			connection.Send(new Earned() { viewer = puppeteer.vID, info = new Earned.Info() { amount = coins } });
		}

		public static void SendPortrait(Connection connection, State.Puppeteer puppeteer)
		{
			var vID = puppeteer?.vID;
			var pawn = puppeteer?.puppet?.pawn;
			if (vID != null && pawn?.Map != null)
				OperationQueue.Add(OperationType.Portrait, () =>
				{
					var portrait = Renderer.GetPawnPortrait(pawn, new Vector2(35f, 55f));
					connection.Send(new Portrait() { viewer = vID, info = new Portrait.Info() { image = portrait } });
				});
		}

		static void SendStates<T>(Connection connection, string key, Func<Pawn, T> valueFunction, State.Puppeteer forPuppeteer = null)
		{
			void SendState(State.Puppeteer puppeteer)
			{
				var vID = puppeteer?.vID;
				var pawn = puppeteer?.puppet?.pawn;

				if (pawn?.Map != null)
					connection.Send(new OutgoingState<T>() { viewer = vID, key = key, val = valueFunction(pawn) });
			}

			if (forPuppeteer != null)
			{
				SendState(forPuppeteer);
				return;
			}
			var puppeteers = State.Instance.ConnectedPuppeteers();
			puppeteers?.Do(p => SendState(p));
		}

		public static void SendAreas(Connection connection, State.Puppeteer forPuppeteer = null)
		{
			string[] GetResult(Pawn pawn)
			{
				return pawn.Map.areaManager.AllAreas
					.Where(a => a.AssignableAsAllowed())
					.Select(a => a.Label).ToArray();
			}
			SendStates(connection, "zones", GetResult, forPuppeteer);
		}

		static List<Pawn> AllColonistsWithCurrentTop(Pawn pawn)
		{
			var list = Tools.AllColonists(false);
			if (list.Remove(pawn))
				list.Insert(0, pawn);
			return list;
		}

		public static void SendPriorities(Connection connection)
		{
			PrioritiyInfo GetResult(Pawn pawn)
			{
				bool IsIncapableOfWholeWorkType(WorkTypeDef work)
				{
					return work.workGiversByPriority.SelectMany(prio => prio.requiredCapacities)
						.Any(capacity => pawn.health.capacities.CapableOf(capacity) == false);
				}

				int[] GetValues(Pawn p)
				{
					return Integrations.GetPawnWorkerDefs().Select(workType =>
					{
						var priority = p.workSettings.GetPriority(workType);
						var passion = (int)p.skills.MaxPassionOfRelevantSkillsFor(workType);
						var disabled = IsIncapableOfWholeWorkType(workType) || p.WorkTypeIsDisabled(workType);
						return disabled ? -1 : passion * 100 + priority;
					})
					.ToArray();
				}

				var columns = Integrations.GetPawnWorkerDefs().Select(workType => workType.labelShort).ToArray();
				var rows = AllColonistsWithCurrentTop(pawn)
					.Select(colonist => new PrioritiyInfo.Priorities()
					{
						pawn = colonist.LabelShortCap,
						yours = colonist == pawn,
						val = GetValues(colonist)
					})
					.ToArray();
				return new PrioritiyInfo()
				{
					columns = columns,
					manual = Current.Game.playSettings.useWorkPriorities,
					norm = Integrations.defaultPriority,
					max = Integrations.maxPriority + 1, // compensate for 0-index
					rows = rows
				};
			}
			SendStates(connection, "priorities", GetResult, null);
		}

		public static void SendSchedules(Connection connection)
		{
			ScheduleInfo GetResult(Pawn pawn)
			{
				string GetValues(Pawn p)
				{
					var schedules = Enumerable.Range(0, 24)
						.Select(hour => p.timetable.GetAssignment(hour) ?? TimeAssignmentDefOf.Anything)
						.ToArray();
					return schedules.Join(s => Defs.Assignments[s], "");
				}
				var rows = AllColonistsWithCurrentTop(pawn)
					.Select(colonist => new ScheduleInfo.Schedules() { pawn = colonist.LabelShortCap, yours = colonist == pawn, val = GetValues(colonist) })
					.ToArray();
				return new ScheduleInfo() { rows = rows };
			}
			SendStates(connection, "schedules", GetResult, null);
		}

		public static void SendNextSocial(Connection connection)
		{
			var puppeteer = RoundRobbin.NextColonist("update-socials");
			if (puppeteer != null)
				SendSocialRelations(connection, puppeteer.vID);
		}

		public static void SendSocialRelations(Connection connection, ViewerID vID)
		{
			var pawn = State.Instance.PuppeteerForViewer(vID)?.puppet?.pawn;
			if (pawn?.relations == null) return;

			string GetType(List<PawnRelationDef> relations, Pawn other, int ourOpinion)
			{
				var text = "";
				for (var i = 0; i < relations.Count; i++)
				{
					var def = relations[i];
					if (text == "") text += ", ";
					text = def.GetGenderSpecificLabelCap(other);
				}
				if (text != "") return text;
				if (ourOpinion < -20) { return "Rival".Translate(); }
				if (ourOpinion > 20) { return "Friend".Translate(); }
				return "Acquaintance".Translate();
			}

			bool ShouldShowPawnRelations(Pawn p) => p != pawn && (!p.RaceProps.Animal || !p.Dead || p.Corpse != null) && p.Name != null && !p.Name.Numerical && p.relations.everSeenByPlayer;
			var others = new List<Pawn>();
			if (pawn.MapHeld != null)
			{
				bool PawnSelector(Pawn p) => p != pawn && p.RaceProps.Humanlike && ShouldShowPawnRelations(p) && (p.relations.OpinionOf(pawn) != 0 || pawn.relations.OpinionOf(p) != 0);
				others.AddRange(pawn.MapHeld.mapPawns.AllPawns.Where(PawnSelector));
			}
			if (pawn.relations.RelatedPawns != null)
				others.AddRange(pawn.relations.RelatedPawns.Where(ShouldShowPawnRelations));

			int relationSorter(SocialRelations.Relation a, SocialRelations.Relation b)
			{
				bool anyA = a._relations.Any();
				bool anyB = b._relations.Any();
				if (anyA != anyB) return anyB.CompareTo(anyA);
				if (anyA && anyB)
				{
					var importanceA = a._relations.Select(r => r.importance).Max();
					var importanceB = b._relations.Select(r => r.importance).Max();
					if (importanceA != importanceB) return importanceB.CompareTo(importanceA);
				}
				if (a._ourOpinionNum != b._ourOpinionNum) return b._ourOpinionNum.CompareTo(a._ourOpinionNum);
				return a.type.CompareTo(b.type);
			}

			IEnumerable<SocialRelations.Opinion> getOpinions(List<PawnRelationDef> relations, Pawn other)
			{
				foreach (var relation in relations)
					yield return new SocialRelations.Opinion() { reason = relation.GetGenderSpecificLabelCap(other), value = relation.opinionOffset.ToStringWithSign() };
				if (pawn.RaceProps.Humanlike)
				{
					var thoughts = pawn.needs?.mood?.thoughts;
					if (thoughts != null)
					{
						var tmpSocialThoughts = new List<ISocialThought>();
						thoughts.GetDistinctSocialThoughtGroups(other, tmpSocialThoughts);
						foreach (var socialThought in tmpSocialThoughts)
						{
							var num = 1;
							var thought = (Thought)socialThought;
							if (thought != null)
							{
								if (thought.def.IsMemory) num = thoughts.memories.NumMemoriesInGroup((Thought_MemorySocial)socialThought);
								yield return new SocialRelations.Opinion() { reason = thought.LabelCapSocial + (num > 1 ? " x" + num : ""), value = thoughts.OpinionOffsetOfGroup(socialThought, other).ToStringWithSign() };
							}
						}
					}
				}
				foreach (var hediff in pawn.health.hediffSet.hediffs)
				{
					var curStage = hediff.CurStage;
					if (curStage != null && curStage.opinionOfOthersFactor != 1f)
						yield return new SocialRelations.Opinion() { reason = hediff.LabelBaseCap, value = curStage.opinionOfOthersFactor.ToStringPercent() + "%" };
				}
				if (pawn.HostileTo(other))
					yield return new SocialRelations.Opinion() { reason = "Hostile".Translate(), value = "" };
			}

			var socialRelations = others.Distinct().Select(other =>
			{
				var portrait = Renderer.GetPawnPortrait(other, new Vector2(64f, 64f), 2f);
				var otherNotHuman = other.RaceProps.Humanlike == false;
				var ourOpinion = pawn.relations.OpinionOf(other);
				var theirOpinion = other.relations.OpinionOf(pawn);
				var relations = pawn.GetRelations(other).ToList();
				relations.Sort((PawnRelationDef a, PawnRelationDef b) => b.importance.CompareTo(a.importance));
				return new SocialRelations.Relation()
				{
					_relations = relations,
					_ourOpinionNum = ourOpinion,

					type = GetType(relations, other, ourOpinion),
					pawn = other.LabelShortCap,
					portrait = portrait,
					opinions = getOpinions(relations, other).ToArray(),
					ourOpinion = otherNotHuman ? "" : ourOpinion.ToStringWithSign(),
					theirOpinion = otherNotHuman ? "" : theirOpinion.ToStringWithSign(),
					situation = SocialCardUtility.GetPawnSituationLabel(other, pawn)
				};
			}).ToList();
			socialRelations.Sort(relationSorter);
			var firstEntry = Find.PlayLog.AllEntries.FirstOrDefault(entry => entry.Concerns(pawn));
			var lastInteraction = firstEntry == null ? "" : ((TaggedString)firstEntry.ToGameStringFromPOV(pawn, false)).RawText.StripTags();
			connection.Send(new SocialRelations() { viewer = vID, info = new SocialRelations.Info() { relations = socialRelations.ToArray(), lastInteraction = lastInteraction } });
		}

		static readonly Dictionary<BodyPartGroupDef, int> partImportance = new Dictionary<BodyPartGroupDef, int>()
		{
			{ DefDatabase<BodyPartGroupDef>.GetNamed("MiddleFingers"), 0 },
			{ DefDatabase<BodyPartGroupDef>.GetNamed("LeftHand"), 1 },
			{ DefDatabase<BodyPartGroupDef>.GetNamed("RightHand"), 2 },
			{ DefDatabase<BodyPartGroupDef>.GetNamed("Eyes"), 3 },
			{ DefDatabase<BodyPartGroupDef>.GetNamed("Teeth"), 4 },
			{ DefDatabase<BodyPartGroupDef>.GetNamed("Mouth"), 5 },
			{ DefDatabase<BodyPartGroupDef>.GetNamed("Hands"), 6 },
			{ DefDatabase<BodyPartGroupDef>.GetNamed("Feet"), 7 },
			{ DefDatabase<BodyPartGroupDef>.GetNamed("Arms"), 8 },
			{ DefDatabase<BodyPartGroupDef>.GetNamed("Legs"), 9 },
			{ DefDatabase<BodyPartGroupDef>.GetNamed("Neck"), 10 },
			{ DefDatabase<BodyPartGroupDef>.GetNamed("FullHead"), 11 },
			{ DefDatabase<BodyPartGroupDef>.GetNamed("UpperHead"), 12 },
			{ DefDatabase<BodyPartGroupDef>.GetNamed("Shoulders"), 13 },
			{ DefDatabase<BodyPartGroupDef>.GetNamed("Waist"), 14 },
			{ DefDatabase<BodyPartGroupDef>.GetNamed("Torso"), 15 },
		};
		static BodyPartGroupDef MostImportantPart(Apparel apparel) => apparel.def.apparel.bodyPartGroups.OrderBy(def => partImportance.TryGetValue(def)).Last();

		public static void SendGear(Connection connection, ViewerID vID)
		{
			var pawn = State.Instance.PuppeteerForViewer(vID)?.puppet?.pawn;
			var wornApparel = pawn?.apparel?.WornApparel;
			if (wornApparel == null) return;

			string GetOverallArmor(StatDef stat)
			{
				var value = 0f;
				var statValue = Mathf.Clamp01(pawn.GetStatValue(stat, true) / 2f);
				var allParts = pawn.RaceProps.body.AllParts;
				var list = pawn.apparel?.WornApparel;
				for (var i = 0; i < allParts.Count; i++)
				{
					var val = 1f - statValue;
					if (list != null)
					{
						for (var j = 0; j < list.Count; j++)
						{
							if (list[j].def.apparel.CoversBodyPart(allParts[i]))
							{
								var num4 = Mathf.Clamp01(list[j].GetStatValue(stat, true) / 2f);
								val *= 1f - num4;
							}
						}
					}
					value += allParts[i].coverageAbs * (1f - val);
				}
				return Mathf.Clamp(value * 2f, 0f, 2f).ToStringPercent();
			}

			connection.Send(new Gear()
			{
				viewer = vID,
				info = new Gear.Info()
				{
					currentMass = MassUtility.GearAndInventoryMass(pawn).ToString("F2"),
					maxMass = MassUtility.Capacity(pawn, null).ToString("F2"),
					comfortableTemps = new[] {
						pawn.GetStatValue(StatDefOf.ComfyTemperatureMin, true).ToStringTemperature("F0"),
						pawn.GetStatValue(StatDefOf.ComfyTemperatureMax, true).ToStringTemperature("F0"),
					},
					overallArmor = new[] {
						GetOverallArmor(StatDefOf.ArmorRating_Sharp),
						GetOverallArmor(StatDefOf.ArmorRating_Blunt),
						GetOverallArmor(StatDefOf.ArmorRating_Heat)
					},

					parts = DefDatabase<BodyPartGroupDef>.AllDefs.OrderBy(def => -1000 * def.listOrder)
						.Select(bodyPartGroupDef =>
						{
							var bodyPartInfo = wornApparel.Where(apparel => MostImportantPart(apparel) == bodyPartGroupDef).ToList();
							var apparels = bodyPartInfo.Select(apparel =>
							{
								var preview = Renderer.GetThingPreview(apparel, new Vector2(96f, 96f));
								var quality = 0;
								if (apparel.TryGetQuality(out var q)) quality = 1 + (int)q;
								return new Gear.Apparel()
								{
									id = apparel.ThingID,
									name = apparel.def.LabelCap,
									tainted = apparel.WornByCorpse,
									forced = pawn.outfits?.forcedHandler.IsForced(apparel) ?? false,
									hp1 = apparel.HitPoints,
									hp2 = apparel.MaxHitPoints,
									mValue = apparel.MarketValue,
									stuff = apparel.Stuff?.LabelCap ?? "",
									mass = apparel.GetStatValue(StatDefOf.Mass).ToString("F2"),
									aSharp = apparel.GetStatValue(StatDefOf.ArmorRating_Sharp).ToStringPercent(),
									aBlunt = apparel.GetStatValue(StatDefOf.ArmorRating_Blunt).ToStringPercent(),
									aHeat = apparel.GetStatValue(StatDefOf.ArmorRating_Heat).ToStringPercent(),
									iCold = apparel.GetStatValue(StatDefOf.Insulation_Cold).ToStringTemperature(),
									iHeat = apparel.GetStatValue(StatDefOf.Insulation_Heat).ToStringTemperatureOffset(),
									quality = quality,
									preview = preview
								};
							})
							.ToArray();

							return new Gear.BodyPart() { name = bodyPartGroupDef.LabelCap, apparels = apparels };
						})
						.Where(part => part.apparels.Length > 0)
						.ToArray()
				}
			});
		}

		public static void SendInventory(Connection connection, ViewerID vID)
		{
			var pawn = State.Instance.PuppeteerForViewer(vID)?.puppet?.pawn;
			var inventory = pawn?.inventory.innerContainer;
			if (inventory == null) return;
			var equipment = pawn?.equipment?.AllEquipmentListForReading;
			if (equipment == null) return;

			Inventory.Item[] getItems(IEnumerable<Thing> owner)
			{
				return owner.Select(thing => new Inventory.Item()
				{
					id = thing.ThingID,
					name = thing.LabelCap,
					mass = (thing.stackCount * thing.GetStatValue(StatDefOf.Mass)).ToString("F2"),
					preview = Renderer.GetThingPreview(thing, new Vector2(32, 32)),
					consumable = (thing.def.IsNutritionGivingIngestible || thing.def.IsNonMedicalDrug) && thing.IngestibleNow && pawn.WillEat(thing, null)
				})
				.ToArray();
			}

			connection.Send(new Inventory()
			{
				viewer = vID,
				info = new Inventory.Info()
				{
					inventory = getItems(inventory),
					equipment = getItems(equipment)
				}
			});
		}

		public static void SendAllState(Connection connection, ViewerID vID)
		{
			var puppeteer = State.Instance.PuppeteerForViewer(vID);

			SendGameInfo(connection, vID);
			SendTimeInfo(connection, vID);
			SendCoins(connection, puppeteer);
			SendPortrait(connection, puppeteer);
			SendAreas(connection, puppeteer);
			SendPriorities(connection);
			SendSchedules(connection);
			OperationQueue.Add(OperationType.SocialRelations, () => SendSocialRelations(connection, vID));
			OperationQueue.Add(OperationType.Gear, () => SendGear(connection, vID));
			OperationQueue.Add(OperationType.Inventory, () => SendInventory(connection, vID));

			if (TwitchToolkitMod.Exists)
				SendToolkitCommands(connection, vID);
		}

		public static void SendGameInfoToAll()
		{
			if (Current.Game == null) return;
			var puppeteers = State.Instance.ConnectedPuppeteers();
			puppeteers.Do(puppeteer => SendGameInfo(Controller.instance.connection, puppeteer.vID));

			//if (TwitchToolkit.Exists)
			//	puppeteers.Do(puppeteer => SendToolkitCommands(Controller.instance.connection, puppeteer.vID));
		}

		public static void SendTimeInfoToAll()
		{
			if (Current.Game == null) return;
			var puppeteers = State.Instance.ConnectedPuppeteers();
			puppeteers.Do(puppeteer => SendTimeInfo(Controller.instance.connection, puppeteer.vID));
		}
	}
}
