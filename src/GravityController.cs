using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GravityFlip
{
    /// <summary>
    /// Gère l'état de la gravité : bascule, mode chaos et indicateur à l'écran.
    /// Le retournement du joueur est fait par <see cref="PlayerRoller"/>.
    /// </summary>
    public class GravityController : MonoBehaviour
    {
        public static bool Inverted { get; private set; }
        public static bool ChaosMode { get; private set; }

        /// <summary>0 = joueur à l'endroit, 1 = joueur retourné (lissé dans le temps).</summary>
        public static float FlipProgress { get; private set; }

        private static readonly FieldInfo GroundCheckField = AccessTools.Field(typeof(NewMovement), "gc");

        private float nextChaosFlip;
        private float nextDebugLog;
        private GUIStyle hudStyle;
        private GUIStyle shadowStyle;

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

            // Masque / affiche le texte sans toucher à la gravité ni au chaos (sauvegardé dans la config).
            if (KeyInput.WasPressed(Plugin.HudKey.Value))
                Plugin.ShowHud.Value = !Plugin.ShowHud.Value;

            if (ChaosMode && Time.time >= nextChaosFlip)
            {
                SetInverted(!Inverted);
                ScheduleChaos();
            }

            float duration = Mathf.Max(0.01f, Plugin.FlipDuration.Value);
            FlipProgress = Mathf.MoveTowards(FlipProgress, Inverted ? 1f : 0f, Time.unscaledDeltaTime / duration);

            SyncGravity();
            LogDebug();
        }

        public static void SetInverted(bool value, bool instant = false)
        {
            if (instant)
                FlipProgress = value ? 1f : 0f;

            if (Inverted == value)
                return;

            Inverted = value;
            SyncGravity();
            PlayerRoller.OnInvertChanged(value);

            // Les corps endormis ignorent la gravité : on les réveille pour qu'ils "tombent" vers le haut.
            foreach (var rb in FindObjectsOfType<Rigidbody>())
            {
                if (!rb.isKinematic)
                    rb.WakeUp();
            }

            Plugin.Log.LogInfo(value ? "Gravité inversée !" : "Gravité normale.");
        }

        // Gravité voulue par le jeu (certains niveaux l'inversent eux-mêmes) et dernière valeur écrite par le mod.
        private static Vector3 gameGravity = new Vector3(0f, -9.81f, 0f);
        private static Vector3 lastApplied;
        private static bool tracking;

        /// <summary>Le « haut » du jeu, sans le mod : opposé à la gravité voulue par le jeu.</summary>
        public static Vector3 GameUp => gameGravity.sqrMagnitude > 1e-6f ? -gameGravity.normalized : Vector3.up;

        /// <summary>Direction dans laquelle on tombe réellement (gravité du jeu, inversée par le mod si besoin).</summary>
        public static Vector3 FallDirection => Inverted ? GameUp : -GameUp;

        /// <summary>
        /// Le mod inverse la gravité PAR RAPPORT à celle du jeu, sans jamais l'écraser : si un niveau
        /// change lui-même la gravité, on adopte sa nouvelle valeur. Mod désactivé = gravité du jeu intacte.
        /// </summary>
        private static void SyncGravity()
        {
            Vector3 current = Physics.gravity;
            if (!tracking || (current - lastApplied).sqrMagnitude > 1e-6f)
            {
                if (tracking)
                    Plugin.Log.LogInfo($"Le jeu a changé la gravité : {current}.");
                gameGravity = current;
                tracking = true;
            }

            Vector3 wanted = Inverted ? -gameGravity : gameGravity;
            if ((current - wanted).sqrMagnitude > 1e-6f)
                Physics.gravity = wanted;
            lastApplied = wanted;
        }

        private void ScheduleChaos()
        {
            float min = Mathf.Max(0.5f, Plugin.ChaosMinDelay.Value);
            float max = Mathf.Max(min, Plugin.ChaosMaxDelay.Value);
            nextChaosFlip = Time.time + Random.Range(min, max);
        }

        private void LogDebug()
        {
            if (!Plugin.DebugLogs.Value || !Inverted || Time.unscaledTime < nextDebugLog)
                return;
            nextDebugLog = Time.unscaledTime + 1f;

            var player = GameObjects.Player;
            if (player == null)
                return;

            object gc = GroundCheckField?.GetValue(player);
            var onGround = gc?.GetType().GetField("onGround");
            string state = onGround != null ? onGround.GetValue(gc).ToString() : "?";
            Plugin.Log.LogInfo($"[debug] plafond touché : {CeilingContact.Touching}, gc.onGround : {state}, " +
                $"haut du joueur : {player.transform.up}, vitesse : {player.GetComponent<Rigidbody>()?.velocity}");
        }

        private void OnGUI()
        {
            if (!Plugin.ShowHud.Value || (!Inverted && !ChaosMode))
                return;

            if (hudStyle == null)
            {
                hudStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.LowerLeft,
                    fontStyle = FontStyle.Bold,
                    wordWrap = false,
                };
                hudStyle.normal.textColor = new Color(1f, 0.25f, 0.2f);
                shadowStyle = new GUIStyle(hudStyle);
                shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            }

            // Taille proportionnelle à la résolution, comme le HUD du jeu.
            int size = Mathf.RoundToInt(Screen.height * 0.03f);
            hudStyle.fontSize = size;
            shadowStyle.fontSize = size;

            string text = Inverted ? "▼ GRAVITÉ INVERSÉE ▼" : "GRAVITÉ NORMALE";
            if (ChaosMode)
                text += $"   CHAOS {Mathf.Max(0f, nextChaosFlip - Time.time):0.0}s";

            // Bas du texte posé juste au-dessus du HUD des armes (en bas à gauche par défaut).
            float x = Screen.width * Plugin.HudX.Value;
            float bottom = Screen.height * Plugin.HudY.Value;
            var rect = new Rect(x, bottom - size * 2f, Screen.width - x, size * 2f);

            GUI.depth = -1000;
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, shadowStyle);
            GUI.Label(rect, text, hudStyle);
        }
    }
}
