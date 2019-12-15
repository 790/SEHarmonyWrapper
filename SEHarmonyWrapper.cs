using System;
using Harmony;
using System.Reflection;
using VRage.Plugins;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Windows.Forms;
using System.IO.Compression;

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
        private Dictionary<string, bool> modList = new Dictionary<string, bool>();
        private Dictionary<string, string> config = new Dictionary<string, string>();
        private Logger logger = new Logger("SEHarmonyWrapper");
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
            var modListFilename = Path.Combine("seharmonywrapper", "modlist.txt");
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
                    if (line.StartsWith("#"))
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
                    if (line.StartsWith("#"))
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
                foreach (var dll in Directory.GetFiles(dir, "*.dll"))
                {
                    LoadPlugin(dll, harmony);
                }
            }

            var modPath = (config.ContainsKey("workshop_path") && config["workshop_path"].Length>0) ? config["workshop_path"] : Path.GetFullPath("..\\..\\..\\workshop\\content\\244850\\");
            
            if (!Directory.Exists(modPath))
            {
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
                            if (!modList.ContainsKey(Path.GetFileName(dir)))
                            {
                                var pluginUrl = "https://steamcommunity.com/workshop/filedetails/?id=" + workshopId;
                                var result = MessageBox.Show($"New SEHarmonyWrapper plugin found:\n\n{name}\n{pluginUrl}\n\nDo you want to enable this plugin?\n\nIf you don't know why you're getting this message, click No.\nYou can edit modlist.txt later to enable/disable plugins", "SEHarmonyWrapper", MessageBoxButtons.YesNo);
                                if (result == DialogResult.Yes)
                                {
                                    modList.Add(workshopId, true);
                                    logger.Log("Whitelisted " + workshopId);
                                }
                                else
                                {
                                    modList.Add(workshopId, false);
                                    logger.Log("Blacklisted " + workshopId);
                                }
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
            File.WriteAllText(modListFilename, String.Join("\r\n", modList.Select(m => m.Key.ToString() + "=" + (m.Value ? "1" : "0"))));
        }
        private bool LoadPlugin(string dll, HarmonyInstance harmony)
        {
            logger.Log("Loading plugin: " + dll);
            Assembly plugin;
            try
            {
                plugin = Assembly.LoadFile(Path.GetFullPath(dll));
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
            } catch(Exception e)
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
}