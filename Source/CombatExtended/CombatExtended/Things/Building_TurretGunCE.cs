﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.Sound;
using UnityEngine;

namespace CombatExtended
{
    /* Class is cloned from Building_TurretGun with various changes made to support fire modes and ammo
     * 
     * Unmodified methods should be kept up-to-date with vanilla class so long as they don't conflict with changes made. Please mark any changes you make from vanilla.
     * -NIA
     */
    [StaticConstructorOnStartup]
    public class Building_TurretGunCE : Building_Turret
    {
        private const int minTicksBeforeAutoReload = 1800;              // This much time must pass before haulers will try to automatically reload an auto-turret

        #region Fields

        protected int burstCooldownTicksLeft;
        public int burstWarmupTicksLeft;                                // Need this public so aim mode can modify it
        protected LocalTargetInfo currentTargetInt = LocalTargetInfo.Invalid;
        private Thing gunInt;
        private bool holdFire;
        protected CompMannable mannableComp;
        protected CompPowerTrader powerComp;
        protected TurretTopCE top;

        public static Material ForcedTargetLineMat = MaterialPool.MatFrom(GenDraw.LineTexPath, ShaderDatabase.Transparent, new Color(1f, 0.5f, 0.5f));

        // New fields
        private CompAmmoUser compAmmo = null;
        private CompFireModes compFireModes = null;
        public bool isReloading = false;
        private int ticksUntilAutoReload = 0;
        
        #endregion

        #region Properties

        public override Verb AttackVerb
        {
            get
            {
                if (Gun == null)
                {
                    return null;
                }
                return this.GunCompEq.verbTracker.PrimaryVerb;
            }
        }
        private bool CanSetForcedTarget
        {
            get
            {
                return mannableComp == null || MannedByColonist;    // Add check for mannableComp == null since we allow auto-turrets to manually target stuff
            }
        }
        public override LocalTargetInfo CurrentTarget
        {
            get
            {
                return this.currentTargetInt;
            }
        }
        public CompEquippable GunCompEq
        {
            get
            {
                return Gun.TryGetComp<CompEquippable>();
            }
        }
        private bool WarmingUp
        {
            get
            {
                return this.burstWarmupTicksLeft > 0;
            }
        }
        private bool CanToggleHoldFire
        {
            get
            {
                return base.Faction == Faction.OfPlayer || this.MannedByColonist;
            }
        }
        private bool MannedByColonist
        {
            get
            {
                return this.mannableComp != null && this.mannableComp.ManningPawn != null && this.mannableComp.ManningPawn.Faction == Faction.OfPlayer;
            }
        }
        public Thing Gun
        {
            get
            {
                if (this.gunInt == null)
                {
                    this.gunInt = ThingMaker.MakeThing(this.def.building.turretGunDef, null);
                    InitGun();
                }
                return this.gunInt;
            }
        }

        // New properties
        public CompAmmoUser CompAmmo
        {
            get
            {
                if (compAmmo == null && Gun != null) compAmmo = Gun.TryGetComp<CompAmmoUser>();
                return compAmmo;
            }
        }
        public CompFireModes CompFireModes
        {
            get
            {
                if (compFireModes == null && Gun != null) compFireModes = Gun.TryGetComp<CompFireModes>();
                return compFireModes;
            }
        }
        public bool NeedsReload
        {
            get
            {
                return mannableComp == null
                    && CompAmmo != null
                    && CompAmmo.hasMagazine
                    && (CompAmmo.curMagCount < CompAmmo.Props.magazineSize || CompAmmo.SelectedAmmo != CompAmmo.currentAmmo);
            }
        }
        public bool AllowAutomaticReload
        {
            get
            {
                return mannableComp == null && CompAmmo != null
                    && CompAmmo.hasMagazine
                    && (ticksUntilAutoReload == 0 || CompAmmo.curMagCount <= Mathf.CeilToInt(CompAmmo.Props.magazineSize / 6));
            }
        }
        public CompMannable MannableComp => mannableComp;

        #endregion

        #region Constructors

        // Uses new TurretTopCE class
        public Building_TurretGunCE()
        {
            this.top = new TurretTopCE(this);
        }

        #endregion

        #region Methods

        // Added handling for ticksUntilAutoReload
        protected void BeginBurst()
        {
            ticksUntilAutoReload = minTicksBeforeAutoReload;
            GunCompEq.PrimaryVerb.TryStartCastOn(CurrentTarget, false, true);
            OnAttackedTarget(CurrentTarget);
        }

        // Added CompAmmo reload check
        protected void BurstComplete()
        {
            if (this.def.building.turretBurstCooldownTime >= 0f)
            {
                this.burstCooldownTicksLeft = this.def.building.turretBurstCooldownTime.SecondsToTicks();
            }
            else
            {
                this.burstCooldownTicksLeft = this.GunCompEq.PrimaryVerb.verbProps.defaultCooldownTime.SecondsToTicks();
            }
            if (CompAmmo != null && CompAmmo.curMagCount <= 0)
            {
                TryOrderReload();
            }
        }

        public override void DeSpawn()
        {
            base.DeSpawn();
            this.ResetCurrentTarget();
        }
        
        public override void Draw()
        {
            top.DrawTurret();
            base.Draw();
        }

        public override void DrawExtraSelectionOverlays()
        {
            float range = this.GunCompEq.PrimaryVerb.verbProps.range;
            if (range < 90f)
            {
                GenDraw.DrawRadiusRing(base.Position, range);
            }
            float minRange = this.GunCompEq.PrimaryVerb.verbProps.minRange;
            if (minRange < 90f && minRange > 0.1f)
            {
                GenDraw.DrawRadiusRing(base.Position, minRange);
            }
            if (this.WarmingUp)
            {
                int degreesWide = (int)((float)this.burstWarmupTicksLeft * 0.5f);
                GenDraw.DrawAimPie(this, this.CurrentTarget, degreesWide, def.size.x * 0.5f);
            }
            if (this.forcedTarget.IsValid && (!this.forcedTarget.HasThing || this.forcedTarget.Thing.Spawned))
            {
                Vector3 b;
                if (this.forcedTarget.HasThing)
                {
                    b = this.forcedTarget.Thing.TrueCenter();
                }
                else
                {
                    b = this.forcedTarget.Cell.ToVector3Shifted();
                }
                Vector3 a = this.TrueCenter();
                b.y = Altitudes.AltitudeFor(AltitudeLayer.MetaOverlays);
                a.y = b.y;
                GenDraw.DrawLineBetween(a, b, Building_TurretGun.ForcedTargetLineMat);
            }
        }

        // Added new variables, removed bool loaded (not used in CE)
        public override void ExposeData()
        {
            base.ExposeData();

            // Look new variables
            Scribe_Deep.Look(ref gunInt, "gunInt");
            InitGun();
            Scribe_Values.Look(ref isReloading, "isReloading", false);
            Scribe_Values.Look(ref ticksUntilAutoReload, "ticksUntilAutoReload", 0);

            Scribe_Values.Look<int>(ref this.burstCooldownTicksLeft, "burstCooldownTicksLeft", 0, false);
            Scribe_Values.Look<int>(ref this.burstWarmupTicksLeft, "burstWarmupTicksLeft", 0, false);
            Scribe_TargetInfo.Look(ref this.currentTargetInt, "currentTarget");
            Scribe_Values.Look<bool>(ref this.holdFire, "holdFire", false, false);
        }

        private void InitGun()
        {
            // Callback for ammo comp
            if (CompAmmo != null)
            {
                CompAmmo.turret = this;
                //if (def.building.turretShellDef != null && def.building.turretShellDef is AmmoDef) CompAmmo.selectedAmmo = (AmmoDef)def.building.turretShellDef;
            }
            List<Verb> allVerbs = this.gunInt.TryGetComp<CompEquippable>().AllVerbs;
            for (int i = 0; i < allVerbs.Count; i++)
            {
                Verb verb = allVerbs[i];
                verb.caster = this;
                verb.castCompleteCallback = new Action(this.BurstComplete);
            }
        }

        // Replaced vanilla loaded text with CE reloading
        public override string GetInspectString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            string inspectString = base.GetInspectString();
            if (!inspectString.NullOrEmpty())
            {
                stringBuilder.AppendLine(inspectString);
            }
            stringBuilder.AppendLine("GunInstalled".Translate() + ": " + this.Gun.LabelCap);
            if (this.GunCompEq.PrimaryVerb.verbProps.minRange > 0f)
            {
                stringBuilder.AppendLine("MinimumRange".Translate() + ": " + this.GunCompEq.PrimaryVerb.verbProps.minRange.ToString("F0"));
            }

            if (isReloading)
            {
                stringBuilder.AppendLine("CE_TurretReloading".Translate());
            }
            else if (Spawned && burstCooldownTicksLeft > 0)
            {
                stringBuilder.AppendLine("CanFireIn".Translate() + ": " + this.burstCooldownTicksLeft.TicksToSecondsString());
            }

            if (CompAmmo != null && CompAmmo.Props.ammoSet != null)
            {
                stringBuilder.AppendLine("CE_AmmoSet".Translate() + ": " + CompAmmo.Props.ammoSet.LabelCap);
            }
            /*
            if (this.def.building.turretShellDef != null)
            {
                if (this.loaded)
                {
                    stringBuilder.AppendLine("ShellLoaded".Translate());
                }
                else
                {
                    stringBuilder.AppendLine("ShellNotLoaded".Translate());
                }
            }
            */
            return stringBuilder.ToString().TrimEndNewlines();
        }

        private bool IsValidTarget(Thing t)
        {
            Pawn pawn = t as Pawn;
            if (pawn != null)
            {
                if (this.GunCompEq.PrimaryVerb.verbProps.projectileDef.projectile.flyOverhead)
                {
                    RoofDef roofDef = base.Map.roofGrid.RoofAt(t.Position);
                    if (roofDef != null && roofDef.isThickRoof)
                    {
                        return false;
                    }
                }
                if (this.mannableComp == null)
                {
                    return !GenAI.MachinesLike(base.Faction, pawn);
                }
                if (pawn.RaceProps.Animal && pawn.Faction == Faction.OfPlayer)
                {
                    return false;
                }
            }
            return true;
        }
        
        public override void OrderAttack(LocalTargetInfo targ)
        {
            if (!targ.IsValid)
            {
                if (this.forcedTarget.IsValid)
                {
                    this.ResetForcedTarget();
                }
                return;
            }
            if ((targ.Cell - base.Position).LengthHorizontal < this.GunCompEq.PrimaryVerb.verbProps.minRange)
            {
                Messages.Message("MessageTargetBelowMinimumRange".Translate(), this, MessageSound.RejectInput);
                return;
            }
            if ((targ.Cell - base.Position).LengthHorizontal > this.GunCompEq.PrimaryVerb.verbProps.range)
            {
                Messages.Message("MessageTargetBeyondMaximumRange".Translate(), this, MessageSound.RejectInput);
                return;
            }
            if (this.forcedTarget != targ)
            {
                this.forcedTarget = targ;
                if (this.burstCooldownTicksLeft <= 0)
                {
                    this.TryStartShootSomething(false);
                }
            }
        }

        private void ResetCurrentTarget()
        {
            this.currentTargetInt = LocalTargetInfo.Invalid;
            this.burstWarmupTicksLeft = 0;
        }

        private void ResetForcedTarget()
        {
            this.forcedTarget = LocalTargetInfo.Invalid;
            this.burstWarmupTicksLeft = 0;
            if (this.burstCooldownTicksLeft <= 0)
            {
                this.TryStartShootSomething(false);
            }
        }

        // Added CompAmmo setup
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            powerComp = base.GetComp<CompPowerTrader>();
            mannableComp = base.GetComp<CompMannable>();
        }
        
        private IAttackTargetSearcher TargSearcher()
        {
            if (this.mannableComp != null && this.mannableComp.MannedNow)
            {
                return this.mannableComp.ManningPawn;
            }
            return this;
        }

        public override void Tick()
        {
            base.Tick();
            if (ticksUntilAutoReload > 0) ticksUntilAutoReload--;   // Reduce time until we can auto-reload
            if (CompAmmo?.curMagCount == 0 && (MannableComp?.MannedNow ?? false)) TryOrderReload();
            /*
            if (!CanSetForcedTarget && forcedTarget.IsValid)
            {
                ResetForcedTarget();
            }
            */
            if (!CanToggleHoldFire)
            {
                holdFire = false;
            }
            if (forcedTarget.ThingDestroyed)
            {
                ResetForcedTarget();
            }
            bool flag = (this.powerComp == null || this.powerComp.PowerOn) && (this.mannableComp == null || this.mannableComp.MannedNow);
            if (flag && base.Spawned)
            {
                this.GunCompEq.verbTracker.VerbsTick();
                if (!this.stunner.Stunned && this.GunCompEq.PrimaryVerb.state != VerbState.Bursting)
                {
                    if (this.WarmingUp)
                    {
                        this.burstWarmupTicksLeft--;
                        if (this.burstWarmupTicksLeft == 0)
                        {
                            this.BeginBurst();
                        }
                    }
                    else
                    {
                        if (this.burstCooldownTicksLeft > 0)
                        {
                            this.burstCooldownTicksLeft--;
                        }
                        if (this.burstCooldownTicksLeft <= 0)
                        {
                            this.TryStartShootSomething(true);
                        }
                    }
                    this.top.TurretTopTick();
                }
            }
            else
            {
                this.ResetCurrentTarget();
            }
        }

        protected LocalTargetInfo TryFindNewTarget()
        {
            IAttackTargetSearcher attackTargetSearcher = this.TargSearcher();
            Faction faction = attackTargetSearcher.Thing.Faction;
            float range = this.GunCompEq.PrimaryVerb.verbProps.range;
            float minRange = this.GunCompEq.PrimaryVerb.verbProps.minRange;
            Building t;
            if (Rand.Value < 0.5f && this.GunCompEq.PrimaryVerb.verbProps.projectileDef.projectile.flyOverhead && faction.HostileTo(Faction.OfPlayer) && base.Map.listerBuildings.allBuildingsColonist.Where(delegate (Building x)
            {
                float num = (float)x.Position.DistanceToSquared(this.Position);
                return num > minRange * minRange && num < range * range;
            }).TryRandomElement(out t))
            {
                return t;
            }
            TargetScanFlags targetScanFlags = TargetScanFlags.NeedThreat;
            if (!this.GunCompEq.PrimaryVerb.verbProps.projectileDef.projectile.flyOverhead)
            {
                targetScanFlags |= TargetScanFlags.NeedLOSToAll;
                targetScanFlags |= TargetScanFlags.LOSBlockableByGas;
            }
            if (this.GunCompEq.PrimaryVerb.verbProps.ai_IsIncendiary)
            {
                targetScanFlags |= TargetScanFlags.NeedNonBurning;
            }
            return (Thing)AttackTargetFinder.BestShootTargetFromCurrentPosition(attackTargetSearcher, new Predicate<Thing>(this.IsValidTarget), range, minRange, targetScanFlags);
        }

        // Added ammo check and use verb warmup time instead of turret's
        protected void TryStartShootSomething(bool canBeginBurstImmediately)
        {
            // Check for ammo first
            if (!base.Spawned 
                || (this.holdFire && this.CanToggleHoldFire) 
                || (this.GunCompEq.PrimaryVerb.verbProps.projectileDef.projectile.flyOverhead && base.Map.roofGrid.Roofed(base.Position))
                || (CompAmmo != null && (isReloading || (mannableComp == null && CompAmmo.curMagCount <= 0))))
            {
                this.ResetCurrentTarget();
                return;
            }
            bool isValid = this.currentTargetInt.IsValid;
            if (this.forcedTarget.IsValid)
            {
                this.currentTargetInt = this.forcedTarget;
            }
            else
            {
                this.currentTargetInt = this.TryFindNewTarget();
            }
            if (!isValid && this.currentTargetInt.IsValid)
            {
                SoundDefOf.TurretAcquireTarget.PlayOneShot(new TargetInfo(base.Position, base.Map, false));
            }
            if (this.currentTargetInt.IsValid)
            {
                // Use verb warmup time instead of turret's
                if (AttackVerb.verbProps.warmupTime > 0f)
                {
                    this.burstWarmupTicksLeft = AttackVerb.verbProps.warmupTime.SecondsToTicks();
                }
                else if (canBeginBurstImmediately)
                {
                    this.BeginBurst();
                }
                else
                {
                    this.burstWarmupTicksLeft = 1;
                }
            }
            else
            {
                this.ResetCurrentTarget();
            }
        }

        // New methods

        public void TryOrderReload()
        {
            /*
            if (mannableComp == null)
            {
                if (!CompAmmo.useAmmo) CompAmmo.LoadAmmo();
                return;
            }
            */

            if ((!mannableComp?.MannedNow ?? true) || (CompAmmo.currentAmmo == CompAmmo.SelectedAmmo && CompAmmo.curMagCount == CompAmmo.Props.magazineSize)) return;
            Job reloadJob = null;
            if (CompAmmo.useAmmo)
            {
                CompInventory inventory = mannableComp.ManningPawn.TryGetComp<CompInventory>();
                if (inventory != null)
                {
                    Thing ammo = inventory.container.FirstOrDefault(x => x.def == CompAmmo.SelectedAmmo);
                    if (ammo != null)
                    {
                        Thing droppedAmmo;
                        int amount = CompAmmo.Props.magazineSize;
                        if (CompAmmo.currentAmmo == CompAmmo.SelectedAmmo) amount -= CompAmmo.curMagCount;
                        if (inventory.container.TryDrop(ammo, this.Position, this.Map, ThingPlaceMode.Direct, Mathf.Min(ammo.stackCount, amount), out droppedAmmo))
                        {
                            reloadJob = new Job(CE_JobDefOf.ReloadTurret, this, droppedAmmo) { count = droppedAmmo.stackCount };
                        }
                    }
                }
            }
            if (reloadJob == null)
            {
                reloadJob = new WorkGiver_ReloadTurret().JobOnThing(mannableComp.ManningPawn, this);
            }
            if (reloadJob != null)
            {
                var pawn = mannableComp.ManningPawn;
                pawn.jobs.StartJob(reloadJob, JobCondition.Ongoing, null, pawn.CurJob?.def != reloadJob.def);
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }
            // Ammo gizmos
            if (CompAmmo != null)
            {
                foreach (Command com in CompAmmo.CompGetGizmosExtra())
                {
                    yield return com;
                }
            }
            // Fire mode gizmos
            if (CompFireModes != null)
            {
                foreach (Command com in CompFireModes.GenerateGizmos())
                {
                    yield return com;
                }
            }
            if (Faction == Faction.OfPlayer)
            {
                // Set forced target gizmo
                if (CanSetForcedTarget)
                {
                    yield return new Command_VerbTarget
                    {
                        defaultLabel = "CommandSetForceAttackTarget".Translate(),
                        defaultDesc = "CommandSetForceAttackTargetDesc".Translate(),
                        icon = ContentFinder<Texture2D>.Get("UI/Commands/Attack", true),
                        verb = GunCompEq.PrimaryVerb,
                        hotKey = KeyBindingDefOf.Misc4
                    };
                }
                // Stop forced attack gizmo
                if (forcedTarget.IsValid)
                {
                    Command_Action stop = new Command_Action();
                    stop.defaultLabel = "CommandStopForceAttack".Translate();
                    stop.defaultDesc = "CommandStopForceAttackDesc".Translate();
                    stop.icon = ContentFinder<Texture2D>.Get("UI/Commands/Halt", true);
                    stop.action = delegate
                    {
                        ResetForcedTarget();
                        SoundDefOf.TickLow.PlayOneShotOnCamera(null);
                    };
                    if (!this.forcedTarget.IsValid)
                    {
                        stop.Disable("CommandStopAttackFailNotForceAttacking".Translate());
                    }
                    stop.hotKey = KeyBindingDefOf.Misc5;
                    yield return stop;
                }
                // Toggle fire gizmo
                if (CanToggleHoldFire)
                {
                    yield return new Command_Toggle
                    {
                        defaultLabel = "CommandHoldFire".Translate(),
                        defaultDesc = "CommandHoldFireDesc".Translate(),
                        icon = ContentFinder<Texture2D>.Get("UI/Commands/HoldFire", true),
                        hotKey = KeyBindingDefOf.Misc6,
                        toggleAction = delegate
                        {
                            holdFire = !holdFire;
                            if (holdFire)
                            {
                                ResetForcedTarget();
                            }
                        },
                        isActive = (() => holdFire)
                    };
                }
            }

        }

        #endregion

    }
}
