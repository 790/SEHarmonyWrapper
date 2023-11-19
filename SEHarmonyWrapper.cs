using System;
using Harmony;
using System.Reflection;
using VRage.Plugins;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Windows.Forms;
using System.IO.Compression;
using System.Text;
using Sandbox.Game.Screens;
using Sandbox.Graphics;
using VRage.Utils;
using VRageMath
using Sandbox.Graphics.GUI;
using SpaceEngineers.Game.GUI;
using VRage.Audio;
using VRage;
using VRage.Game;
using Sandbox;
using System.Diagnostics;
using Sandbox.Game.Screens.Helpers;

namespace SEHarmonyWrapper
{
    public interface ModBase : IPlugin, IDisposable
    {
        void Main(HarmonyInstance harmony, Logger log);
    }
    public class Logger
    {
        string prefix;
        public Logger(string s)
        {
            prefix = s;
        }

        public void Log(string text)
        {
            FileLog.Log(DateTime.Now + " [" + prefix + "] " + text);
        }
    }

    class Main : IPlugin, IDisposable
    {
        private List<object> loadedPlugins = new List<object>();
        public static Dictionary<string, bool> modList = new Dictionary<string, bool>();
        public static Dictionary<string, string> modListNames = new Dictionary<string, string>();
        private Dictionary<string, string> config = new Dictionary<string, string>();
        private Logger logger = new Logger("SEHarmonyWrapper");
        private static string modListFilename = "";
        public Main()
        {
            var harmony = HarmonyInstance.Create("com.github.790.seharmonywrapper");
            //MessageBox.Show("hello", "you", MessageBoxButtons.YesNo);
            if (!Directory.Exists("seharmonywrapper"))
            {
                Directory.CreateDirectory("seharmonywrapper");
            }
            FileLog.logPath = "seharmonywrapper/SEHarmonyWrapper.log";
            File.Delete(FileLog.logPath);
            logger.Log("Hello!");
            harmony.PatchAll();
            modListFilename = Path.Combine("seharmonywrapper", "modlist.txt");
            var configFilename = Path.Combine("seharmonywrapper", "config.txt");
            if (!File.Exists(modListFilename))
            {
                File.WriteAllText(modListFilename, "");
            }
            if (!File.Exists(configFilename))
            {
                File.WriteAllText(configFilename, "# Uncomment line below to set a custom workshop path\r\n#workshop_path=c:\\Steam\\steamapps\\workshop\\content\\244850");
            }
            using (var fr = File.OpenText(modListFilename)) {
                string line = "";
                while ((line = fr.ReadLine()) != null) {
                    if (line.Trim().StartsWith("#"))
                    {
                        continue;
                    }
                    string[] parts = line.Split('=');
                    modList[parts[0]] = (parts[1] == "1");
                }
                fr.Close();
            }

            using (var fr = File.OpenText(configFilename)) {
                string line = "";
                while ((line = fr.ReadLine()) != null)
                {
                    if (line.Trim().StartsWith("#"))
                    {
                        continue;
                    }
                    string[] parts = line.Split('=');
                    config[parts[0].Trim()] = parts[1].Trim();
                }
                fr.Close();
            }

            logger.Log("Scanning local mods from: " + Path.GetFullPath("seharmonywrapper"));
            foreach (var dir in Directory.GetDirectories("seharmonywrapper"))
            {
                if(dir.ToLower().EndsWith(".disabled"))
                {
                    continue;
                }
                modListNames.Add(dir, dir);
                if (!modList.ContainsKey(dir))
                {
                    modList.Add(dir, false);

                    continue;
                }
                
                foreach (var dll in Directory.GetFiles(dir, "*.dll"))
                {
                    LoadPlugin(dll, harmony);
                }
            }

            var modPath = (config.ContainsKey("workshop_path") && config["workshop_path"].Length>0) ? config["workshop_path"] : Path.GetFullPath("..\\..\\..\\workshop\\content\\244850\\");
            
            if (!Directory.Exists(modPath))
            {
                Patch_DrawAppVersion.sehwText.Append("\n" + loadedPlugins.Count() + " loaded plugins");
                logger.Log("Steam workshop directory can't be found. Edit config.txt to set its location");
                return;
            }
            logger.Log("Scanning workshop mods from: " + modPath);
            foreach (var dir in Directory.GetDirectories(modPath))
            {
                var workshopId = Path.GetFileName(dir);
                var pluginZipPath = Path.Combine(dir, "Data", "sehw-plugin.zip");
                var pluginPath = Path.Combine(dir, "Data", "sehw-plugin");
                if (File.Exists(pluginZipPath))
                {
                    logger.Log("Found SEHarmonyWrapper plugin zip: " + pluginZipPath);
                    
                    try
                    {
                        using (ZipArchive zip = ZipFile.OpenRead(pluginZipPath))
                        {
                            var nameFile = zip.Entries.First(e => e.FullName == "name.txt");
                            if(nameFile == null || nameFile.Length > 100)
                            {
                                continue;
                            }
                            string name = new StreamReader(nameFile.Open()).ReadToEnd();
                            if(name.Length == 0)
                            {
                                continue;
                            }
                            modListNames.Add(workshopId, name);
                            if (!modList.ContainsKey(Path.GetFileName(dir)))
                            {
                                logger.Log("New workshop mod found: " + workshopId);
                                modList.Add(workshopId, false);
                            }
                            if (modList.ContainsKey(workshopId) && !modList[workshopId])
                            {
                                logger.Log("Skipping blacklisted " + workshopId);
                                continue;
                            }
                            if(!Directory.Exists(pluginPath))
                            {
                                Directory.CreateDirectory(pluginPath);
                            }
                            foreach (ZipArchiveEntry entry in zip.Entries)
                            {
                                string extractPath = Path.GetFullPath(Path.Combine(pluginPath, entry.FullName));
                                if (extractPath.StartsWith(pluginPath, StringComparison.Ordinal))
                                {
                                    entry.ExtractToFile(extractPath, true);
                                }
                            }
                        }
                        foreach (var dll in Directory.GetFiles(pluginPath, "*.dll"))
                        {
                            LoadPlugin(dll, harmony);
                        }
                    } catch(Exception e)
                    {
                        logger.Log("Could not extract workshop SEHW plugin: " + pluginZipPath);
                        logger.Log(e.ToString());
                    }
                }
            }
            WriteModList();
            Patch_DrawAppVersion.sehwText.Append("\n" + loadedPlugins.Count() + " loaded plugins");
        }
        private bool LoadPlugin(string dll, HarmonyInstance harmony)
        {
            logger.Log("Loading plugin: " + dll);
            Assembly plugin;
            try
            {
                plugin = Assembly.UnsafeLoadFrom(Path.GetFullPath(dll));
            }
            catch (Exception e)
            {
                logger.Log("Exception LoadFile: " + e);
                return false;
            }
            if(plugin == null)
            {
                return false;
            }
            Type baseType = typeof(ModBase);
            var types = new List<Type>();
            try
            {
                
                types = plugin.GetTypes().Where(p => baseType.IsAssignableFrom(p)).ToList();
                if (types.Count() == 0)
                {
                    return false;
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                StringBuilder sb = new StringBuilder();
                foreach (Exception exSub in ex.LoaderExceptions)
                {
                    sb.AppendLine(exSub.Message);
                    FileNotFoundException exFileNotFound = exSub as FileNotFoundException;
                    if (exFileNotFound != null)
                    {
                        if (!string.IsNullOrEmpty(exFileNotFound.FusionLog))
                        {
                            sb.AppendLine("Fusion Log:");
                            sb.AppendLine(exFileNotFound.FusionLog);
                        }
                    }
                    sb.AppendLine();
                }
                string errorMessage = sb.ToString();
                logger.Log(errorMessage);
            }
            catch (Exception e)
            {
                logger.Log("Exception gettypes: " + e);
                return false;
            }
            logger.Log("Got assembly: " + plugin);
            MethodInfo mainMethod = baseType.GetMethod("Main");
            foreach (var t in types)
            {
                logger.Log("Instantiating plugin: " + t);
                try
                {
                    object obj = Activator.CreateInstance(t);
                    // Execute the method.
                    mainMethod.Invoke(obj, new object[] { harmony, new Logger(t.ToString()) });
                    if (obj != null)
                    {
                        loadedPlugins.Add(obj);
                    }
                }
                catch (Exception e)
                {
                    logger.Log("Exception create/invoke: " + e);
                }
            }
            return true;
        }
        public static void WriteModList()
        {
            if(modListFilename != null && modListFilename.Length > 0)
            {
                File.WriteAllText(modListFilename, String.Join("\r\n", modList.Select(m => m.Key.ToString() + "=" + (m.Value ? "1" : "0"))));
            }
            
        }
        private MethodInfo initMethod = typeof(IPlugin).GetMethod("Init");
        private MethodInfo updateMethod = typeof(IPlugin).GetMethod("Update");
        private MethodInfo disposeMethod = typeof(IDisposable).GetMethod("Dispose");
        public void Init(object gameObject)
        {
            foreach (var lm in loadedPlugins)
            {
                initMethod.Invoke(lm, new object[] { gameObject });
            }
        }
        public void Update()
        {
            foreach(var lm in loadedPlugins)
            {
                updateMethod.Invoke(lm, null);
            }
        }
        public void Dispose()
        {
            foreach (var lm in loadedPlugins)
            {
                disposeMethod.Invoke(lm, null);
            }
        }
    }

    [HarmonyPatch(typeof(MyGuiScreenMainMenuBase))]
    [HarmonyPatch("DrawAppVersion")]
    public static class Patch_DrawAppVersion
    {
        public static StringBuilder sehwText = new StringBuilder("SEHarmonyWrapper v0.3");
        public static bool Prefix(MyGuiScreenMainMenuBase __instance)
        {
            Vector2 normalizedCoord = MyGuiManager.ComputeFullscreenGuiCoordinate(MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_BOTTOM, 8, 32);
            normalizedCoord.Y -= 0f;
            MyGuiManager.DrawString("BuildInfo", sehwText, normalizedCoord, 0.6f, new Color?(new Color(MyGuiConstants.LABEL_TEXT_COLOR * 0.8f, 0.6f)), MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_BOTTOM, false, float.PositiveInfinity);
            return true;
        }
    }
    [HarmonyPatch(typeof(MyGuiScreenMainMenu))]
    [HarmonyPatch("RecreateControls")]
    public static class Patch_RecreateControls
    {
        static StringBuilder configText = new StringBuilder("Config");
        public static void Postfix(MyGuiScreenMainMenu __instance)
        {

            Vector2 position = MyGuiManager.ComputeFullscreenGuiCoordinate(MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_BOTTOM, 32, 28);
            MyGuiControlButton myGuiControlButton = new MyGuiControlButton(new Vector2?(position), MyGuiControlButtonStyleEnum.ControlSetting, null, null, MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_BOTTOM, null, configText, 0.5f, MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER, MyGuiControlHighlightType.WHEN_ACTIVE, OnClick, GuiSounds.MouseClick, 0.4f, null, false, null);
            myGuiControlButton.BorderEnabled = false;
            myGuiControlButton.BorderSize = 1;
            myGuiControlButton.BorderHighlightEnabled = true;
            myGuiControlButton.BorderColor = Vector4.Zero;
            __instance.AddControl(myGuiControlButton);
        }
        private static bool isConfigScreenOption = false;
        private static void OnClick(MyGuiControlButton obj)
        {
            if(!isConfigScreenOption)
            {
                isConfigScreenOption = true;
                MyGuiScreenModConfig myGuiScreenModConfig = new MyGuiScreenModConfig();
                myGuiScreenModConfig.Closed += OnWindowClosed;
                MyGuiSandbox.AddScreen(myGuiScreenModConfig);
            }
        }
        private static void OnWindowClosed(MyGuiScreenBase source)
        {
            isConfigScreenOption = false;
        }
    }

    public class MyGuiScreenModConfig : MyGuiScreenBase
    {
        Dictionary<string, bool> tmpModList;
        public MyGuiScreenModConfig() : base(new Vector2?(new Vector2(0.5f, 0.5f)), new Vector4?(MyGuiConstants.SCREEN_BACKGROUND_COLOR), new Vector2?(new Vector2(0.5264286f, 0.7633588f)), false, null, MySandboxGame.Config.UIBkOpacity, MySandboxGame.Config.UIOpacity, null)
        {
            base.EnabledBackgroundFade = true;
            this.m_closeOnEsc = true;
            this.m_drawEvenWithoutFocus = true;
            base.CanHideOthers = true;
            base.CanBeHidden = true;
            tmpModList = new Dictionary<string, bool>(Main.modList);
        }

        public override void LoadContent()
        {
            base.LoadContent();
            this.RecreateControls(true);
        }

        public override void RecreateControls(bool constructor)
        {
            base.RecreateControls(constructor);
            this.BuildControls();
        }

        public override string GetFriendlyName()
        {
            return "MyGuiScreenModConfig";
        }

        MyGuiControlTable modTable;
        protected void BuildControls()
        {
            base.AddCaption("SEHarmonyWrapper plugin load list", null, new Vector2?(new Vector2(0f, 0.003f)), 0.8f);
            MyGuiControlSeparatorList myGuiControlSeparatorList = new MyGuiControlSeparatorList();
            myGuiControlSeparatorList.AddHorizontal(-new Vector2(this.m_size.Value.X * 0.78f / 2f, this.m_size.Value.Y / 2f - 0.075f), this.m_size.Value.X * 0.79f, 0f, null);
            this.Controls.Add(myGuiControlSeparatorList);
            MyGuiControlSeparatorList myGuiControlSeparatorList2 = new MyGuiControlSeparatorList();
            myGuiControlSeparatorList2.AddHorizontal(-new Vector2(this.m_size.Value.X * 0.78f / 2f, -this.m_size.Value.Y / 2f + 0.123f), this.m_size.Value.X * 0.79f, 0f, null);
            this.Controls.Add(myGuiControlSeparatorList2);

            this.modTable = new MyGuiControlTable
            {
                //Position = new Vector2(0.364f, -0.307f),
                Position = new Vector2(0f /*0.264f*/, -0.307f),
                Size = new Vector2(0.5154286f, 0.6633588f),
                OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_TOP,
                ColumnsCount = 3
            };
            this.modTable.VisibleRowsCount = 15;
            this.modTable.SetCustomColumnWidths(new float[]
            {
                0.2f,
                0.6f,
                0.2f
            });
            modTable.SetColumnName(0, new StringBuilder("Source"));
            modTable.SetColumnName(1, new StringBuilder("Name"));
            modTable.SetColumnName(2, new StringBuilder("Enabled"));

            this.Controls.Add(this.modTable);

            foreach (var mod in Main.modList)
            {
                MyGuiControlTable.Row row = new MyGuiControlTable.Row();
                modTable.Add(row);

                long workshopId = 0;
                Int64.TryParse(mod.Key, out workshopId);

                if (workshopId > 0)
                {
                    var lc = new MyGuiControlTable.Cell(MyTexts.Get(MyCommonTexts.Workshop));
                    var m_linkBtn = new MyGuiControlButton(null, MyGuiControlButtonStyleEnum.Default, null, null, MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER, null, MyTexts.Get(MyCommonTexts.Workshop), 0.8f, MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER, MyGuiControlHighlightType.WHEN_ACTIVE, new Action<MyGuiControlButton>(OnLinkButtonClick), GuiSounds.MouseClick, 1f, null, false, null);
                    m_linkBtn.Enabled = true;
                    m_linkBtn.VisualStyle = MyGuiControlButtonStyleEnum.ClickableText;
                    m_linkBtn.UserData = mod.Key;
                    lc.Control = m_linkBtn;
                    modTable.Controls.Add(m_linkBtn);
                    row.AddCell(lc);
                }
                else
                {
                    var lc = new MyGuiControlTable.Cell(MyTexts.Get(MyCommonTexts.Local));
                    var m_linkBtn = new MyGuiControlButton(null, MyGuiControlButtonStyleEnum.Default, null, null, MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER, null, MyTexts.Get(MyCommonTexts.Local), 0.8f, MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER, MyGuiControlHighlightType.WHEN_ACTIVE, new Action<MyGuiControlButton>(OnLinkButtonClickOpenDir), GuiSounds.MouseClick, 1f, null, false, null);
                    m_linkBtn.Enabled = true;
                    m_linkBtn.VisualStyle = MyGuiControlButtonStyleEnum.ClickableText;
                    m_linkBtn.UserData = mod.Key;
                    lc.Control = m_linkBtn;

                    modTable.Controls.Add(m_linkBtn);
                    row.AddCell(lc);
                }
                var modName = mod.Key;
                if(workshopId > 0 && Main.modListNames.ContainsKey(modName))
                {
                    modName += " " + Main.modListNames[modName];
                }
                var c = new MyGuiControlTable.Cell(Path.GetFileName(modName));
                
                row.AddCell(c);
                var cell = new MyGuiControlTable.Cell(new StringBuilder(""));
                
                MyGuiControlCheckbox myGuiControlCheckbox = new MyGuiControlCheckbox(null, null, "", false, MyGuiControlCheckboxStyleEnum.Default, MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER);
                myGuiControlCheckbox.IsChecked = mod.Value;
                myGuiControlCheckbox.IsCheckedChanged += new Action<MyGuiControlCheckbox>(IsCheckedChanged);
                myGuiControlCheckbox.Enabled = true;
                myGuiControlCheckbox.Visible = true;
                myGuiControlCheckbox.UserData = mod.Key;
                cell.Control = myGuiControlCheckbox;
                modTable.Controls.Add(myGuiControlCheckbox);

                row.AddCell(cell);
                
            }
            modTable.SelectedRowIndex = -1;
            this.Controls.Add(new MyGuiControlLabel(new Vector2?(new Vector2(0, 0.25f)), null, "You must restart the game after changing these settings.", null, 0.8f, "Red", MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_BOTTOM));
            this.m_okBtn = new MyGuiControlButton(new Vector2?(new Vector2(0.1f, 0.338f)), MyGuiControlButtonStyleEnum.Default, null, null, MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_BOTTOM, null, MyTexts.Get(MyCommonTexts.Ok), 0.8f, MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER, MyGuiControlHighlightType.WHEN_ACTIVE, new Action<MyGuiControlButton>(this.OnCloseButtonClick), GuiSounds.MouseClick, 1f, null, false, null);
            this.m_okBtn.Enabled = true;
            m_okBtn.ButtonClicked += OnOkButtonClick;
            var m_cancelBtn = new MyGuiControlButton(new Vector2?(new Vector2(-0.1f, 0.338f)), MyGuiControlButtonStyleEnum.Default, null, null, MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_BOTTOM, null, MyTexts.Get(MyCommonTexts.Cancel), 0.8f, MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER, MyGuiControlHighlightType.WHEN_ACTIVE, new Action<MyGuiControlButton>(this.OnCloseButtonClick), GuiSounds.MouseClick, 1f, null, false, null);
            m_cancelBtn.Enabled = true;
            m_cancelBtn.ButtonClicked += OnCloseButtonClick;
            this.Controls.Add(this.m_okBtn);
            this.Controls.Add(m_cancelBtn);
            base.CloseButtonEnabled = true;
        }
        void OnLinkButtonClick(MyGuiControlButton b)
        {
            if (b.UserData != null && b.UserData is string)
            {
                MyGuiSandbox.OpenUrl("https://steamcommunity.com/workshop/filedetails/?id=" + b.UserData, UrlOpenMode.SteamOrExternal, null);
            }
        }
        void OnLinkButtonClickOpenDir(MyGuiControlButton b)
        {
            if(b.UserData != null)
            {
                string dir = b.UserData as string;
                if(dir != null && Directory.Exists(Path.GetFullPath(dir)))
                {
                    Process.Start(Path.GetFullPath(dir));
                }
            }
        }
        void IsCheckedChanged(MyGuiControlCheckbox cb)
        {
            if (tmpModList.ContainsKey((string)cb.UserData))
            {
                tmpModList[(string)cb.UserData] = cb.IsChecked;
            }
        }
        private void OnCloseButtonClick(object sender)
        {
            this.CloseScreen();
        }
        private void OnOkButtonClick(object sender)
        {
            Main.modList = new Dictionary<string, bool>(tmpModList);
            Main.WriteModList();
            this.CloseScreen();
        }

        private MyGuiControlButton m_okBtn;
    }
}