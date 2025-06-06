﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.SQLite;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.EmulationStation;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    partial class StellaGenerator : Generator
    {
        private void CreateControllerConfiguration(SQLiteConnection db)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return;
            }

            SimpleLogger.Instance.Info("[INFO] Creating controller configuration for Stella");

            Dictionary<string, int> double_pads = new Dictionary<string, int>();

            //create new json list
            var jsonList = new List<DynamicJson>();

            //loop controllers
            foreach (var controller in this.Controllers.OrderBy(i => i.PlayerIndex).Take(2))
                ConfigureInput(jsonList, controller, double_pads);

            //Serialize and save to sqlite
            var json = DynamicJson.Serialize(jsonList);
            ForceStellaSetting(db, "joymap", json);
        }

        private void ConfigureInput(List<DynamicJson> json, Controller c, Dictionary<string, int> double_pads)
        {

            if (c == null || c.Config == null)
                return;

            if (c.IsKeyboard)
                return;
            else
                ConfigureJoystick(json, c, c.PlayerIndex, double_pads);
        }

        private void ConfigureJoystick(List<DynamicJson> jsonList, Controller c, int playerIndex, Dictionary<string, int> double_pads)
        {
            if (c == null)
                return;

            InputConfig joy = c.Config;
            if (joy == null)
                return;

            // Define controller name (if multiple controllers with same name, set number at the end)
            int nsamepad;
            string name = c.Name;

            if (double_pads.ContainsKey(name))
                nsamepad = double_pads[name];
            else
                nsamepad = 0;
            
            double_pads[name] = nsamepad + 1;

            // Write json object for the controller
            var jsonc = new DynamicJson();

            // Common part
            var kCommonMode = new List<DynamicJson>();

            var leftDiff = new DynamicJson();
            leftDiff["button"] = GetInputKeyName(c, InputKey.pageup);
            leftDiff["event"] = "ConsoleLeftDiffToggle";
            kCommonMode.Add(leftDiff);

            var rightDiff = new DynamicJson();
            rightDiff["button"] = GetInputKeyName(c, InputKey.pagedown);
            rightDiff["event"] = "ConsoleRightDiffToggle";
            kCommonMode.Add(rightDiff);

            var consoleSelect = new DynamicJson();
            consoleSelect["button"] = GetInputKeyName(c, InputKey.start);
            consoleSelect["event"] = "ConsoleSelect";
            kCommonMode.Add(consoleSelect);

            var colorToggle = new DynamicJson();
            colorToggle["button"] = GetInputKeyName(c, InputKey.l3);
            colorToggle["event"] = "ConsoleColorToggle";
            kCommonMode.Add(colorToggle);

            var reset = new DynamicJson();
            reset["button"] = GetInputKeyName(c, InputKey.r3);
            reset["event"] = "ConsoleReset";
            kCommonMode.Add(reset);

            // Driving part
            var kDrivingMode = new List<DynamicJson>();

            // Joystick part
            var kJoystickMode = new List<DynamicJson>();
            
            var joyUp = new DynamicJson();
            if (GetInputKeyName(c, InputKey.up) == "up")
            {
                joyUp["event"] = playerIndex == 1 ? "LeftJoystickUp" : "RightJoystickUp";
                joyUp["hat"] = "0";
                joyUp["hatDirection"] = "up";
            }
            else
            {
                joyUp["button"] = GetInputKeyName(c, InputKey.up);
                joyUp["event"] = playerIndex == 1 ? "LeftJoystickUp" : "RightJoystickUp";
            }
            kJoystickMode.Add(joyUp);

            var joyDown = new DynamicJson();
            if (GetInputKeyName(c, InputKey.down) == "down")
            {
                joyDown["event"] = playerIndex == 1 ? "LeftJoystickDown" : "RightJoystickDown";
                joyDown["hat"] = "0";
                joyDown["hatDirection"] = "down";
            }
            else
            {
                joyDown["button"] = GetInputKeyName(c, InputKey.down);
                joyDown["event"] = playerIndex == 1 ? "LeftJoystickDown" : "RightJoystickDown";
            }
            kJoystickMode.Add(joyDown);

            var joyLeft = new DynamicJson();
            if (GetInputKeyName(c, InputKey.left) == "left")
            {
                joyLeft["event"] = playerIndex == 1 ? "LeftJoystickLeft" : "RightJoystickLeft";
                joyLeft["hat"] = "0";
                joyLeft["hatDirection"] = "left";
            }
            else
            {
                joyLeft["button"] = GetInputKeyName(c, InputKey.left);
                joyLeft["event"] = playerIndex == 1 ? "LeftJoystickLeft" : "RightJoystickLeft";
            }
            kJoystickMode.Add(joyLeft);

            var joyRight = new DynamicJson();
            if (GetInputKeyName(c, InputKey.right) == "right")
            {
                joyRight["event"] = playerIndex == 1 ? "LeftJoystickRight" : "RightJoystickRight";
                joyRight["hat"] = "0";
                joyRight["hatDirection"] = "right";
            }
            else
            {
                joyRight["button"] = GetInputKeyName(c, InputKey.right);
                joyRight["event"] = playerIndex == 1 ? "LeftJoystickRight" : "RightJoystickRight";
            }
            kJoystickMode.Add(joyRight);

            var joyFire = new DynamicJson();
            joyFire["button"] = GetInputKeyName(c, InputKey.a);
            joyFire["event"] = playerIndex == 1 ? "LeftJoystickFire" : "RightJoystickFire";
            kJoystickMode.Add(joyFire);

            var joyFire5 = new DynamicJson();
            joyFire5["button"] = GetInputKeyName(c, InputKey.y);
            joyFire5["event"] = playerIndex == 1 ? "LeftJoystickFire5" : "RightJoystickFire5";
            kJoystickMode.Add(joyFire5);

            var joyFire9 = new DynamicJson();
            joyFire9["button"] = GetInputKeyName(c, InputKey.x);
            joyFire9["event"] = playerIndex == 1 ? "LeftJoystickFire9" : "RightJoystickFire9";
            kJoystickMode.Add(joyFire9);

            // Keyboard part
            var kKeyboardMode = new List<DynamicJson>();
            
            // Menu part
            var kMenuMode = new List<DynamicJson>();

            var uiUp = new DynamicJson();
            if (GetInputKeyName(c, InputKey.up) == "up")
            {
                uiUp["event"] = "UIUp";
                uiUp["hat"] = "0";
                uiUp["hatDirection"] = "up";
            }
            else
            {
                uiUp["button"] = GetInputKeyName(c, InputKey.up);
                uiUp["event"] = "UIUp";
            }
            kMenuMode.Add(uiUp);

            var uiDown = new DynamicJson();
            if (GetInputKeyName(c, InputKey.down) == "down")
            {
                uiDown["event"] = "UIDown";
                uiDown["hat"] = "0";
                uiDown["hatDirection"] = "down";
            }
            else
            {
                uiDown["button"] = GetInputKeyName(c, InputKey.down);
                uiDown["event"] = "UIDown";
            }
            kMenuMode.Add(uiDown);

            var navPrev = new DynamicJson();
            if (GetInputKeyName(c, InputKey.left) == "left")
            {
                navPrev["event"] = "UINavPrev";
                navPrev["hat"] = "0";
                navPrev["hatDirection"] = "left";
            }
            else
            {
                navPrev["button"] = GetInputKeyName(c, InputKey.left);
                navPrev["event"] = "UINavPrev";
            }
            kMenuMode.Add(navPrev);

            var navNext = new DynamicJson();
            if (GetInputKeyName(c, InputKey.right) == "right")
            {
                navNext["event"] = "UINavNext";
                navNext["hat"] = "0";
                navNext["hatDirection"] = "right";
            }
            else
            {
                navNext["button"] = GetInputKeyName(c, InputKey.right);
                navNext["event"] = "UINavNext";
            }
            kMenuMode.Add(navNext);

            var menuOK = new DynamicJson();
            menuOK["button"] = GetInputKeyName(c, InputKey.start);
            menuOK["event"] = "UIOK";
            kMenuMode.Add(menuOK);

            var menuCancel = new DynamicJson();
            menuCancel["button"] = GetInputKeyName(c, InputKey.b);
            menuCancel["event"] = "UICancel";
            kMenuMode.Add(menuCancel);

            var menuPrevTab = new DynamicJson();
            menuPrevTab["button"] = GetInputKeyName(c, InputKey.pageup);
            menuPrevTab["event"] = "UITabPrev";
            kMenuMode.Add(menuPrevTab);

            var menuNextTab = new DynamicJson();
            menuNextTab["button"] = GetInputKeyName(c, InputKey.pagedown);
            menuNextTab["event"] = "UITabNext";
            kMenuMode.Add(menuNextTab);

            var menuSelect = new DynamicJson();
            menuSelect["button"] = GetInputKeyName(c, InputKey.a);
            menuSelect["event"] = "UISelect";
            kMenuMode.Add(menuSelect);

            // Set objects
            jsonc.SetObject("kCommonMode", kCommonMode);
            jsonc.SetObject("kDrivingMode", kDrivingMode);
            jsonc.SetObject("kJoystickMode", kJoystickMode);
            jsonc.SetObject("kKeyboardMode", kKeyboardMode);
            jsonc.SetObject("kMenuMode", kMenuMode);

            // Name
            if (nsamepad > 0)
                name = name + " #" + (nsamepad + 1).ToString();
            
            jsonc.SetObject("name", name);

            // Add json object for joystick
            jsonList.Add(jsonc);

            SimpleLogger.Instance.Info("[INFO] Assigned controller " + c.DevicePath + " to player : " + c.PlayerIndex.ToString());
        }

        private static string GetInputKeyName(Controller c, InputKey key)
        {
            Int64 pid;

            // If controller is nintendo, A/B and X/Y are reversed
            //bool revertbuttons = (c.VendorID == VendorId.USB_VENDOR_NINTENDO);

            key = key.GetRevertedAxis(out bool revertAxis);

            var input = c.Config[key];
            if (input != null)
            {
                if (input.Type == "button")
                {
                    pid = input.Id;
                    return pid.ToString();
                }

                if (input.Type == "axis")
                {
                    pid = input.Id;
                    switch (pid)
                    {
                        case 0:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return "+LeftX";
                            else return "-LeftX";
                        case 1:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return "+LeftY";
                            else return "-LeftY";
                        case 2:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return "+RightX";
                            else return "-RightX";
                        case 3:
                            if ((!revertAxis && input.Value > 0) || (revertAxis && input.Value < 0)) return "+RightY";
                            else return "-RightY";
                        case 4: return "+LeftTrigger";
                        case 5: return "+RightTrigger";
                    }
                }

                if (input.Type == "hat")
                {
                    pid = input.Value;
                    switch (pid)
                    {
                        case 1: return "up";
                        case 2: return "right";
                        case 4: return "down";
                        case 8: return "left";
                    }
                }
            }
            return "";
        }
    }
}
