using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

public sealed partial class ProceduralCity
{
    // The source city uses 60 m blocks. Preserve its assembled streets, facades and
    // rooftop connections with ONE uniform scale, rather than stretching each mesh.
    private const float SourceScale = BlockSize / 60f;
    private readonly Dictionary<string, Template> catalog = new Dictionary<string, Template>();
    private readonly List<Landmark> urbanBlocks = new List<Landmark>();
    private readonly List<Landmark> streetSets = new List<Landmark>();
    private readonly List<Template> roofCourse = new List<Template>();
    private readonly List<Template> streetKickers = new List<Template>();
    private readonly List<Template> gardenDecor = new List<Template>();
    private readonly Dictionary<string, Vector3> blockCenters = new Dictionary<string, Vector3>();
    private Vector2Int nearestShopChunk;
    public int StreetSetCount => streetSets.Count;
    public int RooftopPartCount => roofCourse.Count;
    public int NativeBlockCount => urbanBlocks.Count;

    public bool IsCroustyBlock(int x, int z) => FloorMod(x, 16) == 0 && FloorMod(z, 16) == 0;
    public bool IsParkBlock(int x, int z) => FloorMod(x, 12) >= 8 && FloorMod(x, 12) <= 9 &&
        FloorMod(z, 12) >= 8 && FloorMod(z, 12) <= 9;
    public bool IsRooftopBlock(int x, int z) => FloorMod(x, 12) >= 2 && FloorMod(x, 12) <= 5 &&
        FloorMod(z, 12) >= 2 && FloorMod(z, 12) <= 5;

    // One station per large district, with its position varying between cities.
    // The district immediately west of spawn places it in the initial 3x3 chunks.
    public bool IsPoliceBlock(int x, int z)
    {
        int regionX = Mathf.FloorToInt(x / 12f);
        int regionZ = Mathf.FloorToInt(z / 12f);
        int anchorX = regionX * 12 + 10 + (int)((uint)CoordinateSeed(regionX, regionZ, 1701) % 2);
        int anchorZ = regionZ * 12 + (int)((uint)CoordinateSeed(regionX, regionZ, 1702) % 2);
        return x == anchorX && z == anchorZ;
    }

    // Entire 128 m lots leave space for the takeoff, ring, landing and run-up.
    public string FeatureChunk(Vector2Int chunk)
    {
        int x = FloorMod(chunk.x, 6), z = FloorMod(chunk.y, 6);
        if (x == 4 && z == 4) return "Grand parc";
        if (x == 1 && z == 0) return "Megarampe et cercle de feu";
        if (x == 0 && z == 5) return "Tours aeriennes";
        return null;
    }

    public bool IsLandmarkBlock(int x, int z) => !IsCroustyBlock(x, z) && !IsRooftopBlock(x, z) &&
        FeatureChunk(new Vector2Int(Mathf.FloorToInt(x / 2f), Mathf.FloorToInt(z / 2f))) == null &&
        (IsPoliceBlock(x, z) || FloorMod(x + z * 2, 7) == 3);

    public string LandmarkNameAt(int x, int z)
    {
        string large = FeatureChunk(new Vector2Int(Mathf.FloorToInt(x / 2f), Mathf.FloorToInt(z / 2f)));
        if (large != null) return large;
        if (IsRooftopBlock(x, z)) return "Parcours des toits";
        if (IsPoliceBlock(x, z)) return "Commissariat";
        if (!IsLandmarkBlock(x, z)) return null;
        string[] names = { "Skatepark", "Place du marche", "Garage 01", "Garage 02", "Garage 03", "Rampe des toits" };
        // Rotate through the source venues across nearby streets; a hash per block
        // could pick the same garage several times in the player's first view.
        int rotation = (int)((uint)CoordinateSeed(0, 0, 991) % names.Length);
        return names[FloorMod(x + z * 3 + rotation, names.Length)];
    }

    private void EnsureNearestShop()
    {
        Vector3 p = player.position - Origin;
        int x = Mathf.RoundToInt(p.x / (BlockSize * 16)) * 16;
        int z = Mathf.RoundToInt(p.z / (BlockSize * 16)) * 16;
        // Feature/rooftop lots take precedence. Search the surrounding shop lattice.
        float distance = float.PositiveInfinity;
        for (int dx = -1; dx <= 1; dx++) for (int dz = -1; dz <= 1; dz++)
        {
            int bx = x + dx * 16, bz = z + dz * 16;
            var chunk = new Vector2Int(bx / 2, bz / 2);
            if (FeatureChunk(chunk) != null || IsRooftopBlock(bx, bz)) continue;
            float d = (new Vector3(bx * BlockSize + 32, 0, bz * BlockSize + 58) - p).sqrMagnitude;
            if (d < distance) { distance = d; nearestShopChunk = chunk; }
        }
        LoadChunk(nearestShopChunk);
    }

    private void ReadTemplates()
    {
        var native = new Dictionary<string, List<Template>>();
        var streets = new Dictionary<string, List<Template>>();
        var groups = new Dictionary<string, List<Template>>();
        foreach (var filter in cityModel.GetComponentsInChildren<MeshFilter>(true))
        {
            var renderer = filter.GetComponent<MeshRenderer>();
            if (filter.sharedMesh == null || renderer == null) continue;
            Matrix4x4 matrix = cityModel.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            Bounds b = filter.sharedMesh.bounds;
            Bounds bounds = new Bounds(matrix.MultiplyPoint3x4(b.center), Vector3.zero);
            for (int i = 0; i < 8; i++) bounds.Encapsulate(matrix.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            var part = new Template { name = filter.name, mesh = filter.sharedMesh, materials = renderer.sharedMaterials,
                matrix = matrix, bounds = bounds };
            catalog[part.name] = part;
            string n = part.name;
            var block = Regex.Match(n, @"(?:Building_|Block_)(r\d+_c\d+)");
            if (n.StartsWith("Building_")) { buildings.Add(part); AddPart(native, block.Groups[1].Value, part); }
            if (n.StartsWith("Block_") && n.EndsWith("_Sidewalk")) blockCenters[block.Groups[1].Value] = new Vector3(bounds.center.x, 0, bounds.center.z);
            if (block.Success && !n.StartsWith("Building_") && !n.EndsWith("_Sidewalk"))
            {
                if (n.Contains("Alley")) AddPart(native, block.Groups[1].Value, part);
                else AddPart(streets, block.Groups[1].Value, part);
            }
            if (Regex.IsMatch(n, @"^(StreetTree|Tree|Palm|FanPalm|FlowerTree|Cypress)_")) trees.Add(part);
            if (n.StartsWith("FlowerBed_") || n.StartsWith("Bush_")) gardenDecor.Add(part);
            if (n.Contains("__StreetKicker_")) streetKickers.Add(part);
            bool course = (n.StartsWith("Route_") && !n.StartsWith("Route_Aerienne")) || n.Contains("__Route_");
            if (course) roofCourse.Add(part);
            if (n.StartsWith("Route_Depart_") || n.StartsWith("Route_Arch_Depart")) AddPart(groups, "Rampe des toits", part);
            if (n.Contains("Stunt_MegaRamp")) AddPart(groups, "Megarampe et cercle de feu", part);
            if (n.Contains("Skatepark")) AddPart(groups, "Skatepark", part);
            if (n.Contains("Place_du_Marche")) AddPart(groups, "Place du marche", part);
            if (n.StartsWith("Police_")) AddPart(groups, "Commissariat", part);
            for (int garage = 1; garage <= 3; garage++) if (n.StartsWith("Garage_0" + garage)) AddPart(groups, "Garage 0" + garage, part);
            if (n.StartsWith("Tour_Aerienne") || n.StartsWith("Rampe_Aerienne") || n.StartsWith("Passerelle_Aerienne") || n.StartsWith("Route_Aerienne")) AddPart(groups, "Tours aeriennes", part);
            if (n.StartsWith("Park_") || n.Contains("__Park_") || n == "Pitch_Lines_Goals") AddPart(groups, "Grand parc", part);
            // Chicken brands are pickup buildings ONLY, never random storefronts.
            if (n == "CROUSTY_Shop") restaurant = part;
        }
        foreach (string brand in new[] { "CROUSTY", "CRISPYCOQ", "POULETROYAL", "WINGZ" })
            if (catalog.TryGetValue(brand + "_Shop", out var shop) && catalog.TryGetValue(brand + "_SignText", out var sign)) shop.sign = sign;
        foreach (var pair in native) if (pair.Value.Exists(p => p.name.StartsWith("Building_"))) urbanBlocks.Add(Assembly(pair.Key, pair.Value));
        foreach (var pair in streets) streetSets.Add(Assembly(pair.Key, pair.Value));
        foreach (var pair in groups) landmarks.Add(Assembly(pair.Key, pair.Value));
        buildings.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        urbanBlocks.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        streetSets.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        landmarks.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        roofCourse.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        trees.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        streetKickers.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        gardenDecor.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
    }

    private static void AddPart(Dictionary<string, List<Template>> groups, string key, Template part)
    {
        if (!groups.TryGetValue(key, out var parts)) groups.Add(key, parts = new List<Template>());
        parts.Add(part);
    }
    private static Landmark Assembly(string name, List<Template> parts)
    {
        var result = new Landmark { name = name, bounds = parts[0].bounds };
        result.parts.AddRange(parts);
        result.parts.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        foreach (var part in parts) result.bounds.Encapsulate(part.bounds);
        return result;
    }

    private void BuildBlock(Chunk chunk, int x, int z, Vector3 center)
    {
        var random = new System.Random(CoordinateSeed(x, z));
        Transform parent = new GameObject(DistrictAt(x, z) + " " + x + "," + z).transform;
        parent.SetParent(chunk.root.transform, false);
        parent.localPosition = center;
        Box("Trottoir", parent, new Vector3(0, .075f, 0), new Vector3(48, .15f, 48), pavement, RadarMapElement.Kind.Block);
        bool course = IsRooftopBlock(x, z);
        bool shop = !course && IsCroustyBlock(x, z);
        string feature = course || shop ? null : LandmarkNameAt(x, z);
        float yaw = random.Next(4) * 90;
        if (course)
        {
            if (!BuildRooftopBlock(parent, x, z)) BuildNativeBlock(parent, random, yaw, DistrictAt(x, z));
        }
        else if (shop)
        {
            // Align the source sign with the pickup street (WINGZ faces -X in the FBX).
            Template pickupBuilding = restaurant;
            if (x != 0 || z != 0)
            {
                string[] brands = { "CROUSTY", "CRISPYCOQ", "POULETROYAL", "WINGZ" };
                pickupBuilding = catalog[brands[random.Next(brands.Length)] + "_Shop"];
            }
            Vector3 front = pickupBuilding.sign.bounds.center - pickupBuilding.bounds.center;
            float storefrontYaw = -Mathf.Round(Mathf.Atan2(front.x, front.z) * Mathf.Rad2Deg / 90) * 90;
            Visual(pickupBuilding, parent, new Vector3(0, .15f, 2), Vector3.one * SourceScale, storefrontYaw, true);
            var pickup = new GameObject("Crousty " + x + "," + z);
            pickup.SetActive(false);
            pickup.transform.SetParent(parent, false);
            pickup.transform.localPosition = new Vector3(0, .15f, 26);
            var store = pickup.AddComponent<CroustyShop>();
            store.pickupRadius = 14; store.beamHeight = 45; store.beamWidth = 1.6f;
            pickup.SetActive(true);
            chunk.shops.Add(store);
            // Two real neighbouring facades, no decorative chicken restaurants.
            for (int i = 0; i < 2; i++)
            {
                var part = buildings.Find(p => p.name == (i == 0 ? "Building_r4_c1_01" : "Building_r4_c1_02"));
                if (part != null) Visual(part, parent, new Vector3(i == 0 ? -17 : 17, .15f, -5), Vector3.one * .85f, 0, true);
            }
        }
        else if (feature != null)
        {
            Landmark module = landmarks.Find(p => p.name == feature);
            if (module != null) PlaceAssembly(module, parent, Vector3.zero, yaw, Mathf.Min(SourceScale, 46 / Mathf.Max(module.bounds.size.x, module.bounds.size.z)), true);
        }
        else BuildNativeBlock(parent, random, yaw, DistrictAt(x, z));

        BuildStreetFurniture(parent, random, yaw);
        if (!course && !shop && feature == null && (x == 0 && z == 1 || random.Next(5) == 0))
            BuildStreetKicker(parent, random);
        if (!course && feature == null) AddAddresses(chunk, parent, x, z);
        AddTrafficLoop(chunk, parent, 27);
    }

    private void BuildNativeBlock(Transform parent, System.Random random, float yaw, District district)
    {
        // Weight dense source blocks; preserve each facade's orientation, height and alley.
        var pool = urbanBlocks.FindAll(p => p.parts.FindAll(t => t.name.StartsWith("Building_")).Count >= 4);
        if (district == District.Industrial && random.Next(3) == 0) pool = urbanBlocks;
        Landmark source = pool[random.Next(pool.Count)];
        Transform root = new GameObject("Ilot original " + source.name).transform;
        root.SetParent(parent, false); root.localRotation = Quaternion.Euler(0, yaw, 0);
        root.localScale = Vector3.one * SourceScale;
        Vector3 offset = blockCenters[source.name];
        foreach (var part in source.parts) AddImported(part, root, offset, part.name.StartsWith("Building_"));
    }

    private void BuildStreetFurniture(Transform parent, System.Random random, float yaw)
    {
        Landmark source = streetSets[random.Next(streetSets.Count)];
        Transform root = new GameObject("Mobilier original " + source.name).transform;
        root.SetParent(parent, false); root.localRotation = Quaternion.Euler(0, yaw, 0);
        root.localScale = Vector3.one * SourceScale;
        Vector3 offset = blockCenters[source.name];
        foreach (var part in source.parts)
        {
            // Signal heads are generated at actual junctions with live phases.
            if (part.name.StartsWith("TrafficLight_")) continue;
            // Trees at the perimeter are fine; inner terrace sets can overlap a ramp/building.
            Vector3 p = part.bounds.center - offset;
            if (Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.z)) < 18 && !part.name.EndsWith("StreetProps")) continue;
            AddImported(part, root, offset, false);
        }
    }

    private void BuildStreetKicker(Transform parent, System.Random random)
    {
        if (streetKickers.Count == 0) return;
        Template ramp = streetKickers[random.Next(streetKickers.Count)];
        // One side of the street remains clear; these small jumps are optional shortcuts.
        var root = Visual(ramp, parent, new Vector3(0, .02f, -26), Vector3.one, 90, true);
        root.name = "Saut de rue - " + ramp.name;
    }

    private void BuildFeatureChunk(Chunk chunk, Vector2Int coordinate)
    {
        string name = FeatureChunk(coordinate);
        var random = new System.Random(CoordinateSeed(coordinate.x, coordinate.y, 412));
        var parent = new GameObject(name).transform;
        parent.SetParent(chunk.root.transform, false); parent.localPosition = new Vector3(64, 0, 64);
        bool park = name == "Grand parc";
        Box(park ? "Grand parc continu" : "Esplanade cascade", parent, new Vector3(0, .075f, 0),
            new Vector3(108, .15f, 108), park ? grass : pavement, park ? RadarMapElement.Kind.Park : RadarMapElement.Kind.Block);
        Landmark source = landmarks.Find(p => p.name == name);
        if (source != null) PlaceAssembly(source, parent, Vector3.zero, random.Next(4) * 90,
            Mathf.Min(1, 106 / Mathf.Max(source.bounds.size.x, source.bounds.size.z)), true);
        // Surround the set with props while keeping the takeoff and landing clear.
        for (int i = 0; i < 4; i++)
        {
            float signX = i % 2 == 0 ? -1 : 1, signZ = i < 2 ? -1 : 1;
            if (park)
            {
                for (int t = 0; t < 4; t++) Visual(trees[random.Next(trees.Count)], parent,
                    new Vector3(signX * random.Next(30, 49), .15f, signZ * random.Next(30, 49)), Vector3.one, random.Next(4) * 90, false);
                if (gardenDecor.Count > 0) Visual(gardenDecor[random.Next(gardenDecor.Count)], parent,
                    new Vector3(signX * 43, .15f, signZ * 25), Vector3.one, i * 90, false);
            }
            else
            {
                Template building = buildings.Find(p => p.name == "Building_r4_c1_01");
                Visual(building, parent, new Vector3(signX * 44, .15f, signZ * 38), Vector3.one * .8f, i * 90, true);
            }
            Template light = catalog["Lamp_001__Block_r0_c0_StreetProps"];
            Visual(light, parent, new Vector3(signX * 51, .15f, signZ * 51), Vector3.one, i * 90, false);
        }
        AddTrafficLoop(chunk, parent, 59);
    }

    private bool BuildRooftopBlock(Transform parent, int x, int z)
    {
        int regionX = Mathf.FloorToInt(x / 12f), regionZ = Mathf.FloorToInt(z / 12f);
        var block = new Vector2Int(FloorMod(x, 12) - 2, FloorMod(z, 12) - 2);
        Quaternion rotation = Quaternion.Euler(0, (uint)CoordinateSeed(regionX, regionZ, 676) % 4 * 90, 0);
        Vector3 center = new Vector3(128, 0, 128);
        Vector3 sourceOrigin = new Vector3(-286, 0, -294);
        bool occupied = false;
        foreach (var part in roofCourse)
        {
            Vector3 position = rotation * ((part.bounds.center - sourceOrigin) * SourceScale - center) + center;
            // Source bridges can span block borders. Assign each mesh exactly once;
            // chunks have a retention ring so a crossing remains loaded beneath the bus.
            var owner = new Vector2Int(Mathf.Clamp(Mathf.FloorToInt(position.x / 64), 0, 3), Mathf.Clamp(Mathf.FloorToInt(position.z / 64), 0, 3));
            Vector3 size = part.bounds.size * SourceScale;
            Vector3 rotatedSize = rotation * size;
            float halfX = Mathf.Abs(rotatedSize.x) * .5f, halfZ = Mathf.Abs(rotatedSize.z) * .5f;
            if (position.x + halfX > block.x * 64 + 8 && position.x - halfX < block.x * 64 + 56 &&
                position.z + halfZ > block.y * 64 + 8 && position.z - halfZ < block.y * 64 + 56) occupied = true;
            if (owner != block) continue;
            Transform root = new GameObject("Parcours des toits - " + part.name).transform;
            root.SetParent(parent, false);
            root.localPosition = center - new Vector3(block.x * 64 + 32, 0, block.y * 64 + 32) - rotation * center;
            root.localRotation = rotation; root.localScale = Vector3.one * SourceScale;
            AddImported(part, root, sourceOrigin, true);
        }
        return occupied;
    }

    private void AddAddresses(Chunk chunk, Transform parent, int x, int z)
    {
        for (int side = 0; side < 2; side++)
        {
            var go = new GameObject("Adresse " + x + "," + z + "/" + side);
            go.SetActive(false); go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(side == 0 ? -11 : 11, .15f, 25);
            var point = go.AddComponent<DeliveryPoint>();
            point.deliveryRadius = 12; point.beamHeight = 80; point.beamWidth = 1.6f;
            go.SetActive(true); chunk.addresses.Add(point); deliveries.RegisterPoint(point);
        }
    }

    private void AddTrafficLoop(Chunk chunk, Transform parent, float extent)
    {
        var route = new GameObject("Circuit").AddComponent<CarPointRoute>();
        route.transform.SetParent(parent, false); route.nextRoute = route;
        // Counter-clockwise around a block keeps every car in the right-hand lane.
        foreach (var corner in new[] { new Vector3(-extent, .6f, -extent), new Vector3(-extent, .6f, extent),
                     new Vector3(extent, .6f, extent), new Vector3(extent, .6f, -extent) })
        {
            var point = new GameObject("Route").transform;
            point.SetParent(route.transform, false); point.localPosition = corner;
        }
        chunk.routes.Add(route);
    }

    private GameObject PlaceAssembly(Landmark source, Transform parent, Vector3 position, float yaw, float scale, bool solid)
    {
        Transform root = new GameObject("Ensemble original - " + source.name).transform;
        root.SetParent(parent, false); root.localPosition = position;
        root.localRotation = Quaternion.Euler(0, yaw, 0); root.localScale = Vector3.one * scale;
        // Keep the source ground datum (not the lowest decorative vertex) for ramps.
        Vector3 offset = new Vector3(source.bounds.center.x, 0, source.bounds.center.z);
        foreach (var part in source.parts) AddImported(part, root, offset, solid);
        return root.gameObject;
    }

    private GameObject Visual(Template part, Transform parent, Vector3 position, Vector3 scale, float yaw, bool solid)
    {
        Transform root = new GameObject(part.name).transform;
        root.SetParent(parent, false); root.localPosition = position;
        root.localRotation = Quaternion.Euler(0, yaw, 0); root.localScale = scale;
        Vector3 offset = new Vector3(part.bounds.center.x, part.bounds.min.y, part.bounds.center.z);
        AddImported(part, root, offset, solid);
        if (part.sign != null) AddImported(part.sign, root, offset, false);
        return root.gameObject;
    }

    private static GameObject AddImported(Template part, Transform root, Vector3 offset, bool solid)
    {
        var mesh = new GameObject("Visuel " + part.name, typeof(MeshFilter), typeof(MeshRenderer));
        mesh.transform.SetParent(root, false);
        mesh.transform.localPosition = part.matrix.GetColumn(3) - (Vector4)offset;
        mesh.transform.localRotation = part.matrix.rotation;
        mesh.transform.localScale = part.matrix.lossyScale;
        mesh.GetComponent<MeshFilter>().sharedMesh = part.mesh;
        mesh.GetComponent<MeshRenderer>().sharedMaterials = part.materials;
        if (solid && !part.name.Contains("Text") && !part.name.Contains("Sign"))
            mesh.AddComponent<MeshCollider>().sharedMesh = part.mesh;
        if (solid) mesh.AddComponent<RadarMapElement>().kind = part.name.Contains("Ramp") || part.name.Contains("Deck") ||
            part.name.Contains("Bridge") ? RadarMapElement.Kind.Road : RadarMapElement.Kind.Building;
        return mesh;
    }
}
