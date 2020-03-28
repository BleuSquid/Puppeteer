﻿using HarmonyLib;
using Newtonsoft.Json;
using Puppeteer.Core;
using System.Collections.Generic;

namespace Puppeteer
{
	public class Viewers
	{
		const string saveFileName = "PuppeteerViewers.json";

		// keys: "{Service}:{ID}" (ViewerID.Identifier)
		public Dictionary<string, Viewer> state = new Dictionary<string, Viewer>();

		public Viewers()
		{
			var data = saveFileName.ReadConfig();
			if (data != null)
				state = JsonConvert.DeserializeObject<Dictionary<string, Viewer>>(data);
		}

		public void Save()
		{
			var data = JsonConvert.SerializeObject(state);
			saveFileName.WriteConfig(data);
		}

		public void Join(Connection connection, Colonists colonists, ViewerID vID)
		{
			if (vID.IsValid)
			{
				if (state.TryGetValue(vID.Identifier, out var viewer))
				{
					viewer.connected = true;
					var info = colonists.FindEntry(viewer.vID);
					viewer.controlling = info?.thingID == null ? null : Tools.ColonistForThingID(int.Parse(info.thingID));
				}
				else
				{
					viewer = new Viewer() { vID = vID, connected = true };
					state[vID.Identifier] = viewer;
				}
				Save();
				SendEarned(connection, viewer);
				SendPortrait(connection, viewer);
			}
		}

		public void Leave(ViewerID vID)
		{
			if (state.TryGetValue(vID.Identifier, out var viewer))
			{
				viewer.connected = false;
				viewer.controlling = null;
				Save();
			}
		}

		public Viewer FindViewer(ViewerID vID)
		{
			if (vID == null) return null;
			if (state.TryGetValue(vID.Identifier, out var viewer))
				return viewer;
			return null;
		}

		public void Earn(Connection connection, int amount)
		{
			state.DoIf(viewer => viewer.Value.connected, viewer =>
			{
				viewer.Value.coins += amount;
				SendEarned(connection, viewer.Value);
			});
		}

		static void SendEarned(Connection connection, Viewer viewer)
		{
			connection.Send(new Earned() { viewer = viewer.vID, info = new Earned.Info() { amount = viewer.coins } });
		}

		public static void SendPortrait(Connection connection, Viewer viewer)
		{
			if (viewer.controlling == null)
				return;

			OperationQueue.Add(OperationType.Portrait, () =>
			{
				var portrait = Renderer.GetPawnPortrait(viewer.controlling, 128);
				connection.Send(new Portrait() { viewer = viewer.vID, info = new Portrait.Info() { image = portrait } });
			});
		}
	}
}