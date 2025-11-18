using System;
using Ink.Runtime;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InkPresenter : MonoBehaviour
{
    public static event Action<Story> OnCreateStory;

    [Header("Ink")]
    [SerializeField] private TextAsset inkJSONAsset = null;
    public Story story;

    [Header("UI Roots")]
    [SerializeField] private Canvas canvas = null;
    [Tooltip("Contenedor con Vertical Layout Group (+ Content Size Fitter).")]
    [SerializeField] private RectTransform contentRoot = null;   // <-- NUEVO

    [Header("UI Prefabs")]
    [SerializeField] private TextMeshProUGUI textPrefab = null;  // <-- ahora TMP
    [SerializeField] private Button buttonPrefab = null;

    void Awake()
    {
        RemoveChildren();
        StartStory();
    }

    void StartStory()
    {
        story = new Story(inkJSONAsset.text);
        OnCreateStory?.Invoke(story);
        RefreshView();
    }

    void RefreshView()
    {
        RemoveChildren();

        // texto de la historia
        while (story.canContinue)
        {
            string line = story.Continue()?.Trim();
            if (!string.IsNullOrEmpty(line))
                CreateContentView(line);
        }

        // opciones
        if (story.currentChoices.Count > 0)
        {
            for (int i = 0; i < story.currentChoices.Count; i++)
            {
                Choice choice = story.currentChoices[i];
                Button btn = CreateChoiceView(choice.text.Trim());
                btn.onClick.AddListener(() =>
                {
                    story.ChooseChoiceIndex(choice.index);
                    RefreshView();
                });
            }
        }
        else
        {
            // Botón para reiniciar la historia
            Button restart = CreateChoiceView("Volver al incio");
            restart.onClick.AddListener(StartStory);

            // Botón para salir de la aplicación
            Button quit = CreateChoiceView("Salir");
            quit.onClick.AddListener(QuitGame);
        }

        // fuerza el layout para que no se monten
        if (contentRoot) LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
    }

    // --- helpers de UI ---
    void CreateContentView(string text)
    {
        var targetParent = (contentRoot != null) ? (Transform)contentRoot : canvas.transform;
        TextMeshProUGUI storyText = Instantiate(textPrefab, targetParent, false);
        storyText.text = text;
    }

    Button CreateChoiceView(string text)
    {
        var targetParent = (contentRoot != null) ? (Transform)contentRoot : canvas.transform;
        Button choice = Instantiate(buttonPrefab, targetParent, false);

        // TMP primero, Text legacy como respaldo
        var tmp = choice.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp) tmp.text = text;
        else
        {
            var legacy = choice.GetComponentInChildren<Text>(true);
            if (legacy) legacy.text = text;
        }
        return choice;
    }

    // borra solo el contenido dinámico
    void RemoveChildren()
    {
        Transform t = (contentRoot != null) ? (Transform)contentRoot : canvas.transform;
        for (int i = t.childCount - 1; i >= 0; --i)
            Destroy(t.GetChild(i).gameObject);
    }

    // cierra la aplicación (o sale del modo Play en el editor)
    void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
