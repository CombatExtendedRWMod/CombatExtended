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
		private const float StuckPenetrationAmount=0.2f;

		public static BulletCE currentBullet;
		private float penAmount=-1f;
		public float ArmorPenetration{
			get{
				if(penAmount<0f){
					penAmount=(def.projectile as ProjectilePropertiesCE).armorPenetration;
				}
				return penAmount;
			}
			set{
				penAmount=value;
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

                // Apply primary damage
				BulletCE.currentBullet=this;
				hitThing.TakeDamage(dinfo).InsertIntoLog(logEntry);
				BulletCE.currentBullet=null;
				float lastArmorPenetration=ArmorPenetration;

				if(hitThing is Pawn){
					Pawn pawn=(Pawn)hitThing;
					ArmorPenetration=ArmorUtilityCE.getRemainingPenetrationAfterDamagePawn(pawn,ArmorPenetration);
				}else{
					ArmorPenetration=ArmorUtilityCE.getRemainingPenetrationAfterDamageThing(hitThing,ArmorPenetration);
				}


				if(ArmorPenetration<StuckPenetrationAmount){
					landed=true;
				}else{
					float newSpeed=ArmorPenetration/lastArmorPenetration*shotSpeed;
					this.shotSpeed=newSpeed;
					relaunch();
				}

                // Apply secondary to non-pawns (pawn secondary damage is handled in the damage worker)
                var projectilePropsCE = def.projectile as ProjectilePropertiesCE;
                if(!(hitThing is Pawn) && projectilePropsCE != null && !projectilePropsCE.secondaryDamage.NullOrEmpty())
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
    }
}