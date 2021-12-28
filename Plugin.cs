using BepInEx;
using BepInEx.Configuration;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Color = UnityEngine.Color;
using Random = UnityEngine.Random;

namespace ULTRAHUD
{    
    [BepInPlugin("cap.ultrakill.ultrahud", "ULTRAHUD", "1.0.0")]
    [BepInProcess("ULTRAKILL.exe")]
    public class Plugin : BaseUnityPlugin
    {
        private string hudsPath; // where the HUDs are stored
        private int hudFilePointer, hudSettingsPointer; // loadingPointer: points at what HUD to load, hudSettingsPointer: pointer for new HUD settings
        private bool loadingHUD; // is a HUD being loaded?
        private Rect windowRect = new Rect(Screen.width / 2 - 330, Screen.height / 2 - 375, 660, 750);
        private bool enableGUI, inMenu, newHUD, editHUD, delHUD; // enableGUI: show the ULTRAHUD menu and its sub-menus, inMenu: tells whether the player is in the ULTRAHUD menu, others are for what sub-menu the player selected
        private bool loadHUDAtStart = true; // checks for loading the pointer'th HUD when entering a level
        private ConfigEntry<int> pointerConfig; // saves pointer so when the player boots the game up, the same HUD will be loaded
        private Texture2D dreamed; // dreamed
        private Dictionary<int, object[]> newHUDSettings = new Dictionary<int, object[]>
        {
            {0, new object[] {"HEALTH", 1, 0, 0}},
            {1, new object[] {"HEALTH NUMBER", 1, 1, 1}},
            {2, new object[] {"SOFT DAMAGE", 1, .39f, 0}},
            {3, new object[] {"HARD DAMAGE", .35f, .35f, .35f}},
            {4, new object[] {"OVERHEAL", 0, 1, 0}},
            {5, new object[] {"STAMINA (FULL)", 0, .87f, 1}},
            {6, new object[] {"STAMINA (CHARGING)", 0, .87f, 1}},
            {7, new object[] {"STAMINA (EMPTY)", 1, 0, 0}},
            {8, new object[] {"RAILCANNON (FULL)", .25f, .91f, 1}},
            {9, new object[] {"RAILCANNON (CHARGING)", 1, 0, 0}},
            {10, new object[] {"BLUE VARIATION", .25f, .91f, 1}},
            {11, new object[] {"GREEN VARIATION", .27f, 1, .27f}},
            {12, new object[] {"RED VARIATION", 1, .24f, .24f}},
            {13, new object[] {"GOLD VARIATION", 1, .88f, .24f}}
        }, editHUDSettings = new Dictionary<int, object[]>
        {
            {0, new object[] {"HEALTH", 1, 0, 0}},
            {1, new object[] {"HEALTH NUMBER", 1, 1, 1}},
            {2, new object[] {"SOFT DAMAGE", 1, .39f, 0}},
            {3, new object[] {"HARD DAMAGE", .35f, .35f, .35f}},
            {4, new object[] {"OVERHEAL", 0, 1, 0}},
            {5, new object[] {"STAMINA (FULL)", 0, .87f, 1}},
            {6, new object[] {"STAMINA (CHARGING)", 0, .87f, 1}},
            {7, new object[] {"STAMINA (EMPTY)", 1, 0, 0}},
            {8, new object[] {"RAILCANNON (FULL)", .25f, .91f, 1}},
            {9, new object[] {"RAILCANNON (CHARGING)", 1, 0, 0}},
            {10, new object[] {"BLUE VARIATION", .25f, .91f, 1}},
            {11, new object[] {"GREEN VARIATION", .27f, 1, .27f}},
            {12, new object[] {"RED VARIATION", 1, .24f, .24f}},
            {13, new object[] {"GOLD VARIATION", 1, .88f, .24f}}
        };
        private Color newColor = new Color(1, 1, 1, 1); // preview color for current selected setting in the new/edit HUD sub-menu
        private string newHexColor = "#FF0000", newRgbColor = "255,0,0", editHexColor, editRgbColor; // variables used to change current HUD setting
        private GUIStyle labelStyle, buttonStyle, textFieldStyle;
        private float[] settingsCache = {0, 0, 0}; // cache for newHUDSettings, used to leave hex/newRgbColor unchanged unless the sliders are changing values
        private string newHUDName = "NEW HUD", editHUDName = "TEST HUD", saveHUDMessage; // saveHUDMessage: when the new HUD is saved, this message will pop up
        private Font vcrFont, broshKFont;
        private bool displaySaveHUDMessage; // check for whether to display save HUD message or not
        private int deleteHUDConfirmations = 2; // how many times the user has to confirm HUD deletion

        private void Awake()
        {
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

            // retrieve pointer
            pointerConfig = Config.Bind("Pointer", "Value", 0, "Next time the game loads, this will be the HUD that's loaded.");
            hudFilePointer = pointerConfig.Value;
            
            // get hudsPath
            List<string> folders = Path.GetFullPath(@"ULTRAKILL.exe").Split('\\').ToList();
            folders.RemoveAt(folders.Count - 1);
            folders.AddRange(new string[] {"BepInEx", "plugins", "ULTRAHUD", "HUDs"});
            hudsPath = String.Join("\\", folders.ToArray());
            Debug.Log($"hudsPath: {hudsPath}");
            Directory.CreateDirectory(hudsPath);

            // check if loadingPointer exceeds number of HUDs
            if (Directory.GetFiles(hudsPath).Length <= hudFilePointer)
                hudFilePointer = 0;

            SceneManager.activeSceneChanged += OnSceneChanged;
        }

        private void Start()
        {
            // assign HUD sub-menu images
            dreamed = new Texture2D(990, 990, TextureFormat.RGBA32, false);
            dreamed.LoadImage(File.ReadAllBytes(@$"{Directory.GetCurrentDirectory()}\BepInEx\plugins\ULTRAHUD\Assets\dreamed.jpg"));

            // assign fonts
            Font[] fonts = AssetBundle.LoadFromFile(@$"{Directory.GetCurrentDirectory()}\BepInEx\plugins\ULTRAHUD\Assets\Asset Bundles\font").LoadAllAssets<Font>();
            broshKFont = fonts[0];
            vcrFont = fonts[1];
        }
        
        private void OnSceneChanged(Scene from, Scene to)
        {
            // reset variables
            inMenu = false;
            enableGUI = false;
            newHUD = false;
            editHUD = false;
            delHUD = false;
            displaySaveHUDMessage = false;
            newHUDName = RandomString(8);
            loadHUDAtStart = true;
        }

        private void Update()
        {
            // load HUD if player just enterd a level
            if (loadHUDAtStart && 
                (SceneManager.GetActiveScene().name.StartsWith("Level") || SceneManager.GetActiveScene().name.StartsWith("Endless")))
            {
                if (Directory.GetFiles(hudsPath).Length == 0)
                    StartCoroutine(SaveNewHUD(newHUDName));
                loadHUDAtStart = false;
                StartCoroutine(LoadHUD(hudFilePointer));
            }

            // load and save HUDs            
            if (Input.GetKeyDown(KeyCode.J) && !loadingHUD)
            {
                hudFilePointer--;
                StartCoroutine(LoadHUD(hudFilePointer));
            }
            else if (Input.GetKeyDown(KeyCode.L) && !loadingHUD)
            {
                hudFilePointer++;
                StartCoroutine(LoadHUD(hudFilePointer));
            }
            // open ultrakill menu
            else if (Input.GetKeyDown(KeyCode.K))
            {
                if (!loadingHUD && MonoSingleton<OptionsManager>.Instance.paused &&
                SceneManager.GetActiveScene().name.StartsWith("Level") || SceneManager.GetActiveScene().name.StartsWith("Endless"))
                {
                    MonoSingleton<OptionsManager>.Instance.pauseMenu.SetActive(false);
                    enableGUI = true;
                }
            }
            // if in ULTRAHUD menu and press escape
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                inMenu = false;
                enableGUI = false;
                newHUD = false;
                editHUD = false;
                delHUD = false;
                displaySaveHUDMessage = false;
            }
        }

        private IEnumerator DeleteHUD(int pointer)
        {
            loadingHUD = true;
            string[] files = Directory.GetFiles(hudsPath);
            if (files.Length == 0)
                yield break;
            File.Delete(files[pointer]);
            pointerConfig.Value = hudFilePointer;
            loadingHUD = false;

            yield break;
        }

        private IEnumerator SaveNewHUD(string hudName)
        {
            loadingHUD = true;
            Color[] colors = new Color[14];

            // get colors from settings into an array, delay it so the game doesn't crash 
            colors[0] = MonoSingleton<ColorBlindSettings>.Instance.GetHudColor(HudColorType.health);
            yield return new WaitForSeconds(0.005f);
            colors[1] = MonoSingleton<ColorBlindSettings>.Instance.GetHudColor(HudColorType.healthAfterImage);
            yield return new WaitForSeconds(0.005f);
            colors[2] = MonoSingleton<ColorBlindSettings>.Instance.GetHudColor(HudColorType.antiHp);
            yield return new WaitForSeconds(0.005f);
            colors[3] = MonoSingleton<ColorBlindSettings>.Instance.GetHudColor(HudColorType.overheal);
            yield return new WaitForSeconds(0.005f);
            colors[4] = MonoSingleton<ColorBlindSettings>.Instance.GetHudColor(HudColorType.healthText);
            yield return new WaitForSeconds(0.005f);
            colors[5] = MonoSingleton<ColorBlindSettings>.Instance.GetHudColor(HudColorType.stamina);
            yield return new WaitForSeconds(0.005f);
            colors[6] = MonoSingleton<ColorBlindSettings>.Instance.GetHudColor(HudColorType.staminaCharging);
            yield return new WaitForSeconds(0.005f);
            colors[7] = MonoSingleton<ColorBlindSettings>.Instance.GetHudColor(HudColorType.staminaEmpty);
            yield return new WaitForSeconds(0.005f);
            colors[8] = MonoSingleton<ColorBlindSettings>.Instance.GetHudColor(HudColorType.railcannonFull);
            yield return new WaitForSeconds(0.005f);
            colors[9] = MonoSingleton<ColorBlindSettings>.Instance.GetHudColor(HudColorType.railcannonCharging);
            yield return new WaitForSeconds(0.005f);
            colors[10] = MonoSingleton<ColorBlindSettings>.Instance.variationColors[0];
            yield return new WaitForSeconds(0.005f);
            colors[11] = MonoSingleton<ColorBlindSettings>.Instance.variationColors[1];
            yield return new WaitForSeconds(0.005f);
            colors[12] = MonoSingleton<ColorBlindSettings>.Instance.variationColors[2];
            yield return new WaitForSeconds(0.005f);
            colors[13] = MonoSingleton<ColorBlindSettings>.Instance.variationColors[3];
            yield return new WaitForSeconds(0.005f);
            for (int i = 0; i < 14; i++)
            {
                colors[i].a = 1;
            }

            // create image and save
            Texture2D t2d = new Texture2D(14, 1);
            for (int i = 0; i < 14; i++)
            {
                t2d.SetPixel(i, 0, colors[i]);
            }
            string[] files = Directory.GetFiles(hudsPath);
            File.WriteAllBytes(hudsPath + $@"\{hudName}.png", t2d.EncodeToPNG());
            Debug.Log("successfully written file, yay");
            hudFilePointer = files.Length - 1;
            LoadHUDSync(hudFilePointer);
            pointerConfig.Value = hudFilePointer;
            loadingHUD = false;
            while (true)
            {
                if (!MonoSingleton<OptionsManager>.Instance.paused)
                {
                    inMenu = false;
                    displaySaveHUDMessage = false;
                    hudName = RandomString(8);
                    break;
                }
            }

            yield break;
        }

        private IEnumerator LoadHUD(int pointer)
        {
            // if there are no files
            if (!Directory.EnumerateFileSystemEntries(hudsPath).Any())
            {
                hudFilePointer = 0;
                Debug.Log("no files");
                yield break;
            }
            // if the player isn't in a level
            if (!SceneManager.GetActiveScene().name.StartsWith("Level") && !SceneManager.GetActiveScene().name.StartsWith("Endless"))
                yield break;
            
            loadingHUD = true;
            string[] files = Directory.GetFiles(hudsPath);
            Debug.Log($"files count: {files.Length}");
            if (pointer < 0)
                pointer = files.Length - 1;

            if (pointer >= files.Length)
                pointer = 0;
            hudFilePointer = pointer;
            pointerConfig.Value = hudFilePointer;

            // get HUD from current pointer
            Debug.Log($"pointer: {pointer}");
            Texture2D t2d = new Texture2D(14, 1);
            t2d.LoadImage(File.ReadAllBytes(files[pointer]));
            // apply colors to game settings
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.health, t2d.GetPixel(0, 0));
            yield return new WaitForSeconds(0.005f);
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.healthAfterImage, t2d.GetPixel(1, 0));
            yield return new WaitForSeconds(0.005f);
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.antiHp, t2d.GetPixel(2, 0));
            yield return new WaitForSeconds(0.005f);
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.overheal, t2d.GetPixel(3, 0));
            yield return new WaitForSeconds(0.005f);
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.healthText, t2d.GetPixel(4, 0));
            yield return new WaitForSeconds(0.005f);
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.stamina, t2d.GetPixel(5, 0));
            yield return new WaitForSeconds(0.005f);
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.staminaCharging, t2d.GetPixel(6, 0));
            yield return new WaitForSeconds(0.005f);
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.staminaEmpty, t2d.GetPixel(7, 0));
            yield return new WaitForSeconds(0.005f);
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.railcannonFull, t2d.GetPixel(8, 0));
            yield return new WaitForSeconds(0.005f);
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.railcannonCharging, t2d.GetPixel(9, 0));
            yield return new WaitForSeconds(0.005f);
            MonoSingleton<ColorBlindSettings>.Instance.variationColors[0] =  t2d.GetPixel(10, 0);
            yield return new WaitForSeconds(0.005f);
            MonoSingleton<ColorBlindSettings>.Instance.variationColors[1] =  t2d.GetPixel(11, 0);
            yield return new WaitForSeconds(0.005f);
            MonoSingleton<ColorBlindSettings>.Instance.variationColors[2] =  t2d.GetPixel(12, 0);
            yield return new WaitForSeconds(0.005f);
            MonoSingleton<ColorBlindSettings>.Instance.variationColors[3] =  t2d.GetPixel(13, 0);
            yield return new WaitForSeconds(0.005f);

            MonoSingleton<ColorBlindSettings>.Instance.UpdateHudColors();
            MonoSingleton<ColorBlindSettings>.Instance.UpdateWeaponColors();
            Debug.Log("loaded new hud");
            editHUDName = Path.GetFileNameWithoutExtension(files[pointer]);
            loadingHUD = false;
        }

        private void LoadHUDSync(int pointer)
        {
            // if there are no files
            if (!Directory.EnumerateFileSystemEntries(hudsPath).Any())
            {
                hudFilePointer = 0;
                Debug.Log("no files");
                return;
            }
            // if the player isn't in a level
            if (!SceneManager.GetActiveScene().name.StartsWith("Level") && !SceneManager.GetActiveScene().name.StartsWith("Endless"))
                return;
            
            loadingHUD = true;
            string[] files = Directory.GetFiles(hudsPath);
            Debug.Log($"files count: {files.Length}");
            if (pointer < 0)
                pointer = files.Length - 1;

            if (pointer >= files.Length)
                pointer = 0;
            hudFilePointer = pointer;
            pointerConfig.Value = hudFilePointer;

            // get HUD from current pointer
            Debug.Log($"pointer: {pointer}");
            Texture2D t2d = new Texture2D(14, 1);
            t2d.LoadImage(File.ReadAllBytes(files[pointer]));
            // apply colors to game settings
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.health, t2d.GetPixel(0, 0));
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.healthAfterImage, t2d.GetPixel(1, 0));
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.antiHp, t2d.GetPixel(2, 0));
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.overheal, t2d.GetPixel(3, 0));
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.healthText, t2d.GetPixel(4, 0));
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.stamina, t2d.GetPixel(5, 0));
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.staminaCharging, t2d.GetPixel(6, 0));
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.staminaEmpty, t2d.GetPixel(7, 0));
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.railcannonFull, t2d.GetPixel(8, 0));
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.railcannonCharging, t2d.GetPixel(9, 0));
            MonoSingleton<ColorBlindSettings>.Instance.variationColors[0] =  t2d.GetPixel(10, 0);
            MonoSingleton<ColorBlindSettings>.Instance.variationColors[1] =  t2d.GetPixel(11, 0);
            MonoSingleton<ColorBlindSettings>.Instance.variationColors[2] =  t2d.GetPixel(12, 0);
            MonoSingleton<ColorBlindSettings>.Instance.variationColors[3] =  t2d.GetPixel(13, 0);

            MonoSingleton<ColorBlindSettings>.Instance.UpdateHudColors();
            MonoSingleton<ColorBlindSettings>.Instance.UpdateWeaponColors();
            Debug.Log("loaded new hud");
            editHUDName = Path.GetFileNameWithoutExtension(files[pointer]);
            loadingHUD = false;
        } 

        private string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789()-_";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[Random.Range(0, s.Length)]).ToArray());
        }

        private void ApplyHUDColors(Dictionary<int, object[]> settings)
        {
            // get colors from current settings and apply them
            Color col = new Color(float.Parse(settings[0][1].ToString()), float.Parse(settings[0][2].ToString()), float.Parse(settings[0][3].ToString()));
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.health, col);
            col = new Color(float.Parse(settings[1][1].ToString()), float.Parse(settings[1][2].ToString()), float.Parse(settings[1][3].ToString()));
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.healthText, col);
            col = new Color(float.Parse(settings[2][1].ToString()), float.Parse(settings[2][2].ToString()), float.Parse(settings[2][3].ToString()));
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.healthAfterImage, col);
            col = new Color(float.Parse(settings[3][1].ToString()), float.Parse(settings[3][2].ToString()), float.Parse(settings[3][3].ToString()));
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.antiHp, col);
            col = new Color(float.Parse(settings[4][1].ToString()), float.Parse(settings[4][2].ToString()), float.Parse(settings[4][3].ToString()));
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.overheal, col);
            col = new Color(float.Parse(settings[5][1].ToString()), float.Parse(settings[5][2].ToString()), float.Parse(settings[5][3].ToString()));
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.stamina, col);
            col = new Color(float.Parse(settings[6][1].ToString()), float.Parse(settings[6][2].ToString()), float.Parse(settings[6][3].ToString()));
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.staminaCharging, col);
            col = new Color(float.Parse(settings[7][1].ToString()), float.Parse(settings[7][2].ToString()), float.Parse(settings[7][3].ToString()));
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.staminaEmpty, col);
            col = new Color(float.Parse(settings[8][1].ToString()), float.Parse(settings[8][2].ToString()), float.Parse(settings[8][3].ToString()));
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.railcannonFull, col);
            col = new Color(float.Parse(settings[9][1].ToString()), float.Parse(settings[9][2].ToString()), float.Parse(settings[9][3].ToString()));
            MonoSingleton<ColorBlindSettings>.Instance.SetHudColor(HudColorType.railcannonCharging, col);
            col = new Color(float.Parse(settings[10][1].ToString()), float.Parse(settings[10][2].ToString()), float.Parse(settings[10][3].ToString()));
            MonoSingleton<ColorBlindSettings>.Instance.variationColors[0] = col;
            col = new Color(float.Parse(settings[11][1].ToString()), float.Parse(settings[11][2].ToString()), float.Parse(settings[11][3].ToString()));
            MonoSingleton<ColorBlindSettings>.Instance.variationColors[1] = col;
            col = new Color(float.Parse(settings[12][1].ToString()), float.Parse(settings[12][2].ToString()), float.Parse(settings[12][3].ToString()));
            MonoSingleton<ColorBlindSettings>.Instance.variationColors[2] = col;
            col = new Color(float.Parse(settings[13][1].ToString()), float.Parse(settings[13][2].ToString()), float.Parse(settings[13][3].ToString()));
            MonoSingleton<ColorBlindSettings>.Instance.variationColors[3] = col;

            // update colors
            MonoSingleton<ColorBlindSettings>.Instance.UpdateHudColors();
            MonoSingleton<ColorBlindSettings>.Instance.UpdateWeaponColors();
        }

        private void AssignEditHUDValues()
        {
            // assign currently loaded HUD to editHUDSettings
            Color col = MonoSingleton<ColorBlindSettings>.Instance.GetHudColor(HudColorType.health);
            editHUDSettings[0][1] = col.r;
            editHUDSettings[0][2] = col.g;
            editHUDSettings[0][3] = col.b;
            col = MonoSingleton<ColorBlindSettings>.Instance.GetHudColor(HudColorType.healthText);
            editHUDSettings[1][1] = col.r;
            editHUDSettings[1][2] = col.g;
            editHUDSettings[1][3] = col.b;
            col = MonoSingleton<ColorBlindSettings>.Instance.GetHudColor(HudColorType.healthAfterImage);
            editHUDSettings[2][1] = col.r;
            editHUDSettings[2][2] = col.g;
            editHUDSettings[2][3] = col.b;
            col = MonoSingleton<ColorBlindSettings>.Instance.GetHudColor(HudColorType.antiHp);
            editHUDSettings[3][1] = col.r;
            editHUDSettings[3][2] = col.g;
            editHUDSettings[3][3] = col.b;
            col = MonoSingleton<ColorBlindSettings>.Instance.GetHudColor(HudColorType.overheal);
            editHUDSettings[4][1] = col.r;
            editHUDSettings[4][2] = col.g;
            editHUDSettings[4][3] = col.b;
            col = MonoSingleton<ColorBlindSettings>.Instance.GetHudColor(HudColorType.stamina);
            editHUDSettings[5][1] = col.r;
            editHUDSettings[5][2] = col.g;
            editHUDSettings[5][3] = col.b;
            col = MonoSingleton<ColorBlindSettings>.Instance.GetHudColor(HudColorType.staminaCharging);
            editHUDSettings[6][1] = col.r;
            editHUDSettings[6][2] = col.g;
            editHUDSettings[6][3] = col.b;
            col = MonoSingleton<ColorBlindSettings>.Instance.GetHudColor(HudColorType.staminaEmpty);
            editHUDSettings[7][1] = col.r;
            editHUDSettings[7][2] = col.g;
            editHUDSettings[7][3] = col.b;
            col = MonoSingleton<ColorBlindSettings>.Instance.GetHudColor(HudColorType.railcannonFull);
            editHUDSettings[8][1] = col.r;
            editHUDSettings[8][2] = col.g;
            editHUDSettings[8][3] = col.b;
            col = MonoSingleton<ColorBlindSettings>.Instance.GetHudColor(HudColorType.railcannonCharging);
            editHUDSettings[9][1] = col.r;
            editHUDSettings[9][2] = col.g;
            editHUDSettings[9][3] = col.b;
            col = MonoSingleton<ColorBlindSettings>.Instance.variationColors[0];
            editHUDSettings[10][1] = col.r;
            editHUDSettings[10][2] = col.g;
            editHUDSettings[10][3] = col.b;
            col = MonoSingleton<ColorBlindSettings>.Instance.variationColors[1];
            editHUDSettings[11][1] = col.r;
            editHUDSettings[11][2] = col.g;
            editHUDSettings[11][3] = col.b;
            col = MonoSingleton<ColorBlindSettings>.Instance.variationColors[2];
            editHUDSettings[12][1] = col.r;
            editHUDSettings[12][2] = col.g;
            editHUDSettings[12][3] = col.b;
            col = MonoSingleton<ColorBlindSettings>.Instance.variationColors[3];
            editHUDSettings[13][1] = col.r;
            editHUDSettings[13][2] = col.g;
            editHUDSettings[13][3] = col.b;

            col = new Color(float.Parse(editHUDSettings[hudSettingsPointer][1].ToString()), float.Parse(editHUDSettings[hudSettingsPointer][2].ToString()), float.Parse(editHUDSettings[hudSettingsPointer][3].ToString()));
            editHexColor = ColorUtility.ToHtmlStringRGB(col); // change hex value
            editRgbColor = $"{Mathf.RoundToInt(col.r * 255)},{Mathf.RoundToInt(col.g * 255)},{Mathf.RoundToInt(col.b * 255)}"; // change rgb value
        }

        private void OnGUI()
        {
            // set styles
            labelStyle = new GUIStyle(GUI.skin.label);
            buttonStyle = new GUIStyle(GUI.skin.button);
            textFieldStyle = new GUIStyle(GUI.skin.textField);
            labelStyle.alignment = TextAnchor.MiddleCenter;
            labelStyle.font = vcrFont;
            labelStyle.fontSize = 40;
            buttonStyle.font = vcrFont;
            buttonStyle.fontSize = 40;
            buttonStyle.alignment = TextAnchor.MiddleCenter;
            textFieldStyle.font = vcrFont;
            textFieldStyle.fontSize = 20;
            textFieldStyle.alignment = TextAnchor.MiddleCenter;

            // draw ULTRAHUD menu
            if (enableGUI && Directory.GetFiles(hudsPath).Length != 0)
            {
                GUI.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 1);
                windowRect = GUILayout.Window(0, windowRect, DrawWindow, "", GUILayout.MaxWidth(660), GUILayout.MaxHeight(750));
            }
        }

        private void DrawWindow(int id)
        {
            GUI.backgroundColor = Color.black;
            switch (id)
            {
                case 0:
                    // if player is in the main ULTRAHUD menu
                    if (!inMenu)
                    {
                        GUI.backgroundColor = new Color(0, 0, 0, 0);
                        labelStyle.font = broshKFont;
                        labelStyle.fontSize = 160;
                        GUILayout.Label("<color=#ffd700>U</color><color=#FFF09F>LTRAH</color><color=#ffd700>U</color><color=#FFF09F>D</color>", labelStyle, GUILayout.MaxHeight(160), GUILayout.MaxWidth(646));
                        labelStyle.fontSize = 40;
                        labelStyle.font = vcrFont;
                        GUI.backgroundColor = Color.black;
                        GUILayout.Space(165);
                        if (GUILayout.Button("NEW HUD", buttonStyle, GUILayout.MaxWidth(646), GUILayout.MaxHeight(60)))
                        {
                            inMenu = true;
                            newHUD = true;
                        }
                        else if (GUILayout.Button("EDIT HUD<color=red><size=20> (NOT IMPLEMENTED)</size></color>", buttonStyle, GUILayout.MaxWidth(646), GUILayout.MaxHeight(60)))
                        {
                            inMenu = true;
                            editHUD = true;
                            AssignEditHUDValues();
                        }
                        // GUILayout.Space(15);
                        if (GUILayout.Button("<color=red>DELETE HUD</color>", buttonStyle, GUILayout.MaxWidth(646), GUILayout.MaxHeight(60)))
                        {
                            inMenu = true;
                            delHUD = true;
                        }
                        GUILayout.FlexibleSpace();
                    }
                    // if player is in new HUD menu
                    else if (newHUD)
                    {
                        // wrap settings pointer
                        if (hudSettingsPointer < 0)
                            hudSettingsPointer = newHUDSettings.Count - 1;
                        if (hudSettingsPointer >= newHUDSettings.Count)
                            hudSettingsPointer = 0;

                        // update display color
                        newColor = new Color
                            (float.Parse(newHUDSettings[hudSettingsPointer][1].ToString()),
                            float.Parse(newHUDSettings[hudSettingsPointer][2].ToString()),
                            float.Parse(newHUDSettings[hudSettingsPointer][3].ToString()));

                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();

                        // go back to ULTRAHUD menu
                        buttonStyle.fontSize = 30;
                        if (GUILayout.Button("BACK", buttonStyle, GUILayout.MaxWidth(100), GUILayout.MaxHeight(40)))
                        {
                            inMenu = false;
                            newHUD = false;
                            editHUD = false;
                            delHUD = false;
                            displaySaveHUDMessage = false;
                        }
                        buttonStyle.fontSize = 40;
                        GUILayout.EndHorizontal();
                        
                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        string newHUDNameCache = newHUDName; // cache for checking whether to display save message or not
                        newHUDName = GUILayout.TextField(
                            newHUDName, 25, textFieldStyle, GUILayout.MaxWidth(646), GUILayout.MaxHeight(60))
                            .ToUpper();
                        GUI.contentColor = Color.white;
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();
                        
                        GUILayout.Space(10);

                        // switch settings
                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("<", buttonStyle, GUILayout.MaxWidth(50), GUILayout.MaxHeight(50)))
                        {
                            hudSettingsPointer--;
                            newColor = new Color
                                (float.Parse(newHUDSettings[hudSettingsPointer][1].ToString()),
                                float.Parse(newHUDSettings[hudSettingsPointer][2].ToString()),
                                float.Parse(newHUDSettings[hudSettingsPointer][3].ToString()));
                            newHexColor = $"#{ColorUtility.ToHtmlStringRGB(newColor)}";
                            newRgbColor = $"{Mathf.FloorToInt(newColor.r * 255)},{Mathf.FloorToInt(newColor.g * 255)},{Mathf.FloorToInt(newColor.b * 255)}";
                        }
                        
                        GUI.contentColor = newColor;
                        GUILayout.Label((string)newHUDSettings[hudSettingsPointer][0], labelStyle, GUILayout.MaxWidth(490), GUILayout.MaxHeight(50));
                        GUI.contentColor = Color.white;
                        if (GUILayout.Button(">", buttonStyle, GUILayout.MaxWidth(50), GUILayout.MaxHeight(50)))
                        {
                            hudSettingsPointer++;
                            newColor = new Color
                                (float.Parse(newHUDSettings[hudSettingsPointer][1].ToString()),
                                float.Parse(newHUDSettings[hudSettingsPointer][2].ToString()),
                                float.Parse(newHUDSettings[hudSettingsPointer][3].ToString()));
                            newHexColor = $"#{ColorUtility.ToHtmlStringRGB(newColor)}";
                            newRgbColor = $"{Mathf.FloorToInt(newColor.r * 255)},{Mathf.FloorToInt(newColor.g * 255)},{Mathf.FloorToInt(newColor.b * 255)}";
                        }   
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();
                        
                        GUILayout.Space(15);

                        // rgb 0.00-1.00 sliders
                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        GUILayout.BeginVertical();
                        GUI.contentColor = Color.red;
                        GUILayout.Label("R ", labelStyle, GUILayout.MaxHeight(40));
                        GUI.contentColor = Color.green;
                        GUILayout.Label("G ", labelStyle, GUILayout.MaxHeight(40));
                        GUI.contentColor = Color.blue;
                        GUILayout.Label("B ", labelStyle, GUILayout.MaxHeight(40));
                        GUI.contentColor = Color.white;
                        GUILayout.EndVertical();
                        GUILayout.BeginVertical();
                        GUILayout.Space(20);
                        GUI.backgroundColor = Color.red;
                        newHUDSettings[hudSettingsPointer][1] = GUILayout.HorizontalSlider(float.Parse(newHUDSettings[hudSettingsPointer][1].ToString()), 0, 1, GUILayout.MaxHeight(40), GUILayout.MaxWidth(420));
                        GUI.backgroundColor = Color.green;
                        newHUDSettings[hudSettingsPointer][2] = GUILayout.HorizontalSlider(float.Parse(newHUDSettings[hudSettingsPointer][2].ToString()), 0, 1, GUILayout.MaxHeight(40), GUILayout.MaxWidth(420));
                        GUI.backgroundColor = Color.blue;
                        newHUDSettings[hudSettingsPointer][3] = GUILayout.HorizontalSlider(float.Parse(newHUDSettings[hudSettingsPointer][3].ToString()), 0, 1, GUILayout.MaxHeight(40), GUILayout.MaxWidth(420));
                        GUI.backgroundColor = Color.black;
                        if (float.Parse(newHUDSettings[hudSettingsPointer][1].ToString()) != settingsCache[0] || float.Parse(newHUDSettings[hudSettingsPointer][2].ToString()) != settingsCache[1] || float.Parse(newHUDSettings[hudSettingsPointer][3].ToString()) != settingsCache[2])
                        {
                            newColor = new Color
                                (float.Parse(newHUDSettings[hudSettingsPointer][1].ToString()),
                                float.Parse(newHUDSettings[hudSettingsPointer][2].ToString()),
                                float.Parse(newHUDSettings[hudSettingsPointer][3].ToString()));
                            newHexColor = $"#{ColorUtility.ToHtmlStringRGB(newColor)}";
                            newRgbColor = $"{Mathf.FloorToInt(newColor.r * 255)},{Mathf.FloorToInt(newColor.g * 255)},{Mathf.FloorToInt(newColor.b * 255)}";
                        }
                        settingsCache[0] = float.Parse(newHUDSettings[hudSettingsPointer][1].ToString());
                        settingsCache[1] = float.Parse(newHUDSettings[hudSettingsPointer][2].ToString());
                        settingsCache[2] = float.Parse(newHUDSettings[hudSettingsPointer][3].ToString());

                        GUILayout.EndVertical();

                        GUILayout.Space(20);

                        GUILayout.BeginVertical();
                        GUILayout.Label(newColor.r.ToString("0.00"), labelStyle, GUILayout.MaxHeight(40));
                        GUILayout.Label(newColor.g.ToString("0.00"), labelStyle, GUILayout.MaxHeight(40));
                        GUILayout.Label(newColor.b.ToString("0.00"), labelStyle, GUILayout.MaxHeight(40));
                        GUILayout.EndVertical();
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();

                        GUILayout.Space(2);

                        // hex and rgb 0-255 inputs
                        GUILayout.BeginHorizontal();
                        bool pressed = false;
                        GUILayout.FlexibleSpace();
                        try
                        {
                            newHexColor = GUILayout.TextField($"#{newHexColor.Substring(1)}", 7, textFieldStyle, GUILayout.MaxHeight(50), GUILayout.MaxWidth(100)).ToUpper();
                        }   
                        catch
                        {
                            newHexColor = "#";
                        }
                        GUILayout.BeginVertical();
                        GUILayout.Space(15f);
                        buttonStyle.fontSize = 20;
                        pressed = GUILayout.Button("APPLY", buttonStyle, GUILayout.MaxWidth(75), GUILayout.MaxHeight(30));
                        buttonStyle.fontSize = 40;
                        GUILayout.Space(15f);
                        GUILayout.EndVertical();
                        if (newHexColor.Length == 7 && ColorUtility.TryParseHtmlString(newHexColor, out Color nc) && pressed)
                        {
                            newColor = nc;
                            newHUDSettings[hudSettingsPointer][1] = newColor.r;
                            newHUDSettings[hudSettingsPointer][2] = newColor.g;
                            newHUDSettings[hudSettingsPointer][3] = newColor.b;
                        }

                        GUILayout.Space(15);

                        bool isRgbValid = true;
                        newRgbColor = GUILayout.TextField(newRgbColor, 11, textFieldStyle, GUILayout.MaxHeight(50), GUILayout.MaxWidth(150));
                        string[] rgbs = newRgbColor.Split(',');
                        float[] vals = new float[3] {-1, -1, -1};
                        if (rgbs.Length == 3)
                        {
                            for (int i = 0; i < 3; i++)
                            {
                                if (int.TryParse(rgbs[i], out int x))
                                    vals[i] = x;
                                else
                                    isRgbValid = false;
                            }

                            for (int i = 0; i < 3; i++)
                                if (vals[i] == -1)
                                    isRgbValid = false;
                        }
                        else
                            isRgbValid = false;

                        pressed = false;
                        GUILayout.BeginVertical();
                        GUILayout.Space(15f);
                        buttonStyle.fontSize = 20;
                        pressed = GUILayout.Button("APPLY", buttonStyle, GUILayout.MaxWidth(75), GUILayout.MaxHeight(30));
                        buttonStyle.fontSize = 40;
                        GUILayout.Space(15f);
                        GUILayout.EndVertical();
                        if (isRgbValid && pressed)
                        {
                            newColor = new Color(float.Parse((vals[0]/255).ToString("F2")), float.Parse((vals[1]/255).ToString("F2")), float.Parse((vals[2]/255).ToString("F2")));
                            newHUDSettings[hudSettingsPointer][1] = newColor.r;
                            newHUDSettings[hudSettingsPointer][2] = newColor.g;
                            newHUDSettings[hudSettingsPointer][3] = newColor.b;
                        }
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();

                        GUILayout.Space(10);

                        // preview button
                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        buttonStyle.fontSize = 30;
                        if (GUILayout.Button("PREVIEW", buttonStyle, GUILayout.MaxHeight(60), GUILayout.MaxWidth(150)))
                            ApplyHUDColors(newHUDSettings);
                        buttonStyle.fontSize = 40;
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();

                        // save HUD
                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        buttonStyle.fontSize = 30;
                        if (GUILayout.Button($"SAVE AS {newHUDName}?", buttonStyle, GUILayout.MaxWidth(646), GUILayout.MaxHeight(60)))
                        {
                            // check for invalid characters
                            if (newHUDName == string.Empty
                                || newHUDName.Contains("\\")
                                || newHUDName.Contains("/")
                                || newHUDName.Contains("*")
                                || newHUDName.Contains("?")
                                || newHUDName.Contains("|")
                                || newHUDName.Contains("\"")
                                || newHUDName.Contains(":")
                                || newHUDName.Contains("<")
                                || newHUDName.Contains(">"))
                                saveHUDMessage = $"COULDN'T SAVE {newHUDName}, ONLY THE ALPHABET, NUMBERS AND SPACE ARE VALID CHARACTERS";
                            else
                            {
                                saveHUDMessage = $"{newHUDName} WILL BE SAVED ONCE YOU EXIT THE ULTRAHUD MENU.";
                                ApplyHUDColors(newHUDSettings);
                                StartCoroutine(SaveNewHUD(newHUDName));
                            }
                            displaySaveHUDMessage = true;
                        }
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();

                        // display save message
                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        if (newHUDNameCache == newHUDName && displaySaveHUDMessage)
                        {
                            labelStyle.fontSize = 20;
                            GUILayout.Label(saveHUDMessage, labelStyle, GUILayout.MinWidth(600), GUILayout.MinHeight(25));
                            labelStyle.fontSize = 40;
                        }
                        else
                            displaySaveHUDMessage = false;
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();

                        GUILayout.FlexibleSpace();

                        // dreamed :kekW:
                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        GUI.backgroundColor = new Color(0, 0, 0, 0);
                        GUI.contentColor = newColor;
                        GUILayout.Box(dreamed, GUILayout.MaxHeight(98), GUILayout.MaxWidth(635));
                        GUI.backgroundColor = Color.black;
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();
                    }
                    else if (editHUD)
                    {
                        GUI.backgroundColor = Color.black;

                        // wrap hudSettingsPointer
                        if (hudSettingsPointer < 0)
                            hudSettingsPointer = editHUDSettings.Count - 1;
                        if (hudSettingsPointer >= editHUDSettings.Count)
                            hudSettingsPointer = 0;

                        // set newColor
                        newColor = new Color
                            (float.Parse(editHUDSettings[hudSettingsPointer][1].ToString()),
                            float.Parse(editHUDSettings[hudSettingsPointer][2].ToString()),
                            float.Parse(editHUDSettings[hudSettingsPointer][3].ToString()));

                        // top of the menu
                        GUILayout.BeginHorizontal();
                        GUILayout.Space(100);
                        GUILayout.FlexibleSpace();
                        GUILayout.Label("EDIT HUD", labelStyle, GUILayout.MaxHeight(40), GUILayout.MaxWidth(400));
                        GUILayout.FlexibleSpace();
                        buttonStyle.fontSize = 30;
                        if (GUILayout.Button("BACK", buttonStyle, GUILayout.MaxWidth(100), GUILayout.MaxHeight(40)))
                        {
                            inMenu = false;
                            newHUD = false;
                            delHUD = false;
                            editHUD = false;
                            displaySaveHUDMessage = false;
                        }
                        buttonStyle.fontSize = 40;
                        GUILayout.EndHorizontal();
                        
                        GUILayout.Space(5);

                        // set HUD name and switch between HUDs
                        GUILayout.BeginHorizontal();
                        buttonStyle.fontSize = 50;
                        if (GUILayout.Button("<", buttonStyle, GUILayout.MinWidth(60), GUILayout.MinHeight(60)))
                        {
                            LoadHUDSync(--hudFilePointer);
                            AssignEditHUDValues();
                            ApplyHUDColors(editHUDSettings);
                        }
                        GUILayout.FlexibleSpace();
                        string editHUDNameCache = editHUDName;
                        editHUDName = GUILayout.TextField(
                            editHUDName, 25, textFieldStyle, GUILayout.MaxWidth(646), GUILayout.MaxHeight(60))
                            .ToUpper();
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button(">", buttonStyle, GUILayout.MinWidth(60), GUILayout.MinHeight(60)))
                        {
                            LoadHUDSync(++hudFilePointer);
                            AssignEditHUDValues();
                            ApplyHUDColors(editHUDSettings);
                        }
                        buttonStyle.fontSize = 40;
                        GUILayout.EndHorizontal();
                        
                        GUILayout.Space(20);

                        // switch settings
                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("<", buttonStyle, GUILayout.MaxWidth(50), GUILayout.MaxHeight(50)))
                        {
                            hudSettingsPointer--;
                            newColor = new Color
                                (float.Parse(editHUDSettings[hudSettingsPointer][1].ToString()),
                                float.Parse(editHUDSettings[hudSettingsPointer][2].ToString()),
                                float.Parse(editHUDSettings[hudSettingsPointer][3].ToString()));
                            editHexColor = $"#{ColorUtility.ToHtmlStringRGB(newColor)}";
                            editRgbColor = $"{Mathf.FloorToInt(newColor.r * 255)},{Mathf.FloorToInt(newColor.g * 255)},{Mathf.FloorToInt(newColor.b * 255)}";
                        }
                        
                        GUI.contentColor = newColor;
                        GUILayout.Label((string)editHUDSettings[hudSettingsPointer][0], labelStyle, GUILayout.MaxWidth(490), GUILayout.MaxHeight(50));
                        GUI.contentColor = Color.white;
                        if (GUILayout.Button(">", buttonStyle, GUILayout.MaxWidth(50), GUILayout.MaxHeight(50)))
                        {
                            hudSettingsPointer++;
                            newColor = new Color
                                (float.Parse(editHUDSettings[hudSettingsPointer][1].ToString()),
                                float.Parse(editHUDSettings[hudSettingsPointer][2].ToString()),
                                float.Parse(editHUDSettings[hudSettingsPointer][3].ToString()));
                            editHexColor = $"#{ColorUtility.ToHtmlStringRGB(newColor)}";
                            editRgbColor = $"{Mathf.FloorToInt(newColor.r * 255)},{Mathf.FloorToInt(newColor.g * 255)},{Mathf.FloorToInt(newColor.b * 255)}";
                        }   
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();
                        
                        GUILayout.Space(15);

                        // rgb 0.00-1.00 sliders
                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        GUILayout.BeginVertical();
                        GUI.contentColor = Color.red;
                        GUILayout.Label("R ", labelStyle, GUILayout.MaxHeight(40));
                        GUI.contentColor = Color.green;
                        GUILayout.Label("G ", labelStyle, GUILayout.MaxHeight(40));
                        GUI.contentColor = Color.blue;
                        GUILayout.Label("B ", labelStyle, GUILayout.MaxHeight(40));
                        GUI.contentColor = Color.white;
                        GUILayout.EndVertical();
                        GUILayout.BeginVertical();
                        GUILayout.Space(20);
                        GUI.backgroundColor = Color.red;
                        editHUDSettings[hudSettingsPointer][1] = GUILayout.HorizontalSlider(float.Parse(editHUDSettings[hudSettingsPointer][1].ToString()), 0, 1, GUILayout.MaxHeight(40), GUILayout.MaxWidth(420));
                        GUI.backgroundColor = Color.green;
                        editHUDSettings[hudSettingsPointer][2] = GUILayout.HorizontalSlider(float.Parse(editHUDSettings[hudSettingsPointer][2].ToString()), 0, 1, GUILayout.MaxHeight(40), GUILayout.MaxWidth(420));
                        GUI.backgroundColor = Color.blue;
                        editHUDSettings[hudSettingsPointer][3] = GUILayout.HorizontalSlider(float.Parse(editHUDSettings[hudSettingsPointer][3].ToString()), 0, 1, GUILayout.MaxHeight(40), GUILayout.MaxWidth(420));
                        GUI.backgroundColor = Color.black;
                        if (float.Parse(editHUDSettings[hudSettingsPointer][1].ToString()) != settingsCache[0] || float.Parse(editHUDSettings[hudSettingsPointer][2].ToString()) != settingsCache[1] || float.Parse(editHUDSettings[hudSettingsPointer][3].ToString()) != settingsCache[2])
                        {
                            newColor = new Color
                                (float.Parse(editHUDSettings[hudSettingsPointer][1].ToString()),
                                float.Parse(editHUDSettings[hudSettingsPointer][2].ToString()),
                                float.Parse(editHUDSettings[hudSettingsPointer][3].ToString()));
                            editHexColor = $"#{ColorUtility.ToHtmlStringRGB(newColor)}";
                            editRgbColor = $"{Mathf.FloorToInt(newColor.r * 255)},{Mathf.FloorToInt(newColor.g * 255)},{Mathf.FloorToInt(newColor.b * 255)}";
                        }
                        settingsCache[0] = float.Parse(editHUDSettings[hudSettingsPointer][1].ToString());
                        settingsCache[1] = float.Parse(editHUDSettings[hudSettingsPointer][2].ToString());
                        settingsCache[2] = float.Parse(editHUDSettings[hudSettingsPointer][3].ToString());

                        GUILayout.EndVertical();

                        GUILayout.Space(20);

                        GUILayout.BeginVertical();
                        GUILayout.Label(newColor.r.ToString("0.00"), labelStyle, GUILayout.MaxHeight(40));
                        GUILayout.Label(newColor.g.ToString("0.00"), labelStyle, GUILayout.MaxHeight(40));
                        GUILayout.Label(newColor.b.ToString("0.00"), labelStyle, GUILayout.MaxHeight(40));
                        GUILayout.EndVertical();
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();

                        GUILayout.Space(2);

                        // hex and rgb 0-255 inputs
                        GUILayout.BeginHorizontal();
                        bool pressed = false;
                        GUILayout.FlexibleSpace();
                        try
                        {
                            editHexColor = GUILayout.TextField($"#{editHexColor.Substring(1)}", 7, textFieldStyle, GUILayout.MaxHeight(50), GUILayout.MaxWidth(100)).ToUpper();
                        }   
                        catch
                        {
                            editHexColor = "#";
                        }
                        GUILayout.BeginVertical();
                        GUILayout.Space(15f);
                        buttonStyle.fontSize = 20;
                        pressed = GUILayout.Button("APPLY", buttonStyle, GUILayout.MaxWidth(75), GUILayout.MaxHeight(30));
                        buttonStyle.fontSize = 40;
                        GUILayout.Space(15f);
                        GUILayout.EndVertical();
                        if (editHexColor.Length == 7 && ColorUtility.TryParseHtmlString(editHexColor, out Color nc) && pressed)
                        {
                            newColor = nc;
                            editHUDSettings[hudSettingsPointer][1] = newColor.r;
                            editHUDSettings[hudSettingsPointer][2] = newColor.g;
                            editHUDSettings[hudSettingsPointer][3] = newColor.b;
                        }

                        GUILayout.Space(15);

                        bool isRgbValid = true;
                        editRgbColor = GUILayout.TextField(editRgbColor, 11, textFieldStyle, GUILayout.MaxHeight(50), GUILayout.MaxWidth(150));
                        string[] rgbs = editRgbColor.Split(',');
                        float[] vals = new float[3] {-1, -1, -1};
                        if (rgbs.Length == 3)
                        {
                            for (int i = 0; i < 3; i++)
                            {
                                if (int.TryParse(rgbs[i], out int x))
                                    vals[i] = x;
                                else
                                    isRgbValid = false;
                            }

                            for (int i = 0; i < 3; i++)
                                if (vals[i] == -1)
                                    isRgbValid = false;
                        }
                        else
                            isRgbValid = false;

                        pressed = false;
                        GUILayout.BeginVertical();
                        GUILayout.Space(15f);
                        buttonStyle.fontSize = 20;
                        pressed = GUILayout.Button("APPLY", buttonStyle, GUILayout.MaxWidth(75), GUILayout.MaxHeight(30));
                        buttonStyle.fontSize = 40;
                        GUILayout.Space(15f);
                        GUILayout.EndVertical();
                        if (isRgbValid && pressed)
                        {
                            newColor = new Color(float.Parse((vals[0]/255).ToString("F2")), float.Parse((vals[1]/255).ToString("F2")), float.Parse((vals[2]/255).ToString("F2")));
                            editHUDSettings[hudSettingsPointer][1] = newColor.r;
                            editHUDSettings[hudSettingsPointer][2] = newColor.g;
                            editHUDSettings[hudSettingsPointer][3] = newColor.b;
                        }
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();

                        GUILayout.Space(10);

                        // preview button
                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        buttonStyle.fontSize = 30;
                        if (GUILayout.Button("PREVIEW", buttonStyle, GUILayout.MaxHeight(60), GUILayout.MaxWidth(150)))
                            ApplyHUDColors(editHUDSettings);
                        buttonStyle.fontSize = 40;
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();

                        // save HUD
                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        buttonStyle.fontSize = 30;
                        if (GUILayout.Button($"SAVE AS {editHUDName}?", buttonStyle, GUILayout.MaxWidth(646), GUILayout.MaxHeight(60)))
                        {
                            // check for invalid characters
                            if (editHUDName == string.Empty
                                || editHUDName.Contains("\\")
                                || editHUDName.Contains("/")
                                || editHUDName.Contains("*")
                                || editHUDName.Contains("?")
                                || editHUDName.Contains("|")
                                || editHUDName.Contains("\"")
                                || editHUDName.Contains(":")
                                || editHUDName.Contains("<")
                                || editHUDName.Contains(">"))
                                saveHUDMessage = $"COULDN'T SAVE {editHUDName}, ONLY THE ALPHABET, NUMBERS AND SPACE ARE VALID CHARACTERS";
                            else
                            {
                                saveHUDMessage = $"{editHUDName} WILL BE SAVED ONCE YOU EXIT THE ULTRAHUD MENU.";
                                ApplyHUDColors(editHUDSettings);
                                StartCoroutine(DeleteHUD(hudFilePointer));
                                StartCoroutine(SaveNewHUD(editHUDName));
                            }
                            displaySaveHUDMessage = true;
                        }
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();

                        // display save message
                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        if (editHUDNameCache == editHUDName && displaySaveHUDMessage)
                        {
                            labelStyle.fontSize = 20;
                            GUILayout.Label(saveHUDMessage, labelStyle, GUILayout.MinWidth(600), GUILayout.MinHeight(60));
                            labelStyle.fontSize = 40;
                        }
                        else
                            displaySaveHUDMessage = false;
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();

                        GUILayout.FlexibleSpace();
                    }
                    // if the player is in the delete HUD menu
                    else if (delHUD)
                    {
                        string[] files = Directory.GetFiles(hudsPath);
                        // wrap file pointer
                        if (hudFilePointer < 0)
                            hudFilePointer = files.Length - 1;
                        if (hudFilePointer >= files.Length)
                            hudFilePointer = 0;

                        string file = Path.GetFileName(files[hudFilePointer]).Replace(".png", "").ToUpper();

                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();

                        // go back to ULTRAHUD menu
                        buttonStyle.fontSize = 30;
                        if (GUILayout.Button("BACK", buttonStyle, GUILayout.MaxWidth(100), GUILayout.MaxHeight(40)))
                        {
                            inMenu = false;
                            newHUD = false;
                            delHUD = false;
                            editHUD = false;
                            displaySaveHUDMessage = false;
                            deleteHUDConfirmations = 2;
                        }
                        buttonStyle.fontSize = 40;
                        GUILayout.EndHorizontal();

                        GUILayout.Space(20);
                        
                        // switch between files to delete
                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("<", buttonStyle, GUILayout.MaxWidth(50), GUILayout.MaxHeight(50)))
                        {
                            hudFilePointer--;
                            deleteHUDConfirmations = 2;
                        }
                        GUILayout.Label(file, labelStyle, GUILayout.MaxWidth(490), GUILayout.MaxHeight(50));
                        if (GUILayout.Button(">", buttonStyle, GUILayout.MaxWidth(50), GUILayout.MaxHeight(50)))
                        {
                            hudFilePointer++;
                            deleteHUDConfirmations = 2;
                        }
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();

                        GUILayout.Space(100);

                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        GUILayout.BeginVertical();
                        GUILayout.FlexibleSpace();

                        switch (deleteHUDConfirmations)
                        {
                            case 2:
                                if (GUILayout.Button($"DELETE \"{file}?\"", buttonStyle, GUILayout.MaxWidth(646), GUILayout.MaxHeight(60)))
                                    deleteHUDConfirmations--;
                                break;
                            case 1:
                                if (GUILayout.Button($"<size=20>ARE YOU SURE? YOU'LL LOSE {file} FOREVER!</size>\n<size=5>(UNLESS YOU DIG IN YOUR RECYCLE BIN)</size>", buttonStyle, GUILayout.MaxWidth(646), GUILayout.MaxHeight(60)))
                                    deleteHUDConfirmations--;
                                break;
                            case 0:
                                if (GUILayout.Button($"<size=25>ARE YOU INSANE??? {file} WILL DIE!!!1!</size>", buttonStyle, GUILayout.MaxWidth(646), GUILayout.MaxHeight(60)))
                                {
                                    StartCoroutine(DeleteHUD(hudFilePointer));
                                    LoadHUDSync(--hudFilePointer);
                                    deleteHUDConfirmations = 2;
                                }
                                break;
                            default:
                                break;
                        }
                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        if (deleteHUDConfirmations != 2)
                            if (GUILayout.Button("CANCEL ACTION", buttonStyle, GUILayout.MaxWidth(320), GUILayout.MaxHeight(60)))
                                deleteHUDConfirmations = 2;
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();

                        GUILayout.FlexibleSpace();
                        GUILayout.EndVertical();
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();

                        GUILayout.Space(100);
                    }
                    break;
                default:
                    break;
            }
        }
    }
}