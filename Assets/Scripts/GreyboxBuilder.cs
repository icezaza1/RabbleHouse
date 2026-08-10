using UnityEngine;

namespace RabbleHouse
{
    /// <summary>
    /// Utility class for creating greybox environment geometry.
    /// Used by the Editor script to build test scenes.
    /// </summary>
    public static class GreyboxBuilder
    {
        private static int _roomLayer;

        public static void BuildRoom()
        {
            _roomLayer = LayerMask.NameToLayer("Default");
            float width = 10f;
            float depth = 10f;

            CreateRoomBounds(width, depth);
            CreateFurniture();
            Debug.Log("[RabbleHouse] Greybox room built! Now set up player prefabs and ragdoll colliders.");
        }

        /// <summary>
        /// Creates floor and 4 walls to contain the action.
        /// </summary>
        private static void CreateRoomBounds(float width, float depth)
        {
            // Floor
            var floor = CreateBox("Floor", new Vector3(width, 0.2f, depth), Vector3.zero);
            floor.GetComponent<Renderer>().sharedMaterial.color = new Color(0.35f, 0.30f, 0.25f);

            // Back wall
            var backWall = CreateBox("Wall_Back", new Vector3(width, 3f, 0.2f),
                new Vector3(0, 1.5f, depth / 2f));
            backWall.GetComponent<Renderer>().sharedMaterial.color = new Color(0.85f, 0.82f, 0.75f);

            // Front wall
            var frontWall = CreateBox("Wall_Front", new Vector3(width, 3f, 0.2f),
                new Vector3(0, 1.5f, -depth / 2f));
            frontWall.GetComponent<Renderer>().sharedMaterial.color = new Color(0.85f, 0.82f, 0.75f);

            // Left wall
            var leftWall = CreateBox("Wall_Left", new Vector3(0.2f, 3f, depth),
                new Vector3(-width / 2f, 1.5f, 0));
            leftWall.GetComponent<Renderer>().sharedMaterial.color = new Color(0.85f, 0.82f, 0.75f);

            // Right wall
            var rightWall = CreateBox("Wall_Right", new Vector3(0.2f, 3f, depth),
                new Vector3(width / 2f, 1.5f, 0));
            rightWall.GetComponent<Renderer>().sharedMaterial.color = new Color(0.85f, 0.82f, 0.75f);

            // Ceiling
            var ceiling = CreateBox("Ceiling", new Vector3(width, 0.2f, depth),
                new Vector3(0, 3f, 0));
            ceiling.GetComponent<Renderer>().sharedMaterial.color = new Color(0.9f, 0.88f, 0.84f);
        }

        /// <summary>
        /// Creates grabbable furniture items.
        /// </summary>
        private static void CreateFurniture()
        {
            // Table (center)
            var table = CreateBox("Furniture_Table", new Vector3(1.2f, 0.8f, 0.6f),
                new Vector3(0, 0.4f, 0));
            table.GetComponent<Renderer>().sharedMaterial.color = new Color(0.55f, 0.35f, 0.2f);
            table.AddComponent<GrabbableObject>();
            table.layer = LayerMask.NameToLayer("Default");
            var tableRb = table.GetComponent<Rigidbody>();
            if (tableRb == null) tableRb = table.AddComponent<Rigidbody>();
            tableRb.mass = 12f;
            tableRb.linearDamping = 2f;
            tableRb.angularDamping = 3f;

            // Chair 1
            var chair1 = CreateBox("Furniture_Chair1", new Vector3(0.5f, 0.5f, 0.5f),
                new Vector3(-2f, 0.25f, 1f));
            chair1.GetComponent<Renderer>().sharedMaterial.color = new Color(0.4f, 0.3f, 0.2f);
            chair1.AddComponent<GrabbableObject>();
            var chair1Rb = chair1.GetComponent<Rigidbody>();
            if (chair1Rb == null) chair1Rb = chair1.AddComponent<Rigidbody>();
            chair1Rb.mass = 5f;
            chair1Rb.linearDamping = 2f;
            chair1Rb.angularDamping = 3f;

            // Chair 2
            var chair2 = CreateBox("Furniture_Chair2", new Vector3(0.5f, 0.5f, 0.5f),
                new Vector3(2f, 0.25f, -1f));
            chair2.GetComponent<Renderer>().sharedMaterial.color = new Color(0.4f, 0.3f, 0.2f);
            chair2.AddComponent<GrabbableObject>();
            var chair2Rb = chair2.GetComponent<Rigidbody>();
            if (chair2Rb == null) chair2Rb = chair2.AddComponent<Rigidbody>();
            chair2Rb.mass = 5f;
            chair2Rb.linearDamping = 2f;
            chair2Rb.angularDamping = 3f;

            // Couch (against back wall)
            var couch = CreateBox("Furniture_Couch", new Vector3(3f, 0.7f, 1f),
                new Vector3(0, 0.35f, 4f));
            couch.GetComponent<Renderer>().sharedMaterial.color = new Color(0.6f, 0.25f, 0.15f);
            couch.AddComponent<GrabbableObject>();
            var couchRb = couch.GetComponent<Rigidbody>();
            if (couchRb == null) couchRb = couch.AddComponent<Rigidbody>();
            couchRb.mass = 20f;
            couchRb.linearDamping = 3f;
            couchRb.angularDamping = 5f;

            // Coffee table
            var coffeeTable = CreateBox("Furniture_CoffeeTable", new Vector3(0.8f, 0.3f, 0.5f),
                new Vector3(0, 0.15f, 2.5f));
            coffeeTable.GetComponent<Renderer>().sharedMaterial.color = new Color(0.45f, 0.28f, 0.15f);
            coffeeTable.AddComponent<GrabbableObject>();
            var ctRb = coffeeTable.GetComponent<Rigidbody>();
            if (ctRb == null) ctRb = coffeeTable.AddComponent<Rigidbody>();
            ctRb.mass = 8f;
            ctRb.linearDamping = 2f;
            ctRb.angularDamping = 3f;

            // Floor lamp
            var lampBase = CreateBox("Furniture_LampBase", new Vector3(0.2f, 0.2f, 0.2f),
                new Vector3(-4f, 0.1f, 3.5f));
            lampBase.GetComponent<Renderer>().sharedMaterial.color = new Color(0.2f, 0.2f, 0.2f);
            lampBase.AddComponent<GrabbableObject>();
            var lampRb = lampBase.GetComponent<Rigidbody>();
            if (lampRb == null) lampRb = lampBase.AddComponent<Rigidbody>();
            lampRb.mass = 3f;
            lampRb.linearDamping = 1f;

            // Bookshelf (against left wall)
            var bookshelf = CreateBox("Furniture_Bookshelf", new Vector3(1f, 1.8f, 0.4f),
                new Vector3(-4.3f, 0.9f, 0));
            bookshelf.GetComponent<Renderer>().sharedMaterial.color = new Color(0.4f, 0.25f, 0.12f);
            bookshelf.AddComponent<GrabbableObject>();
            var bsRb = bookshelf.GetComponent<Rigidbody>();
            if (bsRb == null) bsRb = bookshelf.AddComponent<Rigidbody>();
            bsRb.mass = 15f;
            bsRb.linearDamping = 3f;
            bsRb.angularDamping = 5f;

            // Plant pot
            var plantPot = CreateBox("Furniture_PlantPot", new Vector3(0.3f, 0.3f, 0.3f),
                new Vector3(4f, 0.15f, 2f));
            plantPot.GetComponent<Renderer>().sharedMaterial.color = new Color(0.5f, 0.35f, 0.2f);
            plantPot.AddComponent<GrabbableObject>();
            var ppRb = plantPot.GetComponent<Rigidbody>();
            if (ppRb == null) ppRb = plantPot.AddComponent<Rigidbody>();
            ppRb.mass = 4f;
            ppRb.linearDamping = 1f;

            // Side table
            var sideTable = CreateBox("Furniture_SideTable", new Vector3(0.5f, 0.5f, 0.5f),
                new Vector3(4f, 0.25f, -3f));
            sideTable.GetComponent<Renderer>().sharedMaterial.color = new Color(0.5f, 0.4f, 0.25f);
            sideTable.AddComponent<GrabbableObject>();
            var stRb = sideTable.GetComponent<Rigidbody>();
            if (stRb == null) stRb = sideTable.AddComponent<Rigidbody>();
            stRb.mass = 6f;
            stRb.linearDamping = 2f;
            stRb.angularDamping = 3f;

            // TV stand + TV
            var tvStand = CreateBox("Furniture_TVStand", new Vector3(1.5f, 0.5f, 0.5f),
                new Vector3(0, 0.25f, -4.3f));
            tvStand.GetComponent<Renderer>().sharedMaterial.color = new Color(0.3f, 0.3f, 0.3f);

            var tv = CreateBox("Furniture_TV", new Vector3(1.4f, 0.8f, 0.1f),
                new Vector3(0, 0.9f, -4.3f));
            tv.GetComponent<Renderer>().sharedMaterial.color = new Color(0.05f, 0.05f, 0.1f);
            tv.AddComponent<GrabbableObject>();
            var tvRb = tv.GetComponent<Rigidbody>();
            if (tvRb == null) tvRb = tv.AddComponent<Rigidbody>();
            tvRb.mass = 7f;
            tvRb.linearDamping = 2f;
            tvRb.angularDamping = 3f;
        }

        /// <summary>
        /// Helper: creates a coloured cube with a box collider.
        /// </summary>
        private static GameObject CreateBox(string name, Vector3 size, Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = size;

            // Make sure it has a renderer
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                // Create a unique material instance
                renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            }

            return go;
        }
    }
}