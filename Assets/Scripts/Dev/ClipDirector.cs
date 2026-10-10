using System.Collections;
using DreamWorld.Account;
using DreamWorld.CharacterCreation;
using DreamWorld.Gameplay;
using DreamWorld.World;
using UnityEngine;

namespace DreamWorld.Dev
{
    // Plays short scripted scenes for recording promo clips (dreamWorld > Record Clip in the editor
    // drives this together with Unity Recorder). Each scene runs in game time, so a recording at a
    // capped frame rate comes out smooth no matter how slow the encoder is.
    //   creator: make-you screen cycling hair, outfits, patterns and eyes while she turns
    //   walk:    walking up the path into the town square
    //   fight:   lock on to a shroomling, roll through its hop, combo + heavy to finish it, level up
    //   death:   low on health in the wilds, "YOU WOKE UP", waking at home
    //   town:    slow orbit around her in the town square with the menus hidden
    //   hair:    every hairstyle in a row (3/4 view, menus hidden)
    //   eyes:    every eye style and a few colors, up close (menus hidden)
    //   combo:   sword + shield: the 3-hit combo, a heavy slam, then raising the shield (side view)
    // TikTok round 2 (make you + the town):
    //   bodies:   every body type and a few heights, full body, slowly turning
    //   hairs:    the nicest hairstyles in a row, in different colors
    //   outfits:  tops with patterns, bottoms and shoes, full body
    //   details:  eyes up close, then freckles, blush and sparkles
    //   orion:    walking into the square; Orion scuttles over and says hi (with the menu)
    //   snackula: Count Snackula's gossip about the vampire family in the woods
    //   craft:    chopping a tree, then making a stool at the workbench
    //   decor:    placing furniture at your home
    //   mistpine: a slow drift past the Mullens' glass house in the fog
    public class ClipDirector : MonoBehaviour
    {
        public static bool Running { get; private set; }

        // Statics survive leaving Play mode when domain reload is off: start every session clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Running = false;

        public static void Play(string scene)
        {
            Running = true;
            var go = new GameObject("ClipDirector");
            go.AddComponent<ClipDirector>().StartCoroutine(go.GetComponent<ClipDirector>().Run(scene));
        }

        IEnumerator Run(string scene)
        {
            Running = true;
            switch (scene)
            {
                case "creator": yield return Creator(); break;
                case "walk": yield return Walk(); break;
                case "fight": yield return Fight(); break;
                case "death": yield return Death(); break;
                case "town": yield return Town(); break;
                case "hair": yield return Hair(); break;
                case "eyes": yield return Eyes(); break;
                case "combo": yield return Combo(); break;
                case "bodies": yield return Bodies(); break;
                case "fitcheck": yield return FitCheck(); break;
                case "hairs": yield return Hairs(); break;
                case "outfits": yield return Outfits(); break;
                case "details": yield return Details(); break;
                case "orion": yield return OrionScene(); break;
                case "snackula": yield return Snackula(); break;
                case "craft": yield return Craft(); break;
                case "decor": yield return Decor(); break;
                case "mistpine": yield return Mistpine(); break;
                default: Debug.LogWarning("[ClipDirector] unknown scene " + scene); break;
            }
            Running = false;
            keepCanvases = null;
            shotAngle = float.NaN;
            nearby = null;
            if (fill != null) Destroy(fill.gameObject);
            if (Cam != null) Cam.enabled = true;
            var tpc = FindAnyObjectByType<ThirdPersonCamera>();
            if (tpc != null) tpc.height = 1.35f;
            Destroy(gameObject);
        }

        static WaitForSeconds Wait(float s) => new WaitForSeconds(s);

        // ---------- creator ----------

        IEnumerator Creator()
        {
            var app = FindAnyObjectByType<CharacterAppearance>();
            var flow = FindAnyObjectByType<AccountFlow>();
            if (flow != null && !AccountService.LoggedIn) AccountService.PlayOffline();
            if (flow != null) flow.ShowSelect();
            yield return null;
            // open the creator: a new player if there's room, otherwise edit (nothing is saved without READY)
            UnityEngine.UI.Button make = null, edit = null;
            foreach (var b in FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None))
            {
                var t = b.GetComponentInChildren<UnityEngine.UI.Text>();
                if (t == null) continue;
                if (t.text == "make a new player") make = b;
                if (t.text == "edit") edit = b;
            }
            if (make != null) make.onClick.Invoke();
            else if (edit != null) edit.onClick.Invoke();
            var zoom = FindAnyObjectByType<CreatorCameraZoom>();
            var turntable = FindAnyObjectByType<CharacterTurntable>();
            yield return Wait(0.6f);
            if (zoom != null) zoom.Zoom(0.3f);
            // hair: a few styles and bangs
            for (int i = 0; i < 6; i++)
            {
                app.ChangeStyle(OutfitCategory.Hair, 1);
                if (i == 3) app.SetColor(OutfitCategory.Hair, 5);
                yield return Wait(0.55f);
            }
            if (turntable != null) turntable.Turn(1);
            // tops with patterns
            var top = app.GetSlot(OutfitCategory.Top);
            string[] tops = { "LongSleeve", "Tank", "Hoodie", "Oversized", "Cardigan" };
            for (int i = 0; i < tops.Length; i++)
            {
                top.SelectBase(tops[i]);
                if (top.axes.Count > 0) top.axes[0].index = (i * 2 + 1) % top.axes[0].values.Length;
                top.colorIndex = 12 + i;
                app.ApplyAll();
                yield return Wait(0.6f);
            }
            if (turntable != null) turntable.Turn(-1);
            // up close: eyes and extras
            if (zoom != null) zoom.Zoom(0.7f);
            yield return Wait(0.5f);
            for (int i = 0; i < 4; i++)
            {
                app.ChangeStyle(OutfitCategory.Eyes, 1);
                yield return Wait(0.5f);
            }
            app.ChangeStyle(OutfitCategory.Extra2, 4);   // freckles
            yield return Wait(0.6f);
            app.ChangeStyle(OutfitCategory.Extra3, 6);   // sparkles
            yield return Wait(1.2f);
        }

        // Logs in offline if needed and opens make-you (a new player, or editing the first one; never saved).
        IEnumerator OpenCreator()
        {
            var flow = FindAnyObjectByType<AccountFlow>();
            if (flow != null) AccountService.PlayOffline();   // the offline test players, never the real account
            if (flow != null) flow.ShowSelect();
            yield return null;
            UnityEngine.UI.Button make = null, edit = null;
            foreach (var b in FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None))
            {
                var t = b.GetComponentInChildren<UnityEngine.UI.Text>();
                if (t == null) continue;
                if (t.text == "make a new player") make = b;
                if (t.text == "edit") edit = b;
            }
            if (make != null) make.onClick.Invoke();
            else if (edit != null) edit.onClick.Invoke();
            yield return null;
        }

        IEnumerator Hair()
        {
            yield return OpenCreator();
            PhotoMode.Set(true);
            var app = FindAnyObjectByType<CharacterAppearance>();
            var zoom = FindAnyObjectByType<CreatorCameraZoom>();
            var turntable = FindAnyObjectByType<CharacterTurntable>();
            if (zoom != null) { zoom.Zoom(-1f); zoom.Zoom(0.72f); }   // head and shoulders
            if (turntable != null) turntable.FaceFront(28f);         // slightly turned shows the hair shape
            var hair = app.GetSlot(OutfitCategory.Hair);
            int[] colors = { 5, 4, 12, 0, 7, 6, 13, 10, 2 };
            yield return Wait(0.7f);
            var bases = hair.BaseNames;
            for (int i = 0; i < bases.Count; i++)
            {
                hair.SelectBase(bases[i]);
                hair.useCustom = false;
                hair.colorIndex = colors[i % colors.Length];
                app.ApplyAll();
                yield return Wait(0.85f);
            }
            PhotoMode.Set(false);
        }

        IEnumerator Eyes()
        {
            yield return OpenCreator();
            PhotoMode.Set(true);
            var app = FindAnyObjectByType<CharacterAppearance>();
            var zoom = FindAnyObjectByType<CreatorCameraZoom>();
            var blink = FindAnyObjectByType<EyeBlink>();
            if (blink != null) blink.secondsBetweenBlinks = new Vector2(60f, 60f);
            var hair = app.GetSlot(OutfitCategory.Hair);
            hair.SelectBase("Long");
            if (hair.axes.Count > 0) hair.axes[0].index = 5;   // micro bangs: the whole face shows
            app.ApplyAll();
            var turntable = FindAnyObjectByType<CharacterTurntable>();
            if (turntable != null) turntable.FaceFront();
            if (zoom != null) zoom.Zoom(1f);
            yield return Wait(0.9f);
            var eyes = app.GetSlot(OutfitCategory.Eyes);
            for (int i = 0; i < eyes.StyleCount; i++)
            {
                eyes.styleIndex = i;
                eyes.useCustom = false;
                eyes.colorIndex = i % eyes.colors.Length;
                app.ApplyAll();
                yield return Wait(0.5f);
            }
            if (blink != null) blink.secondsBetweenBlinks = new Vector2(2.2f, 5.5f);
            PhotoMode.Set(false);
        }

        // ---------- world ----------

        static PlayerController Player => PlayerStats.Local != null ? PlayerStats.Local.GetComponent<PlayerController>() : null;
        static ThirdPersonCamera Cam => FindAnyObjectByType<ThirdPersonCamera>();

        IEnumerator Walk()
        {
            var pc = Player;
            keepCanvases = new string[0];   // just the town
            pc.Warp(new Vector3(0, 0.05f, -12.5f), Quaternion.identity);
            Cam.yaw = 0f;
            Cam.pitch = 12f;
            Cam.SnapNext();
            yield return Wait(0.8f);
            pc.UseScriptedInput = true;
            for (float t = 0; t < 6.5f; t += Time.deltaTime)
            {
                pc.ScriptedInput = new Vector2(Mathf.Sin(t * 0.6f) * 0.25f, 1f);
                Cam.yaw = Mathf.Sin(t * 0.3f) * 12f;
                yield return null;
            }
            pc.ScriptedInput = Vector2.zero;
            yield return Wait(1.5f);
            pc.UseScriptedInput = false;
        }

        IEnumerator Fight()
        {
            var pc = Player;
            var stats = PlayerStats.Local;
            stats.FillBars();
            stats.progress.xp = Mathf.Max(0, stats.XpToNext - 10);   // this kill levels her up
            // pick a shroomling out in the wilds and stand a few steps from it
            Monster target = null;
            float best = float.MaxValue;
            foreach (var m in FindObjectsByType<Monster>(FindObjectsSortMode.None))
            {
                float d = Vector3.Distance(m.transform.position, new Vector3(4, 0, -18));
                if (m.Alive && d < best) { best = d; target = m; }
            }
            Vector3 side = Vector3.Cross(Vector3.up, (target.transform.position - new Vector3(0, 0, -14)).normalized);
            Vector3 stand = target.transform.position + (target.transform.position - new Vector3(0, 0, -14)).normalized * -3.2f + side * 0.5f;
            stand.y = 0.05f;
            if (ZoneMap.IsSafe(stand)) stand += side * 3f;
            pc.Warp(stand, Quaternion.LookRotation(Flat(target.transform.position - stand)));
            pc.UseScriptedInput = true;
            // a side-on camera so both of them are in view (a lock-on camera would hide it behind her)
            var cam = Cam;
            cam.pitch = 16f;
            cam.distance = 5f;
            System.Action frame = () =>
            {
                cam.yaw = Mathf.LerpAngle(cam.yaw, Quaternion.LookRotation(Flat(target.transform.position - pc.transform.position)).eulerAngles.y + 62f, 0.15f);
            };
            cam.yaw = Quaternion.LookRotation(Flat(target.transform.position - stand)).eulerAngles.y + 62f;
            cam.SnapNext();
            // wait for its hop wind-up, then roll sideways through it
            for (float t = 0; t < 6f && !target.WindingUp; t += Time.deltaTime) { frame(); yield return null; }
            for (float t = 0; t < 0.3f; t += Time.deltaTime) { frame(); yield return null; }
            pc.ScriptedInput = new Vector2(1f, 0.2f);
            pc.Roll();
            for (float t = 0; t < 0.15f; t += Time.deltaTime) { frame(); yield return null; }
            pc.ScriptedInput = Vector2.zero;
            for (float t = 0; t < 0.75f; t += Time.deltaTime) { frame(); yield return null; }
            // close in, then light combo + heavy until it pops
            for (float t = 0; t < 1.5f && Vector3.Distance(target.transform.position, pc.transform.position) > 1.3f; t += Time.deltaTime)
            {
                pc.ScriptedInput = new Vector2(0, 1);
                pc.transform.rotation = Quaternion.LookRotation(Flat(target.transform.position - pc.transform.position));
                frame();
                yield return null;
            }
            pc.ScriptedInput = Vector2.zero;
            for (int i = 0; i < 6 && target.Alive; i++)
            {
                pc.transform.rotation = Quaternion.LookRotation(Flat(target.transform.position - pc.transform.position));
                if (i < 3) pc.LightAttack(); else pc.HeavyAttack();
                float swing = i < 3 ? 0.62f : 1.05f;
                for (float t = 0; t < swing; t += Time.deltaTime) { frame(); yield return null; }
            }
            for (float t = 0; t < 2.2f; t += Time.deltaTime) yield return null;   // xp + level up toast
            pc.UseScriptedInput = false;
        }

        IEnumerator Combo()
        {
            var pc = Player;
            var stats = PlayerStats.Local;
            foreach (var id in new[] { "beetle_horn_sword", "petal_shield" })
                if (Inventory.Count(stats.Bag, id) == 0) stats.AddItem(id);
            stats.progress.equipped = "beetle_horn_sword";
            stats.progress.offhand = "petal_shield";
            stats.FillBars();
            pc.Warp(new Vector3(3, 0.05f, -4), Quaternion.identity);
            PhotoMode.Set(true);
            var cam = Cam;
            cam.pitch = 9f;
            cam.distance = 3.8f;
            float t0 = Time.time;
            pc.UseScriptedInput = true;   // the guard below is set by hand, not by the mouse
            pc.ScriptedInput = Vector2.zero;
            System.Action frame = () => cam.yaw = pc.transform.eulerAngles.y + 215f + (Time.time - t0) * 3f;
            frame();
            cam.SnapNext();
            for (float t = 0; t < 1f; t += Time.deltaTime) { frame(); yield return null; }
            // three light hits chained into a combo, then a heavy slam
            for (int i = 0; i < 4; i++)
            {
                stats.FillBars();
                if (i < 3) pc.LightAttack(); else pc.HeavyAttack();
                while (pc.Busy) { frame(); yield return null; }
                float pause = i == 2 ? 0.7f : 0f;
                for (float t = 0; t < pause; t += Time.deltaTime) { frame(); yield return null; }
            }
            for (float t = 0; t < 0.8f; t += Time.deltaTime) { frame(); yield return null; }
            // shield up
            for (float t = 0; t < 1.4f; t += Time.deltaTime) { stats.GuardReduction = pc.GuardAmount; frame(); yield return null; }
            stats.GuardReduction = 0f;
            for (float t = 0; t < 0.8f; t += Time.deltaTime) { frame(); yield return null; }
            pc.UseScriptedInput = false;
            PhotoMode.Set(false);
        }

        IEnumerator Death()
        {
            var pc = Player;
            var stats = PlayerStats.Local;
            stats.AddCoins(37);
            Monster target = null;
            float best = float.MaxValue;
            foreach (var m in FindObjectsByType<Monster>(FindObjectsSortMode.None))
            {
                float d = Vector3.Distance(m.transform.position, new Vector3(-10, 0, 1));
                if (m.Alive && d < best) { best = d; target = m; }
            }
            Vector3 stand = target.transform.position + new Vector3(-2.2f, 0.05f, -1f);
            pc.Warp(stand, Quaternion.LookRotation(Flat(target.transform.position - stand)));
            Cam.yaw = Quaternion.LookRotation(Flat(target.transform.position - stand)).eulerAngles.y + 62f;
            Cam.pitch = 16f;
            Cam.distance = 5f;
            Cam.SnapNext();
            stats.health = 10f;
            pc.UseScriptedInput = true;
            for (float t = 0; t < 8f && !stats.Dead; t += Time.deltaTime) yield return null;
            yield return Wait(5.5f);   // the "YOU WOKE UP" screen, then home
            pc.UseScriptedInput = false;
        }

        IEnumerator Town()
        {
            var pc = Player;
            PlayerStats.Local.FillBars();
            pc.Warp(new Vector3(-1.5f, 0.05f, 1.2f), Quaternion.Euler(0, 160, 0));
            PhotoMode.Set(true);
            var cam = Cam;
            cam.pitch = 10f;
            cam.distance = 6.5f;
            cam.yaw = 120f;
            cam.SnapNext();
            for (float t = 0; t < 9f; t += Time.deltaTime)
            {
                cam.yaw = 120f + t * 16f;
                yield return null;
            }
            PhotoMode.Set(false);
        }

        // ---------- TikTok round 2 ----------

        // Only these root canvases stay visible (null = leave them all alone).
        static string[] keepCanvases;

        void LateUpdate()
        {
            if (keepCanvases == null) return;
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (canvas.isRootCanvas) canvas.enabled = System.Array.IndexOf(keepCanvases, canvas.name) >= 0;
        }

        static IEnumerator For(float seconds, System.Action frame = null)
        {
            for (float t = 0; t < seconds; t += Time.deltaTime) { frame?.Invoke(); yield return null; }
        }

        // Not a clip: every top and bottom on all 8 body types, front + side, saved as small PNGs in FitFolder
        // (for checking clipping before picking outfits for a video).
        public static string FitFolder;

        IEnumerator FitCheck()
        {
            yield return OpenCreator();
            PhotoMode.Set(true);
            var app = FindAnyObjectByType<CharacterAppearance>();
            var top = app.GetSlot(OutfitCategory.Top);
            var bottom = app.GetSlot(OutfitCategory.Bottom);
            app.GetSlot(OutfitCategory.Hair).SelectBase("Bob");
            var go = new GameObject("FitCam");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 30f;
            cam.enabled = false;
            var rt = new RenderTexture(240, 400, 24);
            cam.targetTexture = rt;
            var tex = new Texture2D(240, 400, TextureFormat.RGB24, false);
            Vector3 c = app.transform.position + Vector3.up * 0.82f;
            Vector3 fwd = app.transform.forward;
            System.IO.Directory.CreateDirectory(FitFolder);
            foreach (var part in new[] { "top", "bottom" })
            {
                var slot = part == "top" ? top : bottom;
                foreach (var n in slot.BaseNames)
                    for (int body = 0; body < 8; body++)
                    {
                        Wear(top, part == "top" ? n : "Tee");
                        Wear(bottom, part == "bottom" ? n : "Slacks");
                        foreach (var ax in top.axes) ax.index = 0;
                        foreach (var ax in bottom.axes) ax.index = 0;
                        top.colorIndex = part == "top" ? 3 : 11;
                        bottom.colorIndex = part == "bottom" ? 3 : 11;
                        app.ApplyAll();
                        app.SetBody(body / 4, body % 4, 1f);
                        yield return null;
                        yield return null;
                        for (int a = 0; a < 2; a++)
                        {
                            Vector3 dir = Quaternion.Euler(0, a == 0 ? 0f : 70f, 0) * fwd;
                            go.transform.position = c + dir * 3.4f;
                            go.transform.LookAt(c);
                            cam.Render();
                            RenderTexture.active = rt;
                            tex.ReadPixels(new Rect(0, 0, 240, 400), 0, 0);
                            tex.Apply();
                            RenderTexture.active = null;
                            System.IO.File.WriteAllBytes($"{FitFolder}/{part}_{n}_{body}_{a}.png", tex.EncodeToPNG());
                        }
                    }
            }
            Destroy(tex);
            cam.targetTexture = null;
            Destroy(rt);
            Destroy(go);
            PhotoMode.Set(false);
        }

        // One full look: body, hair, top + pattern, bottom + pattern, shoes. Only pieces that passed the fit
        // check on that body (SkinnyJeans clips on curvy/plus/men, SlipDress and Oversized clip everywhere).
        struct Look
        {
            public int sex, type; public string hair, top, bottom, shoes; public int topPattern, bottomPattern, topColor, bottomColor, hairColor;
            public Look(int sex, int type, string hair, int hairColor, string top, int topPattern, int topColor, string bottom, int bottomPattern, int bottomColor, string shoes)
            {
                this.sex = sex; this.type = type; this.hair = hair; this.hairColor = hairColor; this.top = top; this.topPattern = topPattern; this.topColor = topColor;
                this.bottom = bottom; this.bottomPattern = bottomPattern; this.bottomColor = bottomColor; this.shoes = shoes;
            }
        }

        static void Dress(CharacterAppearance app, Look l)
        {
            var hair = app.GetSlot(OutfitCategory.Hair);
            var top = app.GetSlot(OutfitCategory.Top);
            var bottom = app.GetSlot(OutfitCategory.Bottom);
            var shoes = app.GetSlot(OutfitCategory.Shoes);
            hair.SelectBase(l.hair);
            Tint(hair, HairColors[l.hairColor % HairColors.Length]);
            top.SelectBase(l.top);
            foreach (var ax in top.axes) ax.index = 0;
            if (top.axes.Count > 0) top.axes[top.axes.Count - 1].index = l.topPattern;   // the pattern axis is last
            top.colorIndex = l.topColor % top.colors.Length;
            bottom.SelectBase(l.bottom);
            for (int i = 0; i < bottom.axes.Count; i++) bottom.axes[i].index = 0;
            if (bottom.axes.Count > 1) { bottom.axes[0].index = 1; bottom.axes[bottom.axes.Count - 1].index = l.bottomPattern; }   // mid rise
            bottom.colorIndex = l.bottomColor % bottom.colors.Length;
            shoes.SelectBase(l.shoes);
            app.ApplyAll();
            app.SetBody(l.sex, l.type, l.sex == 1 ? 1.04f : 1f);
        }

        IEnumerator Bodies()
        {
            yield return OpenCreator();
            PhotoMode.Set(true);
            var app = FindAnyObjectByType<CharacterAppearance>();
            var zoom = FindAnyObjectByType<CreatorCameraZoom>();
            var turntable = FindAnyObjectByType<CharacterTurntable>();
            if (zoom != null) { zoom.Zoom(-1f); zoom.Zoom(0.28f); }
            if (turntable != null) turntable.FaceFront(-20f);
            Look[] looks =
            {
                new Look(0, 0, "Long", 0, "Tee", 0, 5, "FlareJeans", 0, 8, "Sneakers"),
                new Look(0, 1, "Hime", 1, "Tee", 0, 9, "FlareJeans", 0, 8, "Sneakers"),
                new Look(0, 2, "LongLayered", 3, "Tee", 0, 13, "FlareJeans", 0, 8, "Sneakers"),
                new Look(0, 3, "Bob", 6, "Tee", 0, 17, "FlareJeans", 0, 8, "Sneakers"),
                new Look(1, 0, "Curtains", 4, "Hoodie", 0, 5, "BaggyJeans", 0, 8, "Sneakers"),
                new Look(1, 1, "Shaggy", 1, "Hoodie", 0, 9, "BaggyJeans", 0, 8, "Sneakers"),
                new Look(1, 2, "WolfCut", 5, "Hoodie", 0, 13, "BaggyJeans", 0, 8, "Sneakers"),
                new Look(1, 3, "Pixie", 9, "Hoodie", 0, 17, "BaggyJeans", 0, 8, "Sneakers"),
            };
            Dress(app, looks[0]);
            yield return Wait(0.6f);
            for (int i = 0; i < looks.Length; i++)
            {
                Dress(app, looks[i]);
                if (turntable != null) turntable.FaceFront(-20f + i * 6f);
                yield return Wait(0.8f);
            }
            PhotoMode.Set(false);
        }

        IEnumerator Hairs()
        {
            yield return OpenCreator();
            PhotoMode.Set(true);
            var app = FindAnyObjectByType<CharacterAppearance>();
            var zoom = FindAnyObjectByType<CreatorCameraZoom>();
            var turntable = FindAnyObjectByType<CharacterTurntable>();
            if (zoom != null) { zoom.Zoom(-1f); zoom.Zoom(0.72f); }
            if (turntable != null) turntable.FaceFront(28f);
            var hair = app.GetSlot(OutfitCategory.Hair);
            Wear(app.GetSlot(OutfitCategory.Top), "Cardigan");
            app.ApplyAll();
            yield return Wait(0.7f);
            int n = 0;
            foreach (var b in hair.BaseNames)
            {
                if (hair.IsRetired(hair.BaseNames.IndexOf(b))) continue;   // the broken ones aren't in the creator any more
                hair.SelectBase(b);
                Tint(hair, HairColors[n % HairColors.Length]);
                app.ApplyAll();
                n++;
                yield return Wait(0.75f);
            }
            PhotoMode.Set(false);
        }

        IEnumerator Outfits()
        {
            yield return OpenCreator();
            PhotoMode.Set(true);
            var app = FindAnyObjectByType<CharacterAppearance>();
            var zoom = FindAnyObjectByType<CreatorCameraZoom>();
            var turntable = FindAnyObjectByType<CharacterTurntable>();
            if (zoom != null) { zoom.Zoom(-1f); zoom.Zoom(0.28f); }
            if (turntable != null) turntable.FaceFront(-18f);
            // patterns: 0 plain, 1 stripes, 2 pinstripe, 3 dots, 4 checker, 5 hearts, 6 stars, 7 leopard
            Look[] looks =
            {
                new Look(0, 0, "Twintails", 0, "Sailor", 0, 3, "Pleated", 0, 6, "MaryJanes"),
                new Look(1, 1, "Shaggy", 4, "Flannel", 0, 9, "BaggyJeans", 0, 8, "Sneakers"),
                new Look(0, 1, "Hime", 1, "Cardigan", 5, 12, "LongSkirt", 0, 4, "Loafers"),
                new Look(1, 2, "WolfCut", 1, "Hoodie", 0, 20, "Cargo", 0, 10, "Combat"),
                new Look(0, 3, "Bob", 4, "Sweater", 1, 15, "FlareJeans", 0, 8, "Sneakers"),
                new Look(1, 0, "Curtains", 3, "ButtonUp", 2, 1, "Slacks", 0, 11, "Loafers"),
                new Look(0, 1, "Buns", 5, "PuffBlouse", 3, 18, "RuffleSkirt", 0, 12, "MaryJanes"),
                new Look(1, 3, "Pixie", 9, "Tee", 1, 6, "Shorts", 0, 8, "Sneakers"),
                new Look(0, 2, "Long", 6, "Tank", 7, 2, "Slacks", 0, 11, "Boots"),
                new Look(1, 1, "Curtains", 1, "Sweater", 6, 14, "Cargo", 0, 3, "Boots"),
                new Look(0, 0, "Ponytail", 3, "Hoodie", 6, 20, "ShortShorts", 0, 8, "Platforms"),
                new Look(0, 3, "LongLayered", 7, "LongSleeve", 4, 6, "Skirt", 0, 11, "Boots"),
            };
            Dress(app, looks[0]);
            yield return Wait(0.6f);
            for (int i = 0; i < looks.Length; i++)
            {
                Dress(app, looks[i]);
                if (turntable != null) turntable.FaceFront(i % 2 == 0 ? -18f : 18f);
                yield return Wait(0.75f);
            }
            PhotoMode.Set(false);
        }

        IEnumerator Details()
        {
            yield return OpenCreator();
            PhotoMode.Set(true);
            var app = FindAnyObjectByType<CharacterAppearance>();
            var zoom = FindAnyObjectByType<CreatorCameraZoom>();
            var blink = FindAnyObjectByType<EyeBlink>();
            if (blink != null) blink.secondsBetweenBlinks = new Vector2(60f, 60f);
            var hair = app.GetSlot(OutfitCategory.Hair);
            hair.SelectBase("Long");
            if (hair.axes.Count > 0) hair.axes[0].index = 5;   // micro bangs: the whole face shows
            Tint(hair, HairColors[0]);
            app.ApplyAll();
            var turntable = FindAnyObjectByType<CharacterTurntable>();
            if (turntable != null) turntable.FaceFront(10f);
            if (zoom != null) zoom.Zoom(1f);
            yield return Wait(0.8f);
            var eyes = app.GetSlot(OutfitCategory.Eyes);
            for (int i = 0; i < Mathf.Min(8, eyes.StyleCount); i++)
            {
                eyes.styleIndex = i;
                Tint(eyes, EyeColors[i % EyeColors.Length]);
                app.ApplyAll();
                yield return Wait(0.45f);
            }
            if (blink != null) blink.secondsBetweenBlinks = new Vector2(2.2f, 5.5f);
            app.ChangeStyle(OutfitCategory.Extra2, 4);   // freckles
            yield return Wait(0.6f);
            app.ChangeStyle(OutfitCategory.Extra3, 6);   // sparkles
            yield return Wait(1.0f);
            PhotoMode.Set(false);
        }

        // Hand-picked colors that look nice on camera.
        static readonly Color[] HairColors =
        {
            new Color(0.98f, 0.72f, 0.82f), new Color(0.2f, 0.16f, 0.2f), new Color(0.72f, 0.62f, 0.95f), new Color(0.95f, 0.82f, 0.55f),
            new Color(0.45f, 0.28f, 0.2f), new Color(0.55f, 0.85f, 0.85f), new Color(0.75f, 0.25f, 0.3f), new Color(0.95f, 0.95f, 0.98f),
            new Color(0.35f, 0.35f, 0.75f), new Color(0.98f, 0.6f, 0.4f),
        };
        static readonly Color[] EyeColors =
        {
            new Color(0.95f, 0.45f, 0.7f), new Color(0.35f, 0.75f, 0.45f), new Color(0.6f, 0.4f, 0.95f), new Color(0.3f, 0.55f, 0.95f),
            new Color(0.9f, 0.65f, 0.2f), new Color(0.2f, 0.75f, 0.75f), new Color(0.85f, 0.2f, 0.25f), new Color(0.45f, 0.3f, 0.2f),
        };

        static void Tint(OutfitSlot slot, Color c) { slot.useCustom = true; slot.customColor = c; }

        // The first style whose name contains `part` (so the scenes don't depend on exact list names).
        static void Wear(OutfitSlot slot, string part)
        {
            foreach (var b in slot.BaseNames)
                if (b.Contains(part)) { slot.SelectBase(b); return; }
        }

        static T Find<T>(string name) where T : Component
        {
            foreach (var c in FindObjectsByType<T>(FindObjectsSortMode.None))
                if (c.name == name) return c;
            return null;
        }

        // Starts a world scene from a clean state on the throwaway save.
        static void Fresh()
        {
            PlayerStats.Local.FillBars();
            if (DreamWorld.Quests.DialogueUI.IsOpen) DreamWorld.Quests.DialogueUI.Instance.SendMessage("Close");
        }

        // A camera from the side, framing her and whoever she's facing.
        // The camera's look-at height is lowered so both stay above the dialogue box (Orion is beetle-sized).
        static void TwoShot(Transform a, Transform b, float side = 70f, float pitch = 12f, float dist = 5.2f, float height = 1.35f)
        {
            var cam = Cam;
            cam.height = height;
            cam.yaw = Quaternion.LookRotation(Flat(b.position - a.position)).eulerAngles.y + side;
            cam.pitch = pitch;
            cam.distance = dist;
        }

        // A side-on camera placed by hand (the follow camera is switched off): both people stand in the upper
        // half of the frame, so the dialogue box along the bottom never covers them.
        static readonly float[] SideAngles = { 90f, -90f, 60f, -60f, 120f, -120f, 30f, -30f, 150f, -150f };
        static Light fill;

        static void SideShot(Transform a, Transform b, float dist, float drift = 0f, float[] angles = null, float lookY = -0.2f, float eyeY = 1.5f)
        {
            var tpc = Cam;
            if (tpc.enabled) tpc.enabled = false;
            if (fill == null)   // a soft fill light on the camera, so faces read at night
            {
                fill = new GameObject("ClipFill").AddComponent<Light>();
                fill.type = LightType.Point;
                fill.range = 9f;
                fill.intensity = 2.6f;
                fill.color = new Color(1f, 0.9f, 0.95f);
                fill.shadows = LightShadows.None;
            }
            Vector3 mid = (a.position + b.position) * 0.5f;
            Vector3 along = Flat(b.position - a.position);
            // pick an angle with a clear view of both of them (no lamp post, crate or tree in the way), and keep it
            if (float.IsNaN(shotAngle))
            {
                shotAngle = 90f;
                foreach (var ang in angles ?? SideAngles)
                    if (!Blocked(mid, a, b, Quaternion.Euler(0, ang, 0) * along, dist)) { shotAngle = ang; break; }
            }
            Vector3 perp = Quaternion.Euler(0, shotAngle + drift, 0) * along;
            var cam = tpc.GetComponent<Camera>().transform;
            cam.position = mid + perp * dist + Vector3.up * eyeY;
            cam.rotation = Quaternion.LookRotation(mid + Vector3.up * lookY - cam.position);
            fill.transform.position = cam.position + Vector3.up * 0.5f;
        }

        static float shotAngle = float.NaN;
        static System.Collections.Generic.List<Bounds> nearby;

        static bool Blocked(Vector3 mid, Transform a, Transform b, Vector3 perp, float dist)
        {
            Vector3 eye = mid + perp * dist + Vector3.up * 1.5f;
            if (Physics.CheckSphere(eye, 0.6f, ~0, QueryTriggerInteraction.Ignore)) return true;
            // by what's drawn, not colliders: lamp posts and plants have none
            if (nearby == null)
            {
                nearby = new System.Collections.Generic.List<Bounds>();
                foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    if (!r.enabled || r is ParticleSystemRenderer || r.transform.IsChildOf(a) || r.transform.IsChildOf(b)) continue;
                    var bb = r.bounds;
                    if (bb.size.x > 12f || bb.size.z > 12f || bb.max.y < 0.35f) continue;   // ground, plaza, grass chunks, flat rugs
                    if ((bb.center - mid).sqrMagnitude > 14f * 14f) continue;
                    bb.Expand(0.15f);
                    nearby.Add(bb);
                }
            }
            foreach (var to in new[] { a.position + Vector3.up * 0.3f, a.position + Vector3.up * 1.2f, a.position + Vector3.up * 1.55f, b.position + Vector3.up * 0.25f, mid + Vector3.up * 0.2f })
            {
                var ray = new Ray(eye, to - eye);
                float len = Vector3.Distance(eye, to) - 0.3f;
                foreach (var bb in nearby)
                    if (bb.IntersectRay(ray, out float d) && d < len) return true;
            }
            return false;
        }

        IEnumerator Talk(DreamWorld.Quests.DialogueUI ui, float linger, params int[] picks)
        {
            int p = 0;
            float doneFor = 0f;
            while (DreamWorld.Quests.DialogueUI.IsOpen)
            {
                if (ui.ChoiceCount > 0)
                {
                    yield return Wait(1.1f);
                    if (p >= picks.Length) yield break;
                    ui.Choose(picks[p++]);
                    doneFor = 0f;
                }
                else if (!ui.Typing)
                {
                    doneFor += Time.deltaTime;
                    if (doneFor > linger) { ui.Advance(); doneFor = 0f; }
                }
                yield return null;
            }
        }

        IEnumerator OrionScene()
        {
            var pc = Player;
            var orion = FindAnyObjectByType<DreamWorld.Quests.OrionNpc>();
            var ui = DreamWorld.Quests.DialogueUI.Instance;
            DreamWorld.Quests.QuestManager.Instance.state = new DreamWorld.Quests.QuestState();   // a first visit
            Fresh();
            keepCanvases = new[] { "DialogueUI" };
            // a few steps in front of her, inside the square, facing her
            Vector3 dir = Flat(orion.transform.forward);
            Vector3 stand = orion.transform.position + dir * 7f;
            stand.y = orion.transform.position.y + 0.05f;
            pc.Warp(stand, Quaternion.LookRotation(-dir));
            pc.UseScriptedInput = true;
            float t0 = Time.time;
            System.Action frame = () => SideShot(pc.transform, orion.transform, 4.4f, Mathf.Sin((Time.time - t0) * 0.2f) * 8f);
            frame();
            // step back as she scuttles over: she reassures you she's a person too
            for (float t = 0; t < 7f && !DreamWorld.Quests.DialogueUI.IsOpen; t += Time.deltaTime)
            {
                if (Vector3.Distance(Flat3(orion.transform.position), Flat3(pc.transform.position)) < 2.6f)
                    orion.SendMessage("Talk", true);   // "wait wait, don't be scared! ... i'm a person too"
                frame();
                yield return null;
            }
            nearby = null;
            shotAngle = float.NaN;   // she's here now: find a clear angle on the two of them
            var talk = StartCoroutine(Talk(ui, 1.5f, 3, 5));   // "collect", then "that's all"
            for (float t = 0; t < 26f && DreamWorld.Quests.DialogueUI.IsOpen; t += Time.deltaTime) { frame(); yield return null; }
            StopCoroutine(talk);
            if (DreamWorld.Quests.DialogueUI.IsOpen) ui.SendMessage("Close");
            pc.UseScriptedInput = false;
        }

        IEnumerator Snackula()
        {
            var pc = Player;
            var q = DreamWorld.Quests.QuestManager.Instance;
            q.state = new DreamWorld.Quests.QuestState();   // met Orion, haven't heard about the Mullens yet
            q.Set("orion_met");
            if (!q.Finished(DreamWorld.Quests.QuestBook.OrionIntro)) q.Complete(DreamWorld.Quests.QuestBook.OrionIntro);
            DreamWorld.Town.NpcTalk count = null;
            foreach (var n in FindObjectsByType<DreamWorld.Town.NpcTalk>(FindObjectsSortMode.None)) if (n.npcName == "Count Snackula") count = n;
            var ui = DreamWorld.Quests.DialogueUI.Instance;
            Fresh();
            keepCanvases = new[] { "DialogueUI" };
            Vector3 dir = Flat(count.transform.forward);
            Vector3 stand = count.transform.position + dir * 2.2f;
            stand.y = count.transform.position.y + 0.05f;
            pc.Warp(stand, Quaternion.LookRotation(-dir));
            pc.UseScriptedInput = true;
            float t0 = Time.time;
            System.Action frame = () => SideShot(pc.transform, count.transform, 4.3f, (Time.time - t0) * 1.2f, new[] { 125f, -125f, 145f, -145f, 105f, -105f, 160f, -160f }, 0.55f, 1.75f);
            frame();
            yield return For(0.8f, frame);
            count.Interact(PlayerStats.Local);
            var talk = StartCoroutine(Talk(ui, 1.4f, 0, 0, 0));
            for (float t = 0; t < 16f && DreamWorld.Quests.DialogueUI.IsOpen; t += Time.deltaTime) { frame(); yield return null; }
            StopCoroutine(talk);
            if (DreamWorld.Quests.DialogueUI.IsOpen) ui.SendMessage("Close");
            pc.UseScriptedInput = false;
        }

        IEnumerator Craft()
        {
            var pc = Player;
            var stats = PlayerStats.Local;
            Fresh();
            keepCanvases = new[] { "CraftingUI", "GameHUD" };
            // a birch near the town (birch makes the prettiest stool)
            DreamWorld.Gathering.ResourceNode tree = null;
            float best = float.MaxValue;
            foreach (var n in FindObjectsByType<DreamWorld.Gathering.ResourceNode>(FindObjectsSortMode.None))
            {
                if (n.item != "wood_birch" || n.Depleted) continue;
                float d = n.transform.position.magnitude;
                if (d < best) { best = d; tree = n; }
            }
            Vector3 stand = tree.transform.position - Flat(tree.transform.position) * 1.6f;
            stand.y = tree.transform.position.y + 0.05f;
            pc.Warp(stand, Quaternion.LookRotation(Flat(tree.transform.position - stand)));
            pc.UseScriptedInput = true;
            System.Action frame = () => SideShot(pc.transform, tree.transform, 5.2f, 0f, new[] { 120f, -120f, 90f, -90f, 150f, -150f });
            frame();
            yield return For(0.6f, frame);
            for (int i = 0; i < 4 && !tree.Depleted; i++)
            {
                stats.FillBars();
                pc.LightAttack();
                tree.Interact(stats);
                yield return For(0.7f, frame);
            }
            yield return For(1.0f, frame);
            // then the workbench in the square
            for (int i = 0; i < 12; i++) stats.AddItem("wood_birch", 1);
            stats.AddItem("thread", 4);
            shotAngle = float.NaN;
            nearby = null;
            var bench = FindAnyObjectByType<DreamWorld.Gathering.CraftingBench>();
            Vector3 bdir = Flat(-bench.transform.position);
            Vector3 bstand = bench.transform.position + bdir * 1.8f;
            bstand.y = bench.transform.position.y + 0.05f;
            pc.Warp(bstand, Quaternion.LookRotation(-bdir));
            System.Action benchFrame = () => SideShot(pc.transform, bench.transform, 4.2f);
            benchFrame();
            yield return For(0.8f, benchFrame);
            DreamWorld.Gathering.CraftingUI.Open();
            var craft = DreamWorld.Gathering.CraftingUI.Instance;
            yield return For(0.9f, benchFrame);
            DreamWorld.Gathering.Recipe stool = null;
            foreach (var r in DreamWorld.Gathering.Crafting.Recipes) if (r.id == "decor_stool") stool = r;
            craft.SendMessage("Pick", stool);
            yield return For(0.9f, benchFrame);
            for (int i = 0; i < 3; i++)   // flip through the woods so the stool changes color
            {
                craft.SendMessage("StepWood", 1);
                yield return For(0.55f, benchFrame);
            }
            craft.SendMessage("Pick", stool);   // back to the wood she has (birch)
            yield return For(0.6f, benchFrame);
            craft.SendMessage("CraftCurrent");
            yield return For(1.4f, benchFrame);
            craft.SendMessage("Show", false);
            yield return For(0.5f, benchFrame);
            pc.UseScriptedInput = false;
        }

        IEnumerator Decor()
        {
            var pc = Player;
            var stats = PlayerStats.Local;
            Fresh();
            if (stats.progress.plot < 0) stats.progress.plot = 0;
            yield return For(0.5f);   // the home moves onto the plot
            var home = DreamWorld.Town.HomeBase.Instance;
            var decor = DreamWorld.Town.HomeDecor.Instance;
            keepCanvases = new[] { "DecorHint" };
            // in bag order = the order they get placed
            string[] items = { "decor_rug", "decor_table_oak", "decor_crystal_lamp_birch", "decor_stool_birch", "decor_planter_pine", "decor_stone_lantern" };
            foreach (var id in items)
                if (Items.Get(id) != null && Inventory.Count(stats.Bag, id) == 0) stats.AddItem(id, 1);
            var root = home.contents.transform;
            // the yard in front of the cottage (the home zone is 11 m square around the plot's middle)
            pc.Warp(root.TransformPoint(new Vector3(-2.6f, 0.05f, -2.6f)), Quaternion.LookRotation(root.TransformDirection(new Vector3(1f, 0, -0.6f))));
            pc.UseScriptedInput = true;
            yield return For(0.3f);
            decor.StartDecorating();
            // the follow camera is off: aiming the camera's center at a spot is what places things there
            var tpc = Cam;
            tpc.enabled = false;
            var cam = tpc.GetComponent<Camera>().transform;
            Vector3 eye = root.TransformPoint(new Vector3(3.5f, 3.4f, -9.5f));
            Vector3[] spots =
            {
                new Vector3(0.3f, 0f, -3.9f), new Vector3(0.3f, 0f, -3.9f), new Vector3(0.3f, 0.6f, -3.9f),
                new Vector3(1.6f, 0f, -3.3f), new Vector3(-3.4f, 0f, -4.6f), new Vector3(3.6f, 0f, -4.7f),
            };
            Vector3 look = root.TransformPoint(new Vector3(0, 0, -3.5f));
            cam.position = eye;
            cam.rotation = Quaternion.LookRotation(look - eye);
            yield return For(0.6f);
            foreach (var spot in spots)
            {
                Vector3 from = look, to = root.TransformPoint(spot);
                for (float t = 0; t < 0.7f; t += Time.deltaTime)
                {
                    look = Vector3.Lerp(from, to, Mathf.SmoothStep(0, 1, t / 0.7f));
                    cam.position = eye;
                    cam.rotation = Quaternion.LookRotation(look - eye);
                    yield return null;
                }
                yield return For(0.35f);
                if (!decor.PlaceAimed()) Debug.Log("[ClipDirector] couldn't place at " + spot);
                yield return For(0.45f);
            }
            decor.StopDecorating();
            // a slow look around the finished yard
            Vector3 center = root.TransformPoint(new Vector3(0, 0.4f, -3.8f));
            float a0 = Mathf.Atan2(eye.x - center.x, eye.z - center.z) * Mathf.Rad2Deg;
            float r = Vector3.Distance(Flat3(eye), Flat3(center));
            for (float t = 0; t < 3.5f; t += Time.deltaTime)
            {
                float ang = (a0 + t * 9f) * Mathf.Deg2Rad;
                cam.position = center + new Vector3(Mathf.Sin(ang) * r * 0.8f, 2.6f, Mathf.Cos(ang) * r * 0.8f);
                cam.rotation = Quaternion.LookRotation(center - cam.position);
                yield return null;
            }
            pc.UseScriptedInput = false;
        }

        IEnumerator Mistpine()
        {
            var pc = Player;
            Fresh();
            keepCanvases = new string[0];
            Transform house = null;
            foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t.name == "Mist_MullenHouse") { house = t; break; }
            var bounds = new Bounds(house.position, Vector3.zero);
            foreach (var r in house.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            // stand on the path in front of the glass wall and drift the camera around
            Vector3 stand = bounds.center + (pc.transform.position - bounds.center).normalized * (bounds.extents.magnitude * 0.9f);
            pc.Warp(stand + Vector3.up * 0.05f, Quaternion.LookRotation(Flat(bounds.center - stand)));
            pc.UseScriptedInput = true;
            var cam = Cam;
            float face = Quaternion.LookRotation(Flat(bounds.center - stand)).eulerAngles.y;
            cam.yaw = face - 35f;
            cam.pitch = 8f;
            cam.distance = 7f;
            cam.SnapNext();
            pc.ScriptedInput = new Vector2(0, 0.35f);
            yield return For(7f, () => { cam.yaw += Time.deltaTime * 7f; });
            pc.ScriptedInput = Vector2.zero;
            pc.UseScriptedInput = false;
        }

        static Vector3 Flat3(Vector3 v) { v.y = 0; return v; }

        static Vector3 Flat(Vector3 v) { v.y = 0; return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward; }
    }
}
