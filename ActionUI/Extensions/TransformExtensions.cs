using System.Text.RegularExpressions;
using UnityEngine;

namespace ModifAmorphic.Outward.Unity.ActionUI.Extensions
{
    public static class TransformExtensions
    {
        // Regex to match character UIDs in paths (e.g., r8GjXM2R4Uyy7hXbTwUe4w)
        // Outward UIDs are exactly 22 base64-like characters (may contain underscores/hyphens/plus/slash)
        private static readonly Regex CharacterUIDPattern = new Regex(@"_?[A-Za-z0-9_\-+/]{20,24}", RegexOptions.Compiled);

        public static string GetPath(this Transform transform)
        {
            var path = transform.gameObject.name;
            var parent = transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return "/" + path;
        }

        /// <summary>
        /// Gets the transform path with character UIDs stripped out for global matching.
        /// </summary>
        public static string GetNormalizedPath(this Transform transform)
        {
            var path = transform.GetPath();
            return NormalizePath(path);
        }

        /// <summary>
        /// Normalizes a path by stripping character UIDs.
        /// </summary>
        public static string NormalizePath(string path)
        {
            var normalized = CharacterUIDPattern.Replace(path, "[CHARACTER]");
            if (path != normalized)
            {
                // Simple debug log to console/file to trace what is being replaced
                // Using standard Unity Debug if available or just rely on the fact that this is called often
                // We'll use the ModifAmorphic logger if we could access it, but this is a static extension class.
                // We can use UnityEngine.Debug.Log directly as this is a MonoBehaviour project (imported Unity libs)
                UnityEngine.Debug.Log($"[ActionUI Debug] NormalizePath: '{path}' -> '{normalized}'");
            }
            return normalized;
        }

        public static UIPosition ToRectTransformPosition(this RectTransform rectTransform)
        {
            return new UIPosition()
            {
                Position = rectTransform.position.ToUIPosition2D(),
                AnchoredPosition = rectTransform.anchoredPosition.ToUIPosition2D(),
                AnchoredMin = rectTransform.anchorMin.ToUIPosition2D(),
                AnchoredMax = rectTransform.anchorMax.ToUIPosition2D(),
                OffsetMin = rectTransform.offsetMin.ToUIPosition2D(),
                OffsetMax = rectTransform.offsetMax.ToUIPosition2D(),
                Pivot = rectTransform.pivot.ToUIPosition2D()
            };
        }
    }
}
