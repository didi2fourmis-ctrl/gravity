using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace GravityFlip
{
    /// <summary>
    /// Retourne l'image verticalement via la matrice de projection. Contrairement à une
    /// rotation de 180°, la gauche et la droite restent intactes : déplacements latéraux
    /// et visée horizontale restent naturels. Seul l'axe Y de la souris doit être inversé.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public class CameraFlipper : MonoBehaviour
    {
        private static readonly FieldInfo ReverseYField = AccessTools.Field(typeof(CameraController), "reverseY");

        private Camera modifiedCamera;
        private float currentScale = 1f;
        private bool cullingInverted;

        private CameraController mouseController;
        private bool mouseApplied;
        private bool originalReverseY;

        private void OnEnable()
        {
            Camera.onPreRender += OnCameraPreRender;
            Camera.onPostRender += OnCameraPostRender;
        }

        private void OnDisable()
        {
            Camera.onPreRender -= OnCameraPreRender;
            Camera.onPostRender -= OnCameraPostRender;
            if (modifiedCamera != null)
                modifiedCamera.ResetProjectionMatrix();
            modifiedCamera = null;
            UpdateMouse(false);
        }

        private void LateUpdate()
        {
            var cam = Camera.main;

            if (modifiedCamera != null && modifiedCamera != cam)
            {
                modifiedCamera.ResetProjectionMatrix();
                modifiedCamera = null;
            }

            // Effet "carte qui se retourne" : l'échelle verticale passe de 1 à -1.
            currentScale = Plugin.FlipCamera.Value ? Mathf.Cos(GravityController.FlipProgress * Mathf.PI) : 1f;

            if (cam != null)
            {
                if (currentScale < 0.999f)
                {
                    float scale = Mathf.Abs(currentScale) < 0.02f ? (currentScale < 0f ? -0.02f : 0.02f) : currentScale;
                    // Repartir de la projection du jeu (FOV compris) puis appliquer l'échelle.
                    cam.ResetProjectionMatrix();
                    cam.projectionMatrix = Matrix4x4.Scale(new Vector3(1f, scale, 1f)) * cam.projectionMatrix;
                    modifiedCamera = cam;
                }
                else if (modifiedCamera == cam)
                {
                    cam.ResetProjectionMatrix();
                    modifiedCamera = null;
                }
            }

            UpdateMouse(currentScale < 0f && Plugin.InvertMouseY.Value);
        }

        // Une image miroir inverse l'ordre des sommets : sans ça, Unity masquerait les mauvaises faces.
        private void OnCameraPreRender(Camera cam)
        {
            if (cam == modifiedCamera && currentScale < 0f)
            {
                GL.invertCulling = true;
                cullingInverted = true;
            }
        }

        private void OnCameraPostRender(Camera cam)
        {
            if (cullingInverted && cam == modifiedCamera)
            {
                GL.invertCulling = false;
                cullingInverted = false;
            }
        }

        private void UpdateMouse(bool invert)
        {
            if (ReverseYField == null || ReverseYField.FieldType != typeof(bool))
                return;

            var controller = GameObjects.CameraControl;
            if (controller != mouseController)
            {
                mouseController = controller;
                mouseApplied = false;
            }
            if (controller == null)
                return;

            if (invert && !mouseApplied)
            {
                originalReverseY = (bool)ReverseYField.GetValue(controller);
                ReverseYField.SetValue(controller, !originalReverseY);
                mouseApplied = true;
            }
            else if (!invert && mouseApplied)
            {
                ReverseYField.SetValue(controller, originalReverseY);
                mouseApplied = false;
            }
        }
    }
}
