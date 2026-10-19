using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace GravityFlip
{
    /// <summary>
    /// Pendant un saut du joueur en gravité inversée, toute impulsion qui pousse vers le plafond
    /// (y positif) est retournée : on saute "vers le bas", en s'éloignant du plafond.
    /// Une impulsion déjà dans le bon sens (code réécrit par UpVectorRewriter) n'est pas touchée.
    /// </summary>
    internal static class JumpScope
    {
        internal static int Depth;
        internal static Rigidbody PlayerBody;

        internal static void Mirror(Rigidbody body, ref Vector3 value)
        {
            if (Depth > 0 && GravityController.Inverted && body == PlayerBody && value.y > 0f)
                value.y = -value.y;
        }
    }

    [HarmonyPatch]
    internal static class NewMovementJumpPatch
    {
        private static readonly string[] JumpMethods = { "Jump", "WallJump" };

        private static IEnumerable<MethodBase> Candidates() =>
            AccessTools.GetDeclaredMethods(typeof(NewMovement))
                .Where(m => JumpMethods.Contains(m.Name))
                .Cast<MethodBase>();

        private static bool Prepare()
        {
            bool found = Candidates().Any();
            if (!found)
                Plugin.Log.LogWarning("Méthodes de saut introuvables : les sauts ne seront pas inversés.");
            return found;
        }

        private static IEnumerable<MethodBase> TargetMethods() => Candidates();

        private static void Prefix(NewMovement __instance)
        {
            CeilingContact.JumpGraceUntil = Time.time + 0.3f;
            JumpScope.Depth++;
            JumpScope.PlayerBody = __instance.GetComponent<Rigidbody>();
        }

        private static void Finalizer()
        {
            if (JumpScope.Depth > 0)
                JumpScope.Depth--;
        }
    }

    [HarmonyPatch(typeof(Rigidbody), nameof(Rigidbody.AddForce), typeof(Vector3), typeof(ForceMode))]
    internal static class RigidbodyAddForcePatch
    {
        private static void Prefix(Rigidbody __instance, ref Vector3 __0)
        {
            JumpScope.Mirror(__instance, ref __0);
        }
    }

    [HarmonyPatch(typeof(Rigidbody), nameof(Rigidbody.velocity), MethodType.Setter)]
    internal static class RigidbodyVelocityPatch
    {
        private static void Prefix(Rigidbody __instance, ref Vector3 __0)
        {
            JumpScope.Mirror(__instance, ref __0);
        }
    }
}
