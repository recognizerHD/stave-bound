using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Stavebound.Staves
{
    /// <summary>
    /// Recolours the glowing parts of a cloned core stand, so one prefab can serve every tier.
    /// <para>
    /// Five staves sharing a silhouette and differing only in the colour of the thing they hold
    /// reads as a set. Five unrelated props read as five mods. That is the whole argument for tinting
    /// rather than hunting for five different stands (DESIGN.md §11 — clone, never ship art).
    /// </para>
    /// <para>
    /// It finds what to tint rather than being told: a glow in Valheim is usually three layers — an
    /// emissive material, a Light throwing colour onto the stone around it, and a ParticleSystem
    /// spitting sparks. Colour the material alone and you get a blue core sitting in a pool of the
    /// original light, shedding sparks of the wrong colour.
    /// </para>
    /// </summary>
    internal static class CoreTint
    {
        /// <summary>
        /// Shader colour properties worth setting, in the order they matter. Emission is what makes a
        /// core look lit rather than painted.
        /// </summary>
        private static readonly string[] ColourProperties = { "_EmissionColor", "_Color", "_TintColor" };

        /// <summary>
        /// Applies <paramref name="tint"/> to everything in the prefab that glows, leaving the stone
        /// alone.
        /// <para>
        /// The stone is spared by targeting emissive materials only — the lit part of a prefab is the
        /// part with a non-black emission colour, which holds without needing to know a single child
        /// object's name. Structure-independent on purpose: this has to survive Iron Gate rearranging
        /// a prefab in a patch.
        /// </para>
        /// </summary>
        internal static void Apply(GameObject prefab, Color tint, string describe)
        {
            if (prefab == null)
            {
                return;
            }

            var touched = new List<string>();

            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;

                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    if (source == null || !IsEmissive(source))
                    {
                        continue;
                    }

                    // A NEW material, never the shared one. Writing to sharedMaterial would recolour
                    // every core stand in every burial chamber in the world, and anything else that
                    // happens to use it - the same class of mistake as mutating shared item data.
                    var tinted = new Material(source) { name = $"{source.name}_{describe}" };

                    foreach (string property in ColourProperties)
                    {
                        if (!tinted.HasProperty(property))
                        {
                            continue;
                        }

                        // Emission is HDR - the black core sits at 2.67, well past white - so setting
                        // a plain colour here would swap a glowing core for a dimly painted one. Keep
                        // the original brightness and change only the hue.
                        tinted.SetColor(property, property == "_EmissionColor"
                            ? AtIntensityOf(tint, source.GetColor(property))
                            : tint);
                    }

                    materials[i] = tinted;
                    changed = true;
                }

                if (changed)
                {
                    renderer.sharedMaterials = materials;
                    touched.Add($"material on {renderer.name}");
                }
            }

            foreach (Light light in prefab.GetComponentsInChildren<Light>(true))
            {
                light.color = tint;
                touched.Add($"light on {light.name}");
            }

            foreach (ParticleSystem particles in prefab.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = particles.main;
                main.startColor = tint;
                touched.Add($"particles on {particles.name}");
            }

            // Says what it found, because "the tint did nothing" and "the tint found nothing to
            // colour" look identical in game and have completely different fixes.
            Jotunn.Logger.LogInfo(touched.Count == 0
                ? $"Tint {describe}: found nothing emissive on {prefab.name} - the glow is not where this expects it."
                : $"Tint {describe} on {prefab.name}: {touched.Count} target(s) - {string.Join(", ", touched.Distinct())}");
        }

        /// <summary>
        /// <paramref name="tint"/> rescaled to burn as brightly as <paramref name="original"/> did.
        /// Without this every tinted core would be dimmer than the one it was cloned from, and the
        /// difference only shows up at night.
        /// </summary>
        private static Color AtIntensityOf(Color tint, Color original)
        {
            float wanted = original.maxColorComponent;
            float have = tint.maxColorComponent;

            if (wanted <= 0f || have <= 0f)
            {
                return tint;
            }

            float scale = wanted / have;
            return new Color(tint.r * scale, tint.g * scale, tint.b * scale, tint.a);
        }

        /// <summary>
        /// Is this material one of the glowing bits? Emission keywords and a non-black emission colour
        /// are what separate the core from the rock it sits in.
        /// </summary>
        private static bool IsEmissive(Material material)
        {
            if (material.IsKeywordEnabled("_EMISSION") || material.IsKeywordEnabled("_EMISSIVE"))
            {
                return true;
            }

            return material.HasProperty("_EmissionColor") &&
                   material.GetColor("_EmissionColor").maxColorComponent > 0.01f;
        }
    }
}
