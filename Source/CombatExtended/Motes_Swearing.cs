using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace CombatExtended
{
    public class Motes_Swearing
    {
        public static readonly List<ThingDef> SwearList = new List<ThingDef>();

        public static void InitializeSwearList()
        {
            for (int i = 01; i <= 52; i++)
            {
                SwearList.Add(ThingDef.Named("Mote_Swear_" + i.ToString("D2") + "a"));
            }
        }
    }
}
