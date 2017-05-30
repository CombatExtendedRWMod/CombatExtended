﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Harmony;
using Verse;
using RimWorld;
using UnityEngine;
using Verse.AI;

namespace CombatExtended.Harmony
{
    /*
     * The additional item to be inserted adds learning information for CE when a weapon is selected for equipping.
     * The target is actually a nested (compiler generated) class' method.
     * Used dynamic targetting in case the target assemly changes the name of the target class will most certainly change or even shift position (removing the ability to count).
     * Looking for a signature (field by name/type) that identifies the desired class without looking at it's code.
     */
    [HarmonyPatch]
    static class FloatMenuMakerMap_PatchKnowledge
    {
        static readonly string logPrefix = Assembly.GetExecutingAssembly().GetName().Name + " :: " + typeof(FloatMenuMakerMap_PatchKnowledge).Name + " :: ";

        static MethodBase TargetMethod()
        {
            List<Type> classes = typeof(FloatMenuMakerMap).GetNestedTypes(AccessTools.all).ToList();
            MethodBase target = null; //classes.First().GetMethods().First(); // a bailout so that harmony doesn't choke.
            foreach (Type clas in classes.Where(c => c.Name.Contains("AddHumanlikeOrders")))
            {
                FieldInfo info = AccessTools.Field(clas, "equipment");
                if (info != null && info.FieldType == typeof(ThingWithComps))
                {
                    target = clas.GetMethods(AccessTools.all).FirstOrDefault(m => m.Name.Contains("m__"));
                    break;
                }
            }
            return target;
        }

        // __instance required, don't need to interface with any properties/fields.
        // __result isn't apt, target return is void.
        static void Postfix()
        {
            LessonAutoActivator.TeachOpportunity(CE_ConceptDefOf.CE_AimingSystem, OpportunityType.GoodToKnow);
        }

    }

    [HarmonyPatch(typeof(FloatMenuMakerMap))]
    [HarmonyPatch("AddHumanlikeOrders")]
    [HarmonyPatch(new Type[] { typeof(Vector3), typeof(Pawn), typeof(List<FloatMenuOption>) })]
    static class FloatMenuMakerMap_Modify_AddHumanlikeOrders
    {
        static readonly string logPrefix = Assembly.GetExecutingAssembly().GetName().Name + " :: " + typeof(FloatMenuMakerMap_Modify_AddHumanlikeOrders).Name + " :: ";

        /* 
         * Opted for a postfix as the original Detour had the code inserted generally after other code had run and because we want the target's code
         * to always run unmodified.
         * There are two goals for this postfix, to add menu items for stabalizing a target and to add inventory pickup functions for pawns.
         * -Both when right clicking on something with a pawn selected.
         */

        // __instance isn't apt, target is static.
        // __result isn't apt, target return is void.
        [HarmonyPostfix]
        static void AddMenuItems(Vector3 clickPos, Pawn pawn, List<FloatMenuOption> opts)
        {
            // Stabilize
            if (pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
            {
                foreach (LocalTargetInfo curTarget in GenUI.TargetsAt(clickPos, TargetingParameters.ForRescue(pawn), true)) // !! This needs to be patched into A17
                {
                    Pawn patient = (Pawn)curTarget.Thing;
                    if (patient.Downed
                        //&& pawn.CanReserveAndReach(patient, PathEndMode.InteractionCell, Danger.Deadly)
                        && pawn.CanReach(patient, PathEndMode.InteractionCell, Danger.Deadly)
                        && patient.health.hediffSet.GetHediffsTendable().Any(h => h.CanBeStabilizied()))
                    {
                        if (pawn.story.WorkTypeIsDisabled(WorkTypeDefOf.Doctor))
                        {
                            opts.Add(new FloatMenuOption("CE_CannotStabilize".Translate() + ": " + "IncapableOfCapacity".Translate(WorkTypeDefOf.Doctor.gerundLabel), null, MenuOptionPriority.Default));
                        }
                        else
                        {
                            string label = "CE_Stabilize".Translate(patient.LabelCap);
                            Action action = delegate
                            {
                                if (pawn.inventory == null || pawn.inventory.innerContainer == null || !pawn.inventory.innerContainer.Any(t => t.def.IsMedicine))
                                {
                                    Messages.Message("CE_CannotStabilize".Translate() + ": " + "CE_NoMedicine".Translate(), patient, MessageSound.RejectInput);
                                    return;
                                }
                                // Drop medicine from inventory
                                Medicine medicine = (Medicine)pawn.inventory.innerContainer.OrderByDescending(t => t.GetStatValue(StatDefOf.MedicalPotency)).FirstOrDefault();
                                Thing medThing;
                                if (medicine != null && pawn.inventory.innerContainer.TryDrop(medicine, pawn.Position, pawn.Map, ThingPlaceMode.Direct, 1, out medThing))
                                {
                                    Job job = new Job(CE_JobDefOf.Stabilize, patient, medThing);
                                    job.count = 1;
                                    pawn.jobs.TryTakeOrderedJob(job);
                                    PlayerKnowledgeDatabase.KnowledgeDemonstrated(CE_ConceptDefOf.CE_Stabilizing, KnowledgeAmount.Total);
                                }
                            };
                            opts.Add(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(label, action, MenuOptionPriority.Default, null, patient), pawn, patient, "ReservedBy"));
                        }
                    }
                }
            }

            // Item pickup.
            IntVec3 c = IntVec3.FromVector3(clickPos);
            CompInventory compInventory = pawn.TryGetComp<CompInventory>();
            if (compInventory != null)
            {
                List<Thing> thingList = c.GetThingList(pawn.Map);
                if (!thingList.NullOrEmpty<Thing>())
                {
                    Thing item = thingList.FirstOrDefault(thing => thing.def.alwaysHaulable && !(thing is Corpse));
                    if (item != null)
                    {
                        FloatMenuOption pickUpOption;
                        int count = 0;
                        if (!pawn.CanReach(item, PathEndMode.Touch, Danger.Deadly))
                        {
                            pickUpOption = new FloatMenuOption("CE_CannotPickUp".Translate() + " " + item.LabelShort + " (" + "NoPath".Translate() + ")", null);
                        }
                        /* else if (!pawn.CanReserve(item))
                        {
                            pickUpOption = new FloatMenuOption("CE_CannotPickUp".Translate() + " " + item.LabelShort + " (" + "ReservedBy".Translate(new object[] { pawn.Map.reservationManager.FirstReserverOf(item, pawn.Faction).LabelShort }), null);
                        } */
                        else if (!compInventory.CanFitInInventory(item, out count))
                        {
                            pickUpOption = new FloatMenuOption("CE_CannotPickUp".Translate() + " " + item.LabelShort + " (" + "CE_InventoryFull".Translate() + ")", null);
                        }
                        else
                        {
                            // Pick up one
                            pickUpOption = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("CE_PickUp".Translate() + " " + item.LabelShort,
                                new Action(delegate
                                {
                                    item.SetForbidden(false, false);
                                    Job job = new Job(JobDefOf.TakeInventory, item);
                                    job.count = 1;
                                    job.playerForced = true;
                                    pawn.Notify_HoldTrackerJob(job);
                                    pawn.jobs.TryTakeOrderedJob(job);
                                    PlayerKnowledgeDatabase.KnowledgeDemonstrated(CE_ConceptDefOf.CE_InventoryWeightBulk, KnowledgeAmount.SpecificInteraction);
                                })), pawn, item, "ReservedBy");
                        }
                        opts.Add(pickUpOption);
                        if (count > 1 && item.stackCount > 1)
                        {
                            // Pick up half
                            int countHalf = item.def.stackLimit / 2;
                            if (count > 3 && count > countHalf)
                            {
                                FloatMenuOption pickUpHalfStackOption = FloatMenuUtility.DecoratePrioritizedTask( new FloatMenuOption("CE_PickUpHalf".Translate() + " " + item.LabelShort + " x" + countHalf.ToString(),
                                   new Action(delegate
                                   {
                                       item.SetForbidden(false, false);
                                       Job job = new Job(JobDefOf.TakeInventory, item);
                                       job.count = countHalf;
                                       job.playerForced = true;
                                       pawn.Notify_HoldTrackerJob(job);
                                       pawn.jobs.TryTakeOrderedJob(job);
                                       PlayerKnowledgeDatabase.KnowledgeDemonstrated(CE_ConceptDefOf.CE_InventoryWeightBulk, KnowledgeAmount.SpecificInteraction);
                                   })), pawn, item, "ReservedBy");
                                opts.Add(pickUpHalfStackOption);
                            }

                            // Pick up all
                            FloatMenuOption pickUpStackOption = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("CE_PickUp".Translate() + " " + item.LabelShort + " x" + count.ToString(),
                                new Action(delegate
                                {
                                    item.SetForbidden(false, false);
                                    Job job = new Job(JobDefOf.TakeInventory, item);
                                    job.count = count;
                                    job.playerForced = true;
                                    pawn.Notify_HoldTrackerJob(job);
                                    pawn.jobs.TryTakeOrderedJob(job);
                                    PlayerKnowledgeDatabase.KnowledgeDemonstrated(CE_ConceptDefOf.CE_InventoryWeightBulk, KnowledgeAmount.SpecificInteraction);
                                })), pawn, item, "ReservedBy");
                            opts.Add(pickUpStackOption);
                        }
                    }
                }
            }
        }


        /* Dev Notes (Don't need to read this, a short explanation is just before the method below):
         * The IL of the region I'm interested in (As of RimWorld 0.17.6351.26908, generated via Harmony debug mode):
         * 
         * L_0b20: br Label #71 //end of previous logic block ("CannotWearBecauseOfMissingBodyParts")
         * // want to insert the new logic here...
         * L_0b25: Label #70    //Make sure this label is on our new logic block and not right here.
         * L_0b25: ldstr "ForceWear"
         * L_0b2a: ldc.i4.1
         * L_0b2b: newarr System.Object
         * L_0b30: dup
         * L_0b31: ldc.i4.0
         * L_0b32: ldloc.s 42 (RimWorld.FloatMenuMakerMap+<AddHumanlikeOrders>c__AnonStorey43C)
         * L_0b34: ldfld RimWorld.Apparel apparel   // need to remember the instruction before this to load the apparel as an arg to caller.
         * L_0b39: callvirt System.String get_LabelShort()
         * L_0b3e: stelem.ref
         * L_0b3f: call System.String Translate(System.String, System.Object[])
         * L_0b44: ldloc.s 42 (RimWorld.FloatMenuMakerMap+<AddHumanlikeOrders>c__AnonStorey43C)
         * L_0b46: ldftn Void <>m__63B()
         * L_0b4c: newobj Void .ctor(Object, IntPtr)
         * L_0b51: ldc.i4.5
         * L_0b52: ldnull
         * L_0b53: ldnull
         * L_0b54: ldc.r4 0
         * L_0b59: ldnull
         * L_0b5a: ldnull
         * L_0b5b: newobj Void .ctor(String, Action, MenuOptionPriority, Action, Thing, Single, Func`2, WorldObject) // creates the force wear menu item.
         * L_0b60: ldloc.s 34 (RimWorld.FloatMenuMakerMap+<AddHumanlikeOrders>c__AnonStorey436)
         * L_0b62: ldfld Verse.Pawn pawn
         * L_0b67: ldloc.s 42 (RimWorld.FloatMenuMakerMap+<AddHumanlikeOrders>c__AnonStorey43C)
         * L_0b69: ldfld RimWorld.Apparel apparel
         * L_0b6e: call LocalTargetInfo op_Implicit(Verse.Thing)
         * L_0b73: ldstr "ReservedBy"
         * L_0b78: call Verse.FloatMenuOption DecoratePrioritizedTask(Verse.FloatMenuOption, Verse.Pawn, LocalTargetInfo, System.String)
         * L_0b7d: stloc.s 21 (Verse.FloatMenuOption)
         * L_0b7f: Label #69
         * L_0b7f: Label #71
         * L_0b7f: ldarg.2
         * L_0b80: ldloc.s 21 (Verse.FloatMenuOption)   // need to remember this instruction for re-use in call.
         * L_0b82: callvirt Void Add(Verse.FloatMenuOption) //adds the menu item to the list (opts).
         * L_0b87: Label #66    //This is the label we want to jump to in our new logic.
         * L_0b87: Label #67
         * L_0b87: ldloc.s 34 (RimWorld.FloatMenuMakerMap+<AddHumanlikeOrders>c__AnonStorey436)
         * L_0b89: ldfld Verse.Pawn pawn
         * L_0b8e: callvirt Verse.Map get_Map()
         * L_0b93: callvirt Boolean get_IsPlayerHome()
         * 
         * A couple of routes, opted to allow the called new method to modify the list directly but could have altered the
         * IL structure to store the returned object (or null) to the local variable, branch if not null to the list.add code.
         * The path I went with should be easier to maintain.
         * 
         * New method call signature: (Pawn, Apparel, List<FloatMenuOption)
         * In the target method that correspods to arg1, a field of a nested class (discovered dynamically via code inspection), and arg2.
         * 
         * Detailed Patch Notes (plan, kept in sync with the code below):
         * *Search Phase:
         * -Locate ldstr "ForceWear"
         * --When found, store the List<Label>. (mem1)
         * -Start keeping a previous instruction cache.
         * -Locate ldfld RimWorld.Apparel apparel
         * --When found, store the previous instruction unaltered for re-use later. (mem2)
         * -Locate callvirt Void Add(Verse.FloatMenuOption)
         * -The next instruction found is expected to have a label.
         * --If it has a label, store the label as an object for re-use later. (mem3)
         * --If it DOES NOT have a label, dump the unaltered instructions as the patch failed.
         * *Patch phase:
         * -Locate ldstr "ForceWear"
         * -Insert (before)
         * --load arg1 onto the stack (Pawn), ensure that the label (mem1) is on this instruction.
         * --load the remembered class in mem1
         * --load the field RimWorld.Apparel apparel, expected that this replaces the previous item on the stack (so only 2 things so far).
         * --load arg2 onto the stack (List<FloatMenuOption)
         * --Call the new method (expected to absorb 3 items from the stack and add 1 bool back).
         * --Branch false to label remembered in mem2.
         * --Modify the instruction we located, strip the label from it.
         * 
         */

        /* The goal of this infix is to add a check for if the pawn is too loaded down with stuff (worn/inventory) before allowing them to wear
         * something.
         * Because all 3 decompilers I checked the Core code with gave me different sets of logic had to do the patch entirely from IL with no decent high
         * level reference.  That made things a bit tougher, I've left most of my dev notes intact above but reading is mostly optional.
         * When maintaining this patch will need to temporarily return all the unmodified instructions with Harmony debug mode enabled and compare
         * the code block above (in dev notes) with what Harmony generated to locate the bug.  This patch *should* work fine if the section of
         * FloatMenuMakerMap.AddHumanlikeOrders doesn't change in logic significantly.
         * The label relocation is a little soft.
         */
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Modify_ForceWear(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            int searchPhase = 0;
            bool patched = false;
            string targetString = "ForceWear";
            CodeInstruction previous = null;

            List<Label> startLabel = null;   // mem0
            CodeInstruction apparelField1 = null;   // mem1
            CodeInstruction apparelField2 = null;   // mem1
            List<Label> branchLabel = null;  // mem2

            foreach (CodeInstruction instruction in instructions)
            {
                // NOTE: The reverse order of the phases is important.

                // -The next instruction found is expected to have a label.
                if (searchPhase == 3)
                {
                    if (instruction.labels != null)
                        branchLabel = instruction.labels;
                    break;
                }

                // -Locate callvirt Void Add(Verse.FloatMenuOption)
                if (searchPhase == 2 && instruction.opcode == OpCodes.Callvirt && (instruction.operand as MethodInfo) != null && (instruction.operand as MethodInfo).Name == "Add"
                    && (previous.operand as LocalVariableInfo) != null && (previous.operand as LocalVariableInfo).LocalType == typeof(FloatMenuOption))
                    searchPhase = 3;

                // -Locate ldfld RimWorld.Apparel apparel
                if (searchPhase == 1 && instruction.opcode == OpCodes.Ldfld && (instruction.operand as FieldInfo) != null && (instruction.operand as FieldInfo).Name == "apparel")
                {
                    apparelField1 = previous;
                    apparelField2 = instruction;
                    searchPhase = 2;
                }

                // -Locate ldstr "ForceWear"
                if (searchPhase == 0 && instruction.opcode == OpCodes.Ldstr && (instruction.operand as String) != null && (instruction.operand as String).Equals(targetString))
                {
                    startLabel = instruction.labels;
                    searchPhase = 1;
                }

                // -Start keeping a previous instruction cache.
                if (searchPhase > 0 && searchPhase < 3)
                    previous = instruction;
            }

            if (branchLabel != null)
            {
                // search succeeded, find our insertion point again and insert the patch.
                foreach (CodeInstruction instruction in instructions)
                {
                    if (!patched && instruction.opcode == OpCodes.Ldstr && (instruction.operand as String) != null && (instruction.operand as String).Equals(targetString))
                    {
                        // arg0 Pawn, also set the label here.
                        CodeInstruction ldPawn = new CodeInstruction(OpCodes.Ldarg_1);
                        ldPawn.labels.AddRange(startLabel);
                        yield return ldPawn;
                        // arg1 Apparel.
                        yield return apparelField1;
                        yield return apparelField2;
                        // arg2 List<FloatMenuOption>.
                        yield return new CodeInstruction(OpCodes.Ldarg_2);
                        // call
                        yield return new CodeInstruction(OpCodes.Call, typeof(FloatMenuMakerMap_Modify_AddHumanlikeOrders).GetMethod("ForceWearInventoryCheck", AccessTools.all));
                        // branch if return from call is false, indicates original code should not run.
                        yield return new CodeInstruction(OpCodes.Brfalse, branchLabel.First());
                        // modify the original labeled instruction to remove it's label.
                        instruction.labels.Clear();
                    }
                    yield return instruction;
                }
            } else
            {
                // patch failure, just dump the data out
                Log.Error(string.Concat(logPrefix, "Error applying patch to ForceWear, no change."));
                foreach (CodeInstruction instruction in instructions)
                    yield return instruction;
            }

        }

        /// <summary>
        /// This method is a short segment of code originally from Detour_FloatMenuMakerMap.  Handles checking of the pawn has enough space to wear the item.
        /// </summary>
        /// <param name="pawn">Pawn which the FloatMenu is being generated for.</param>
        /// <param name="apparel">Apparel which the player right clicked on with intent to have the pawn wear.</param>
        /// <param name="opts">List of FloatMenuOption which this method may modify in the event the pawn can't wear the item.</param>
        /// <returns>bool, true indicates the pawn CAN wear the item (and that opts is unmodified), false indicates the pawn cannot wear the item (and opts is modified).</returns>
        /// <remarks>This method should be used exclusively with the Harmony infix patch since the patch is difficult to alter.  return and mutability MUST be maintained carefully.</remarks>
        static bool ForceWearInventoryCheck(Pawn pawn, Apparel apparel, List<FloatMenuOption> opts)
        {
            CompInventory compInventory = pawn.TryGetComp<CompInventory>();
            int count;
            if (compInventory != null && !compInventory.CanFitInInventory(apparel, out count, false, true))
            {
                FloatMenuOption item4 = new FloatMenuOption("CannotWear".Translate(new object[] { apparel.Label }) + " (" + "CE_InventoryFull".Translate() + ")", null);
                opts.Add(item4);
                return false;
            }
            return true;
        }

    }
}