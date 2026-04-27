using EmulatorLauncher.Common;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.PadToKeyboard;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace EmulatorLauncher
{
    class N64RecompGenerator : Generator
    {
        private static string _exename;
        private static string _exeFile;

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string path = AppConfig.GetFullPath("n64recomplauncher");
            if (!Directory.Exists(path))
                return null;

            string exe = Path.Combine(path, "N64RecompLauncher.exe");
            _exename = Path.GetFileNameWithoutExtension(exe);
            if (!File.Exists(exe))
                return null;

            string n64recompJSON = Path.Combine(path, "settings.json");
            SetupLauncher(n64recompJSON);

            string recompiledGames = Path.Combine(path, "RecompiledGames");
            string gamesJSON = Path.Combine(path, "games.json");

            if (File.Exists(gamesJSON) && FileTools.IsExtension(rom, ".lnk") && !SystemConfig.getOptBoolean("n64recomp_useLauncher"))
            {
                SimpleLogger.Instance.Info("[N64Recomp] Shortcut detected, trying to find target game...");

                string targetGame = FileTools.GetShortcutArgswsh(rom);
                if (targetGame.StartsWith("--run "))
                    targetGame = targetGame.Substring("--run ".Length).Trim();

                string json = File.ReadAllText(gamesJSON);
                GameCatalog catalog = JsonConvert.DeserializeObject<GameCatalog>(json);

                if (catalog != null)
                {
                    var allGames = catalog.Standard.Concat(catalog.Experimental).Concat(catalog.Custom);
                    if (allGames.Any(g => g.Name.Equals(targetGame, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        var gametoConf = allGames.FirstOrDefault(g => g.Name.Equals(targetGame, StringComparison.InvariantCultureIgnoreCase));
                        
                        string gameFolder = Path.Combine(recompiledGames, gametoConf.FolderName);
                        if (Directory.Exists(gameFolder))
                        {
                            var exeFiles = Directory.GetFiles(gameFolder, "*.exe", SearchOption.TopDirectoryOnly);
                            if (exeFiles.Length > 0)
                            {
                                _exename = Path.GetFileNameWithoutExtension(exeFiles[0]);
                                _exeFile = exeFiles[0];
                                SimpleLogger.Instance.Info("[N64Recomp] Monitoring " + _exename);
                            }
                            
                            if (catalog.Standard != null && catalog.Standard.Any(g => g.Name.Equals(targetGame, StringComparison.InvariantCultureIgnoreCase)))
                                SetupRecompGame(gameFolder);
                        }
                    }
                }
            }

            if (FileTools.IsExtension(rom, ".lnk") && !SystemConfig.getOptBoolean("n64recomp_useLauncher"))
            {
                if (_exename != "N64RecompLauncher")
                {
                    return new ProcessStartInfo()
                    {
                        FileName = _exeFile,
                        WorkingDirectory = Path.GetDirectoryName(_exeFile),
                    };
                }

                else
                {
                    return new ProcessStartInfo()
                    {
                        FileName = rom,
                        WorkingDirectory = path,
                    };
                }
            }
            else
            {
                return new ProcessStartInfo()
                {
                    FileName = exe,
                    WorkingDirectory = path
                };
            }
        }

        private static void SetupLauncher(string conf)
        {
            bool fullscreen = ShouldRunFullscreen();

            JObject jsonObj;

            if (File.Exists(conf))
            {
                string json = File.ReadAllText(conf);
                jsonObj = JObject.Parse(json);
            }
            else
            {
                jsonObj = new JObject();
            }

            jsonObj["IsPortable"] = true;
            jsonObj["StartFullscreen"] = fullscreen ? true : false;
            jsonObj["EnableGamepadInput"] = true;

            File.WriteAllText(conf, jsonObj.ToString(Formatting.Indented));
        }

        private static void SetupRecompGame(string gameFolder)
        {
            bool fullscreen = ShouldRunFullscreen();

            string graphicsConf = Path.Combine(gameFolder, "graphics.json");
            string generalConf = Path.Combine(gameFolder, "general.json");
            string soundConf = Path.Combine(gameFolder, "sound.json");

            SetupGraphics(graphicsConf, fullscreen);
            SetupGeneral(generalConf);
            SetupSound(soundConf);
        }

        private static void SetupGraphics(string conf, bool fullscreen)
        {
            JObject jsonObj;
            
            if (File.Exists(conf))
            {
                string json = File.ReadAllText(conf);
                jsonObj = JObject.Parse(json);
            }
            else
            {
                jsonObj = new JObject();
            }

            if (fullscreen)
                jsonObj["wm_option"] = "Fullscreen";
            else
                jsonObj["wm_option"] = "Windowed";

            File.WriteAllText(conf, jsonObj.ToString(Formatting.Indented));
        }

        private static void SetupGeneral(string conf)
        {
            // TODO
        }

        private static void SetupSound(string conf)
        {
            // TODO
        }

        public override PadToKey SetupCustomPadToKeyMapping(PadToKey mapping)
        {
            return PadToKey.AddOrUpdateKeyMapping(mapping, _exename, InputKey.hotkey | InputKey.start, "(%{CLOSE})");
        }

        public override int RunAndWait(System.Diagnostics.ProcessStartInfo path)
        {
            foreach (Process px in Process.GetProcessesByName("N64RecompLauncher"))
            {
                try { px.Kill(); }
                catch (Exception ex) { SimpleLogger.Instance.Warning("[RunAndWait] Unable to kill existing N64RecompLauncher process: " + ex.Message); }
            }

            Process process = Process.Start(path);
            Thread.Sleep(500);

            var processToMonitor = Process.GetProcessesByName(_exename).FirstOrDefault();
            processToMonitor?.WaitForExit();

            return 0;
        }

        public override void Cleanup()
        {
            base.Cleanup();

            var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension("N64RecompLauncher"));
            try
            {
                SimpleLogger.Instance.Info("[N64Recomp] Killing N64RecompLauncher...");

                foreach (var process in processes)
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                            process.WaitForExit(3000);
                        }
                    }
                    catch
                    { }
                }
            }
            catch { }
        }

        #region library
        public static void UpdateN64RecompGames()
        {
            try
            {
                string recompLauncherPath = Program.AppConfig.GetFullPath("n64recomplauncher");
                if (!Directory.Exists(recompLauncherPath))
                {
                    SimpleLogger.Instance.Error("[N64Recomp] Invalid path.");
                    return;
                }

                string gamesJSON = Path.Combine(recompLauncherPath, "games.json");
                if (!File.Exists(gamesJSON))
                {
                    SimpleLogger.Instance.Error("[N64Recomp] games.json not found.");
                    return;
                }

                string recompGamesPath = Path.Combine(recompLauncherPath, "RecompiledGames");
                if (!Directory.Exists(recompGamesPath))
                {
                    SimpleLogger.Instance.Error("[N64Recomp] No games installed.");
                    return;
                }

                string json = File.ReadAllText(gamesJSON);
                GameCatalog catalog = JsonConvert.DeserializeObject<GameCatalog>(json);

                var allGames = catalog.Standard.Concat(catalog.Experimental).Concat(catalog.Custom);
                string romPath = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "roms", "n64recomp");
                if (!Directory.Exists(romPath))
                    try { Directory.CreateDirectory(romPath); } catch { return; }

                foreach (var game in allGames)
                {
                    string gamePath = Path.Combine(recompGamesPath, game.FolderName);
                    if (Directory.Exists(gamePath))
                    {
                        SimpleLogger.Instance.Info("[N64Recomp] Found: " + game.Name);
                        CreateShortcut(game, romPath, recompLauncherPath);
                    }
                }
            }
            catch { }
        }

        private static void CreateShortcut(GameEntry game, string romPath, string recompLauncherPath)
        {
            string runName = game.Name;

            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            string target = Path.Combine(recompLauncherPath, "N64RecompLauncher.exe");
            string shortcutPath = Path.Combine(romPath, game.Name.Replace(":", " -") + ".lnk");
            if (File.Exists(shortcutPath))
                return;

            try
            {
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = target;
                shortcut.arguments = $"--run {game.Name}";
                shortcut.WorkingDirectory = recompLauncherPath;
                shortcut.Save();

                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
            }
            catch { }

            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
        }


        public class GameEntry
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("repository")]
            public string Repository { get; set; }

            [JsonProperty("folderName")]
            public string FolderName { get; set; }

            [JsonProperty("gameIconUrl")]
            public string GameIconUrl { get; set; }
        }

        public class GameCatalog
        {
            [JsonProperty("standard")]
            public List<GameEntry> Standard { get; set; }

            [JsonProperty("experimental")]
            public List<GameEntry> Experimental { get; set; }

            [JsonProperty("custom")]
            public List<GameEntry> Custom { get; set; }
        }
        #endregion
    }
}
