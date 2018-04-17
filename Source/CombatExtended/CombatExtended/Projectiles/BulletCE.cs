using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using Verse.Sound;
using RimWorld;
using UnityEngine;

namespace CombatExtended
{
    public class BulletCE : ProjectileCE
    {
        private const float StunChance = 0.1f;
        private float armorPenetration = -1;
        /**
         * <summary>
         * Armor penetration of this bullet. Everytime this bullet hit something, this value will be reduced using ArmorUtilityCE.TryPenetrateArmor() method.
         * </summary>
         */
        private float ArmorPenetration
        {
        	get
        	{
        		if (armorPenetration < 0)
        		{
        			armorPenetration = ((ProjectilePropertiesCE)def.projectile).armorPenetration;
        		}
        		return armorPenetration;
        	}
        	set
        	{
        		armorPenetration = value;
        	}
        }

        private void LogImpact(Thing hitThing, out BattleLogEntry_RangedImpact logEntry)
        {
			logEntry =
				new BattleLogEntry_RangedImpact(
					launcher,
					hitThing,
					intendedTarget,
					equipmentDef,
					def);
			
			Find.BattleLog.Add(logEntry);
        }
        
        protected override void Impact(Thing hitThing)
        {
            Map map = base.Map;
            BattleLogEntry_RangedImpact logEntry = null;
			
            if (logMisses
                || 
                (!logMisses
                    && hitThing != null
                    && (hitThing is Pawn
                        || hitThing is Building_Turret)
                 ))
            {
            	LogImpact(hitThing, out logEntry);
            }

            float relaunchSpeed = -1f;
            
            if (hitThing != null)
            {
                int damageAmountBase = def.projectile.damageAmountBase;
                DamageDefExtensionCE damDefCE = def.projectile.damageDef.GetModExtension<DamageDefExtensionCE>() ?? new DamageDefExtensionCE();

                DamageInfo dinfo = new DamageInfo(
                    def.projectile.damageDef,
                    damageAmountBase,
                    ExactRotation.eulerAngles.y,
                    launcher,
                    null,
                    def);
                
                // Set impact height
                BodyPartDepth partDepth = damDefCE != null && damDefCE.harmOnlyOutsideLayers ? BodyPartDepth.Outside : BodyPartDepth.Undefined;
                	//NOTE: ExactPosition.y isn't always Height at the point of Impact!
                BodyPartHeight partHeight = new CollisionVertical(hitThing).GetCollisionBodyHeight(ExactPosition.y);
                dinfo.SetBodyRegion(partHeight, partDepth);
                if (damDefCE != null && damDefCE.harmOnlyOutsideLayers) dinfo.SetBodyRegion(BodyPartHeight.Undefined, BodyPartDepth.Outside);
                // Choose a part before TakeDamage method call, because I need it to help ArmorUtilityCE.GetAfterArmorDamage() to find this bullet.
                Pawn pawn = hitThing as Pawn;
                BodyPartRecord hitPart = null;
                if (pawn != null)
                {
                    hitPart = pawn.health.hediffSet.GetRandomNotMissingPart(dinfo.Def, dinfo.Height, dinfo.Depth);
                    dinfo.SetHitPart(hitPart);
                }
                Bullet_ArmorPenetrationTrackerCE.Bullet_ArmorPenetrationRecordCE record = Bullet_ArmorPenetrationTrackerCE.records.Find((r) => r.launcher == launcher && r.bulletDef == def && r.damageDef == def.projectile.damageDef && r.hitPart == hitPart);
                if (record != null)
                {
                    Log.Message("Error: A Pawn has damaged the same part with the same weapon the same damage type at the same time.");
                }
                record = new Bullet_ArmorPenetrationTrackerCE.Bullet_ArmorPenetrationRecordCE(launcher, def, def.projectile.damageDef, hitPart, ArmorPenetration);
                Bullet_ArmorPenetrationTrackerCE.records.Add(record);

                // Apply primary damage
                hitThing.TakeDamage(dinfo).InsertIntoLog(logEntry);

                // Delete the record
                Bullet_ArmorPenetrationTrackerCE.records.Remove(record);

                // Apply secondary to non-pawns (pawn secondary damage is handled in the damage worker)
                var projectilePropsCE = def.projectile as ProjectilePropertiesCE;
                if(projectilePropsCE != null && !projectilePropsCE.secondaryDamage.NullOrEmpty())
                {
                    if(!(hitThing is Pawn))
                    {
                        foreach(SecondaryDamage cur in projectilePropsCE.secondaryDamage)
                        {
                        if (hitThing.Destroyed) break;
                        var secDinfo = new DamageInfo(
							cur.def,
							cur.amount,
							ExactRotation.eulerAngles.y,
							launcher,
							null,
							def);
                        hitThing.TakeDamage(secDinfo).InsertIntoLog(logEntry);
                        }
                    }

                    // If there is secondary damage, this bullet should never penetrate.
                    ExactPosition = ExactPosition;
                    landed = true;
                }

                // Calculate a armor penetration value then try penetrate it.
                float newAP = record.armorPenetration;
                float HPToArmor = -1f;
                float uselessNum = 0f;    //useless
                if(pawn != null)
                {
                    // Armor of a pawn is HealthScale * ArmorUtilityCE.ArmorPerHealthScale
                    HPToArmor = pawn.HealthScale* ArmorUtilityCE.ArmorPerHealthScale;
                }
                else
                {
                    // Armor of non-pawn is MaxHitPoints / 100 * ArmorUtilityCE.ArmorPer100HP
                    HPToArmor = hitThing.MaxHitPoints / 100 * ArmorUtilityCE.ArmorPer100MaxHP;
                }
                ArmorUtilityCE.TryPenetrateArmor(def.projectile.damageDef, HPToArmor, ref newAP, ref uselessNum);
                float lastAP = ArmorPenetration;
                ArmorPenetration = newAP;
                // Final remaining armor penetration is compared with ArmorUtilityCE.RequiredAPToPenetrate to decide whether it can penetrate.
                if (ArmorPenetration < ArmorUtilityCE.RequiredAPToPenetrate)
                {
                    ExactPosition = ExactPosition;
                    landed = true;
                }
                else
                {
                    // New speed to be used to relaunch this bullet. Remaining speed is the same portion with remaining armor penetration.
                    relaunchSpeed = shotSpeed * (ArmorPenetration / lastAP);
                }
            }
            else
            {
                SoundDefOf.BulletImpactGround.PlayOneShot(new TargetInfo(base.Position, map, false));
                
                //Only display a dirt hit for projectiles with a dropshadow
                if (base.castShadow)
                	MoteMaker.MakeStaticMote(ExactPosition, map, ThingDefOf.Mote_ShotHit_Dirt, 1f);
            }
            base.Impact(hitThing);
        }
        /**
         * <summary>
         * Save armor penetration.
         * </summary>
         */
        public override void ExposeData()
        {
        	base.ExposeData();

        	Scribe_Values.Look<float>(ref armorPenetration, "ap", -1f);

        }
    }
}