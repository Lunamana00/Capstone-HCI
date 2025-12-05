using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Automatically ignores collisions between all tail bone colliders
/// to prevent self-collision issues that can interfere with physics simulation.
/// Attach this to the Tail Root GameObject.
/// </summary>
public class TailSelfCollisionIgnorer : MonoBehaviour
{
    [Tooltip("Automatically find and disable collisions on Start")]
    public bool autoSetupOnStart = true;

    [Header("Debug")]
    [SerializeField] private int ignoredCollisionCount = 0;

    void Start()
    {
        if (autoSetupOnStart)
        {
            IgnoreAllTailSelfCollisions();
        }
    }

    [ContextMenu("Ignore Self Collisions")]
    public void IgnoreAllTailSelfCollisions()
    {
        // Collect all colliders in this tail hierarchy
        Collider[] tailColliders = GetComponentsInChildren<Collider>();

        if (tailColliders.Length < 2)
        {
            Debug.LogWarning($"TailSelfCollisionIgnorer: Only found {tailColliders.Length} collider(s). Need at least 2 to ignore collisions.");
            return;
        }

        // Ignore collisions between every pair of colliders
        int count = 0;
        for (int i = 0; i < tailColliders.Length; i++)
        {
            for (int j = i + 1; j < tailColliders.Length; j++)
            {
                Physics.IgnoreCollision(tailColliders[i], tailColliders[j], true);
                count++;
            }
        }

        ignoredCollisionCount = count;
        Debug.Log($"TailSelfCollisionIgnorer: Ignored {count} collision pairs among {tailColliders.Length} tail colliders");
    }

    [ContextMenu("Re-enable Self Collisions")]
    public void ReEnableSelfCollisions()
    {
        // Re-enable collisions between all pairs
        Collider[] tailColliders = GetComponentsInChildren<Collider>();

        int count = 0;
        for (int i = 0; i < tailColliders.Length; i++)
        {
            for (int j = i + 1; j < tailColliders.Length; j++)
            {
                Physics.IgnoreCollision(tailColliders[i], tailColliders[j], false);
                count++;
            }
        }

        ignoredCollisionCount = 0;
        Debug.Log($"TailSelfCollisionIgnorer: Re-enabled {count} collision pairs");
    }
}
