﻿using System;
using System.Reflection;
using System.Resources;
using System.Text.RegularExpressions;

namespace SBRW.Launcher.RunTime.LauncherCore.Languages.Visual_Forms
{
    static class Translations
    {
        private static ResourceManager? Lang_Launcher = null;

        public static bool ResetCache = false;

        public static string Application_Language = "en-US";

        public static string Database(string Text_Request)
        {
            try
            {
                if (Lang_Launcher == null || ResetCache)
                {
                    Lang_Launcher = UI(Application_Language) switch
                    {
                        _ => new ResourceManager("SBRW.Launcher.App.Languages.English_Texts", Assembly.GetExecutingAssembly()),
                    };
                    ResetCache = false;
                }

                try
                {
                    if (!string.IsNullOrWhiteSpace(Text_Request) && Lang_Launcher != null)
                    {
                        return Regex.Unescape(Lang_Launcher.GetString(Text_Request)??"Languages Not Found");
                    }
                    else
                    {
                        return "Languages Programer ERROR";
                    }
                }
                catch (Exception Error)
                {
                    return "Languages Program ERROR";
                }
            }
            catch (Exception Error)
            {
                return "Languages ERROR";
            }
        }

        /// <summary>
        /// Check System Language and Return Current Lang for Speech Files
        /// </summary>
        /// <param name="Language"></param>
        /// <returns></returns>
        public static string Speech_Files(string Provided_Language)
        {
            switch (Provided_Language.ToLowerInvariant())
            {
                case "ger":
                case "deu":
                case "de":
                    return "de";
                case "rus":
                case "ru":
                    return "ru";
                case "spa":
                case "es":
                    return "es";
                default:
                    return "en";
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public static int Speech_Files_Size()
        {
            switch (Speech_Files("en".ToLowerInvariant()))
            {
                case "de":
                    return 105948386;
                case "ru":
                    return 121367723;
                case "es":
                    return 101540466;
                default:
                    return 141805935;
            }
        }

        public static string UI(string Chosen_Lang)
        {
            if (!string.IsNullOrWhiteSpace(Chosen_Lang))
            {
                /* French */
                if (Chosen_Lang.Contains("fr"))
                {
                    return "fr";
                }
                else if (Chosen_Lang.Contains("en"))
                {
                    return "en";
                }
                else
                {
                    return Chosen_Lang;
                }
            }
            else
            {
                return "en-US";
            }
        }

        public static string UI(string Chosen_Lang, bool Force_Reset_Cache)
        {
            ResetCache = Force_Reset_Cache;
            return UI(Chosen_Lang);
        }
    }
}
