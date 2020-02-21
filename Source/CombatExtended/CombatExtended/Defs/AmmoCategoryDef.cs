using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using UnityEngine;

namespace CombatExtended
{
    public class AmmoCategoryDef : Def
    {
        public bool advanced = false;
        public string labelShort;

        public string LabelCapShort => labelShort.CapitalizeFirst();

        public Color color = new Color(0.2f, 0.8f, 0.85f);
    }
}
