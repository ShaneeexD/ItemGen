using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using EFT;
using EFT.HealthSystem;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;

namespace ItemGen.Client
{
    /// <summary>
    /// Patches the health controller so stims with EffectsDamage (e.g. DestroyedPart/Fracture)
    /// target the actual damaged body part instead of being hardcoded to EBodyPart.Head.
    /// Without this, the MedEffect created by a stim is always on Head, so DestroyedPart
    /// cannot restore blacked limbs.
    /// </summary>
    [HarmonyPatch]
    internal static class StimEffectsDamagePatch
    {
        static MethodBase TargetMethod()
        {
            var baseType = typeof(ActiveHealthController).BaseType;
            return AccessTools.Method(baseType, "method_7");
        }

        private static T GetItemComponent<T>(Item item) where T : class
        {
            var componentsObject = Traverse.Create(item).Field("Components").GetValue();
            if (!(componentsObject is System.Collections.IEnumerable components))
            {
                return null;
            }

            foreach (var component in components)
            {
                if (component is T typed)
                {
                    return typed;
                }
            }

            return null;
        }

        private static readonly Type GClass3009Type = typeof(ActiveHealthController).BaseType;
        private static readonly MethodInfo Method_9 = AccessTools.Method(GClass3009Type, "method_9");

        static void Postfix(
            ref bool __result,
            object __instance,
            Item item,
            EBodyPart bodyPart,
            bool fastSearch,
            ref EBodyPart? damagedBodyPart)
        {
            if (!__result)
            {
                return;
            }

            if (!BundleInjector.IsCustomItem(item.StringTemplateId))
            {
                return;
            }

            var healthEffects = GetItemComponent<HealthEffectsComponent>(item);
            if (healthEffects == null)
            {
                return;
            }

            var medKit = GetItemComponent<MedKitComponent>(item);
            if (medKit != null)
            {
                // This is a medkit/healing item, not a stim — do not apply stim-specific damage-effect handling.
                return;
            }

            var damageEffects = healthEffects.DamageEffects;
            if (damageEffects == null || damageEffects.Count == 0)
            {
                return;
            }

            // If there are no stimulator buffs, the original code already falls through to
            // method_9 and finds the correct body part. Only patch when stim buffs would
            // otherwise force the effect onto Head.
            if (string.IsNullOrEmpty(healthEffects.StimulatorBuffs))
            {
                return;
            }
            var bestBodyPart = FindBestBodyPart(__instance, healthEffects, medKit, bodyPart, damageEffects);

            if (bestBodyPart.HasValue)
            {
                damagedBodyPart = bestBodyPart.Value;
                __result = true;
            }
            else
            {
                damagedBodyPart = null;
                __result = false;
            }
        }

        private static EBodyPart? FindBestBodyPart(
            object healthController,
            HealthEffectsComponent healthEffects,
            MedKitComponent medKit,
            EBodyPart bodyPart,
            Dictionary<EDamageEffectType, GClass1443> damageEffects)
        {
            if (Method_9 != null)
            {
                try
                {
                    return (EBodyPart?)Method_9.Invoke(healthController, new object[] { healthEffects, medKit, bodyPart });
                }
                catch
                {
                    // Fall through to manual fallback.
                }
            }

            var controllerType = healthController.GetType();
            var baseType = controllerType.BaseType;
            if (baseType != null)
            {
                var method = baseType.GetMethod("method_9", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (method != null)
                {
                    try
                    {
                        return (EBodyPart?)method.Invoke(healthController, new object[] { healthEffects, medKit, bodyPart });
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }

            return ManualFindBestBodyPart(healthController, healthEffects, medKit, bodyPart, damageEffects);
        }

        private static readonly IReadOnlyList<EBodyPart> RealBodyParts =
            Traverse.Create(typeof(GClass3058)).Field("RealBodyParts").GetValue<IReadOnlyList<EBodyPart>>();

        private static readonly MethodInfo IsBodyPartDestroyedMethod =
            AccessTools.Method(typeof(ActiveHealthController), "IsBodyPartDestroyed");

        private static readonly MethodInfo FindActiveEffectGeneric =
            AccessTools.Method(typeof(ActiveHealthController), "FindActiveEffect");

        private static readonly Type FractureType =
            typeof(ActiveHealthController).GetNestedType("Fracture", BindingFlags.Public | BindingFlags.NonPublic);

        private static readonly Type HeavyBleedingType =
            typeof(ActiveHealthController).GetNestedType("HeavyBleeding", BindingFlags.Public | BindingFlags.NonPublic);

        private static readonly Type LightBleedingType =
            typeof(ActiveHealthController).GetNestedType("LightBleeding", BindingFlags.Public | BindingFlags.NonPublic);

        private static EBodyPart? ManualFindBestBodyPart(
            object healthController,
            HealthEffectsComponent healthEffects,
            MedKitComponent medKit,
            EBodyPart bodyPart,
            Dictionary<EDamageEffectType, GClass1443> damageEffects)
        {
            IEnumerable<EBodyPart> partsToCheck;
            if (bodyPart != EBodyPart.Common)
            {
                partsToCheck = new[] { bodyPart };
            }
            else
            {
                partsToCheck = RealBodyParts;
            }

            foreach (var part in partsToCheck)
            {
                if (IsCandidateBodyPart(part, healthController, medKit, damageEffects))
                {
                    return part;
                }
            }

            return null;
        }

        private static bool IsCandidateBodyPart(
            EBodyPart partToCheck,
            object healthController,
            MedKitComponent medKit,
            Dictionary<EDamageEffectType, GClass1443> damageEffects)
        {
            if (damageEffects.TryGetValue(EDamageEffectType.DestroyedPart, out var destroyedPart))
            {
                if (partToCheck != EBodyPart.Head && partToCheck != EBodyPart.Chest)
                {
                    bool isDestroyed = (bool)IsBodyPartDestroyedMethod.Invoke(healthController, new object[] { partToCheck });
                    if (isDestroyed && (medKit == null || medKit.HpResource >= (float)destroyedPart.Cost))
                    {
                        return true;
                    }
                }
            }

            if (EffectActive(EDamageEffectType.Fracture, FractureType, partToCheck, healthController, medKit, damageEffects))
            {
                return true;
            }

            if (EffectActive(EDamageEffectType.HeavyBleeding, HeavyBleedingType, partToCheck, healthController, medKit, damageEffects))
            {
                return true;
            }

            if (EffectActive(EDamageEffectType.LightBleeding, LightBleedingType, partToCheck, healthController, medKit, damageEffects))
            {
                return true;
            }

            return false;
        }

        private static bool EffectActive(
            EDamageEffectType effectType,
            Type effectClassType,
            EBodyPart partToCheck,
            object healthController,
            MedKitComponent medKit,
            Dictionary<EDamageEffectType, GClass1443> damageEffects)
        {
            if (effectClassType == null || !damageEffects.TryGetValue(effectType, out var effectProps))
            {
                return false;
            }

            if (medKit != null && medKit.HpResource < (float)effectProps.Cost)
            {
                return false;
            }

            var findMethod = FindActiveEffectGeneric.MakeGenericMethod(effectClassType);
            return findMethod.Invoke(healthController, new object[] { partToCheck }) != null;
        }
    }

    /// <summary>
    /// After the original MedEffect.Residue heals the primary body part, this applies
    /// EffectsDamage (DestroyedPart, Fracture, Bleeding) to a random subset of the remaining
    /// real body parts, limited by the maxBodyPartsToHeal value encoded in StimulatorBuffs.
    /// </summary>
    [HarmonyPatch]
    internal static class StimEffectsDamageMultiPatch
    {
        static MethodBase TargetMethod()
        {
            var medEffectType = AccessTools.TypeByName("EFT.HealthSystem.ActiveHealthController+MedEffect");
            return AccessTools.Method(medEffectType, "Residue");
        }

        private static readonly MethodInfo RestoreBodyPartMethod = AccessTools.Method(typeof(ActiveHealthController), "RestoreBodyPart");
        private static readonly MethodInfo IsBodyPartDestroyedMethod = AccessTools.Method(typeof(ActiveHealthController), "IsBodyPartDestroyed");
        private static readonly MethodInfo Method_16_Generic = AccessTools.Method(typeof(ActiveHealthController), "method_16");
        private static readonly MethodInfo Method_46 = AccessTools.Method(typeof(ActiveHealthController), "method_46");

        private static readonly Type FractureType = typeof(ActiveHealthController).GetNestedType("Fracture", BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly Type HeavyBleedingType = typeof(ActiveHealthController).GetNestedType("HeavyBleeding", BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly Type LightBleedingType = typeof(ActiveHealthController).GetNestedType("LightBleeding", BindingFlags.Public | BindingFlags.NonPublic);

        private static readonly IReadOnlyList<EBodyPart> RealBodyParts =
            Traverse.Create(typeof(GClass3058)).Field("RealBodyParts").GetValue<IReadOnlyList<EBodyPart>>();

        private static readonly Regex MaxBodyPartsRegex = new Regex(@"_MaxBodyParts_(?<max>\d+)$", RegexOptions.Compiled);

        static void Postfix(object __instance)
        {
            var healthEffects = Traverse.Create(__instance).Property("HealthEffectsComponent_0").GetValue<HealthEffectsComponent>();
            if (healthEffects == null || healthEffects.DamageEffects == null || healthEffects.DamageEffects.Count == 0)
            {
                return;
            }

            var interrupted = Traverse.Create(__instance).Field("Bool_2").GetValue<bool>();
            if (interrupted)
            {
                return;
            }

            var healthController = Traverse.Create(__instance).Property("HealthController").GetValue();
            if (healthController == null)
            {
                return;
            }

            var item = GetItemFromMedEffect(__instance);
            if (item == null || !BundleInjector.IsCustomItem(item.StringTemplateId) || !(item is MedsItemClass))
            {
                return;
            }

            var bodyPart = Traverse.Create(__instance).Property("BodyPart").GetValue<EBodyPart>();
            var medKit = Traverse.Create(__instance).Property("MedKitComponent_0").GetValue<MedKitComponent>();
            var damageEffects = healthEffects.DamageEffects;

            var maxBodyParts = GetMaxBodyParts(healthEffects.StimulatorBuffs);
            // The original Residue already handles one body part; count it against the limit.
            var additionalLimit = maxBodyParts <= 0 ? int.MaxValue : maxBodyParts - 1;
            if (additionalLimit <= 0)
            {
                return;
            }

            var candidates = new List<EBodyPart>();
            foreach (var bodyPartToHeal in RealBodyParts)
            {
                if (bodyPartToHeal == bodyPart)
                {
                    continue;
                }

                if (IsCandidateBodyPart(bodyPartToHeal, healthController, medKit, damageEffects))
                {
                    candidates.Add(bodyPartToHeal);
                }
            }

            if (candidates.Count == 0)
            {
                return;
            }

            Shuffle(candidates);

            var selected = candidates.Count <= additionalLimit
                ? candidates
                : candidates.GetRange(0, additionalLimit);

            foreach (var bodyPartToHeal in selected)
            {
                ApplyEffectsToBodyPart(bodyPartToHeal, healthController, medKit, damageEffects);
            }
        }

        private static Item GetItemFromMedEffect(object medEffect)
        {
            var type = medEffect.GetType();
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (typeof(Item).IsAssignableFrom(prop.PropertyType))
                    return prop.GetValue(medEffect) as Item;
            }
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (typeof(Item).IsAssignableFrom(field.FieldType))
                    return field.GetValue(medEffect) as Item;
            }
            return null;
        }

        private static int GetMaxBodyParts(string stimulatorBuffs)
        {
            if (string.IsNullOrEmpty(stimulatorBuffs))
            {
                return 0;
            }

            var match = MaxBodyPartsRegex.Match(stimulatorBuffs);
            if (match.Success && int.TryParse(match.Groups["max"].Value, out var max))
            {
                return max;
            }

            return 0;
        }

        private static bool IsCandidateBodyPart(
            EBodyPart bodyPartToHeal,
            object healthController,
            MedKitComponent medKit,
            Dictionary<EDamageEffectType, GClass1443> damageEffects)
        {
            if (damageEffects.TryGetValue(EDamageEffectType.DestroyedPart, out var destroyedPart))
            {
                bool isDestroyed = (bool)IsBodyPartDestroyedMethod.Invoke(healthController, new object[] { bodyPartToHeal });
                if (isDestroyed && (medKit == null || medKit.HpResource >= (float)destroyedPart.Cost))
                {
                    return true;
                }
            }

            if (EffectActive(EDamageEffectType.Fracture, FractureType, bodyPartToHeal, healthController, medKit, damageEffects))
            {
                return true;
            }

            if (EffectActive(EDamageEffectType.HeavyBleeding, HeavyBleedingType, bodyPartToHeal, healthController, medKit, damageEffects))
            {
                return true;
            }

            if (EffectActive(EDamageEffectType.LightBleeding, LightBleedingType, bodyPartToHeal, healthController, medKit, damageEffects))
            {
                return true;
            }

            return false;
        }

        private static bool EffectActive(
            EDamageEffectType effectType,
            Type effectClassType,
            EBodyPart bodyPartToHeal,
            object healthController,
            MedKitComponent medKit,
            Dictionary<EDamageEffectType, GClass1443> damageEffects)
        {
            if (effectClassType == null || !damageEffects.TryGetValue(effectType, out var effectProps))
            {
                return false;
            }

            if (medKit != null && medKit.HpResource < (float)effectProps.Cost)
            {
                return false;
            }

            var findMethod = AccessTools.Method(typeof(ActiveHealthController), "FindActiveEffect").MakeGenericMethod(effectClassType);
            return findMethod.Invoke(healthController, new object[] { bodyPartToHeal }) != null;
        }

        private static void ApplyEffectsToBodyPart(
            EBodyPart bodyPartToHeal,
            object healthController,
            MedKitComponent medKit,
            Dictionary<EDamageEffectType, GClass1443> damageEffects)
        {
            if (damageEffects.TryGetValue(EDamageEffectType.DestroyedPart, out var destroyedPart))
            {
                bool isDestroyed = (bool)IsBodyPartDestroyedMethod.Invoke(healthController, new object[] { bodyPartToHeal });
                if (isDestroyed && (medKit == null || medKit.HpResource >= (float)destroyedPart.Cost))
                {
                    float penalty = (float)UnityEngine.Random.Range(destroyedPart.HealthPenaltyMin, destroyedPart.HealthPenaltyMax) / 100f;
                    RestoreBodyPartMethod.Invoke(healthController, new object[] { bodyPartToHeal, penalty });
                    if (medKit != null)
                    {
                        medKit.HpResource = Mathf.Max(0f, medKit.HpResource - (float)destroyedPart.Cost);
                    }
                }
            }

            TryRemoveEffect(EDamageEffectType.Fracture, FractureType, bodyPartToHeal, healthController, medKit, damageEffects);
            TryRemoveEffect(EDamageEffectType.HeavyBleeding, HeavyBleedingType, bodyPartToHeal, healthController, medKit, damageEffects);
            TryRemoveEffect(EDamageEffectType.LightBleeding, LightBleedingType, bodyPartToHeal, healthController, medKit, damageEffects);
        }

        private static void TryRemoveEffect(
            EDamageEffectType effectType,
            Type effectClassType,
            EBodyPart bodyPartToHeal,
            object healthController,
            MedKitComponent medKit,
            Dictionary<EDamageEffectType, GClass1443> damageEffects)
        {
            if (effectClassType == null || !damageEffects.TryGetValue(effectType, out var effectProps))
            {
                return;
            }

            if (medKit != null && medKit.HpResource < (float)effectProps.Cost)
            {
                return;
            }

            var method = Method_16_Generic.MakeGenericMethod(effectClassType);
            var result = method.Invoke(healthController, new object[] { bodyPartToHeal });
            if (result == null)
            {
                return;
            }

            if (medKit != null)
            {
                medKit.HpResource = Mathf.Max(0f, medKit.HpResource - (float)effectProps.Cost);
            }

            Method_46.Invoke(healthController, new object[] { result });
        }

        private static void Shuffle(List<EBodyPart> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                var temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }
    }
}
