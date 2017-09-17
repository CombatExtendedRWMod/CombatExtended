using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace CombatExtended
{
    public class CE_MoteMaker
    {// RimWorld.MoteMaker
        public static Mote ThrowSwearIcon(Thing pawn, ThingDef moteDef, Color color)
        {
            var cell = pawn.Position;
            var map = pawn.Map;
            if (!cell.ShouldSpawnMotesAt(map) || map.moteCounter.Saturated)
            {
                return null;
            }
            MoteThrown moteThrown = (MoteThrown)ThingMaker.MakeThing(moteDef, null);
            moteThrown.instanceColor = color;
            moteThrown.Attach(pawn);
            moteThrown.Scale = 1.0f;
            moteThrown.rotationRate = Rand.Range(-3f, 3f);
            moteThrown.exactPosition = cell.ToVector3Shifted();
            moteThrown.exactPosition += new Vector3(0.35f, 0f, 0.35f);
            moteThrown.exactPosition += new Vector3(Rand.Value, 0f, Rand.Value) * 0.1f;
            moteThrown.SetVelocity((float)Rand.Range(30, 60), 0.42f);
            GenSpawn.Spawn(moteThrown, cell, map);
            return moteThrown;
        }

     // public static Mote ThrowSwearIcon(Thing pawn, Pawn pawn2, ThingDef swearMote, Color color)
     // {
     //
     //     MoteInteraction moteInteraction = (MoteInteraction)ThingMaker.MakeThing(swearMote, null);
     //     moteInteraction.Scale = 1.25f;
     //     moteInteraction.SetupInteractionMote(GraphicDatabase.Get<Graphic_Single>(swearMote.graphicData.texPath, ShaderDatabase.Cutout, Vector2.one, color).MatFront.mainTexture as Texture2D, pawn2);
     //     moteInteraction.Attach(pawn);
     //     GenSpawn.Spawn(moteInteraction, pawn.Position);
     //
     //     return moteInteraction;
     // }
    }
}
