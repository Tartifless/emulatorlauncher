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
        private bool _batFile = false;

        private Dictionary<string, string> recompExecutables = new Dictionary<string, string>()
        {
            { "Banjo 64", "BanjoRecompiled.exe" },
            { "Bomberman 64", "BM64Recompiled.exe" },
            { "Infinite Mario 64", "Infinite Mario 64.exe" },
            { "Chameleon Twist", "ChameleonTwistJPRecompiled.exe" },
            { "SM64 CoopDX", "sm64coopdx.exe" },
            { "Dragon Ball Z Budokai", "dbz1.exe" },
            { "Dinosaur Planet", "DinosaurPlanetRecompiled.exe" },
            { "Duke Nukem: Zero Hour", "DNZHRecompiled.exe" },
            { "Dr. Mario 64", "drmario64_recomp.exe" },
            { "Sonic 1 Forever", "SonicForever.exe" },
            { "Sonic 3 AIR", "Sonic3AIR.exe" },
            { "Perfect Dark", "pd.x86_64.exe" },
            { "Animal Crossing (Game Cube)", "AnimalCrossing.exe" },
            { "Goemon 64", "Goemon64Recompiled.exe" },
            { "Zelda MM (2 Ship 2 Harkinian)", "2ship.exe" },
            { "Super Mario 64 (Ghostship)", "Ghostship.exe" },
            { "Zelda OoT (Ship of Harkinian)", "soh.exe" },
            { "Mario Kart 64 (SpaghettiKart)", "Spaghettify.exe" },
            { "Star Fox 64 (Starship)", "Starship.exe" },
            { "Super Mario Bros. Remastered", "SMB1R.exe" },
            { "LoD: Severed Chains", "launch.bat" },
            { "Mario Kart 64", "MarioKart64Recompiled.exe" },
            { "Banjo-Kazooie: Nuts & Bolts", "renut-windows-x64.exe" },
            { "Mega Man 64", "MegaMan64Recompiled.exe" },
            { "REDRIVER 2", "REDRIVER2.exe" },
            { "Quest 64", "Quest64Recompiled.exe" },
            { "Super Metroid Launcher", "Super Metroid Launcher.exe" },
            { "Zelda: ALttP (Zelda 3 Launcher)", "Zelda 3 Launcher.exe" },
            { "Super Mario World", "smw.exe" },
            { "Viva Pinata Trouble in Paradise", "retip-windows-x64.exe" },
            { "Sonic Unleashed Recompiled", "UnleashedRecomp.exe" },
            { "Star Fox 64", "Starfox64Recompiled.exe" },
            { "WipeOut Phantom Edition", "wipeout.exe" },
            { "Zelda 64", "Zelda64Recompiled.exe" }
        };

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
                    var allGames = (catalog.Standard ?? Enumerable.Empty<GameEntry>())
                        .Concat(catalog.Experimental ?? Enumerable.Empty<GameEntry>())
                        .Concat(catalog.Custom ?? Enumerable.Empty<GameEntry>());
                    
                    if (allGames.Any(g => g.Name.Equals(targetGame, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        var gametoConf = allGames.FirstOrDefault(g => g.Name.Equals(targetGame, StringComparison.InvariantCultureIgnoreCase));
                        
                        string gameFolder = Path.Combine(recompiledGames, gametoConf.FolderName);
                        
                        if (recompExecutables.ContainsKey(gametoConf.Name))
                        {
                            _exename = Path.GetFileNameWithoutExtension(recompExecutables[gametoConf.Name]);
                            _exeFile = Path.Combine(gameFolder, recompExecutables[gametoConf.Name]);

                            if (recompExecutables[gametoConf.Name].EndsWith(".bat", StringComparison.InvariantCultureIgnoreCase))
                            {
                                _batFile = true;
                                _exename = "";
                            }

                            if (gametoConf.Name == "Perfect Dark" && SystemConfig.isOptSet("pdark_region"))
                            {
                                switch (SystemConfig["pdark_region"])
                                {
                                    case "EUR":
                                        _exename = "pd.pal.x86_64";
                                        _exeFile = Path.Combine(gameFolder, "pd.pal.x86_64.exe");
                                        break;
                                    case "JPN":
                                        _exename = "pd.jpn.x86_64";
                                        _exeFile = Path.Combine(gameFolder, "pd.jpn.x86_64.exe");
                                        break;
                                    default:
                                        _exename = "pd.x86_64";
                                        _exeFile = Path.Combine(gameFolder, "pd.x86_64.exe");
                                        break;
                                }
                            }
                            
                            SimpleLogger.Instance.Info("[N64Recomp] Monitoring " + _exename);

                            if (catalog.Standard != null && catalog.Standard.Any(g => g.Name.Equals(targetGame, StringComparison.InvariantCultureIgnoreCase)))
                                SetupRecompGame(gameFolder);
                        }
                        
                        else if (Directory.Exists(gameFolder))
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
                    var psi = new ProcessStartInfo()
                    {
                        FileName = _exeFile,
                        WorkingDirectory = Path.GetDirectoryName(_exeFile),
                    };

                    if (_batFile)
                    {
                        psi.UseShellExecute = true;
                    }

                    return psi;
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

            if (_batFile)
                process.WaitForExit();

            else if (!string.IsNullOrEmpty(_exename))
            {
                Process processToMonitor = null;
                int elapsed = 0;
                while (processToMonitor == null && elapsed < 10000)
                {
                    Thread.Sleep(200);
                    elapsed += 200;
                    processToMonitor = Process.GetProcessesByName(_exename).FirstOrDefault();
                }
                processToMonitor?.WaitForExit();
            }

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

                var allGames = (catalog.Standard ?? Enumerable.Empty<GameEntry>())
                    .Concat(catalog.Experimental ?? Enumerable.Empty<GameEntry>())
                    .Concat(catalog.Custom ?? Enumerable.Empty<GameEntry>());

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
            string cleanName = FileTools.CleanupName(game.Name);
            string shortcutPath = Path.Combine(romPath, cleanName + ".lnk");
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
