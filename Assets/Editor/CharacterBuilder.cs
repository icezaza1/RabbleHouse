using UnityEngine;
using UnityEditor;

namespace RabbleHouse
{
    /// <summary>
    /// Editor tool that builds a ragdollable stick-figure character from
    /// primitive shapes and saves it as a prefab. Run via:
    ///   Tools > Rabble House > Build Character Prefabs
    ///
    /// Hierarchy (ragdoll chain):
    ///   Root (movement rb + capsule)
    ///   └─ Torso (rb)
    ///      ├─ Head (rb)
    ///      ├─ UpperArm_L (rb) → LowerArm_L (rb)
    ///      ├─ UpperArm_R (rb) → LowerArm_R (rb)
    ///      ├─ Thigh_L (rb) → Shin_L (rb)
    ///      └─ Thigh_R (rb) → Shin_R (rb)
    /// Each chain link has a CharacterJoint to its parent so the whole
    /// body flops as a ragdoll when enabled.
    /// </summary>
    public static class CharacterBuilder
    {
        private const string PrefabDir = "Assets/Prefabs/Characters";

        [MenuItem("Tools/Rabble House/Build Character Prefabs")]
        public static void BuildCharacterPrefabs()
        {
            BuildPlayer("PlayerBean", new Color(0.20f, 0.45f, 0.90f)); // blue
            BuildPlayer("AIFighter1", new Color(0.90f, 0.30f, 0.20f));  // red
            BuildPlayer("AIFighter2", new Color(0.20f, 0.80f, 0.35f));  // green

            AssetDatabase.SaveAssets();
            Debug.Log("[RabbleHouse] Character prefabs built in " + PrefabDir);
        }

        /// <summary>
        /// Builds a single stick-figure character prefab with a ragdoll.
        /// Root = movement rigidbody + capsule; children = ragdoll parts.
        /// </summary>
        private static void BuildPlayer(string name, Color color)
        {
            var root = new GameObject(name);
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 3f;
            rb.linearDamping = 2f;
            rb.angularDamping = 3f;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            // Root capsule (the collider PlayerController uses for movement)
            var col = root.AddComponent<CapsuleCollider>();
            col.height = 1.6f;
            col.radius = 0.35f;
            col.center = new Vector3(0, 0.8f, 0);

            // Visual: a capsule body + sphere head (the "bean" look)
            CreateVisual(PrimitiveType.Capsule, "VisualBody", root.transform, Vector3.zero, new Vector3(0.7f, 0.8f, 0.35f), color);
            CreateVisual(PrimitiveType.Sphere, "VisualHead", root.transform, new Vector3(0, 1.15f, 0), new Vector3(0.22f, 0.22f, 0.22f), color);

            // --- Ragdoll chain: Torso at hips, Head on top ---
            var torso = BuildLimb("Torso", root.transform, new Vector3(0, 0.8f, 0), Vector3.zero, PrimitiveType.Capsule,
                new Vector3(0.32f, 0.42f, 0.26f), color);

            var head = BuildLimb("Head", torso.transform, new Vector3(0, 0.42f, 0), Vector3.zero, PrimitiveType.Sphere,
                Vector3.one * 0.24f, color);

            // --- Arms ---
            var upperArmL = BuildLimb("UpperArm_L", torso.transform, new Vector3(-0.32f, 0.28f, 0), Vector3.zero, PrimitiveType.Capsule,
                new Vector3(0.12f, 0.35f, 0.12f), color);
            var lowerArmL = BuildLimb("LowerArm_L", upperArmL.transform, new Vector3(0, -0.30f, 0), Vector3.zero, PrimitiveType.Capsule,
                new Vector3(0.10f, 0.30f, 0.10f), color);

            var upperArmR = BuildLimb("UpperArm_R", torso.transform, new Vector3(0.32f, 0.28f, 0), Vector3.zero, PrimitiveType.Capsule,
                new Vector3(0.12f, 0.35f, 0.12f), color);
            var lowerArmR = BuildLimb("LowerArm_R", upperArmR.transform, new Vector3(0, -0.30f, 0), Vector3.zero, PrimitiveType.Capsule,
                new Vector3(0.10f, 0.30f, 0.10f), color);

            // --- Legs ---
            var thighL = BuildLimb("Thigh_L", torso.transform, new Vector3(-0.13f, -0.36f, 0), Vector3.zero, PrimitiveType.Capsule,
                new Vector3(0.16f, 0.40f, 0.16f), color);
            var shinL = BuildLimb("Shin_L", thighL.transform, new Vector3(0, -0.38f, 0), Vector3.zero, PrimitiveType.Capsule,
                new Vector3(0.14f, 0.40f, 0.14f), color);

            var thighR = BuildLimb("Thigh_R", torso.transform, new Vector3(0.13f, -0.36f, 0), Vector3.zero, PrimitiveType.Capsule,
                new Vector3(0.16f, 0.40f, 0.16f), color);
            var shinR = BuildLimb("Shin_R", thighR.transform, new Vector3(0, -0.38f, 0), Vector3.zero, PrimitiveType.Capsule,
                new Vector3(0.14f, 0.40f, 0.14f), color);

            // Gameplay components. NOTE: PlayerSpawner is intentionally NOT added —
            // it's a scene helper that creates characters from scratch, and adding it
            // to a prefab would spawn a duplicate capsule at runtime.
            root.AddComponent<PlayerController>();
            root.AddComponent<PlayerHealth>();
            root.AddComponent<RagdollController>();

            // Save the prefab
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");

            string path = $"{PrefabDir}/{name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        /// <summary>
        /// Creates a limb: a primitive with its own collider + rigidbody,
        /// connected to the parent's rigidbody with a CharacterJoint.
        /// </summary>
        private static GameObject BuildLimb(string name, Transform parent, Vector3 localPos, Vector3 localRot,
            PrimitiveType shape, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localRot);
            go.transform.localScale = scale;

            // Material
            var rend = go.GetComponent<Renderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = color;
            rend.sharedMaterial = mat;

            // Ragdoll physics (each limb gets own rb + collider)
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.8f;
            rb.linearDamping = 1f;
            rb.angularDamping = 2f;

            // CharacterJoint to parent
            if (parent != null)
            {
                var parentRb = parent.GetComponentInParent<Rigidbody>();
                if (parentRb != null && parentRb.gameObject != go)
                {
                    var joint = go.AddComponent<CharacterJoint>();
                    joint.connectedBody = parentRb;
                    joint.autoConfigureConnectedAnchor = true;
                }
            }

            return go;
        }

        /// <summary>
        /// Simple visual-only primitive (no collider/rb) — decoration for the root.
        /// </summary>
        private static GameObject CreateVisual(PrimitiveType type, string name, Transform parent,
            Vector3 localPos, Vector3 localScale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;

            var rend = go.GetComponent<Renderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = color;
            rend.sharedMaterial = mat;

            return go;
        }
    }
}