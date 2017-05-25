using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using UnityEngine;

namespace CombatExtended
{
    public class Settings : ModSettings
    {
        #region Settings

        // General settings
        private bool showCasings = true;
        private bool showTaunts = true;
        private bool allowMeleeHunting = false;

        public bool ShowCasings => showCasings;
        public bool ShowTaunts => showTaunts;
        public bool AllowMeleeHunting => allowMeleeHunting;

        // Ammo settings
        private bool enableAmmoSystem = true;
        private bool rightClickAmmoSelect = false;
        private bool autoReloadOnChangeAmmo = true;
        private bool autoTakeAmmo = true;
        private bool showCaliberOnGuns = true;

        public bool EnableAmmoSystem => enableAmmoSystem;
        public bool RightClickAmmoSelect => rightClickAmmoSelect;
        public bool AutoReloadOnChangeAmmo => autoReloadOnChangeAmmo;
        public bool AutoTakeAmmo => autoTakeAmmo;
        public bool ShowCaliberOnGuns => showCaliberOnGuns;

        #endregion

        private bool lastAmmoSystemStatus;

        #region Methods

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref showCasings, "showCasings", true);
            Scribe_Values.Look(ref showTaunts, "showTaunts", true);
            Scribe_Values.Look(ref allowMeleeHunting, "allowMeleeHunting", false);

            // Ammo settings
            Scribe_Values.Look(ref enableAmmoSystem, "enableAmmoSystem", true);
            Scribe_Values.Look(ref rightClickAmmoSelect, "rightClickAmmoSelect", false);
            Scribe_Values.Look(ref autoReloadOnChangeAmmo, "autoReloadOnChangeAmmo", true);
            Scribe_Values.Look(ref autoTakeAmmo, "autoTakeAmmo", true);
            Scribe_Values.Look(ref showCaliberOnGuns, "showCaliberOnGuns", true);

            lastAmmoSystemStatus = enableAmmoSystem;    // Store this now so we can monitor for changes
        }

        public void DoWindowContents(Rect canvas)
        {
            Listing_Standard list = new Listing_Standard();
            list.ColumnWidth = (canvas.width - 17) / 2; // Subtract 17 for gap between columns
            list.Begin(canvas);

            // Do general settings
            Text.Font = GameFont.Medium;
            list.Label("CE_Settings_HeaderGeneral".Translate());
            Text.Font = GameFont.Small;
            list.Gap();

            list.CheckboxLabeled("CE_Settings_ShowCasings_Title".Translate(), ref showCasings, "CE_Settings_ShowCasings_Desc".Translate());
            list.CheckboxLabeled("CE_Settings_ShowTaunts_Title".Translate(), ref showTaunts, "CE_Settings_ShowTaunts_Desc".Translate());
            list.CheckboxLabeled("CE_Settings_AllowMeleeHunting_Title".Translate(), ref allowMeleeHunting, "CE_Settings_AllowMeleeHunting_Desc".Translate());

            // Do ammo settings
            list.NewColumn();

            Text.Font = GameFont.Medium;
            list.Label("CE_Settings_HeaderAmmo".Translate());
            Text.Font = GameFont.Small;
            list.Gap();
            
            list.CheckboxLabeled("CE_Settings_EnableAmmoSystem_Title".Translate(), ref enableAmmoSystem, "CE_Settings_EnableAmmoSystem_Desc".Translate());
            list.GapLine();
            if (enableAmmoSystem)
            {
                list.CheckboxLabeled("CE_Settings_RightClickAmmoSelect_Title".Translate(), ref rightClickAmmoSelect, "CE_Settings_RightClickAmmoSelect_Desc".Translate());
                list.CheckboxLabeled("CE_Settings_AutoReloadOnChangeAmmo_Title".Translate(), ref autoReloadOnChangeAmmo, "CE_Settings_AutoReloadOnChangeAmmo_Desc".Translate());
                list.CheckboxLabeled("CE_Settings_AutoTakeAmmo_Title".Translate(), ref autoTakeAmmo, "CE_Settings_AutoTakeAmmo_Desc".Translate());
                list.CheckboxLabeled("CE_Settings_ShowCaliberOnGuns_Title".Translate(), ref showCaliberOnGuns, "CE_Settings_ShowCaliberOnGuns_Desc".Translate());
            }
            else
            {
                GUI.contentColor = Color.gray;
                list.Label("CE_Settings_RightClickAmmoSelect_Title".Translate());
                list.Label("CE_Settings_AutoReloadOnChangeAmmo_Title".Translate());
                list.Label("CE_Settings_AutoTakeAmmo_Title".Translate());
                list.Label("CE_Settings_ShowCaliberOnGuns_Title".Translate());
                GUI.contentColor = Color.white;
            }

            list.End();

            // Update ammo if setting changes
            if (lastAmmoSystemStatus != enableAmmoSystem)
            {
                AmmoInjector.Inject();
                lastAmmoSystemStatus = enableAmmoSystem;
            }
        }

        #endregion
    }
}
