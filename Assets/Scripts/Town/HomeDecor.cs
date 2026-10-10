using System.Collections.Generic;
using System.Linq;
using DreamWorld.Account;
using DreamWorld.Gameplay;
using DreamWorld.World;
using UnityEngine;
using UnityEngine.UI;

namespace DreamWorld.Town
{
    // Decorating your home. The furniture you've placed (progress.decor) is spawned onto your plot, and pressing B
    // inside your home zone starts decorating: aim with the camera, Q/E pick which decor item from your bag,
    // R turns it, click puts it down, X picks up the piece you're looking at, B or Esc stops.
    // Pieces can go on the floor, the yard or on top of other furniture (a lamp on a table).
    [RequireComponent(typeof(HomeBase))]
    public class HomeDecor : MonoBehaviour
    {
        public static HomeDecor Instance { get; private set; }
        public static bool Active => Instance != null && Instance.active;
        // decorating, or stopped this very frame (so the B/Esc that ended it isn't read again by the HUD)
        public static bool Busy => Active || (Instance != null && Instance.stoppedFrame == Time.frameCount);

        const float MaxReach = 9f;
        HomeBase home;
        Transform placed;            // parent of the spawned pieces, under the home contents
        PlayerProgress spawnedFor;
        bool active;
        int stoppedFrame = -1;
        int pick;                    // index into the decor items in your bag
        float yaw;
        DecorPiece ghost;
        string ghostItem;
        bool ghostOk;
        Vector3 ghostLocal;
        Text hint;
        GameObject hintRoot;

        void Awake()
        {
            Instance = this;
            home = GetComponent<HomeBase>();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        static PlayerStats P => PlayerStats.Local;

        // ---------------------------------------------------------------- the placed pieces

        void Update()
        {
            var p = P;
            if (p == null || home.contents == null) return;
            if (placed == null)
            {
                placed = new GameObject("PlacedDecor").transform;
                placed.SetParent(home.contents.transform, false);
            }
            if (spawnedFor != p.progress) Respawn(p.progress);

            if (active) Decorating(p);
            else if (DreamWorld.Gameplay.Keys.Down(KeyCode.B) && CanStart(p)) Begin(p);
        }

        void Respawn(PlayerProgress progress)
        {
            spawnedFor = progress;
            for (int i = placed.childCount - 1; i >= 0; i--) Destroy(placed.GetChild(i).gameObject);
            for (int i = 0; i < progress.decor.Count; i++) Spawn(progress.decor[i], i);
        }

        static GameObject Prefab(string item)
        {
            var def = Items.Get(item);
            string recipe = def != null && def.icon != null ? def.icon : item;   // crafted decor shares its recipe's model (and icon)
            return Resources.Load<GameObject>("Decor/" + recipe);
        }

        DecorPiece Make(string item, Transform parent)
        {
            var prefab = Prefab(item);
            if (prefab == null) { Debug.LogWarning("[decor] no model for " + item); return null; }
            var piece = Instantiate(prefab, parent).GetComponent<DecorPiece>();
            var def = Items.Get(item);
            if (def != null && def.tint != Color.white) piece.SetWood(def.tint);
            return piece;
        }

        void Spawn(PlacedDecor d, int index)
        {
            var piece = Make(d.item, placed);
            if (piece == null) return;
            piece.transform.localPosition = new Vector3(d.x, d.y, d.z);
            piece.transform.localRotation = Quaternion.Euler(0, d.yaw, 0);
            piece.index = index;
            piece.name = d.item;
        }

        // ---------------------------------------------------------------- decorating

        // For buttons and tests: the same as pressing B, clicking and pressing X.
        public bool StartDecorating() { var p = P; if (p == null || active || !CanStart(p)) return false; Begin(p); return true; }
        public void StopDecorating() { if (active) Stop(); }
        public bool PlaceAimed()
        {
            var p = P;
            if (!active || p == null || ghost == null || !ghost.gameObject.activeSelf || !ghostOk) return false;
            Place(p, ghostItem);
            return true;
        }
        public bool PickUpAimed()
        {
            var p = P;
            if (!active || p == null) return false;
            Aim(p, out var target);
            if (target == null) return false;
            PickUp(p, target);
            return true;
        }


        static Zone HomeZone => HomeBase.Instance != null && HomeBase.Instance.contents != null
            ? HomeBase.Instance.contents.GetComponentInChildren<Zone>() : null;

        bool CanStart(PlayerStats p)
        {
            if (p.Dead || !home.contents.activeInHierarchy) return false;
            if ((GameHUD.Instance != null && GameHUD.Instance.MenuOpen) || InventoryUI.IsOpen || Quests.DialogueUI.IsOpen || Gathering.CraftingUI.IsOpen) return false;
            var zone = HomeZone;
            return zone != null && zone.Contains(p.transform.position);
        }

        List<string> DecorInBag(PlayerStats p) =>
            p.Bag.Where(s => !s.Empty && Items.Get(s.id)?.kind == ItemKind.Decor).Select(s => s.id).Distinct().ToList();

        void Begin(PlayerStats p)
        {
            active = true;
            pick = 0;
            BuildHint();
            hintRoot.SetActive(true);
            if (DecorInBag(p).Count == 0)
                GameHUD.Instance?.Toast("no decor in your bag. craft some at a workbench! (you can still pick things up with X)");
        }

        void Stop()
        {
            active = false;
            stoppedFrame = Time.frameCount;
            if (ghost != null) Destroy(ghost.gameObject);
            ghost = null;
            ghostItem = null;
            if (hintRoot != null) hintRoot.SetActive(false);
        }

        void Decorating(PlayerStats p)
        {
            var zone = HomeZone;
            if (p.Dead || zone == null || !zone.Contains(p.transform.position) || InventoryUI.IsOpen
                || (GameHUD.Instance != null && GameHUD.Instance.MenuOpen))
            {
                Stop();
                return;
            }
            if (DreamWorld.Gameplay.Keys.Down(KeyCode.B) || DreamWorld.Gameplay.Keys.Down(KeyCode.Escape)) { Stop(); return; }

            var items = DecorInBag(p);
            if (items.Count > 0)
            {
                if (DreamWorld.Gameplay.Keys.Down(KeyCode.E)) pick++;
                if (DreamWorld.Gameplay.Keys.Down(KeyCode.Q)) pick--;
                pick = ((pick % items.Count) + items.Count) % items.Count;
            }
            if (DreamWorld.Gameplay.Keys.Down(KeyCode.R)) yaw = (yaw + 45f) % 360f;
            string item = items.Count > 0 ? items[pick] : null;

            // the ghost follows what you're looking at
            if (item != ghostItem)
            {
                if (ghost != null) Destroy(ghost.gameObject);
                ghost = item != null ? Make(item, null) : null;
                if (ghost != null)
                {
                    ghost.name = "DecorGhost";
                    foreach (var c in ghost.GetComponentsInChildren<Collider>()) c.enabled = false;
                    foreach (var l in ghost.GetComponentsInChildren<Light>()) l.enabled = false;
                }
                ghostItem = item;
            }
            var hit = Aim(p, out var target);
            if (ghost != null)
            {
                ghost.gameObject.SetActive(hit.HasValue);
                if (hit.HasValue)
                {
                    ghost.transform.SetPositionAndRotation(hit.Value.point, home.contents.transform.rotation * Quaternion.Euler(0, yaw, 0));
                    ghostOk = Fits(ghost, hit.Value, zone);
                    ghost.SetGhost(ghostOk);
                    ghostLocal = home.contents.transform.InverseTransformPoint(hit.Value.point);
                }
            }

            if (Input.GetMouseButtonDown(0) && ghost != null && ghost.gameObject.activeSelf)
            {
                if (!ghostOk) GameHUD.Instance?.Toast("it doesn't fit there");
                else Place(p, item);
            }
            if (DreamWorld.Gameplay.Keys.Down(KeyCode.X))
            {
                if (target != null) PickUp(p, target);
                else GameHUD.Instance?.Toast("look at a piece of your furniture to pick it up");
            }

            var def = item != null ? Items.Get(item) : null;
            hint.text = (def != null ? $"decorating: {def.name} (x{Inventory.Count(p.Bag, item)})" : "decorating: no decor in your bag")
                      + "\nQ/E: switch   R: turn   click: place   X: pick up   B: done";
        }

        // What the camera's looking at, within reach: the surface hit (floor, yard, a table top) and the placed
        // piece it belongs to (if any).
        RaycastHit? Aim(PlayerStats p, out DecorPiece target)
        {
            target = null;
            var cam = Camera.main;
            if (cam == null) return null;
            var ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            foreach (var h in Physics.RaycastAll(ray, 40f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
            {
                if (h.collider.transform.IsChildOf(p.transform)) continue;
                if (Vector3.Distance(h.point, p.transform.position) > MaxReach) return null;
                target = h.collider.GetComponentInParent<DecorPiece>();
                if (target != null && !target.transform.IsChildOf(placed)) target = null;
                return h;
            }
            return null;
        }

        // On a flat-ish surface, inside the home zone, and not poking into walls or other furniture.
        bool Fits(DecorPiece piece, RaycastHit surface, Zone zone)
        {
            if (surface.normal.y < 0.7f) return false;
            var box = piece.GetComponentInChildren<BoxCollider>(true);
            if (box == null) return zone.Contains(surface.point);
            var t = box.transform;
            Vector3 c = t.TransformPoint(box.center);
            Vector3 half = Vector3.Scale(box.size, t.lossyScale) * 0.5f * 0.9f;
            if (!zone.Contains(c)) return false;
            // lift the test box a little so it doesn't touch the surface it's sitting on
            c += Vector3.up * 0.06f;
            half.y = Mathf.Max(0.01f, half.y - 0.06f);
            foreach (var col in Physics.OverlapBox(c, half, t.rotation, ~0, QueryTriggerInteraction.Ignore))
            {
                if (col == surface.collider || col.transform.IsChildOf(P.transform)) continue;
                return false;
            }
            return true;
        }

        void Place(PlayerStats p, string item)
        {
            if (!Inventory.Remove(p.Bag, item, 1)) return;
            var d = new PlacedDecor { item = item, x = ghostLocal.x, y = ghostLocal.y, z = ghostLocal.z, yaw = yaw };
            p.progress.decor.Add(d);
            Spawn(d, p.progress.decor.Count - 1);
            Physics.SyncTransforms();
            Saved(p);
            DreamWorld.Audio.AudioManager.Play("item");
        }

        void PickUp(PlayerStats p, DecorPiece piece)
        {
            int i = piece.index;
            if (i < 0 || i >= p.progress.decor.Count) return;
            var d = p.progress.decor[i];
            if (p.AddItem(d.item, 1) == 0) { GameHUD.Instance?.Toast("your bag is full"); return; }
            p.progress.decor.RemoveAt(i);
            Respawn(p.progress);
            Saved(p);
            GameHUD.Instance?.Toast($"picked up {Items.Get(d.item)?.name ?? d.item}");
        }

        static void Saved(PlayerStats p)
        {
            var life = p.GetComponent<PlayerLife>();
            if (life != null) life.Save();
        }

        void BuildHint()
        {
            if (hintRoot != null) return;
            var hud = GameHUD.Instance;
            var ui = new TerminalUI(hud != null ? hud.titleFont : null, hud != null ? hud.bodyFont : null);
            var canvas = ui.Canvas("DecorHint", 6);
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(canvas, false);
            fill.GetComponent<Image>().color = TerminalUI.Panel;
            TerminalUI.Place((RectTransform)fill.transform, new Vector2(0.5f, 0), new Vector2(0, 250), new Vector2(980, 110));
            var box = ui.PanelBox(canvas, "DecorHintBox");
            TerminalUI.Place(box, new Vector2(0.5f, 0), new Vector2(0, 250), new Vector2(980, 110));
            hint = ui.Label(box, "", 28, TerminalUI.Text);
            TerminalUI.Stretch(hint.rectTransform, 20);
            hintRoot = canvas.gameObject;
            // the crosshair you aim with
            var dot = ui.Label(canvas, "+", 40, TerminalUI.Accent);
            TerminalUI.Place(dot.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60, 60));
        }
    }
}
