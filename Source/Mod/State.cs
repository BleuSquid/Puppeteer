﻿using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Puppeteer
{
	public class State
	{
		const string saveFileName = "PuppeteerState.json";

		public static State instance = Load();

		public class Puppet
		{
			[JsonProperty] internal int _id;
			internal void Init(ref int id) { _id = ++id; }
			internal void Update() { _pawn = pawn?.thingIDNumber ?? 0; _puppeteer = puppeteer?._id ?? 0; }
			internal void Restore(State state) { pawn = Tools.ColonistForThingID(_pawn); puppeteer = state.viewerToPuppeteer.Values.FirstOrDefault(v => v._id == _puppeteer); }

			[JsonIgnore] public Pawn pawn;
			[JsonProperty] private int _pawn;

			[JsonIgnore] public Puppeteer puppeteer; // optional
			[JsonProperty] private int _puppeteer;
		}

		public class Puppeteer
		{
			[JsonProperty] internal int _id;
			internal void Init(ref int id) { _id = ++id; }
			internal void Update() { _puppet = puppet?._id ?? 0; }
			internal void Restore(State state) { puppet = state.pawnToPuppet.Values.FirstOrDefault(v => v._id == _puppet); }

			public ViewerID vID;

			[JsonIgnore] public Puppet puppet; // optional
			[JsonProperty] private int _puppet;

			public bool connected;
			public DateTime lastCommandIssued;
			public string lastCommand;
			public int coinsEarned;
			public int gridSize;
		}

		// new associations are automatically create for:
		//
		//  Viewer  ---creates--->  Puppeteer  ---optionally-has--->  Puppet
		//  Pawn    ---creates--->  Puppet     ---optionally-has--->  Puppeteer
		//
		public ConcurrentDictionary<int, Puppet> pawnToPuppet = new ConcurrentDictionary<int, Puppet>(); // int == pawn.thingID
		public ConcurrentDictionary<string, Puppeteer> viewerToPuppeteer = new ConcurrentDictionary<string, Puppeteer>();

		//

		static State Load()
		{
			var data = saveFileName.ReadConfig();
			if (data == null) return new State();
			var state = JsonConvert.DeserializeObject<State>(data);
			state.viewerToPuppeteer.Values.Do(p => p.Restore(state));
			state.pawnToPuppet.Values.Do(p => p.Restore(state));
			return state;
		}

		public void Save()
		{
			var id = 0;
			viewerToPuppeteer.Values.Do(p => p.Init(ref id));
			pawnToPuppet.Values.Do(p => p.Init(ref id));
			viewerToPuppeteer.Values.Do(p => p.Update());
			pawnToPuppet.Values.Do(p => p.Update());
			var data = JsonConvert.SerializeObject(this);
			saveFileName.WriteConfig(data);
		}

		// viewers

		public Puppeteer PuppeteerForViewer(ViewerID vID)
		{
			_ = viewerToPuppeteer.TryGetValue(vID.Identifier, out var puppeteer);
			return puppeteer;
		}

		Puppeteer CreatePuppeteerForViewer(ViewerID vID)
		{
			var puppeteer = new Puppeteer()
			{
				vID = vID,
				lastCommandIssued = DateTime.Now,
				lastCommand = "Became a puppeteer"
			};
			_ = viewerToPuppeteer.TryAdd(vID.Identifier, puppeteer);
			return puppeteer;
		}

		/*public IEnumerable<ViewerID> AllViewers()
		{
			return viewerToPuppeteer.Keys;
		}

		public IEnumerable<ViewerID> ConnectedViewers()
		{
			return viewerToPuppeteer
				.Where(pair => pair.Value.connected)
				.Select(pair => pair.Key);
		}

		public IEnumerable<ViewerID> AvailableViewers()
		{
			return viewerToPuppeteer
				.Where(pair => pair.Value.puppet == null)
				.Select(pair => pair.Key);
		}*/

		public IEnumerable<Puppeteer> ConnectedPuppeteers()
		{
			return viewerToPuppeteer.Values
				.Where(puppeteer => puppeteer.connected);
		}

		public bool HasPuppet(ViewerID vID)
		{
			return PuppeteerForViewer(vID)?.puppet != null;
		}

		public void Assign(ViewerID vID, Pawn pawn)
		{
			if (pawn == null) return;
			var puppet = PuppetForPawn(pawn);
			if (puppet == null) return;
			var puppeteer = PuppeteerForViewer(vID);
			if (puppeteer == null) return;
			puppeteer.puppet = puppet;
			puppet.puppeteer = puppeteer;
		}

		public void Unassign(ViewerID vID)
		{
			var puppeteer = PuppeteerForViewer(vID);
			if (puppeteer == null) return;
			if (puppeteer.puppet != null)
				puppeteer.puppet.puppeteer = null;
			puppeteer.puppet = null;
		}

		public void SetConnected(ViewerID vID, bool connected)
		{
			var puppeteer = PuppeteerForViewer(vID) ?? CreatePuppeteerForViewer(vID);
			puppeteer.connected = connected;
			var pawn = puppeteer.puppet?.pawn;
			if (pawn != null)
				Tools.SetColonistNickname(pawn, connected ? vID.name : null);
		}

		// pawns

		public Puppet PuppetForPawn(Pawn pawn)
		{
			if (pawn == null) return null;
			_ = pawnToPuppet.TryGetValue(pawn.thingIDNumber, out var puppet);
			return puppet;
		}

		public void UpdatePawn(Pawn pawn)
		{
			if (pawn == null) return;
			var puppet = PuppetForPawn(pawn);
			if (puppet != null)
			{
				puppet.pawn = pawn;
				return;
			}
			_ = pawnToPuppet.TryAdd(pawn.thingIDNumber, new Puppet()
			{
				pawn = pawn,
				puppeteer = null
			});
		}

		public void RemovePawn(Pawn pawn)
		{
			if (pawn == null) return;
			if (pawnToPuppet.TryRemove(pawn.thingIDNumber, out var puppet))
				puppet.puppeteer.puppet = null;
		}

		public bool? IsConnected(Pawn pawn)
		{
			if (pawn == null) return null;
			var puppet = PuppetForPawn(pawn);
			var puppeteer = puppet?.puppeteer;
			if (puppeteer == null) return null;
			return puppeteer.connected;
		}

		public HashSet<Puppet> AllPuppets()
		{
			return pawnToPuppet.Values.ToHashSet();
		}

		public HashSet<Puppet> AssignedPuppets()
		{
			return viewerToPuppeteer.Values.Select(puppeteer => puppeteer.puppet).OfType<Puppet>().ToHashSet();
		}

		public IEnumerable<Puppet> AvailablePuppets()
		{
			return AllPuppets().Except(AssignedPuppets());
		}
	}
}