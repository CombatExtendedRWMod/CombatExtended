﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace CombatExtended
{
    public class CompSuppressable : ThingComp
    {
        #region Constants

        private const float minSuppressionDist = 5f;        //Minimum distance to be suppressed from, so melee won't be suppressed if it closes within this distance
        private const float maxSuppression = 1050f;          //Cap to prevent suppression from building indefinitely
        private const int TicksForDecayStart = 120;          // How long since last suppression before decay starts
        private const float suppressionDecayRate = 5f;    // How much suppression decays per tick
        private const int ticksPerMote = 150;               //How many ticks between throwing a mote

        #endregion

        #region Fields
        
        // --------------- Location calculations ---------------

        /*
         * We track the initial location from which a pawn was suppressed and the total amount of suppression coming from that location separately.
         * That way if suppression stops coming from location A but keeps coming from location B the location will get updated without bouncing 
         * pawns or having to track fire coming from multiple locations
         */
        private IntVec3 suppressorLoc;
        private float locSuppressionAmount = 0f;
        
        private float currentSuppression = 0f;
        public bool isSuppressed = false;

        private int ticksUntilDecay = 0;

        #endregion

        #region Properties

        public CompProperties_Suppressable Props => (CompProperties_Suppressable)props;
        public IntVec3 SuppressorLoc => suppressorLoc;

        public float CurrentSuppression => currentSuppression;
        public float ParentArmor
        {
            get
            {
                float armorValue = 0f;
                Pawn pawn = parent as Pawn;
                if (pawn != null)
                {
                    //Get most protective piece of armor
                    if (pawn.apparel.WornApparel != null && pawn.apparel.WornApparel.Count > 0)
                    {
                        List<Apparel> wornApparel = new List<Apparel>(pawn.apparel.WornApparel);
                        foreach (Apparel apparel in wornApparel)
                        {
                            float apparelArmor = apparel.GetStatValue(StatDefOf.ArmorRating_Sharp, true);
                            if (apparelArmor > armorValue)
                            {
                                armorValue = apparelArmor;
                            }
                        }
                    }
                }
                else
                {
                    Log.Error("Tried to get parent armor of non-pawn");
                }
                return armorValue;
            }
        }
        private float SuppressionThreshold
        {
            get
            {
                float threshold = 0f;
                Pawn pawn = parent as Pawn;
                if (pawn != null)
                {
                    //Get morale
                    float hardBreakThreshold = pawn.mindState?.mentalBreaker?.BreakThresholdMajor ?? 0;
                    float currentMood = pawn.needs?.mood?.CurLevel ?? 0.5f;
                    threshold = Mathf.Sqrt(Mathf.Max(0, currentMood - hardBreakThreshold)) * maxSuppression * 0.125f;
                }
                else
                {
                    Log.Error("CE tried to get suppression threshold of non-pawn");
                }
                return threshold;
            }
        }

        public bool IsHunkering
        {
            get
            {
                if (currentSuppression > (SuppressionThreshold * 10))
                {
                    if (isSuppressed)
                    {
                        return true;
                    }
                    // Removing suppression log
                    else
                    {
                        Log.Error("CE hunkering without suppression, this should never happen");
                    }
                }
                return false;
            }
        }
        public bool CanReactToSuppression
        {
            get
            {
                Pawn pawn = parent as Pawn;
                return !pawn.Position.InHorDistOf(SuppressorLoc, minSuppressionDist)
                    && !pawn.Downed
                    && !pawn.InMentalState;
            }
        }

        #endregion

        #region Methods
        
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref currentSuppression, "currentSuppression", 0f);
            Scribe_Values.Look(ref suppressorLoc, "suppressorLoc");
            Scribe_Values.Look(ref locSuppressionAmount, "locSuppression", 0f);
            Scribe_Values.Look(ref isSuppressed, "isSuppressed", false);
            Scribe_Values.Look(ref ticksUntilDecay, "ticksUntilDecay", 0);
        }

        public void AddSuppression(float amount, IntVec3 origin)
        {
            Pawn pawn = parent as Pawn;
            if (pawn == null)
            {
                Log.Error("CE trying to suppress non-pawn " + parent.ToString() + ", this should never happen");
                return;
            }

            // No suppression on berserking or fleeing pawns
            if (!CanReactToSuppression)
            {
                currentSuppression = 0f;
                isSuppressed = false;
                return;
            }

            // Add suppression to global suppression counter
            var suppressAmount = amount * pawn.GetStatValue(CE_StatDefOf.Suppressability);
            currentSuppression += suppressAmount;
            if (Controller.settings.DebugShowSuppressionBuildup)
            {
                MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, suppressAmount.ToString());
            }
            ticksUntilDecay = TicksForDecayStart;
            if (currentSuppression > maxSuppression)
            {
                currentSuppression = maxSuppression;
            }

            // Add suppression to current suppressor location if appropriate
            if (suppressorLoc == origin)
            {
                locSuppressionAmount += amount;
            }
            else if (locSuppressionAmount < SuppressionThreshold)
            {
                suppressorLoc = origin;
                locSuppressionAmount = currentSuppression;
            }

            // Assign suppressed status and assign reaction job
            if (currentSuppression > SuppressionThreshold)
            {
                isSuppressed = true;
                Job reactJob = SuppressionUtility.GetRunForCoverJob(pawn);
                if (reactJob == null && IsHunkering)
                {
                    reactJob = new Job(CE_JobDefOf.HunkerDown, pawn);
                    LessonAutoActivator.TeachOpportunity(CE_ConceptDefOf.CE_Hunkering, pawn, OpportunityType.Critical);
                }
                if (reactJob != null && reactJob.def != pawn.CurJob?.def)
                {
                    pawn.jobs.StartJob(reactJob, JobCondition.InterruptForced, null, true);
                    LessonAutoActivator.TeachOpportunity(CE_ConceptDefOf.CE_SuppressionReaction, pawn, OpportunityType.Critical);
                }
            }
        }

        public override void CompTick()
        {
            base.CompTick();

            //Apply decay once per second
            if(ticksUntilDecay > 0)
            {
                ticksUntilDecay--;
            }
            else if (currentSuppression > 0)
            {
                //Decay global suppression
                if (Controller.settings.DebugShowSuppressionBuildup && Gen.IsHashIntervalTick(parent, 30))
                {
                    MoteMaker.ThrowText(parent.DrawPos, parent.Map, "-" + (suppressionDecayRate * 30).ToString(), Color.red);
                }
                currentSuppression -= Mathf.Min(suppressionDecayRate, currentSuppression);
                isSuppressed = currentSuppression > 0;

                //Decay location suppression
                locSuppressionAmount -= Mathf.Min(suppressionDecayRate, locSuppressionAmount);
            }

            //Throw mote at set interval
            if (Gen.IsHashIntervalTick(this.parent, ticksPerMote) && CanReactToSuppression)
            {
                if (IsHunkering)
                {
                    MoteMaker.ThrowMetaIcon(parent.Position, parent.Map, CE_ThingDefOf.Mote_HunkerIcon);
                }
	            else if (this.isSuppressed)
                {
                    //MoteMaker.ThrowText(this.parent.Position.ToVector3Shifted(), parent.Map, "CE_SuppressedMote".Translate());
                    MoteMaker.ThrowMetaIcon(parent.Position, parent.Map, CE_ThingDefOf.Mote_SuppressIcon);
                }
			}

            /*if (Gen.IsHashIntervalTick(parent, ticksPerMote + Rand.Range(30, 300))
                && parent.def.race.Humanlike && !robotBodyList.Contains(parent.def.race.body.defName))
            {
                if (isHunkering || isSuppressed)
                {
                    AGAIN: string rndswearsuppressed = RulePackDef.Named("SuppressedMote").Rules.RandomElement().Generate();

                    if (rndswearsuppressed == "[suppressed]" || rndswearsuppressed == "" || rndswearsuppressed == " ")
                    {
                        goto AGAIN;
                    }
                    MoteMaker.ThrowText(this.parent.Position.ToVector3Shifted(), Find.VisibleMap, rndswearsuppressed);
                }
                //standard    MoteMaker.ThrowText(parent.Position.ToVector3Shifted(), "CE_SuppressedMote".Translate());
            }*/
        }

        #endregion
    }
}