using System.Collections.Generic;
using UnityEngine;
using Zeldexile.Domain.Regions;

namespace Zeldexile.World.Region
{
    /// <summary>
    /// Region descriptions are JSON files in Resources/World/Regions. Adding a region is adding a file:
    /// the atlas registers every one by its sectors, so nothing lists regions by hand.
    /// </summary>
    public static class RegionLibrary
    {
        public const string Folder = "World/Regions";

        public static RegionData Load(string id)
        {
            var asset = Resources.Load<TextAsset>(Folder + "/" + id);
            return asset == null ? null : Parse(asset.text);
        }

        public static RegionData Parse(string json)
        {
            var data = JsonUtility.FromJson<RegionData>(json);
            if (data == null) return null;
            data.hills ??= new HillData[0];
            data.plateaus ??= new PlateauData[0];
            data.streams ??= new StreamData[0];
            data.paths ??= new PathData[0];
            data.bridges ??= new BridgeData[0];
            data.areas ??= new AreaData[0];
            data.pois ??= new PoiData[0];
            data.connections ??= new ConnectionData[0];
            data.shots ??= new ShotData[0];
            return data;
        }

        /// <summary>Every region in the project, registered on a grid.</summary>
        public static WorldAtlas LoadAtlas(WorldGrid grid)
        {
            var atlas = new WorldAtlas(grid);
            var all = new List<RegionData>();
            foreach (var asset in Resources.LoadAll<TextAsset>(Folder))
            {
                var data = Parse(asset.text);
                if (data != null) all.Add(data);
            }

            // Stable order, whatever order the files load in.
            all.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            foreach (var r in all) atlas.Add(r);
            return atlas;
        }
    }
}
