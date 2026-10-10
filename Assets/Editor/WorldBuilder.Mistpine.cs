using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DreamWorld.Gameplay;
using DreamWorld.Town;
using DreamWorld.World;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace DreamWorld.EditorTools
{
    // Mistpine Hollow, the foggy Twilight-parody woods west of town where the Mullens live. The art comes from
    // ArtSource/build_mistpine.py (exported by ArtSource/export_mistpine.py) plus Assets/Models/Mistpine/
    // mistpine_layout.json: every tree trunk, pack-model dressing, lamp, path and named spot (the Mullens, Stella,
    // Jakeob, the wolf bushes, the Boulder Brute). A trail leaves town past the glow trail and runs west to the
    // hollow's entrance; AreaAtmosphere fades the night sky into gray mist and drizzle along it.
    public static partial class WorldBuilder
    {
        const string MistDir = "Assets/Models/Mistpine";
        // the hollow's center in the world. Its 160 m square spans x -244..-84, z -74..86; the entrance is its east edge.
        static readonly Vector3 MistOrigin = new Vector3(-164, 0, 6);
        static readonly Vector3[] MistTrail = { new Vector3(-50.5f, 0, 18.5f), new Vector3(-62, 0, 12.5f), new Vector3(-74, 0, 7.5f), new Vector3(-88, 0, 6) };

        [Serializable] class MistDress { public string model; public float[] pos; public float yaw; public float height; public string season; public float[] tint; }
        [Serializable] class MistLayoutJson { public MistDress[] dressing; }

        // Blender layout coordinates (x, y, height) -> world. The pieces are rotated 180 degrees when they import,
        // so the root is turned back and the mapping is a plain offset.
        static Vector3 MW(float bx, float by, float bz = 0f) => new Vector3(bx + MistOrigin.x, bz, by + MistOrigin.z);
        static Vector3 MW(IList<float> v) => MW(v[0], v[1], v.Count > 2 ? v[2] : 0f);
        static float MYaw(float blenderYaw) => 90f - blenderYaw;   // Blender yaw (CCW from +x) -> Unity yaw

        // The trail out of town. Built early so later scattering keeps off it (Trail registers keep-out boxes).
        static void BuildMistpineTrail(Transform root)
        {
            Trail(root, "mistpine trail", MistTrail, 2.6f, lanternEvery: 8f);
            // the whole west side between town and the hollow is walkable (not just the trail)
            var corridor = new GameObject("PlayArea_west woods").AddComponent<PlayArea>();
            corridor.transform.SetParent(root);
            corridor.transform.position = new Vector3(-73, 0, 6);
            corridor.transform.localScale = new Vector3(30, 1, 156);
            var sign = new GameObject("Sign_mistpine").AddComponent<BuildingSign>();
            sign.transform.SetParent(root);
            sign.transform.position = new Vector3(-58, 0, 16.5f);
            sign.title = "to mistpine hollow";
            sign.message = "west: mistpine hollow. a misty forest where it's always about to rain. the glass house is down the path. the quarry is NOT safe.";
            sign.range = 2.4f;
            Place(root, "Signpost", new Vector3(-58.4f, 0, 16.9f), 250, 1f, collider: false);
        }

        static void BuildMistpine(Transform root, Random rng)
        {
            var layoutText = File.ReadAllText($"{MistDir}/mistpine_layout.json");
            var layout = MiniJson(layoutText);
            var mist = new GameObject("MistpineHollow").transform;
            mist.SetParent(root);

            // ---- the art pieces (one FBX each), shared PSX vertex-color materials
            var art = new GameObject("Art").transform;
            art.SetParent(mist);
            art.SetPositionAndRotation(MistOrigin, Quaternion.Euler(0, 180, 0));
            var solid = new HashSet<string> { "Mist_Ground", "Mist_Quarry", "Mist_MullenHouse", "Mist_Details" };
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { MistDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                SetupImport(path);
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var go = (GameObject)Object.Instantiate(src, art, false);
                go.name = src.name;
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    if (solid.Contains(src.name)) r.gameObject.AddComponent<MeshCollider>();
                }
            }

            // ---- tree trunks (the forest meshes have no colliders)
            var trunks = new GameObject("TreeTrunks").transform;
            trunks.SetParent(mist);
            foreach (var t in (List<object>)layout["trees"])
            {
                var v = ((List<object>)t).Select(Convert.ToSingle).ToList();
                var c = new GameObject("Trunk").AddComponent<CapsuleCollider>();
                c.transform.SetParent(trunks);
                c.transform.position = MW(v[0], v[1], v[2]);
                c.radius = Mathf.Max(0.25f, v[3]);
                c.height = 6f;
                c.center = new Vector3(0, 3f, 0);
            }

            // ---- pack dressing (rocks, bushes, alders, owls, the cabin, the cave...)
            var dressing = new GameObject("Dressing").transform;
            dressing.SetParent(mist);
            var dl = JsonUtility.FromJson<MistLayoutJson>(layoutText);
            foreach (var d in dl.dressing)
            {
                var tint = d.tint != null && d.tint.Length >= 3 ? new Color(d.tint[0], d.tint[1], d.tint[2]) : Color.white;
                PutNature(dressing, d.model, MW(d.pos), MYaw(d.yaw), d.height, d.season ?? "summer", tint);
            }

            // ---- lights: path lanterns, the campfire, the glass house glowing in the mist
            foreach (var l in (List<object>)layout["lamps"])
            {
                var v = ((List<object>)l).Select(Convert.ToSingle).ToList();
                AddLight(mist, MW(v), new Color(1f, 0.82f, 0.55f), 7f, 1.1f);   // mist sits at the world origin
            }
            var spots = (Dictionary<string, object>)layout["spots"];
            Vector3 S(string name) => MW(((List<object>)spots[name]).Select(Convert.ToSingle).ToList());
            AddLight(mist, S("wolfcamp") + Vector3.up * 1.2f, new Color(1f, 0.6f, 0.25f), 10f, 1.6f);
            AddLight(mist, S("house") + new Vector3(0, 2.5f, 0), new Color(1f, 0.85f, 0.6f), 16f, 1.2f);
            AddLight(mist, S("house") + new Vector3(0, 6f, 0), new Color(1f, 0.85f, 0.6f), 12f, 0.9f);

            // ---- walkable area + safe zones (the path and the Mullens' clearing) + the weather
            var area = new GameObject("PlayArea_mistpine hollow").AddComponent<PlayArea>();
            area.transform.SetParent(mist);
            area.transform.position = MistOrigin;
            area.transform.localScale = new Vector3(156, 1, 156);
            var walk = ((List<object>)layout["path"]).Select(p => ((List<object>)p).Select(Convert.ToSingle).ToList()).Select(p => MW(p[0], p[1])).ToList();
            for (int i = 0; i < walk.Count - 1; i++) SafeStrip(mist, "mistpine path", walk[i], walk[i + 1], 6f);
            SafeZone(mist, "the mullen house", S("house"), new Vector3(40, 0, 40), 0);
            var weather = new GameObject("MistpineAtmosphere").AddComponent<AreaAtmosphere>();
            weather.transform.SetParent(mist);

            var welcome = new GameObject("Sign_mistpine entrance").AddComponent<BuildingSign>();
            welcome.transform.SetParent(mist);
            welcome.transform.position = MW(76, 3.5f) + Vector3.up * 0.1f;
            welcome.title = "mistpine hollow";
            welcome.message = "welcome to mistpine hollow. population: a few. it is always cloudy here, and that's how some of us like it.";
            welcome.range = 2.6f;

            MistpineFolks(mist, S);
            MistpineMonsters(mist, rng, S);
        }

        // The Mullens (pale, gold-eyed, extremely calm), Stella and the Wolfpups.
        static void MistpineFolks(Transform mist, Func<string, Vector3> S)
        {
            var folks = new GameObject("MistpineFolk").transform;
            folks.SetParent(mist);
            const string Pale = "Skin=#EEEAF4;Eyes=Almond #C9A23A;Mouth=Smile;";
            var house = S("house");
            float Face(Vector3 from, Vector3 to) => Quaternion.LookRotation(new Vector3(to.x - from.x, 0, to.z - from.z)).eulerAngles.y;
            void Folk(string name, string look, string[] lines, Vector3? at = null)
            {
                var pos = at ?? S(name);
                var go = Npc(folks, name, pos, Face(pos, house + Vector3.right * 12), look, lines);
                go.AddComponent<GroundFollow>();
            }
            Folk("Carlily Mullen", Pale + "Brows=Soft #D8C080;Hair=Pixie #E8D08A;Top=Sweater 13;Bottom=SkinnyJeans 11 rise:High;Legwear=none;Shoes=Loafers 11;Neck=none;Headwear=none;Glasses=none;Extra1=none",
                 new[] { "welcome to our home. we're vegetarians. we mostly eat snacks.", "i've been a doctor for... a while. don't ask how long.",
                         "the fog keeps us comfortable. and hidden. mostly comfortable." });
            Folk("Esmay Mullen", Pale + "Brows=Soft #7A4A2A;Hair=Long #8A5A3A bangs:Middle length:Long;Top=Cardigan 4;Bottom=LongSkirt 10;Legwear=none;Shoes=MaryJanes 10;Neck=none;Headwear=none;Glasses=none;Extra1=none",
                 new[] { "oh, a guest! let me pretend to cook something.", "i decorated the whole house. it's mostly windows.", "stay as long as you like, sweetheart." });
            Folk("Edwerd Mullen", Pale + "Brows=Thick #6A3A20;Hair=Pixie #8A4A2A;Top=Hoodie 11;Bottom=SkinnyJeans 11 rise:Mid;Legwear=none;Shoes=Boots 11;Neck=none;Headwear=none;Glasses=none;Extra1=none",
                 new[] { "you smell... nice. that came out wrong.", "i've been seventeen for a very long time. high school is a loop.",
                         "i sparkle in the sun. it's very inconvenient. hence the meadow." });
            Folk("Alyss Mullen", Pale + "Brows=Thin #151515;Hair=Pixie #151515;Top=SlipDress 12;Bottom=none;Legwear=none;Shoes=Platforms 11;Neck=Choker 11;Headwear=none;Glasses=none;Extra1=none",
                 new[] { "i saw you coming. i see everyone coming.", "we should play baseball. there's a storm later.", "cute outfit. i knew you'd wear that." }, S("house_door") + new Vector3(1.5f, 0, -2.5f));
            Folk("Jaspurr Mullen", Pale + "Brows=Straight #B89850;Hair=Long #D9B870 bangs:Middle length:Medium;Top=Flannel 11;Bottom=Cargo 11 rise:Mid;Legwear=none;Shoes=Boots 10;Neck=none;Headwear=none;Glasses=none;Extra1=none",
                 new[] { "sorry, i'm feeling everyone's feelings right now.", "i'll keep my distance. it's a me thing, not a you thing." });
            Folk("Rosaleaf Mullen", Pale + "Brows=Arched #C8A860;Hair=Long #F0D890 bangs:Side length:Long;Top=SlipDress 0;Bottom=none;Legwear=none;Shoes=Platforms 0;Neck=Chain 2;Headwear=none;Glasses=none;Extra1=none",
                 new[] { "don't touch the convertible.", "i'm not mean, i'm just perfect.", "fine. you can stay. for a bit." });
            Folk("Emmitt Mullen", Pale + "Brows=Thick #2A1A10;Hair=Pixie #2A1A10;Top=Hoodie 8;Bottom=BaggyJeans 11 rise:Mid;Legwear=none;Shoes=Sneakers 11;Neck=none;Headwear=none;Glasses=none;Extra1=none",
                 new[] { "wanna arm wrestle? no? coward.", "the big rock guy in the quarry? i'd fight him. you go first though.", "batter up! storm's coming." });
            Folk("Stella", "Skin=1;Eyes=Doe #5A3A28;Mouth=Tiny;Brows=Soft #4A2A18;Hair=Long #5A3A28 bangs:Middle length:Long;Top=Flannel 8;Bottom=BaggyJeans 21 rise:Mid;Legwear=none;Shoes=Combat 11;Neck=none;Headwear=none;Glasses=none;Extra1=none",
                 new[] { "my truck is older than everyone here. except the mullens.", "is it weird that i like the rain?", "i'm not clumsy. the ground is uneven." });
            Folk("Jakeob", "Skin=4;Eyes=Round #3A2418;Mouth=Grin;Brows=Thick #121212;Hair=Long #121212 bangs:Middle length:Medium;Top=Flannel 0;Bottom=Shorts 10 rise:Mid;Legwear=none;Shoes=Sneakers 10;Neck=none;Headwear=none;Glasses=none;Extra1=none",
                 new[] { "hey! want a snack? we have a LOT of snacks.", "the wolfpups and the mullens don't really get along. long story. like, four books long.",
                         "it's always warm by the fire. come sit." });
            // two wolfpups (huskies) hanging around the campfire
            var camp = S("wolfcamp");
            for (int i = 0; i < 2; i++)
            {
                var pup = Animal(folks, "Husky", camp + new Vector3(i == 0 ? 3f : -2.5f, 0, i == 0 ? -2f : 3f), 0.9f, 4f, "wolfpup",
                                 i == 0 ? new[] { "awoo?", "*the wolfpup sniffs your snacks*" } : new[] { "*the wolfpup wags at you*", "grrr... (it's a friendly grrr)" });
                pup.AddComponent<GroundFollow>();
            }
        }

        // The Boulder Brute in its quarry (the Mullens questline boss) and a few foggy-woods monsters.
        static void MistpineMonsters(Transform mist, Random rng, Func<string, Vector3> S)
        {
            GameObject Variant(string baseModel, string name, Action<Monster> tweak, float scale = 1f)
            {
                var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{EnemyDir}/{baseModel}.prefab");
                if (src == null) { Debug.LogWarning("[Mistpine] missing enemy " + baseModel); return null; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                go.transform.localScale = Vector3.one * scale;
                var m = go.GetComponent<Monster>();
                tweak(m);
                go.AddComponent<GroundFollow>();
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{EnemyDir}/Mist_{name.Replace(" ", "")}.prefab");
                Object.DestroyImmediate(go);
                return prefab;
            }
            var brute = Variant("MossGolem", "Boulder Brute", m =>
            {
                m.displayName = "boulder brute";
                m.maxHealth = 420; m.damage = 26; m.xpReward = 360; m.coinReward = new Vector2Int(90, 140);
                m.moveSpeed = 1.1f; m.poise = true; m.sightRange = 12f; m.leashRange = 20f;
                m.barHeight *= 1.8f;
                m.drops = new[] { D("golem_moss", 2, 4), D("dream_shard", 2, 3), D("glow_jelly", 1, 2) };
            }, 1.8f);
            var golem = Variant("MossGolem", "Mist Golem", m => { m.displayName = "mist golem"; }, 1.15f);
            var ivy = Variant("IvySerpent", "Fern Serpent", m => { m.displayName = "fern serpent"; });
            var bat = Variant("NightBat", "Fog Bat", m => { m.displayName = "fog bat"; });

            void Spawner(GameObject prefab, string label, IEnumerable<Vector3> at, float respawn)
            {
                if (prefab == null) return;
                var s = new GameObject("Spawner_" + label).AddComponent<MonsterSpawner>();
                s.transform.SetParent(mist);
                s.prefab = prefab.GetComponent<Monster>();
                s.respawnSeconds = respawn;
                foreach (var p in at)
                {
                    var spot = new GameObject("Spot").transform;
                    spot.SetParent(s.transform);
                    spot.SetPositionAndRotation(p, Quaternion.Euler(0, rng.Next(360), 0));
                }
            }
            Spawner(brute, "boulder brute", new[] { S("boulder_brute") }, 240f);
            // wild spots: deep in the trees, away from the path, the house and the Wolfpups' camp
            var wild = new List<Vector3>();
            var safeSpots = new[] { S("house"), S("wolfcamp"), S("entrance"), S("meadow") };
            for (int tries = 0; tries < 400 && wild.Count < 10; tries++)
            {
                var p = MistOrigin + new Vector3((float)rng.NextDouble() * 140 - 70, 0, (float)rng.NextDouble() * 140 - 70);
                if (ZoneMapContainsSafe(mist, p) || safeSpots.Any(q => new Vector2(q.x - p.x, q.z - p.z).magnitude < 22f) || wild.Any(q => (q - p).magnitude < 14f)) continue;
                wild.Add(p);
            }
            Spawner(golem, "mist golems", wild.Take(3), 70f);
            Spawner(ivy, "fern serpents", wild.Skip(3).Take(4), 45f);
            Spawner(bat, "fog bats", wild.Skip(7).Take(3), 45f);
        }

        // Is p inside one of the safe Zone boxes already built under `parent`? (ZoneMap only fills in at runtime.)
        static bool ZoneMapContainsSafe(Transform parent, Vector3 p)
        {
            foreach (var z in parent.GetComponentsInChildren<Zone>())
                if (z.kind == ZoneKind.Safe && z.Contains(new Vector3(p.x, z.transform.position.y, p.z))) return true;
            return false;
        }

        // ---------------------------------------------------------------- tiny JSON reader (lists, dicts, numbers, strings)

        static Dictionary<string, object> MiniJson(string text)
        {
            int i = 0;
            object Value()
            {
                Ws();
                char c = text[i];
                if (c == '{')
                {
                    var d = new Dictionary<string, object>();
                    i++;
                    Ws();
                    if (text[i] == '}') { i++; return d; }
                    while (true)
                    {
                        Ws();
                        var k = (string)Value();
                        Ws(); i++;   // :
                        d[k] = Value();
                        Ws();
                        if (text[i++] == '}') return d;
                    }
                }
                if (c == '[')
                {
                    var l = new List<object>();
                    i++;
                    Ws();
                    if (text[i] == ']') { i++; return l; }
                    while (true)
                    {
                        l.Add(Value());
                        Ws();
                        if (text[i++] == ']') return l;
                    }
                }
                if (c == '"')
                {
                    int s = ++i;
                    while (text[i] != '"') i += text[i] == '\\' ? 2 : 1;
                    return text.Substring(s, i++ - s);
                }
                int st = i;
                while (i < text.Length && "-+.eE0123456789truefalsn".IndexOf(text[i]) >= 0) i++;
                var tok = text.Substring(st, i - st);
                if (tok == "true") return true;
                if (tok == "false") return false;
                if (tok == "null") return null;
                return double.Parse(tok, System.Globalization.CultureInfo.InvariantCulture);
            }
            void Ws() { while (i < text.Length && char.IsWhiteSpace(text[i])) i++; }
            return (Dictionary<string, object>)Value();
        }
    }
}
