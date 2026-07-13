using System;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using UnityEngine;

namespace ItemGen.Client
{
    [HarmonyPatch(typeof(ItemView), "UpdateScale")]
    internal static class ContainerIconSizePatch
    {
        public static void Postfix(ItemView __instance)
        {
            try
            {
                var item = __instance.Item;
                if (item == null || !BundleInjector.IsCustomItem(item.StringTemplateId))
                {
                    return;
                }

                var mainImage = Traverse.Create(__instance).Field("MainImage").GetValue<Component>();
                if (mainImage == null || !mainImage.gameObject.activeInHierarchy)
                {
                    return;
                }

                // Respect any custom IconScale already set by the base game.
                if (__instance.IconScale.HasValue)
                {
                    return;
                }

                var rectTransform = __instance.RectTransform;
                if (rectTransform == null)
                {
                    return;
                }

                var imageRect = mainImage.GetComponent<RectTransform>();
                if (imageRect == null)
                {
                    return;
                }

                Vector2 slotSize = rectTransform.sizeDelta;
                Vector2 imageSize = imageRect.sizeDelta;

                if (slotSize.x <= 0f || slotSize.y <= 0f || imageSize.x <= 0f || imageSize.y <= 0f)
                {
                    return;
                }

                // If the native icon is larger than the slot, shrink it to fit
                // while preserving aspect ratio. Never upscale.
                float scaleX = slotSize.x / imageSize.x;
                float scaleY = slotSize.y / imageSize.y;
                float scale = Mathf.Min(scaleX, scaleY);

                if (scale < 1f)
                {
                    imageRect.localScale = new Vector3(scale, scale, 1f);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[ItemGen] ContainerIconSizePatch failed: {ex}");
            }
        }
    }
}
