using System;
using Verse;
using System.Collections.Generic;
namespace CombatExtended
{
	public class Bullet_ArmorPenetrationTrackerCE
	{
        public static List<Bullet_ArmorPenetrationRecordCE> records = new List<Bullet_ArmorPenetrationRecordCE>();

        public class Bullet_ArmorPenetrationRecordCE
		{
			public Thing launcher;
			public ThingDef bulletDef;
			public DamageDef damageDef;
			public BodyPartRecord hitPart;
			public float armorPenetration;
            /**
             * <summary>
             * Constructor that create a bullet-ap pair record. First 4 parms represent a bullet, sense I can't put a bullet reference into a DamageInfo.
             * </summary>
             * <param name="launcher">Thing that is the launcher of this bullet</param>
             * <param name ="bulletDef">ThingDef of this bullet</param>
             * <param name ="damageDef">DamageDef of this damage</param>
             * <param name ="hitPart">BodyPartRecord that is hit by this bullet</param>
             * <param name ="armorPenetration">armorPenetration of this bullet</param>
             */
            public Bullet_ArmorPenetrationRecordCE(Thing launcher, ThingDef bulletDef, DamageDef damageDef, BodyPartRecord hitPart, float armorPenetration)
			{
				this.launcher = launcher;
                this.bulletDef = bulletDef;
				this.damageDef = damageDef;
				this.hitPart = hitPart;
				this.armorPenetration = armorPenetration;
			}
		}
	}
}