﻿using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnboundLib.Extensions;
using System.Linq;

namespace UnboundLib.Patches
{
    [HarmonyPatch(typeof(PointVisualizer), "DoShowPoints")]
    class PointVisualizer_Patch_DoShowPoints
    {
        static int GetColorIDFromPlayerID(int playerID)
        {
            return PlayerManager.instance.players[playerID].colorID();
        }
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var m_GetPlayerSkinColors = UnboundLib.ExtensionMethods.GetMethodInfo(typeof(PlayerSkinBank), nameof(PlayerSkinBank.GetPlayerSkinColors));
            var m_getColorID = UnboundLib.ExtensionMethods.GetMethodInfo(typeof(PointVisualizer_Patch_DoShowPoints), nameof(PointVisualizer_Patch_DoShowPoints.GetColorIDFromPlayerID));

            foreach (var ins in instructions)
            {
                if (ins.Calls(m_GetPlayerSkinColors))
                {
                    // we want colorID instead of 0/1
                    yield return new CodeInstruction(OpCodes.Call, m_getColorID); // call the colorID method, which pops the constant 0/1 off the stack and leaves the result [colorID, ...]
                }

                yield return ins;

            }
        }
    }
}
