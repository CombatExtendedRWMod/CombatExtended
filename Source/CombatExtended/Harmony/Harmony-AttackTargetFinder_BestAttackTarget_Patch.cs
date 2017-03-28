using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Harmony;
using Harmony.ILCopying;
using Verse;
using Verse.AI;

/*
 * concept is a bit nuts.  Want to try copying a method into a new method, patch the new method to insert a true/false method call and branch to continue if false.
 * and then patch BestAttackTarget to use the new (copied) method.
 */
// To be sure things happen in the right order, can't rely on PatchAll at all.

namespace CombatExtended.Harmony
{
	[StaticConstructorOnStartup]
	static class AttackTargetFinder_BestAttackTarget_Patch
	{
		static readonly string logPrefix = Assembly.GetExecutingAssembly().GetName().Name + " :: " + typeof(AttackTargetFinder_BestAttackTarget_Patch).Name + " :: ";
		
		static DynamicMethod modifiedCode;
		
		static AttackTargetFinder_BestAttackTarget_Patch()
		{
			
			
			// the method we want to copy from.
			MethodInfo oldMethod = typeof(GenClosest).GetMethod("ClosestThing_Global", AccessTools.all);
			// the method we want to copy to.
			MethodInfo newMethod = typeof(AttackTargetFinder_BestAttackTarget_Patch).GetMethod("ClosestThingTarget_Global", AccessTools.all);
			// the method which adds the line of code we want added in order to turn the old method into the new method.
			MethodInfo patchMethod = typeof(AttackTargetFinder_BestAttackTarget_Patch).GetMethod("CopyPatchClosestThing_Global", AccessTools.all);
			
			List<MethodInfo> empty = new List<MethodInfo>();  // doesn't matter...
			List<MethodInfo> transpilers = new List<MethodInfo>(); // puts the transpiler into the format expected by patcher.
			transpilers.Add(patchMethod);
			
			// get the patched original method (after the below line the original code will have the new stuff we want added to it).
			var replacement = MethodPatcher.CreatePatchedMethod(oldMethod, empty, empty, transpilers);
			if (replacement == null) throw new MissingMethodException("Cannot create dynamic replacement for " + oldMethod);
			modifiedCode = replacement;
			
			// get the memory location of the start of the new method.
			var originalCodeStart = Memory.GetMethodStart(newMethod);
			// get the memory location of the start of our patched old method.
			var patchCodeStart = Memory.GetMethodStart(replacement);
			// write the jump to the new code into the old method.
			Memory.WriteJump(originalCodeStart, patchCodeStart);
			
			
			MethodInfo source = typeof(AttackTargetFinder).GetMethod("BestAttackTarget", AccessTools.all); // (ProfoundDarkness) I still don't know how to handle some types of args so skipped that.
			MethodInfo transpiler = typeof(AttackTargetFinder_BestAttackTarget_Patch).GetMethod("BestAttackTarget_Patch", AccessTools.all);
			HarmonyBase.instance.Patch(source, null, null, new HarmonyMethod(transpiler));
			
		}
		
		static IEnumerable<CodeInstruction> CopyPatchClosestThing_Global(IEnumerable<CodeInstruction> instructions)
		{
			bool foundComputation = false;
			object continueLabel = null;
			int instructionCount = 0;
			bool foundInsertion = false;
			bool foundClassCast = false;
			int localThingIndex = -1;
			List<OpCode> indexOpcode = new List<OpCode>() { OpCodes.Stloc_0, OpCodes.Stloc_1, OpCodes.Stloc_2, OpCodes.Stloc_3 };
			List<OpCode> outputOpcode = new List<OpCode>() { OpCodes.Ldloc_0, OpCodes.Ldloc_1, OpCodes.Ldloc_2, OpCodes.Ldloc_3 };
			
			foreach(CodeInstruction instruction in instructions)
			{
				// the interesting instruction comes after the class cast so that's why this if is above the Classcast check.
				if (foundClassCast)
				{
					if (indexOpcode.Contains(instruction.opcode))
						localThingIndex = indexOpcode.IndexOf(instruction.opcode);
					else if (instruction.opcode == OpCodes.Stloc)
						localThingIndex = (int)instruction.operand;
				}
				
				// (ProfoundDarkness) I know there is a more apt way than ToString() but not sure what that is...
				if (!foundClassCast && instruction.opcode == OpCodes.Castclass && instruction.operand.ToString().Contains("Thing"))
					foundClassCast = true;
				
				if (!foundComputation && instruction.opcode == OpCodes.Call && (instruction.operand as MethodInfo).Name.Contains("get_LengthHorizontalSquared"))
					foundComputation = true;
				
				if (foundComputation && instruction.opcode == OpCodes.Ldloc_0)
					foundInsertion = true;
				
				if (foundComputation && instruction.opcode == OpCodes.Bge_Un)
				{
					continueLabel = instruction.operand;
					break; // found the stuff we needed to setup the patch...
				}
				
				if (!foundInsertion)
					instructionCount++;
			}
			
			// error check...
			if (continueLabel == null || localThingIndex < 0 || !foundInsertion)
			{
				Log.Warning(string.Concat(logPrefix, "Unable to find either the key label or local variable when generating method based on Verse.GenClosest.ClosestThing_Global(), Pawns may waste ammo on targets they have no hope of hitting."));
				foreach (CodeInstruction instruction in instructions)
					yield return instruction;
			} else {
				bool patched = false;
				foreach (CodeInstruction instruction in instructions)
				{
					if (!patched)
					{
						if (instructionCount <= 0)
						{
							// Roughyly equivelent line of code: if (!CanShootTarget(center, thing)) continue;
							// patched after the line float lengthHorizontalSquared = (center - thing.Position).LengthHorizontalSquared;
							// reason is that location is relatively easy to find in the IL.  Instruction count is what matters so if a better insertion point is found, change instructionCount to fit.
							
							// load first argument for method call. (also first argument that called us.)
							yield return new CodeInstruction(OpCodes.Ldarg_0);
							// load second argument for method call, the current Thing (target) being considered for being shot.
							object index = null;
							if (localThingIndex > 3) index = localThingIndex;
							yield return new CodeInstruction(localThingIndex < 4 ? outputOpcode[localThingIndex] : OpCodes.Ldloc, index);
							// call the new method.
							yield return new CodeInstruction(OpCodes.Call, typeof(AttackTargetFinder_BestAttackTarget_Patch).GetMethod("CanShootTarget", AccessTools.all));
							// branch if the call failed...
							yield return new CodeInstruction(OpCodes.Brfalse, continueLabel);
							patched = true;
						} else
							instructionCount --;
					}
					
					yield return instruction;
				}
			}
		}
		
		// code here doesn't matter...  It's nice if it's got the same signauture.
		static Thing ClosestThingTarget_Global(IntVec3 center, IEnumerable searchSet, float maxDistance = 99999, Predicate<Thing> validator = null)
		{
			return null;
		}
		
		static IEnumerable<CodeInstruction> BestAttackTarget_Patch(IEnumerable<CodeInstruction> instructions)
		{
			Log.Message("BestAttackTarget_Patch");
			return instructions
				.MethodReplacer(typeof(GenClosest).GetMethod("ClosestThing_Global", AccessTools.all),
				                modifiedCode);
//				                typeof(AttackTargetFinder_BestAttackTarget_Patch).GetMethod("ClosestThingTarget_Global", AccessTools.all));
		}
		
		static bool CanShootTarget(IntVec3 shooterPosition, Thing target)
		{
			Log.Message(string.Concat(logPrefix, "Called CanShootTarget, always returns false so targets should never shoot anything..."));
			return false;
		}
		
	}
}