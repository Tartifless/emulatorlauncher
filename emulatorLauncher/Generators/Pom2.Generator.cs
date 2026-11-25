using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    /// <summary>
    /// POM2 - Apple II family emulator (habib256/pom2)
    ///
    ///  - POM2 has no portable mode : ResourcePaths.cpp userConfigDir() reads %APPDATA% first, then
    ///    %LOCALAPPDATA%, with no override hook. APPDATA is redirected on the child process so
    ///    state.cfg lands in '<emulator>\POM2\state.cfg'. Inside POM2, APPDATA is read only there.
    ///
    ///  - '--kiosk' is the frontend mode : exclusive fullscreen, no menus, and setKioskMode() calls
    ///    settings->setReadOnly(true). POM2 reads state.cfg but never writes it back, so the file
    ///    this generator produces is authoritative.
    ///
    ///  - state.cfg is a flat, section-less "key=value" store, '#' comments on their own line only.
    ///    ConfigFile produces exactly that. Values are never paths here : Settings.cpp unescapeValue()
    ///    decodes \n \r \s \t \\, so a raw Windows path would be corrupted.
    ///
    ///  - Controllers : POM2 identifies pads by GLFW slot index (JoystickInput.h, kHostCount = 16),
    ///    not by SDL index nor by GUID - glfwGetJoystickGUID is never called. The RetroBat player
    ///    order cannot be mapped onto it, so the slot is left to the user (pom2_joystick), with the
    ///    emulator's own auto-bind as the default. There is deliberately no Pom2.Controllers.cs.
    ///
    ///  - No savestates : POM2 has no snapshot hotkey, '--snapshot-save' is a boot-time action only.
    /// </summary>
    class Pom2Generator : Generator
    {
        public Pom2Generator()
        {
            DependsOnDesktopResolution = true;
        }

        protected override bool UseGenericScreenPlacement { get { return true; } }

        /// <summary>Image types POM2 can classify and boot (DiskImage.cpp classifyDiskForSlot).</summary>
        private static readonly string[] _diskExtensions = new string[] { ".woz", ".dsk", ".do", ".po", ".nib", ".hdv", ".2mg", ".d13" };

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            if (!GetEmulatorPathAndExe(emulator, out string path, out string exe, "POM2.exe"))
                return null;

            bool fullscreen = ShouldRunFullscreen();

            // POM2 opens a .zip itself, but into its own media cache : RetroBat extracts the archive
            // so that the working copies stay in the saves folder (and so that .7z works too)
            List<string> disks = null;
            string romExtension = Path.GetExtension(rom).ToLowerInvariant();
            if (romExtension == ".zip" || romExtension == ".7z" || romExtension == ".squashfs")
            {
                string uncompressedRomPath = this.TryUnZipGameIfNeeded(system, rom, false, false);
                if (Directory.Exists(uncompressedRomPath))
                {
                    disks = GetArchiveDiskFiles(uncompressedRomPath);
                    ValidateUncompressedGame();
                }
            }

            if (disks == null)
                disks = GetGameDiskFiles(rom);

            if (disks.Count == 0)
                throw new ApplicationException("No Apple II disk image found in " + Path.GetFileName(rom));

            int discNumber = 0;
            if (SystemConfig.isOptSet("pom2_discnumber") && !string.IsNullOrEmpty(SystemConfig["pom2_discnumber"]))
                discNumber = SystemConfig["pom2_discnumber"].ToInteger();

            if (discNumber < 0 || discNumber >= disks.Count)
                discNumber = 0;

            if (!File.Exists(disks[discNumber]))
                throw new ApplicationException("File '" + disks[discNumber] + "' does not exist");

            // Every disk of the game gets its working copy, side by side in the saves folder : the kiosk
            // disk menu (Start, or F1) lists the booted disk's folder, the game's other disks first.
            // POM2 reads the file's read-only attribute as the write-protect notch (MediaNotch.h),
            // so the working copy carries the protection, exactly like AppleWin.
            bool diskWritable = true;
            for (int i = 0; i < disks.Count; i++)
            {
                if (!File.Exists(disks[i]))
                {
                    SimpleLogger.Instance.Warning("[Generator] POM2 disk not found : " + disks[i]);
                    continue;
                }

                bool writable;
                disks[i] = ResolveDiskWorkingCopy(system, emulator, disks[i], out writable, true);
                diskWritable &= writable;
            }

            rom = disks[discNumber];

            // Disk II drive 2 : the game's next disk, like AppleWin. Only a 5.25" image fits a Disk II drive
            string drive2Disk = null;
            bool useDrive2 = !SystemConfig.isOptSet("pom2_drive2") || SystemConfig.getOptBoolean("pom2_drive2");
            if (useDrive2 && discNumber + 1 < disks.Count && IsFloppy525(disks[discNumber + 1]))
                drive2Disk = disks[discNumber + 1];

            // Portable configuration : mirrors ResourcePaths.cpp userConfigDir(), <APPDATA>\POM2
            string configPath = Path.Combine(path, "POM2");
            if (!Directory.Exists(configPath))
            {
                try { Directory.CreateDirectory(configPath); }
                catch (Exception ex) { SimpleLogger.Instance.Warning("[Generator] Cannot create " + configPath + " : " + ex.Message); }
            }

            SetupRoms(path, SystemConfig.isOptSet("pom2_machine") && !string.IsNullOrEmpty(SystemConfig["pom2_machine"]) ? SystemConfig["pom2_machine"] : "iie");
            SetupConfiguration(configPath);

            var commandArray = new List<string>();

            // Exclusive fullscreen, no menus, and settings become read-only
            if (fullscreen)
                commandArray.Add("--kiosk");

            // Inserted without booting, a refused image is only a warning (CliDispatcher.cpp, main.cpp)
            if (drive2Disk != null)
            {
                commandArray.Add("--mount");
                commandArray.Add("\"s6d2:" + drive2Disk + "\"");
            }

            // The positional image is the boot disk : POM2 accepts only one (CliDispatcher.cpp)
            commandArray.Add("\"" + rom + "\"");

            string args = string.Join(" ", commandArray);

            var ret = new ProcessStartInfo()
            {
                FileName = exe,
                Arguments = args,
                // Needed so ResourcePaths finds roms\ and fonts\ beside POM2.exe, and so the
                // HOME-less imgui.ini fallback stays inside the emulator folder
                WorkingDirectory = path,
                UseShellExecute = false
            };

            // Redirect the per-user settings directory into the emulator folder
            ret.EnvironmentVariables["APPDATA"] = path;

            // Safety net : if the working copy could not be made we are mounting the user's own file,
            // and this is the only lever that protects it (MediaWritePolicy.h). AppleWin has no
            // equivalent, which is why it always gets a copy.
            if (!diskWritable)
                ret.EnvironmentVariables["POM2_MEDIA_WRITE_DEFAULT"] = "protected";

            return ret;
        }

        /// <summary>
        /// Disks of an extracted archive : its own .m3u when it has one, otherwise every disk image in name
        /// order (a game's sides). The same disk in two formats is kept once, in the _diskExtensions order.
        /// </summary>
        private static List<string> GetArchiveDiskFiles(string folder)
        {
            var files = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories);

            string m3u = files.FirstOrDefault(file => Path.GetExtension(file).Equals(".m3u", StringComparison.OrdinalIgnoreCase));
            if (m3u != null)
                return GetGameDiskFiles(m3u);

            return files
                .Where(file => _diskExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                .GroupBy(file => Path.Combine(Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file)), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderBy(file => Array.IndexOf(_diskExtensions, Path.GetExtension(file).ToLowerInvariant())).First())
                .OrderBy(file => Path.GetFileName(file), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>5.25" images, the only ones a Disk II drive takes. A .po or .2mg can also be a 3.5" or a hard disk.</summary>
        private static bool IsFloppy525(string file)
        {
            if (!File.Exists(file))
                return false;

            string ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext == ".dsk" || ext == ".do" || ext == ".nib" || ext == ".woz" || ext == ".d13")
                return true;

            if (ext == ".po" || ext == ".2mg")
                return new FileInfo(file).Length < 400 * 1024;

            return false;
        }

        // ROMs searched by POM2 for each machine, machine then character ROM, the first file found is used (SystemProfile.cpp)
        private static readonly Dictionary<string, string[][]> _machineRoms = new Dictionary<string, string[][]>()
        {
            { "ii",    new[] { new[] { "apple2o.rom", "apple2.rom" }, new[] { "apple2_char.rom" } } },
            { "ii+",   new[] { new[] { "apple2p.rom", "apple2.rom" }, new[] { "apple2_char.rom" } } },
            { "iie",   new[] { new[] { "apple2e.rom" }, new[] { "apple2e_char.rom", "apple2_char.rom" } } },
            { "iie-u", new[] { new[] { "apple2e_unenh.rom", "342-0135-b.64.rom", "apple2e.rom" }, new[] { "apple2e_char_2k.rom", "342-0133-a.chr.rom", "apple2_char.rom" } } },
            { "iic",   new[] { new[] { "apple2c-32Kv0.rom", "apple2c-16K.rom", "3420033a.256" }, new[] { "apple2e_char.rom", "apple2_char.rom" } } },
            { "iic+",  new[] { new[] { "apple2cp.rom", "apple2c-plus.rom", "apple2c-32Kv0.rom" }, new[] { "apple2e_char.rom", "apple2_char.rom" } } },
        };

        /// <summary>
        /// POM2 packages ship no Apple ROM since 1.1.0 : the ROMs of 'bios\pom2' are copied to the 'roms' folder beside POM2.exe
        /// (searched after %LOCALAPPDATA%\POM2\roms, ResourcePaths.cpp). A missing machine or character ROM stops the launch with a message,
        /// in kiosk mode POM2 would only show a blank screen.
        /// </summary>
        private void SetupRoms(string path, string machine)
        {
            string biosPath = Path.Combine(AppConfig.GetFullPath("bios"), "pom2");
            string romsPath = Path.Combine(path, "roms");
            FileTools.TryCreateDirectory(romsPath);

            if (Directory.Exists(biosPath))
            {
                foreach (string file in Directory.GetFiles(biosPath))
                {
                    string target = Path.Combine(romsPath, Path.GetFileName(file));
                    if (File.Exists(target))
                        continue;

                    try { File.Copy(file, target); }
                    catch (Exception ex) { SimpleLogger.Instance.Warning("[Generator] Cannot copy POM2 ROM " + file + " : " + ex.Message); }
                }
            }

            // PAL machines use the ROMs of the NTSC machine
            string baseMachine = machine.EndsWith("-pal") ? machine.Substring(0, machine.Length - 4) : machine;
            string[][] machineRoms;
            if (!_machineRoms.TryGetValue(baseMachine, out machineRoms))
                return;

            // The Disk II PROMs are optional : without them POM2 runs its own 16 and 13-sector ones (SlotCardFactory.cpp)
            var missing = machineRoms.Where(group => !group.Any(file => RomExists(path, file))).Select(group => group[0]).ToList();
            if (missing.Count > 0)
                throw new ApplicationException("Missing POM2 ROM in bios\\pom2 : " + string.Join(", ", missing));
        }

        // POM2 searches %LOCALAPPDATA%\POM2\roms first (ROMs downloaded by POM2 itself), then the emulator folder
        private static bool RomExists(string path, string file)
        {
            string localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            if (!string.IsNullOrEmpty(localAppData) && File.Exists(Path.Combine(localAppData, "POM2", "roms", file)))
                return true;

            return File.Exists(Path.Combine(path, "roms", file));
        }

        /// <summary>
        /// Write POM2's state.cfg. Flat "key=value" store, no sections (Settings.cpp load/save).
        /// Unknown keys already in the file are preserved.
        /// </summary>
        private void SetupConfiguration(string configPath)
        {
            string cfgFile = Path.Combine(configPath, "state.cfg");

            try
            {
                var cfg = ConfigFile.FromFile(cfgFile, new ConfigFileOptions() { CaseSensitive = true, KeepEmptyValues = true });

                // Machine. POM2's own fresh-install default is "iie-pal" (//e Enhanced PAL, 50Hz,
                // MainWindow.cpp:853) - deliberately not used : almost every Apple II title is NTSC,
                // and "iie" also matches the ROM auto-probe, which avoids a second cold reset.
                BindFeature(cfg, "system_profile", "pom2_machine", "iie");
                BindFeature(cfg, "char_rom_locale", "pom2_charrom", "default");
                BindFeature(cfg, "ramworks_banks", "pom2_ramworks", "1");

                // Video
                BindFeature(cfg, "hi_res_mode", "pom2_video", "ColorCompositeOE");
                BindFeature(cfg, "aspect_mode", "pom2_aspect", "square");
                BindBoolFeatureOn(cfg, "crt_effects_enabled", "pom2_crteffects", "true", "false");

                // Disks
                BindBoolFeature(cfg, "disk_turbo", "pom2_diskturbo", "true", "false");

                // Slot 4 sound card. Ignored on the //c profiles, which have no expansion slots.
                BindFeature(cfg, "slot_4_card", "pom2_slot4", "mockingboard");

                // Audio
                BindBoolFeature(cfg, "floppy_sound_muted", "pom2_nofloppysound", "true", "false");

                // Game port. "-2" is the "key absent" sentinel POM2 uses to keep its own auto-bind
                // (MainWindow.cpp:538) - writing it is how AUTOMATIC stays automatic.
                BindFeature(cfg, "joystick_host", "pom2_joystick", "-2");
                BindFeature(cfg, "joystick_deadzone", "pom2_deadzone", "0.00");

                // Mouse as paddles 0/1 (PaddleMouse.h). Off by default, like POM2 itself : it
                // replaces the pad's paddles, so forcing it on would break joystick games.
                BindBoolFeature(cfg, "paddle_mouse", "pom2_paddlemouse", "true", "false");
                BindFeature(cfg, "paddle_mouse_range", "pom2_paddlemouse_range", "100");

                // The AI control server binds an HTTP port. Off by default, but a value persisted by
                // a windowed session would survive : force it.
                cfg["ai_control_enable"] = "false";

                // POM2 remounts the media named by these keys at startup. Clear them so a launch
                // boots the requested image and nothing else.
                cfg["disk_path"] = "";
                cfg["hdv_path"] = "";

                cfg.Save(cfgFile, false);
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Warning("[Generator] Cannot write POM2 state.cfg : " + ex.Message);
            }
        }
    }
}