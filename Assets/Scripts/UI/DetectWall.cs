using RabbleHouse;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class DetectWall : MonoBehaviour
{
    [SerializeField] private LayerMask wallLayer;
    [SerializeField] private float transparency = 0.3f;
    private Transform playerTransform;
    private Transform cameraTransform;

    private Dictionary<GameObject, Material> originalMaterials = new Dictionary<GameObject, Material>();
    private List<GameObject> transparentWalls = new List<GameObject>();

    private void Start()
    {
        cameraTransform = GetComponent<Transform>();
        FindPlayer();
    }

    private void Update()
    {
        DetectWalls();
    }
    private void FindPlayer()
    {
        PlayerInput playerInput = FindAnyObjectByType<PlayerInput>();
        if (playerInput != null)
        {
            PlayerHealth playerHealth = playerInput.GetComponent<PlayerHealth>();
            playerTransform = FindCoreRigidbody(playerHealth).transform;
        }
    }

    private Rigidbody FindCoreRigidbody(PlayerHealth target)
    {
        Rigidbody[] bodies = target.GetComponentsInChildren<Rigidbody>(true);
        foreach (var rb in bodies)
            if (rb.transform.name.Contains("Hips"))
                return rb;
        return bodies.Length > 0 ? bodies[0] : null;
    }

    private void DetectWalls()
    {
        // Reset transparency for previously detected walls
        ResetWallTransparency();

        // Raycast from the camera to the player
        Vector3 direction = playerTransform.position - cameraTransform.position;
        RaycastHit[] hits = Physics.RaycastAll(cameraTransform.position, direction.normalized, direction.magnitude, wallLayer);

        foreach (RaycastHit hit in hits)
        {
            GameObject wall = hit.collider.gameObject;
            if (!originalMaterials.ContainsKey(wall))
            {
                // Store the original material once
                Renderer renderer = wall.GetComponent<Renderer>();
                if (renderer != null)
                {
                    originalMaterials[wall] = renderer.material;

                    // Create a new material instance
                    Material transparentMat = new Material(renderer.material);
                    SetMaterialTransparency(transparentMat, transparency);
                    renderer.material = transparentMat;

                    transparentWalls.Add(wall);
                }
            }
        }
    }

    private void ResetWallTransparency()
    {
        foreach (GameObject wall in transparentWalls)
        {
            if (wall != null && originalMaterials.ContainsKey(wall))
            {
                Renderer renderer = wall.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material = originalMaterials[wall]; // Restore original material
                }
                originalMaterials.Remove(wall);
            }
        }
        transparentWalls.Clear();
    }

    private void SetMaterialTransparency(Material mat, float alpha)
    {
        Color color = mat.color;
        color.a = alpha;
        mat.color = color;

        // Correct transparency blending settings
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0); // Disable depth writing
        mat.SetInt("_Surface", 1); // Ensure transparency
        mat.renderQueue = 3000; // Render after opaque objects
    }
}
