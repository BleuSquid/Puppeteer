﻿using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;

namespace Puppeteer
{
	[StaticConstructorOnStartup]
	public static class Assets
	{
		public static readonly Texture2D new27 = LoadTexture("New");
		public static readonly Texture2D puppet = LoadTexture("Puppet");
		public static readonly Texture2D bubble = LoadTexture("Bubble");
		public static readonly Texture2D colonist = LoadTexture("Colonist");
		public static readonly Texture2D[] connected = LoadTextures("Connected0", "Connected1", "Connected2");
		public static readonly Texture2D[] status = LoadTextures("Status0", "Status1");
		public static readonly Texture2D[] numbers = LoadTextureRow("Numbers", new[] { 10, 7, 10, 9, 10, 10, 10, 9, 10, 10, 23 });
		public static readonly Texture2D DeleteX = ContentFinder<Texture2D>.Get("UI/Buttons/Delete");
		public static readonly Texture2D ShowOffLimits = ContentFinder<Texture2D>.Get("UX/ShowOffLimits", true);
		public static readonly Texture2D OffLimitsAdd = ContentFinder<Texture2D>.Get("UX/OffLimitsAdd", true);
		public static readonly Texture2D OffLimitsSub = ContentFinder<Texture2D>.Get("UX/OffLimitsSub", true);
		public static readonly Texture2D[] AreaEdit = LoadTextures("UX/AreaSub", "UX/AreaAdd");
		public static readonly Texture2D tagBackground = SolidColorMaterials.NewSolidColorTexture(new Color(1, 1, 1, 0.1f));

		static Material _stripesMaterial = null;
		public static Material StripesMaterial
		{
			get
			{
				if (_stripesMaterial == null)
				{
					var path = Path.Combine(Tools.GetModRootDirectory(), "Resources", "offlimits");
					var assets = AssetBundle.LoadFromFile(path).LoadAllAssets<Material>();
					_stripesMaterial = assets[0];
				}
				return _stripesMaterial;
			}
		}

		static Shader _stripesShader = null;
		public static Shader StripesShader
		{
			get
			{
				if (_stripesShader == null)
				{
					var path = Path.Combine(Tools.GetModRootDirectory(), "Resources", "offlimits");
					var assets = AssetBundle.LoadFromFile(path).LoadAllAssets<Shader>();
					_stripesShader = assets[0];
				}
				return _stripesShader;
			}
		}

		static Texture2D LoadTexture(string path, bool makeReadonly = true)
		{
			var fullPath = Path.Combine(Tools.GetModRootDirectory(), "Textures", $"{path}.png");
			var data = File.ReadAllBytes(fullPath);
			if (data == null || data.Length == 0) throw new Exception($"Cannot read texture {fullPath}");
			var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
			if (tex.LoadImage(data) == false) throw new Exception($"Cannot create texture {fullPath}");
			tex.Compress(true);
			tex.wrapMode = TextureWrapMode.Clamp;
			tex.filterMode = FilterMode.Trilinear;
			tex.Apply(true, makeReadonly);
			return tex;
		}

		static Texture2D[] LoadTextures(params string[] paths)
		{
			// ContentFinder<Texture2D>.Get(path, true)
			return paths.Select(path => LoadTexture(path)).ToArray();
		}

		static Texture2D[] LoadTextureRow(string path, int[] offsets)
		{
			var original = LoadTexture(path, false);
			var x = 0;
			var height = original.height;
			return offsets.Select(width =>
			{
				var tex = new Texture2D(width, height, TextureFormat.ARGB32, false);
				var pixels = original.GetPixels(x, 0, width, height);
				tex.SetPixels(0, 0, width, height, pixels);
				tex.Apply();
				tex.Compress(true);
				tex.wrapMode = TextureWrapMode.Clamp;
				tex.filterMode = FilterMode.Trilinear;
				tex.Apply(true, true);
				x += width;
				return tex;
			})
			.ToArray();
		}
	}
}