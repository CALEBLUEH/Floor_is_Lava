using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class LevelCarouselController : MonoBehaviour
{
    [SerializeField] private RectTransform[] cards;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    private int selected;

    private void Awake()
    {
        previousButton?.onClick.AddListener(() => Select(selected - 1));
        nextButton?.onClick.AddListener(() => Select(selected + 1));
        Select(0, true);
    }

    private void Select(int index, bool immediate = false)
    {
        if (cards == null || cards.Length == 0) return;
        selected = (index % cards.Length + cards.Length) % cards.Length;
        for (int i = 0; i < cards.Length; i++)
        {
            int delta = i - selected;
            if (delta > cards.Length / 2) delta -= cards.Length;
            if (delta < -cards.Length / 2) delta += cards.Length;
            bool visible = Mathf.Abs(delta) <= 1;
            cards[i].gameObject.SetActive(visible);
            if (!visible) continue;
            Vector2 position = new Vector2(delta * 560f, delta == 0 ? 5f : 25f);
            float scale = delta == 0 ? 1f : .72f;
            cards[i].SetSiblingIndex(delta == 0 ? cards.Length : i);
            cards[i].DOKill();
            if (immediate) { cards[i].anchoredPosition = position; cards[i].localScale = Vector3.one * scale; }
            else
            {
                RectTransform card = cards[i];
                DOTween.To(() => card.anchoredPosition, value => card.anchoredPosition = value, position, .36f).SetEase(Ease.OutCubic).SetUpdate(true);
                card.DOScale(scale, .36f).SetEase(Ease.OutBack).SetUpdate(true);
            }
        }
    }

#if UNITY_EDITOR
    public void Configure(RectTransform[] levelCards, Button previous, Button next)
    { cards = levelCards; previousButton = previous; nextButton = next; }
#endif
}
