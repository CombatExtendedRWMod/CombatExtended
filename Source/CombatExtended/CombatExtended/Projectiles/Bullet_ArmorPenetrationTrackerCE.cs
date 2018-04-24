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

            /// <summary>
            /// A bullet-ap pair record. First 4 parms represent a bullet, sense I can't put a bullet reference into a DamageInfo.
            /// <see cref="T:CombatExtended.Bullet_ArmorPenetrationTrackerCE.Bullet_ArmorPenetrationRecordCE"/> class.
            /// </summary>
            /// <param name="launcher">Launcher.</param>
            /// <param name="bulletDef">Bullet def.</param>
            /// <param name="damageDef">Damage def.</param>
            /// <param name="hitPart">Hit part.</param>
            /// <param name="armorPenetration">Armor penetration.</param>
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