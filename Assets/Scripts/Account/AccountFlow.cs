using System.Collections;
using System.Collections.Generic;
using DreamWorld.CharacterCreation;
using DreamWorld.Net;
using UnityEngine;
using UnityEngine.UI;

namespace DreamWorld.Account
{
    // Runs in the creator scene, in front of the character creator:
    //   1. log in / sign up / play offline   (skipped if this computer is still logged in)
    //   2. "your dreamers": up to 4 saved characters; pick one to play or edit, or make a new one
    //   3. the character creator; READY saves the character to the account and enters the world
    public class AccountFlow : MonoBehaviour
    {
        public CharacterAppearance appearance;
        public CharacterCreatorUI creator;
        public Font titleFont;
        public Font bodyFont;
        public string worldScene = "World_MushroomHollow";

        TerminalUI ui;
        RectTransform canvas, loginPanel, selectPanel;
        InputField userField, passField;
        Text loginStatus, selectStatus, accountLabel;
        readonly List<(Button button, Text label)> slotRows = new List<(Button, Text)>();
        Button playButton, editButton, deleteButton, createButton;
        List<CharacterRecord> records = new List<CharacterRecord>();
        int selected = -1;
        bool confirmDelete, busy;

        IEnumerator Start()
        {
            if (NetSession.FromCommandLine(out _, out _)) yield break;   // servers/test clients skip accounts
            yield return null;                                          // let the creator build its UI first
            ui = new TerminalUI(titleFont, bodyFont);
            canvas = ui.Canvas("AccountCanvas", 10);
            BuildLogin();
            BuildSelect();
            ui.Scanlines(canvas);
            creator.Show(false);
            loginPanel.gameObject.SetActive(false);
            selectPanel.gameObject.SetActive(false);
            Resume();
        }

        async void Resume()
        {
            if (AccountService.LoggedIn || await AccountService.TryResume()) ShowSelect();
            else ShowLogin();
        }

        void Update()
        {
            if (loginPanel != null && loginPanel.gameObject.activeSelf && !busy &&
                (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
                SignIn(false);
        }

        // ---------- login ----------

        void BuildLogin()
        {
            loginPanel = ui.PanelBox(canvas, "Login");
            TerminalUI.Place(loginPanel, new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(640, 600));
            var title = ui.Label(canvas, "dreamWorld", 110, TerminalUI.Text, title: true);
            title.gameObject.AddComponent<Shadow>().effectColor = TerminalUI.Accent;
            title.GetComponent<Shadow>().effectDistance = new Vector2(6, -6);
            TerminalUI.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 370), new Vector2(900, 140));
            title.transform.SetParent(loginPanel, true);

            var header = ui.Label(loginPanel, "== LOG IN ==", 36, TerminalUI.Dim);
            TerminalUI.Place(header.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -36), new Vector2(500, 44));

            userField = ui.Field(loginPanel, "username");
            TerminalUI.Place((RectTransform)userField.transform, new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(520, 70));
            passField = ui.Field(loginPanel, "password", password: true);
            TerminalUI.Place((RectTransform)passField.transform, new Vector2(0.5f, 1), new Vector2(0, -196), new Vector2(520, 70));
            userField.text = PlayerPrefs.GetString("dreamWorld.lastUser", "");

            var login = ui.Button(loginPanel, "log in", 38, () => SignIn(false));
            login.GetComponent<Image>().color = TerminalUI.Accent;
            TerminalUI.Place((RectTransform)login.transform, new Vector2(0.5f, 1), new Vector2(-135, -290), new Vector2(250, 70));
            var signUp = ui.Button(loginPanel, "sign up", 38, () => SignIn(true));
            TerminalUI.Place((RectTransform)signUp.transform, new Vector2(0.5f, 1), new Vector2(135, -290), new Vector2(250, 70));

            var offline = ui.Button(loginPanel, "play offline", 32, PlayOffline);
            TerminalUI.Place((RectTransform)offline.transform, new Vector2(0.5f, 1), new Vector2(0, -376), new Vector2(300, 56));

            loginStatus = ui.Label(loginPanel, "", 30, TerminalUI.Accent);
            loginStatus.horizontalOverflow = HorizontalWrapMode.Wrap;
            TerminalUI.Place(loginStatus.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -462), new Vector2(560, 70));
            var hint = ui.Label(loginPanel, "username: 3-20 letters, like bellabug (numbers optional)\npassword: 8+ characters with A-Z, a-z, 0-9 and a symbol",
                                24, TerminalUI.Dim);
            TerminalUI.Place(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(600, 60));
        }

        void ShowLogin()
        {
            creator.Show(false);
            selectPanel.gameObject.SetActive(false);
            loginPanel.gameObject.SetActive(true);
            loginStatus.text = "";
        }

        async void SignIn(bool create)
        {
            if (busy) return;
            if (userField.text.Trim().Length == 0 || passField.text.Length == 0)
            {
                loginStatus.text = "type a username and password";
                return;
            }
            busy = true;
            loginStatus.text = create ? "making your account..." : "logging in...";
            var error = await AccountService.SignIn(userField.text, passField.text, create);
            busy = false;
            if (error != null)
            {
                loginStatus.text = error;
                return;
            }
            passField.text = "";
            ShowSelect();
        }

        void PlayOffline()
        {
            AccountService.PlayOffline();
            ShowSelect();
        }

        // ---------- your dreamers ----------

        void BuildSelect()
        {
            selectPanel = ui.PanelBox(canvas, "Select");
            TerminalUI.Place(selectPanel, new Vector2(0, 0.5f), new Vector2(64, 0), new Vector2(560, 760), new Vector2(0, 0.5f));
            var header = ui.Label(selectPanel, "== YOUR DREAMERS ==", 36, TerminalUI.Dim);
            TerminalUI.Place(header.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -36), new Vector2(500, 44));
            accountLabel = ui.Label(selectPanel, "", 28, TerminalUI.Dim, TextAnchor.MiddleLeft);
            TerminalUI.Place(accountLabel.rectTransform, new Vector2(0.5f, 1), new Vector2(-60, -86), new Vector2(380, 36));
            var logout = ui.Button(selectPanel, "log out", 26, LogOut);
            TerminalUI.Place((RectTransform)logout.transform, new Vector2(0.5f, 1), new Vector2(190, -86), new Vector2(130, 40));

            for (int i = 0; i < AccountService.MaxCharacters; i++)
            {
                int slot = i;
                var b = ui.Button(selectPanel, "", 38, () => Select(slot));
                TerminalUI.Place((RectTransform)b.transform, new Vector2(0.5f, 1), new Vector2(0, -160 - i * 92), new Vector2(480, 80));
                var label = b.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleLeft;
                TerminalUI.Stretch(label.rectTransform, 24);
                slotRows.Add((b, label));
            }

            playButton = ui.Button(selectPanel, "play", 38, Play);
            playButton.GetComponent<Image>().color = TerminalUI.Accent;
            TerminalUI.Place((RectTransform)playButton.transform, new Vector2(0.5f, 0), new Vector2(-160, 120), new Vector2(150, 66));
            editButton = ui.Button(selectPanel, "edit", 38, Edit);
            TerminalUI.Place((RectTransform)editButton.transform, new Vector2(0.5f, 0), new Vector2(0, 120), new Vector2(150, 66));
            deleteButton = ui.Button(selectPanel, "delete", 34, Delete);
            TerminalUI.Place((RectTransform)deleteButton.transform, new Vector2(0.5f, 0), new Vector2(160, 120), new Vector2(150, 66));
            createButton = ui.Button(selectPanel, "make a new dreamer", 36, Create);
            createButton.GetComponent<Image>().color = TerminalUI.Accent;
            TerminalUI.Place((RectTransform)createButton.transform, new Vector2(0.5f, 0), new Vector2(0, 120), new Vector2(420, 66));

            selectStatus = ui.Label(selectPanel, "", 28, TerminalUI.Accent);
            TerminalUI.Place(selectStatus.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(520, 40));
        }

        public async void ShowSelect()
        {
            creator.Show(false);
            loginPanel.gameObject.SetActive(false);
            selectPanel.gameObject.SetActive(true);
            accountLabel.text = AccountService.Mode == AccountMode.Online ? "account: " + AccountService.Username : "offline (saved on this computer)";
            selectStatus.text = "loading...";
            records = await AccountService.LoadCharacters();
            selectStatus.text = "";
            int start = records.Count > 0 ? records[0].slot : 0;
            Select(AccountService.CurrentSlot >= 0 ? AccountService.CurrentSlot : start);
        }

        CharacterRecord RecordAt(int slot) => records.Find(r => r.slot == slot);

        void Select(int slot)
        {
            selected = slot;
            confirmDelete = false;
            var record = RecordAt(slot);
            // preview the character on the stage behind the menu
            CharacterSave.ApplyJson(appearance, record != null ? record.look : appearance.DefaultLook);
            for (int i = 0; i < slotRows.Count; i++)
            {
                var r = RecordAt(i);
                slotRows[i].label.text = $"{i + 1}  " + (r != null ? r.name : "+ empty");
                slotRows[i].label.color = r != null ? TerminalUI.Text : TerminalUI.Dim;
                slotRows[i].button.GetComponent<Image>().color = i == slot ? TerminalUI.Accent : Color.white;
            }
            bool filled = record != null;
            playButton.gameObject.SetActive(filled);
            editButton.gameObject.SetActive(filled);
            deleteButton.gameObject.SetActive(filled);
            deleteButton.GetComponentInChildren<Text>().text = "delete";
            createButton.gameObject.SetActive(!filled);
        }

        void Play()
        {
            var record = RecordAt(selected);
            if (record == null) return;
            AccountService.CurrentSlot = record.slot;
            AccountService.CurrentName = record.name;
            CharacterSave.ApplyJson(appearance, record.look);
            CharacterSave.Save(appearance);
            if (Application.CanStreamedLevelBeLoaded(worldScene))
                UnityEngine.SceneManagement.SceneManager.LoadScene(worldScene);
        }

        void Edit()
        {
            var record = RecordAt(selected);
            if (record == null) return;
            AccountService.CurrentSlot = record.slot;
            AccountService.CurrentName = record.name;
            OpenCreator();
        }

        void Create()
        {
            AccountService.CurrentSlot = selected;
            AccountService.CurrentName = "";
            CharacterSave.ApplyJson(appearance, appearance.DefaultLook);
            OpenCreator();
        }

        void OpenCreator()
        {
            selectPanel.gameObject.SetActive(false);
            creator.Show(true);
        }

        async void Delete()
        {
            if (!confirmDelete)
            {
                confirmDelete = true;
                deleteButton.GetComponentInChildren<Text>().text = "sure?";
                return;
            }
            selectStatus.text = "deleting...";
            await AccountService.DeleteCharacter(selected);
            if (AccountService.CurrentSlot == selected) AccountService.CurrentSlot = -1;
            records = await AccountService.LoadCharacters();
            selectStatus.text = "";
            Select(selected);
        }

        void LogOut()
        {
            AccountService.SignOut();
            ShowLogin();
        }
    }
}
