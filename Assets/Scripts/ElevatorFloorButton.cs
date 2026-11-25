using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class ElevatorFloorButton : MonoBehaviour
{
    public int floor;
    public bool isEnter;
    public bool isMainMenu;
    public Renderer face;
}
