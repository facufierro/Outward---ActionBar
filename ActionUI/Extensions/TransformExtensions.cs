using System.Text.RegularExpressions;
using UnityEngine;

namespace ModifAmorphic.Outward.Unity.ActionUI.Extensions
{
    public static class TransformExtensions
    {
        // Regex to match character UID path segments only (e.g., /r8GjXM2R4Uyy7hXbTwUe4w/)
        // Restricting to full path segments avoids accidental replacement of long static object names.
        private static readonly Regex CharacterUIDPattern = new Regex(@"(?<=/|^)_?[A-Za-z0-9_\-+]{20,24}(?=/|$)", RegexOptions.Compiled);

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
            if (string.IsNullOrWhiteSpace(path))
                return path;

            var normalized = CharacterUIDPattern.Replace(path, "[CHARACTER]");
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
