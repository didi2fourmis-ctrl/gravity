using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GravityFlip
{
    /// <summary>
    /// Gère l'état de la gravité : bascule, mode chaos, et adaptation du joueur
    /// (le détecteur de sol est déplacé au sommet du joueur pour qu'il puisse
    /// marcher, glisser et sauter depuis le plafond).
    /// </summary>
    public class GravityController : MonoBehaviour
    {
        public static bool Inverted { get; private set; }
        public static bool ChaosMode { get; private set; }

        /// <summary>0 = vue normale, 1 = vue retournée (lissé dans le temps).</summary>
        public static float FlipProgress { get; private set; }

        private static readonly FieldInfo GroundCheckField = AccessTools.Field(typeof(NewMovement), "gc");

        private float nextChaosFlip;

        private NewMovement trackedPlayer;
        private Transform groundCheck;
        private Vector3 groundCheckOriginal;
        private Vector3 groundCheckApplied;
        private bool groundCheckMirrored;

        private GUIStyle hudStyle;

        private void Awake()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            trackedPlayer = null;
            groundCheck = null;
            groundCheckMirrored = false;

            if (Plugin.ResetOnSceneLoad.Value)
                SetInverted(false, instant: true);
        }

        private void Update()
        {
            if (KeyInput.WasPressed(Plugin.ToggleKey.Value))
                SetInverted(!Inverted);

            if (KeyInput.WasPressed(Plugin.ChaosKey.Value))
            {
                ChaosMode = !ChaosMode;
                if (ChaosMode)
                    ScheduleChaos();
                Plugin.Log.LogInfo(ChaosMode ? "Mode chaos activé." : "Mode chaos désactivé.");
            }

            if (ChaosMode && Time.time >= nextChaosFlip)
            {
                SetInverted(!Inverted);
                ScheduleChaos();
            }

            float duration = Mathf.Max(0.01f, Plugin.FlipDuration.Value);
            FlipProgress = Mathf.MoveTowards(FlipProgress, Inverted ? 1f : 0f, Time.unscaledDeltaTime / duration);

            EnforceGravityDirection();
            SyncPlayer();
        }

        public static void SetInverted(bool value, bool instant = false)
        {
            if (instant)
                FlipProgress = value ? 1f : 0f;

            if (Inverted == value)
                return;

            Inverted = value;
            EnforceGravityDirection();

            // Les corps endormis ignorent la gravité : on les réveille pour qu'ils "tombent" vers le haut.
            foreach (var rb in FindObjectsOfType<Rigidbody>())
            {
                if (!rb.isKinematic)
                    rb.WakeUp();
            }

            Plugin.Log.LogInfo(value ? "Gravité inversée !" : "Gravité normale.");
        }

        /// <summary>
        /// On ne garde que le signe : si le jeu modifie l'intensité de la gravité,
        /// sa valeur est respectée, seule la direction est imposée.
        /// </summary>
        private static void EnforceGravityDirection()
        {
            var g = Physics.gravity;
            if (Inverted ? g.y < 0f : g.y > 0f)
                Physics.gravity = new Vector3(g.x, -g.y, g.z);
        }

        private void ScheduleChaos()
        {
            float min = Mathf.Max(0.5f, Plugin.ChaosMinDelay.Value);
            float max = Mathf.Max(min, Plugin.ChaosMaxDelay.Value);
            nextChaosFlip = Time.time + Random.Range(min, max);
        }

        private void SyncPlayer()
        {
            var player = GameObjects.Player;
            if (player == null)
                return;

            if (player != trackedPlayer)
            {
                trackedPlayer = player;
                groundCheck = FindGroundCheck(player);
                groundCheckMirrored = false;
                if (groundCheck != null)
                    groundCheckOriginal = groundCheck.localPosition;
                else
                    Plugin.Log.LogWarning("Détecteur de sol du joueur introuvable : impossible de marcher au plafond.");
            }

            if (groundCheck == null)
                return;

            LogDebug(player);

            // Le jeu a déplacé le détecteur lui-même (glissade, accroupi…) : on adopte sa nouvelle position.
            if (groundCheckMirrored && groundCheck.localPosition != groundCheckApplied)
            {
                groundCheckOriginal = groundCheck.localPosition;
                groundCheckMirrored = false;
            }

            if (Inverted && !groundCheckMirrored)
            {
                groundCheckApplied = MirrorAcrossBody(player, groundCheck, groundCheckOriginal);
                groundCheck.localPosition = groundCheckApplied;
                groundCheckMirrored = true;
            }
            else if (!Inverted && groundCheckMirrored)
            {
                groundCheck.localPosition = groundCheckOriginal;
                groundCheckMirrored = false;
            }
        }

        private float nextDebugLog;

        private void LogDebug(NewMovement player)
        {
            if (!Plugin.DebugLogs.Value || !Inverted || Time.unscaledTime < nextDebugLog)
                return;
            nextDebugLog = Time.unscaledTime + 1f;

            object gc = GroundCheckField?.GetValue(player);
            var onGround = gc?.GetType().GetField("onGround");
            string state = onGround != null ? onGround.GetValue(gc).ToString() : "?";
            Plugin.Log.LogInfo($"[debug] plafond touché : {CeilingContact.Touching}, gc.onGround : {state}, " +
                $"vitesse : {player.GetComponent<Rigidbody>()?.velocity}");
        }

        private static Transform FindGroundCheck(NewMovement player)
        {
            if (GroundCheckField?.GetValue(player) is Component component)
                return component.transform;

            foreach (var child in player.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.Contains("GroundCheck"))
                    return child;
            }
            return null;
        }

        /// <summary>Symétrie de la position du détecteur par rapport au centre du corps du joueur.</summary>
        private static Vector3 MirrorAcrossBody(NewMovement player, Transform target, Vector3 localPosition)
        {
            var parent = target.parent;
            var capsule = player.GetComponent<CapsuleCollider>();
            Vector3 center = capsule != null
                ? player.transform.TransformPoint(capsule.center)
                : player.transform.position;

            Vector3 world = parent != null ? parent.TransformPoint(localPosition) : localPosition;
            Vector3 up = player.transform.up;
            float height = Vector3.Dot(world - center, up);
            Vector3 mirrored = world - 2f * height * up;

            return parent != null ? parent.InverseTransformPoint(mirrored) : mirrored;
        }

        private void OnGUI()
        {
            if (!Plugin.ShowHud.Value || (!Inverted && !ChaosMode))
                return;

            if (hudStyle == null)
            {
                hudStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.UpperCenter,
                    fontSize = 22,
                    fontStyle = FontStyle.Bold,
                };
                hudStyle.normal.textColor = new Color(1f, 0.25f, 0.2f);
            }

            string text = Inverted ? "▲ GRAVITÉ INVERSÉE ▲" : "GRAVITÉ NORMALE";
            if (ChaosMode)
                text += $"   CHAOS {Mathf.Max(0f, nextChaosFlip - Time.time):0.0}s";

            GUI.Label(new Rect(0f, 12f, Screen.width, 40f), text, hudStyle);
        }
    }
}
