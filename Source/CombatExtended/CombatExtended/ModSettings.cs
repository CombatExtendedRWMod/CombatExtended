using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using UnityEngine;
using HugsLib;
using HugsLib.Settings;

namespace CombatExtended
{
    public class ModSettings : ModBase
    {
        public override string ModIdentifier
        {
            get
            {
                return "CombatExtended";
            }
        }

        #region Settings

        private static SettingHandle<bool> showCasingsInt;
        private static SettingHandle<bool> showTauntsInt;
        private static SettingHandle<bool> allowMeleeHunting;

        public static bool ShowCasings => showCasingsInt;
        public static bool ShowTaunts => showTauntsInt;
        public static bool AllowMeleeHunting => allowMeleeHunting;

        // Ammo settings
        private static SettingHandle<bool> enableAmmoSystemInt;
        private static SettingHandle<bool> rightClickAmmoSelectInt;
        private static SettingHandle<bool> autoReloadOnChangeAmmoInt;
        private static SettingHandle<bool> autoTakeAmmoInt;
        private static SettingHandle<bool> showCaliberOnGunsInt;

        public static bool EnableAmmoSystem { get { return enableAmmoSystemInt; } }
        public static bool RightClickAmmoSelect { get { return rightClickAmmoSelectInt; } }
        public static bool AutoReloadOnChangeAmmo { get { return autoReloadOnChangeAmmoInt; } }
        public static bool AutoTakeAmmo { get { return autoTakeAmmoInt; } }
        public static bool ShowCaliberOnGuns { get { return showCaliberOnGunsInt; } }

        #endregion

        public override void DefsLoaded()
        {
            showCasingsInt = Settings.GetHandle("showCasings", "CE_Settings_ShowCasings_Title".Translate(), "CE_Settings_ShowCasings_Desc".Translate(), true);
            showTauntsInt = Settings.GetHandle("showTaunts", "CE_Settings_ShowTaunts_Title".Translate(), "CE_Settings_ShowTaunts_Desc".Translate(), true);
            allowMeleeHunting = Settings.GetHandle("allowMeleeHunting", "CE_Settings_AllowMeleeHunting_Title".Translate(), "CE_Settings_AllowMeleeHunting_Desc".Translate(), false);

            // Ammo settings

            enableAmmoSystemInt = Settings.GetHandle("enableAmmoSystem", "CE_Settings_EnableAmmoSystem_Title".Translate(), "CE_Settings_EnableAmmoSystem_Desc".Translate(), true);
            rightClickAmmoSelectInt = Settings.GetHandle("rightClickAmmoSelect", "CE_Settings_RightClickAmmoSelect_Title".Translate(), "CE_Settings_RightClickAmmoSelect_Desc".Translate(), false);
            autoReloadOnChangeAmmoInt = Settings.GetHandle("autoReloadOnChangeAmmo", "CE_Settings_AutoReloadOnChangeAmmo_Title".Translate(), "CE_Settings_AutoReloadOnChangeAmmo_Desc".Translate(), false);
            autoTakeAmmoInt = Settings.GetHandle("autoTakeAmmo", "CE_Settings_AutoTakeAmmo_Title".Translate(), "CE_Settings_AutoTakeAmmo_Desc".Translate(), true);
            showCaliberOnGunsInt = Settings.GetHandle("showCaliberOnGuns", "CE_Settings_ShowCaliberOnGuns_Title".Translate(), "CE_Settings_ShowCaliberOnGuns_Desc".Translate(), true);
            
            rightClickAmmoSelectInt.VisibilityPredicate = GetAmmoSystemEnabled;
            autoReloadOnChangeAmmoInt.VisibilityPredicate = GetAmmoSystemEnabled;
            autoTakeAmmoInt.VisibilityPredicate = GetAmmoSystemEnabled;
            showCaliberOnGunsInt.VisibilityPredicate = GetAmmoSystemEnabled;
        }
        
		public override void SettingsChanged()
		{
			AmmoInjector.Inject();
		}
        
        private bool GetAmmoSystemEnabled()
        {
            return enableAmmoSystemInt;
        }
    }
}
