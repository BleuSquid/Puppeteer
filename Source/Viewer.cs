﻿using JsonFx.Json;

namespace Puppeteer
{
	public class Viewer
	{
		public ViewerID vID;
		public string name = null;
		[JsonIgnore] public bool connected = false;
		public int coins = 0;
	}
}