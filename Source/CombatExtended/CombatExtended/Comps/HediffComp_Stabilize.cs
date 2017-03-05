﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using UnityEngine;

namespace CombatExtended
{
    [StaticConstructorOnStartup]
    public class HediffComp_Stabilize : HediffComp
    {
        private const float bleedIncreasePerSec = 0.01f;    // After stabilizing, bleed modifier is increased by this much
        private const float internalBleedOffset = 0.5f;

        private static readonly Texture2D StabilizedIcon = ContentFinder<Texture2D>.Get("UI/Icons/Medical/Stabilized_Icon");

        private bool stabilized = false;
        private float bleedModifier = 1;

        public HediffCompProperties_Stabilize Props { get { return props as HediffCompProperties_Stabilize; } }
        public bool Stabilized { get { return stabilized; } }
        public float BleedModifier
        {
            get
            {
                float mod = bleedModifier;
                if (parent.Part.depth == BodyPartDepth.Inside) mod += internalBleedOffset;
                return Mathf.Clamp01(mod);
            }
        }

        public void Stabilize(Pawn medic, Medicine medicine)
        {
            if (stabilized)
            {
                Log.Error("CE tried to stabilize an injury that is already stabilized before");
                return;
            }
            if (medicine == null)
            {
                Log.Error("CE tried to stabilize without medicine");
                return;
            }
            float bleedReduction = medic.GetStatValue(StatDefOf.HealingQuality) * medicine.GetStatValue(StatDefOf.MedicalPotency);
            bleedModifier = 1 - bleedReduction; // Especially high treatment quality extends time at 0% bleed by setting bleedModifier to a negative number
            stabilized = true;
        }

        public override void CompExposeData()
        {
            Scribe_Values.LookValue(ref stabilized, "stabilized", false);
            Scribe_Values.LookValue(ref bleedModifier, "bleedModifier", 1);
        }

        public override void CompPostTick()
        {
            // Increase bleed modifier once per second
            if (stabilized && bleedModifier < 1 && parent.ageTicks % 60 == 0)
            {
                bleedModifier = bleedModifier + bleedIncreasePerSec;
                if (bleedModifier >= 1)
                {
                    bleedModifier = 1;
                    //stabilized = false;
                }
            }
        }

        public override TextureAndColor CompStateIcon
        {
            get
            {
                if (stabilized && !parent.IsOld() && !parent.IsTended()) return new TextureAndColor(StabilizedIcon, Color.white);
                return TextureAndColor.None;
            }
        }

        public override string CompDebugString()
        {
            if (parent.BleedRate < 0) return "Not bleeding";
            if (!stabilized) return "Not stabilized";
            return String.Concat("Stabilized", parent.Part.depth == BodyPartDepth.Inside ? " internal bleeding" : "", "\nbleed rate modifier: ", bleedModifier.ToString());
        }
    }
}
