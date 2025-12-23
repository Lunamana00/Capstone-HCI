using UnityEngine;

public class LimbSelfCollisionIgnorer : MonoBehaviour
{
    [Tooltip("Automatically find and disable collisions on Start")]
    public bool autoSetupOnStart = true;

    [Header("Debug")]
    [SerializeField] private int ignoredCollisionCount = 0;

    private void Start()
    {
        if (autoSetupOnStart)
        {
            IgnoreAllSelfCollisions();
        }
    }

    [ContextMenu("Ignore Self Collisions")]
    public void IgnoreAllSelfCollisions()
    {
        Collider[] limbColliders = GetComponentsInChildren<Collider>();
        if (limbColliders.Length < 2)
        {
            Debug.LogWarning($"LimbSelfCollisionIgnorer: Only found {limbColliders.Length} collider(s). Need at least 2 to ignore collisions.");
            return;
        }

        int count = 0;
        for (int i = 0; i < limbColliders.Length; i++)
        {
            for (int j = i + 1; j < limbColliders.Length; j++)
            {
                Physics.IgnoreCollision(limbColliders[i], limbColliders[j], true);
                count++;
            }
        }

        ignoredCollisionCount = count;
        Debug.Log($"LimbSelfCollisionIgnorer: Ignored {count} collision pairs among {limbColliders.Length} limb colliders");
    }

    [ContextMenu("Re-enable Self Collisions")]
    public void ReEnableSelfCollisions()
    {
        Collider[] limbColliders = GetComponentsInChildren<Collider>();

        int count = 0;
        for (int i = 0; i < limbColliders.Length; i++)
        {
            for (int j = i + 1; j < limbColliders.Length; j++)
            {
                Physics.IgnoreCollision(limbColliders[i], limbColliders[j], false);
                count++;
            }
        }

        ignoredCollisionCount = 0;
        Debug.Log($"LimbSelfCollisionIgnorer: Re-enabled {count} collision pairs");
    }
}
