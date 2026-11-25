using UnityEngine;

[DisallowMultipleComponent]
public class PortalPair : MonoBehaviour
{
    [SerializeField] private FloorTile portalA;
    [SerializeField] private FloorTile portalB;

    private void Awake()
    {
        LinkPortals();
    }

    private void OnValidate()
    {
        LinkPortals();
    }

    private void LinkPortals()
    {
        if (portalA == null || portalB == null || portalA == portalB)
        {
            return;
        }

        portalA.SetPortalPartner(portalB);
        portalB.SetPortalPartner(portalA);
    }

#if UNITY_EDITOR
    public void ConfigurePrefab(FloorTile firstPortal, FloorTile secondPortal)
    {
        portalA = firstPortal;
        portalB = secondPortal;
        LinkPortals();
    }
#endif
}
