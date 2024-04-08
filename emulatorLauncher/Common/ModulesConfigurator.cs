using System;
using System.Drawing;
using System.IO;
using EmulatorLauncher.Common.FileFormats;
using System.Windows.Forms;

namespace EmulatorLauncher.Common
{
    public class ModulesConfigurator
    {
        public static void ConfigureModules()
        {
            string pluginsPath = Path.Combine(Program.AppConfig.GetFullPath("retrobat"), "plugins");
            if (!Directory.Exists(pluginsPath))
                return;

            ConfigureModuleMarqueeManager(pluginsPath);
        }

        private static void ConfigureModuleMarqueeManager(string pluginsPath)
        {
            string pluginPath = Path.Combine(pluginsPath, "MarqueeManager");
            if (!Directory.Exists(pluginsPath))
                return;

            string configFile = Path.Combine(pluginPath, "config.ini");
            if (!File.Exists(configFile))
                return;

            SimpleLogger.Instance.Info("[MODULES] Configuring MarqueeManager");

            ScreenResolution marqueeResolution = ScreenResolution.CurrentResolution;
            string marqueeWidth = marqueeResolution.Width.ToString();
            string marqueeHeight = marqueeResolution.Height.ToString();

            using (var ini = IniFile.FromFile(configFile, IniOptions.UseSpaces))
            {
                int screenIndex = 1;

                Screen screen = Screen.AllScreens[screenIndex];
                if (Program.SystemConfig.isOptSet("MarqueeIndex") && !string.IsNullOrEmpty(Program.SystemConfig["MarqueeIndex"]))
                {
                    screenIndex = Program.SystemConfig["MarqueeIndex"].ToInteger();
                    ini.WriteValue("Settings", "ScreenNumber", screenIndex.ToString());
                }

                try 
                {
                    marqueeWidth = screen.Bounds.Width.ToString();
                    marqueeHeight = screen.Bounds.Height.ToString();
                } 
                catch { }

                if (Program.SystemConfig.isOptSet("MarqueeWidth") && !string.IsNullOrEmpty(Program.SystemConfig["MarqueeWidth"]))
                    ini.WriteValue("Settings", "MarqueeWidth", Program.SystemConfig["MarqueeWidth"]);
                else
                    ini.WriteValue("Settings", "MarqueeWidth", marqueeWidth);

                if (Program.SystemConfig.isOptSet("MarqueeHeight") && !string.IsNullOrEmpty(Program.SystemConfig["MarqueeHeight"]))
                    ini.WriteValue("Settings", "MarqueeHeight", Program.SystemConfig["MarqueeHeight"]);
                else
                    ini.WriteValue("Settings", "MarqueeHeight", marqueeHeight);

                if (Program.SystemConfig.isOptSet("MarqueeRA") && Program.SystemConfig.getOptBoolean("MarqueeRA"))
                    ini.WriteValue("Settings", "MarqueeRetroAchievements", "true");
                else
                    ini.WriteValue("Settings", "MarqueeRetroAchievements", "false");

                if (Program.SystemConfig.isOptSet("MarqueeAutoGeneration") && Program.SystemConfig.getOptBoolean("MarqueeAutoGeneration"))
                    ini.WriteValue("Settings", "MarqueeAutoGeneration", "false");
                else
                    ini.WriteValue("Settings", "MarqueeAutoGeneration", "true");

                if (Program.SystemConfig.isOptSet("MarqueeAutoConvert") && Program.SystemConfig.getOptBoolean("MarqueeAutoConvert"))
                    ini.WriteValue("Settings", "MarqueeAutoConvert", "true");
                else
                    ini.WriteValue("Settings", "MarqueeAutoConvert", "false");

                if (Program.SystemConfig.isOptSet("MarqueeAutoScraping") && Program.SystemConfig.getOptBoolean("MarqueeAutoScraping"))
                    ini.WriteValue("Settings", "MarqueeAutoScraping", "true");
                else
                    ini.WriteValue("Settings", "MarqueeAutoScraping", "false");

                ini.Save();
            }
        }
    }
}
