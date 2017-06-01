﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace CombatExtended
{
    [DefOf]
    public static class CE_StatDefOf
    {
        // *** Item stats ***
        public static readonly StatDef Bulk = StatDef.Named("Bulk"); // for items in inventory
        public static readonly StatDef WornBulk = StatDef.Named("WornBulk"); // worn apparel

        // *** Ranged weapon stats ***
        public static readonly StatDef ShotSpread = StatDef.Named("ShotSpread"); // pawn capacity
        public static readonly StatDef SwayFactor = StatDef.Named("SwayFactor"); // pawn capacity
        public static StatDef SightsEfficiency;
        public static readonly StatDef AimingAccuracy = StatDef.Named("AimingAccuracy"); // pawn capacity
        public static readonly StatDef ReloadSpeed = StatDef.Named("ReloadSpeed"); // pawn capacity

        // *** Melee weapon stats ***
        public static StatDef MeleeWeapon_Penetration;

        // *** Pawn stats ***
        public static StatDef CarryBulk;    // Inventory max space
        public static StatDef CarryWeight;  // Inventory max weight
        public static StatDef MeleeCritChance;
        public static StatDef MeleeDodgeChance;
        public static StatDef MeleeParryChance;


        public static StatDef Suppressability;
    }
}
