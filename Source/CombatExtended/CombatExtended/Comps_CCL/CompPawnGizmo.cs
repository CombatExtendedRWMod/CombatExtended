using System.Collections.Generic;
using Verse;

namespace CombatExtended
{
    public class CompPawnGizmo : ThingComp
    {
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            var equip = parent is Pawn pawn
                ? pawn.equipment.Primary
                : null;

            if(
                ( equip != null )&&
                ( !equip.AllComps.NullOrEmpty() )
            )
            {
                foreach( var comp in equip.AllComps )
                {
                    if(( comp is CompRangedGizmoGiver gizmoGiver ) &&
                       ( gizmoGiver.isRangedGiver )
                    )
                    {
                        foreach( var gizmo in gizmoGiver.CompGetGizmosExtra() )
                        {
                            yield return gizmo;
                        }
                    }
                }
            }
        }

    }

}
