using UnityEngine;

namespace GravityFlip
{
    /// <summary>
    /// Retrouve le joueur et le contrôleur de caméra du niveau en cours. La recherche est
    /// mise en cache et refaite au plus deux fois par seconde tant qu'ils sont absents (menus).
    /// </summary>
    internal static class GameObjects
    {
        private static NewMovement player;
        private static CameraController cameraControl;
        private static float nextPlayerSearch;
        private static float nextCameraSearch;

        public static NewMovement Player => Find(ref player, ref nextPlayerSearch);
        public static CameraController CameraControl => Find(ref cameraControl, ref nextCameraSearch);

        private static T Find<T>(ref T cached, ref float nextSearch) where T : Object
        {
            if (cached != null || Time.unscaledTime < nextSearch)
                return cached;
            nextSearch = Time.unscaledTime + 0.5f;
            cached = Object.FindObjectOfType<T>();
            return cached;
        }
    }
}
