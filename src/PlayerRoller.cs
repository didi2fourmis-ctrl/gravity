using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace GravityFlip
{
    /// <summary>
    /// Retourne le joueur de 180° autour de l'axe avant quand la gravité est inversée :
    /// ses pieds (et le détecteur de sol) touchent le plafond, la caméra est à hauteur
    /// d'yeux sous le plafond et la vue est naturellement à l'envers.
    ///
    /// Le jeu réécrit la rotation du joueur (le lacet de la souris) à chaque image. Après chacune
    /// de ses méthodes, on relit ce lacet et on réapplique notre retournement par-dessus.
    /// Tête en bas, tourner la souris à droite ferait tourner à gauche : on inverse donc
    /// le lacet autour de sa valeur au moment du retournement.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public class PlayerRoller : MonoBehaviour
    {
        private static readonly FieldInfo RotationYField = AccessTools.Field(typeof(CameraController), "rotationY");

        private static bool wrotePlayer;
        private static Quaternion lastPlayerWrite;
        private static bool wroteCamera;
        private static Quaternion lastCameraWrite;

        private static float gameYaw;
        private static bool yawMirrored;
        private static float yawPivot;
        private static float gamePitch;

        private void LateUpdate() => Apply();

        public static void OnInvertChanged(bool inverted)
        {
            if (!Plugin.FlipPlayer.Value)
                return;

            var player = GameObjects.Player;
            if (player != null && !wrotePlayer)
                gameYaw = YawOf(player.transform.rotation);

            if (inverted && !yawMirrored)
            {
                yawPivot = gameYaw;
                yawMirrored = true;
            }
            else if (!inverted && yawMirrored)
            {
                // On rend au jeu le lacet affiché, pour que la vue ne saute pas en se remettant à l'endroit.
                float shown = 2f * yawPivot - gameYaw;
                var controller = GameObjects.CameraControl;
                if (controller != null && RotationYField != null && RotationYField.FieldType == typeof(float))
                    RotationYField.SetValue(controller, shown);
                gameYaw = shown;
                yawMirrored = false;
            }
        }

        public static void Apply()
        {
            var player = GameObjects.Player;
            if (player == null)
            {
                wrotePlayer = wroteCamera = false;
                return;
            }

            float roll = Plugin.FlipPlayer.Value ? 180f * Mathf.SmoothStep(0f, 1f, GravityController.FlipProgress) : 0f;
            Transform t = player.transform;

            // Le jeu a-t-il réécrit la rotation depuis notre dernier passage ?
            if (!wrotePlayer || Quaternion.Angle(t.rotation, lastPlayerWrite) > 0.01f)
                gameYaw = YawOf(t.rotation);

            if (roll < 0.01f && !yawMirrored)
            {
                wrotePlayer = wroteCamera = false;
                return;
            }

            float shownYaw = yawMirrored ? 2f * yawPivot - gameYaw : gameYaw;
            Quaternion desired = Quaternion.Euler(0f, shownYaw, 0f) * Quaternion.Euler(0f, 0f, roll);

            if (Quaternion.Angle(t.rotation, desired) > 0.01f)
            {
                t.rotation = desired;
                var body = player.GetComponent<Rigidbody>();
                if (body != null)
                    body.rotation = desired;
            }
            lastPlayerWrite = t.rotation;
            wrotePlayer = true;

            FixCamera(t);
        }

        /// <summary>
        /// Si la caméra est orientée en coordonnées monde par le jeu (au lieu de suivre le joueur),
        /// on la remet dans le repère retourné du joueur en gardant son inclinaison haut/bas.
        /// </summary>
        private static void FixCamera(Transform player)
        {
            var controller = GameObjects.CameraControl;
            if (controller == null)
                return;
            Transform cam = controller.transform;

            bool gameWrote = !wroteCamera || Quaternion.Angle(cam.rotation, lastCameraWrite) > 0.01f;
            if (!gameWrote)
                return;

            // La caméra suit déjà le joueur : son « haut » est du même côté que celui du joueur.
            if (Vector3.Dot(cam.up, player.up) > 0f)
            {
                wroteCamera = false;
                return;
            }

            gamePitch = -Mathf.Asin(Mathf.Clamp(cam.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
            cam.rotation = player.rotation * Quaternion.Euler(gamePitch, 0f, 0f);
            lastCameraWrite = cam.rotation;
            wroteCamera = true;
        }

        private static float YawOf(Quaternion rotation)
        {
            Vector3 forward = rotation * Vector3.forward;
            return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        }
    }

    /// <summary>Réapplique le retournement juste après chaque méthode Unity du joueur et de la caméra.</summary>
    [HarmonyPatch]
    internal static class PlayerRollerPatch
    {
        private static readonly string[] Messages = { "Update", "LateUpdate", "FixedUpdate" };

        private static IEnumerable<MethodBase> TargetMethods() =>
            new[] { typeof(CameraController), typeof(NewMovement) }
                .SelectMany(type => Messages.Select(name => AccessTools.DeclaredMethod(type, name)))
                .Where(method => method != null)
                .Cast<MethodBase>();

        private static void Postfix() => PlayerRoller.Apply();
    }
}
