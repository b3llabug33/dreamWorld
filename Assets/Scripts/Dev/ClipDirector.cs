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
                default: Debug.LogWarning("[ClipDirector] unknown scene " + scene); break;
            }
            Running = false;
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

        // ---------- world ----------

        static PlayerController Player => PlayerStats.Local != null ? PlayerStats.Local.GetComponent<PlayerController>() : null;
        static ThirdPersonCamera Cam => FindAnyObjectByType<ThirdPersonCamera>();

        IEnumerator Walk()
        {
            var pc = Player;
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

        static Vector3 Flat(Vector3 v) { v.y = 0; return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward; }
    }
}
