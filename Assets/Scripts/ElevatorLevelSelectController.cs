using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class ElevatorLevelSelectController : MonoBehaviour
{
    private const string CurrentFloorKey = "ACG.Elevator.CurrentFloor";

    [System.Serializable]
    public sealed class Floor
    {
        public string title;
        public string scene;
        [TextArea] public string description;
        public Color color;
    }

    [SerializeField] private Camera eyeCamera;
    [SerializeField] private ElevatorFirstPersonMover playerMover;
    [SerializeField] private Transform leftDoor;
    [SerializeField] private Transform rightDoor;
    [SerializeField] private Vector3 leftOpenOffset = new Vector3(-0.75f, 0f, 0f);
    [SerializeField] private Vector3 rightOpenOffset = new Vector3(0.75f, 0f, 0f);
    [SerializeField] private ElevatorFloorButton[] floorButtons;
    [SerializeField] private ElevatorFloorButton enterButton;
    [SerializeField] private ElevatorFloorButton mainMenuButton;
    [SerializeField] private Vector3 mainMenuButtonOffset = new Vector3(0f, 0f, .25f);
    [SerializeField, Min(0.5f)] private float buttonReach = 6f;
    [SerializeField] private TextMeshProUGUI titleLabel;
    [SerializeField] private TextMeshProUGUI descriptionLabel;
    [SerializeField] private TextMeshProUGUI bestLabel;
    [SerializeField] private TextMeshProUGUI instructionLabel;
    [SerializeField] private TextMeshPro floorDisplay;
    [SerializeField] private TextMeshPro portalLabel;
    [SerializeField] private Renderer portalRenderer;
    [SerializeField] private Image eyeCurtain;
    [SerializeField] private Image glare;
    [SerializeField] private Floor[] floors;

    private readonly Color idleColor = new Color(0.12f, 0.20f, 0.28f);
    private Vector3 leftClosed;
    private Vector3 rightClosed;
    private Quaternion originalRotation;
    private Vector3 originalEyePosition;
    private float yaw;
    private float pitch;
    private int selected = -1;
    private ElevatorFloorButton aimed;
    private bool canChoose;
    private bool travelling;
    private int currentFloor;
    private AudioClip arrivalDing;

    private void Awake()
    {
        leftClosed = leftDoor.localPosition;
        rightClosed = rightDoor.localPosition;
        leftDoor.localPosition = leftClosed + leftOpenOffset;
        rightDoor.localPosition = rightClosed + rightOpenOffset;
        playerMover?.SetCanMove(false);
        originalRotation = eyeCamera.transform.localRotation;
        originalEyePosition = eyeCamera.transform.localPosition;
        eyeCurtain.color = Color.black;
        glare.color = new Color(1f, 1f, 1f, 0f);
        portalRenderer.gameObject.SetActive(false);
        currentFloor = floors != null && floors.Length > 0
            ? Mathf.Clamp(PlayerPrefs.GetInt(CurrentFloorKey, 0), 0, floors.Length - 1)
            : 0;
        arrivalDing = Resources.Load<AudioClip>("SceneAudio/ElevatorDing");
        if (floorDisplay != null) floorDisplay.text = (currentFloor + 1).ToString("00");
        if (instructionLabel != null)
            instructionLabel.rectTransform.anchoredPosition += Vector2.down * 49f;
        ShowFloor(-1);
        RefreshButtons();
        LockCursor();
        StartCoroutine(Arrival());
    }

    private void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && PauseMenuController.Instance == null)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        if (Input.GetKeyDown(KeyCode.Backspace) && !travelling)
            ReturnToMainMenu();
        if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            LockCursor();
            return;
        }
        if (Cursor.lockState == CursorLockMode.Locked && !travelling)
        {
            yaw = Mathf.Repeat(yaw + Input.GetAxis("Mouse X") * 2.2f, 360f);
            pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * 2.2f, -55f, 55f);
            eyeCamera.transform.localRotation = originalRotation * Quaternion.Euler(pitch, yaw, 0f);
        }
        if (!canChoose || travelling)
            return;

        aimed = FindAimedButton();

        RefreshButtons();
        if (aimed != null && aimed.isMainMenu)
            ShowMainMenu();
        else if (aimed != null && !aimed.isEnter)
            ShowFloor(aimed.floor);
        else if (selected >= 0)
            ShowFloor(selected);
        else
            ShowFloor(-1);

        if (Cursor.lockState == CursorLockMode.Locked && Input.GetMouseButtonDown(0) && aimed != null)
        {
            if (aimed.isMainMenu)
            {
                ReturnToMainMenu();
            }
            else if (aimed.isEnter)
            {
                if (selected >= 0) StartCoroutine(Travel());
                else instructionLabel.text = "SELECT A FLOOR FIRST";
            }
            else
            {
                selected = aimed.floor;
                instructionLabel.text = "FLOOR " + (selected + 1).ToString("00") + " SELECTED  •  PRESS ENTER";
                RefreshButtons();
            }
        }
        if (selected >= 0 && Input.GetKeyDown(KeyCode.Return)) StartCoroutine(Travel());
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void ReturnToMainMenu()
    {
        if (!travelling) SceneManager.LoadScene("Interface_MainMenu");
    }

    private ElevatorFloorButton FindAimedButton()
    {
        RaycastHit[] hits = Physics.RaycastAll(eyeCamera.transform.position,
            eyeCamera.transform.forward, buttonReach, ~0, QueryTriggerInteraction.Collide);
        ElevatorFloorButton closest = null;
        float distance = float.PositiveInfinity;
        foreach (RaycastHit hit in hits)
        {
            ElevatorFloorButton button = hit.collider.GetComponentInParent<ElevatorFloorButton>();
            if (button == null || hit.distance >= distance) continue;
            closest = button;
            distance = hit.distance;
        }
        return closest;
    }

    private IEnumerator Arrival()
    {
        instructionLabel.text = "";
        yield return Fade(eyeCurtain, 1f, 0f, 1.8f);
        yield return new WaitForSeconds(0.45f);
        yield return MoveDoors(false, 1.25f);
        canChoose = true;
        playerMover?.SetCanMove(true);
        instructionLabel.text = "WASD TO WALK  •  AIM AT A FLOOR KEY  •  CLICK TO SELECT";
    }

    private IEnumerator Travel()
    {
        travelling = true;
        canChoose = false;
        playerMover?.SetCanMove(false);
        Floor floor = floors[selected];
        portalRenderer.gameObject.SetActive(true);
        portalRenderer.material.color = floor.color * 1.35f;
        portalLabel.text = floor.title.ToUpperInvariant();
        portalLabel.color = floor.color;
        int destinationFloor = selected;
        int direction = System.Math.Sign(destinationFloor - currentFloor);
        string movement = direction > 0 ? "ASCENDING" : direction < 0 ? "DESCENDING" : "ARRIVING";
        instructionLabel.text = movement + " AT FLOOR " + (destinationFloor + 1).ToString("00");

        if (direction == 0)
        {
            yield return new WaitForSeconds(.5f);
        }
        else
        {
            floorDisplay.text = (currentFloor + 1).ToString("00");
            float startingFloorElapsed = 0f;
            while (startingFloorElapsed < 0.42f)
            {
                startingFloorElapsed += Time.deltaTime;
                eyeCamera.transform.localPosition = originalEyePosition +
                    Vector3.up * (Mathf.Sin(Time.time * 34f) * 0.012f);
                yield return null;
            }
            for (int floorIndex = currentFloor + direction;
                 direction > 0 ? floorIndex <= destinationFloor : floorIndex >= destinationFloor;
                 floorIndex += direction)
            {
                floorDisplay.text = (floorIndex + 1).ToString("00");
                float elapsed = 0f;
                while (elapsed < 0.42f)
                {
                    elapsed += Time.deltaTime;
                    eyeCamera.transform.localPosition = originalEyePosition +
                        Vector3.up * (Mathf.Sin(Time.time * 34f) * 0.012f);
                    yield return null;
                }
            }
        }

        currentFloor = destinationFloor;
        PlayerPrefs.SetInt(CurrentFloorKey, currentFloor);
        PlayerPrefs.Save();
        eyeCamera.transform.localPosition = originalEyePosition;
        floorDisplay.text = (currentFloor + 1).ToString("00");
        instructionLabel.text = "FLOOR " + (currentFloor + 1).ToString("00") + "  •  ARRIVED";
        SceneAudioDirector.PlayEffect(arrivalDing);
        yield return new WaitForSeconds(0.4f);
        yield return FaceDoors(.75f);
        yield return MoveDoors(true, 1.3f);
        yield return Fade(glare, 0f, 0.8f, 1.1f);
        yield return new WaitForSeconds(0.45f);
        yield return Fade(glare, 0.8f, 1f, 0.4f);
        SceneManager.LoadScene(floor.scene);
    }

    private IEnumerator FaceDoors(float duration)
    {
        Renderer leftVisual = leftDoor.GetComponentInChildren<Renderer>();
        Renderer rightVisual = rightDoor.GetComponentInChildren<Renderer>();
        Vector3 target = leftVisual != null && rightVisual != null
            ? (leftVisual.bounds.center + rightVisual.bounds.center) * .5f
            : (leftDoor.position + rightDoor.position) * .5f;
        target.y = eyeCamera.transform.position.y;
        Vector3 direction = target - eyeCamera.transform.position;
        if (direction.sqrMagnitude < .01f) yield break;
        Quaternion start = eyeCamera.transform.rotation;
        Quaternion end = Quaternion.LookRotation(direction.normalized, Vector3.up);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            eyeCamera.transform.rotation = Quaternion.Slerp(start, end,
                Mathf.SmoothStep(0f, 1f, elapsed / duration));
            yield return null;
        }
        eyeCamera.transform.rotation = end;
    }

    private IEnumerator MoveDoors(bool open, float duration)
    {
        Vector3 leftStart = leftDoor.localPosition;
        Vector3 rightStart = rightDoor.localPosition;
        Vector3 leftEnd = leftClosed + (open ? leftOpenOffset : Vector3.zero);
        Vector3 rightEnd = rightClosed + (open ? rightOpenOffset : Vector3.zero);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            leftDoor.localPosition = Vector3.Lerp(leftStart, leftEnd, t);
            rightDoor.localPosition = Vector3.Lerp(rightStart, rightEnd, t);
            yield return null;
        }
        leftDoor.localPosition = leftEnd;
        rightDoor.localPosition = rightEnd;
    }

    private static IEnumerator Fade(Image image, float from, float to, float duration)
    {
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            Color color = image.color;
            color.a = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            image.color = color;
            yield return null;
        }
        Color final = image.color;
        final.a = to;
        image.color = final;
    }

    private void ShowFloor(int floor)
    {
        if (floor < 0 || floor >= floors.Length)
        {
            titleLabel.text = "CHOOSE A FLOOR";
            descriptionLabel.text = "Aim at a numbered elevator button to preview a stage.";
            bestLabel.text = "BEST RECORD";
            return;
        }
        Floor data = floors[floor];
        titleLabel.text = (floor + 1).ToString("00") + "  /  " + data.title.ToUpperInvariant();
        titleLabel.color = data.color;
        descriptionLabel.text = data.description;
        if (data.scene == "Level_Tutorial")
        {
            bestLabel.text = BestTimeRecord.IsPerfect(data.scene)
                ? "STATUS  COMPLETE  •  GOLD"
                : BestTimeRecord.HasCompleted(data.scene) ? "STATUS  COMPLETE" : "STATUS  NOT COMPLETE";
        }
        else if (data.scene == "Level_Survival")
        {
            bestLabel.text = BestTimeRecord.TryGet(data.scene, out float survivalSeconds)
                ? "BEST SURVIVAL  " + BestTimeRecord.Format(survivalSeconds) +
                  (BestTimeRecord.IsPerfect(data.scene) ? "  •  GOLD" : string.Empty)
                : "BEST SURVIVAL  --:--.--";
        }
        else
        {
            bestLabel.text = BestTimeRecord.TryGet(data.scene, out float seconds, out bool perfect)
                ? "BEST RECORD  " + BestTimeRecord.Format(seconds) + (perfect ? "  •  PERFECT" : string.Empty)
                : "BEST RECORD  --";
        }
    }

    private void ShowMainMenu()
    {
        titleLabel.text = "MAIN MENU";
        titleLabel.color = new Color(.95f, .72f, .32f);
        descriptionLabel.text = "Return to the main menu.";
        bestLabel.text = "CLICK TO RETURN";
    }

    private void RefreshButtons()
    {
        foreach (ElevatorFloorButton button in floorButtons)
        {
            if (button == null || button.face == null) continue;
            bool highlighted = button == aimed || button.floor == selected;
            bool perfect = button.floor >= 0 && button.floor < floors.Length &&
                BestTimeRecord.IsPerfect(floors[button.floor].scene);
            Color gold = highlighted
                ? new Color(1f, .84f, .24f)
                : new Color(1f, .58f, .06f);
            bool completed = button.floor >= 0 && button.floor < floors.Length &&
                BestTimeRecord.HasCompleted(floors[button.floor].scene);
            Color green = highlighted
                ? new Color(.34f, 1f, .55f)
                : new Color(.08f, .62f, .27f);
            Color color = perfect ? gold : completed ? green : highlighted ? floors[button.floor].color : idleColor;
            button.face.material.color = color;
            button.face.material.SetColor("_EmissionColor",
                perfect ? color * (highlighted ? 1.7f : .85f) :
                completed ? color * (highlighted ? 1.35f : .75f) :
                highlighted ? color * 1.2f : Color.black);
        }
        if (enterButton != null && enterButton.face != null)
        {
            Color color = selected >= 0 ? new Color(0.25f, 1f, 0.65f) : idleColor;
            enterButton.face.material.color = color;
            enterButton.face.material.SetColor("_EmissionColor", selected >= 0 ? color : Color.black);
        }
        if (mainMenuButton != null && mainMenuButton.face != null)
        {
            Color color = mainMenuButton == aimed ? new Color(.95f, .72f, .32f) : idleColor;
            mainMenuButton.face.material.color = color;
            mainMenuButton.face.material.SetColor("_EmissionColor", mainMenuButton == aimed ? color : Color.black);
        }
    }

#if UNITY_EDITOR
    public void ConfigureMainMenuButtonForEditor(ElevatorFloorButton button)
    {
        mainMenuButton = button;
    }

    public void Configure(Camera camera, ElevatorFirstPersonMover mover, Transform left, Transform right,
        Vector3 leftOffset, Vector3 rightOffset, ElevatorFloorButton[] buttons,
        ElevatorFloorButton enter, TextMeshProUGUI title, TextMeshProUGUI description,
        TextMeshProUGUI best, TextMeshProUGUI instruction, TextMeshPro display,
        TextMeshPro portalText, Renderer portal, Image curtain, Image reveal, Floor[] levelData)
    {
        eyeCamera = camera; playerMover = mover; leftDoor = left; rightDoor = right;
        leftOpenOffset = leftOffset; rightOpenOffset = rightOffset; floorButtons = buttons;
        enterButton = enter; titleLabel = title; descriptionLabel = description;
        bestLabel = best; instructionLabel = instruction; floorDisplay = display;
        portalLabel = portalText; portalRenderer = portal; eyeCurtain = curtain; glare = reveal;
        floors = levelData;
    }
#endif
}
