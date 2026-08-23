using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stavebound.Portals;
using UnityEngine;

namespace Stavebound.Tiers
{
    /// <summary>
    /// <c>stave_inspect</c> — dump a prefab's object tree, renderers, materials and shader
    /// properties.
    /// <para>
    /// Whether a portal's runes can be lit individually is a question about the *model*, and models
    /// live in asset bundles where no decompiler reaches. If the glow is one mesh with one emissive
    /// material, per-rune control means authoring a mask or attaching our own glyphs; if the runes are
    /// separate child objects, it is a matter of toggling them. Nothing else can tell us which.
    /// </para>
    /// <para>
    /// Also the right tool for choosing what to clone the stave pieces from (DESIGN.md §11), since
    /// it shows what a candidate stone is actually made of.
    /// </para>
    /// </summary>
    internal sealed class PrefabInspectCommand : StaveboundCommand
    {
        /// <summary>Deep enough for a portal, shallow enough not to flood the console.</summary>
        private const int MaxDepth = 4;

        public override string Name => "stave_inspect";

        public override string Help =>
            "stave_inspect <prefab> - show a prefab's child objects, renderers, materials and " +
            "shader colour properties. Use it to find out what can be lit or tinted separately.";

        protected override void Execute(string[] args, Terminal context)
        {
            string wanted = string.Join(" ", args).Trim();
            if (wanted.Length == 0)
            {
                Echo(context, "Stavebound: name a prefab, e.g. stave_inspect portal_wood");
                return;
            }

            if (ZNetScene.instance == null)
            {
                Echo(context, "Stavebound: no world loaded.");
                return;
            }

            GameObject prefab = ZNetScene.instance.GetPrefab(wanted);
            if (prefab == null)
            {
                Echo(context, $"Stavebound: no prefab called \"{wanted}\". Try stave_prefabs {wanted}");
                return;
            }

            Echo(context, $"Stavebound inspect - {prefab.name}");
            Describe(context, prefab.transform, 0);
        }

        private static void Describe(Terminal context, Transform node, int depth)
        {
            string indent = new string(' ', (depth + 1) * 2);

            // Components tell us what a node is for; a renderer tells us whether it can be lit.
            List<string> components = node.GetComponents<Component>()
                .Where(c => c != null && !(c is Transform))
                .Select(c => c.GetType().Name)
                .ToList();

            var line = new StringBuilder($"{indent}{node.name}");
            if (components.Count > 0)
            {
                line.Append($"  [{string.Join(", ", components)}]");
            }

            if (!node.gameObject.activeSelf)
            {
                line.Append("  (inactive)");
            }

            Echo(context, line.ToString());

            var renderer = node.GetComponent<Renderer>();
            if (renderer != null)
            {
                foreach (Material material in renderer.sharedMaterials.Where(m => m != null))
                {
                    // sharedMaterial, not material: touching .material would clone it per instance and
                    // leak a material every time somebody looked at a portal.
                    Echo(context, $"{indent}  material '{material.name}' shader '{material.shader?.name}'" +
                                  DescribeColours(material));
                }
            }

            if (depth >= MaxDepth)
            {
                if (node.childCount > 0)
                {
                    Echo(context, $"{indent}  ... {node.childCount} more child object(s), deeper than this dump goes");
                }

                return;
            }

            for (int i = 0; i < node.childCount; i++)
            {
                Describe(context, node.GetChild(i), depth + 1);
            }
        }

        /// <summary>
        /// The colour and emission properties a material actually has, which is what decides whether
        /// tinting by tier is a one-line change or a shader problem.
        /// </summary>
        private static string DescribeColours(Material material)
        {
            var found = new List<string>();
            foreach (string property in new[] { "_Color", "_EmissionColor", "_EmissiveColor", "_TintColor" })
            {
                try
                {
                    if (material.HasProperty(property))
                    {
                        found.Add($"{property}={material.GetColor(property)}");
                    }
                }
                catch (Exception)
                {
                    // A property can exist without being a colour. Not worth a line of output.
                }
            }

            return found.Count == 0 ? string.Empty : "  " + string.Join(" ", found);
        }
    }
}
