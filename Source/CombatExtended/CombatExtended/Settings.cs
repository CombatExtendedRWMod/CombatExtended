﻿using System;
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

        // Debug settings - make sure all of these default to false for the release build
        private bool debugDrawPartialLoSChecks = false;
        private bool debugEnableInventoryValidation = false;
        private bool debugDrawTargetCoverChecks = false;
        private bool debugShowTreeCollisionChance = false;
        private bool debugShowSuppressionBuildup = false;

        public bool DebugDrawPartialLoSChecks => debugDrawPartialLoSChecks;
        public bool DebugEnableInventoryValidation => debugEnableInventoryValidation;
        public bool DebugDrawTargetCoverChecks => debugDrawTargetCoverChecks;
        public bool DebugShowTreeCollisionChance => debugShowTreeCollisionChance;
        public bool DebugShowSuppressionBuildup => debugShowSuppressionBuildup;

        #endregion

        private bool lastAmmoSystemStatus;

        #region Methods

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref showCasings, "showCasings", true);
            Scribe_Values.Look(ref showTaunts, "showTaunts", true);
            Scribe_Values.Look(ref allowMeleeHunting, "allowMeleeHunting", false);

#if DEBUG
            // Debug settings
            Scribe_Values.Look(ref debugDrawPartialLoSChecks, "drawPartialLoSChecks", false);
            Scribe_Values.Look(ref debugEnableInventoryValidation, "enableInventoryValidation", false);
            Scribe_Values.Look(ref debugDrawTargetCoverChecks, "debugDrawTargetCoverChecks", false);
            Scribe_Values.Look(ref debugShowTreeCollisionChance, "debugShowTreeCollisionChance", false);
            Scribe_Values.Look(ref debugShowSuppressionBuildup, "debugShowSuppressionBuildup", false);
#endif

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

#if DEBUG
            // Do Debug settings
            list.GapLine();
            Text.Font = GameFont.Medium;
            list.Label("Debug");
            Text.Font = GameFont.Small;
            list.Gap();

            list.CheckboxLabeled("Draw partial LoS checks", ref debugDrawPartialLoSChecks, "Displays line of sight checks against partial cover.");
            list.CheckboxLabeled("Draw target cover checks", ref debugDrawTargetCoverChecks, "Displays highest cover of target as it is selected.");
            list.CheckboxLabeled("Enable inventory validation", ref debugEnableInventoryValidation, "Inventory will refresh its cache every tick and log any discrepancies.");
            list.CheckboxLabeled("Display tree collision chances", ref debugShowTreeCollisionChance, "Projectiles will display chances of coliding with trees as they pass by.");
            list.CheckboxLabeled("Display suppression buildup", ref debugShowSuppressionBuildup, "Pawns will display buildup numbers when taking suppression.");
#endif

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
                TutorUtility.DoModalDialogIfNotKnown(CE_ConceptDefOf.CE_AmmoSettings);
            }
        }

        #endregion
    }
}
