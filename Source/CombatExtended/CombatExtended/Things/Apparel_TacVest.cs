using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using UnityEngine;

namespace CombatExtended
{
    public class Apparel_TacVest : Apparel_VisibleAccessory
    {
        private const float YOffsetInterval_Clothes = 0.005f; // copy-pasted from PawnRenderer

        protected override float GetAltitudeOffset(Rot4 rotation)
        {
            return 0.0269999985f;
        }
    }
}
