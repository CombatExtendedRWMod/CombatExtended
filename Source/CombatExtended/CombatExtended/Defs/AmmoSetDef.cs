using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using UnityEngine;

namespace CombatExtended
{
    public class AmmoSetDef : Def
    {
        public List<AmmoLink> ammoTypes;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string str in base.ConfigErrors())
            {
                yield return str;
            }
            
            var multiplesWithoutSingles = 
                        ammoTypes.Where(x => x.amount > 1).Select(x => x.ammo)
                .Except(ammoTypes.Where(x => x.amount == 1).Select(x => x.ammo));

            if (multiplesWithoutSingles.Any())
                yield return "has multiple ammoDefs with Amount > 1, without a fallback of Amount = 1; namely "
                    + String.Join(",", multiplesWithoutSingles.Select(x => x.defName).ToArray());
        }
    }
}
