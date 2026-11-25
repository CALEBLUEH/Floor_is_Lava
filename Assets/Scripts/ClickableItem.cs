using UnityEngine;

public class ClickableItem : MonoBehaviour
{
    public enum ItemType
    {
        SpeedBoost,
        Shield,
        Heal,
        Slow,
        TimeLoss
    }

    public ItemType itemType;

    private void OnMouseDown()
    {
        Debug.Log("Clicked: " + itemType);

        ApplyEffect();

        Destroy(gameObject);
    }

    void ApplyEffect()
    {
        switch (itemType)
        {
            case ItemType.SpeedBoost:
                Debug.Log("SPEED BOOST!");
                break;

            case ItemType.Shield:
                Debug.Log("SHIELD!");
                break;

            case ItemType.Heal:
                Debug.Log("HEAL!");
                break;

            case ItemType.Slow:
                Debug.Log("SLOWED!");
                break;

            case ItemType.TimeLoss:
                Debug.Log("TIME LOST!");
                break;
        }
    }
}