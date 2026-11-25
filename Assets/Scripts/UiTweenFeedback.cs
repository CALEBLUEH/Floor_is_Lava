using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class UiTweenFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private bool floatContinuously;
    private Vector3 originalScale;
    private Vector2 originalPosition;

    private void Awake()
    {
        originalScale = transform.localScale;
        if (transform is RectTransform rect) originalPosition = rect.anchoredPosition;
        if (label == null) label = GetComponentInChildren<TMP_Text>();
        if (floatContinuously && transform is RectTransform floating)
            DOTween.To(() => floating.anchoredPosition.y, y => floating.anchoredPosition = new Vector2(floating.anchoredPosition.x, y), originalPosition.y + 13f, 1.35f).SetLink(gameObject, LinkBehaviour.KillOnDestroy).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
    }

    private void OnDestroy() => transform.DOKill();
    public void OnPointerEnter(PointerEventData eventData) => Focus(true);
    public void OnPointerExit(PointerEventData eventData) => Focus(false);
    public void OnSelect(BaseEventData eventData) => Focus(true);
    public void OnDeselect(BaseEventData eventData) => Focus(false);

    private void Focus(bool focused)
    {
        transform.DOKill();
        transform.DOScale(focused ? originalScale * 1.075f : originalScale, .2f).SetEase(focused ? Ease.OutBack : Ease.OutQuad).SetUpdate(true);
        if (focused && label != null)
        {
            RectTransform textRect = label.rectTransform;
            float y = textRect.anchoredPosition.y;
            textRect.DOKill();
            DOTween.To(() => textRect.anchoredPosition.y, value => textRect.anchoredPosition = new Vector2(textRect.anchoredPosition.x, value), y + 10f, .13f).SetLink(gameObject, LinkBehaviour.KillOnDestroy).SetLoops(2, LoopType.Yoyo).SetEase(Ease.OutQuad).SetUpdate(true);
        }
    }

#if UNITY_EDITOR
    public void Configure(TMP_Text target, bool floating) { label = target; floatContinuously = floating; }
#endif
}
