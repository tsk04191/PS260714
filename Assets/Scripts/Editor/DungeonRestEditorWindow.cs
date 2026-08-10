using System;
using System.Collections.Generic;
using System.IO;
using PS260714.Localization.Editor;
using Sirenix.OdinInspector.Editor;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UguiImage = UnityEngine.UI.Image;

public sealed class DungeonRestEditorWindow : EditorWindow
{
    public const string MenuPath = PS260714EditorMenu.RestEditor;

    private const string ClientScenePath = "Assets/Scenes/ClientScene.unity";
    private const string RestSdPrefabPath =
        "Assets/Resources/Presentation/DungeonRestCharacterSd.prefab";
    private const float InspectorWidth = 470f;
    private const float InspectorLabelWidth = 160f;
    private const float PreviewAspect = 16f / 9f;
    private const float PreviewMargin = 12f;

    private readonly List<DungeonRestSO> _rests = new();
    private PS260714UIToolkitAssetToolbar _assetToolbar;
    private PS260714UIToolkitAssetList<DungeonRestSO> _restList;
    private PS260714UIToolkitRenameRow _renameRow;
    private Label _statusLabel;
    private IMGUIContainer _preview;
    private IMGUIContainer _inspector;
    private DungeonRestSO _selectedRest;
    private PropertyTree _propertyTree;
    private Vector2 _inspectorScroll;

    public static void Open(DungeonRestSO selected = null)
    {
        DungeonRestEditorWindow window =
            GetWindow<DungeonRestEditorWindow>();
        if (selected != null)
            window._selectedRest = selected;
        window.titleContent = new GUIContent("Rest Editor");
        window.minSize = new Vector2(1240f, 650f);
        window.Show();
        window.RefreshAssets(selected);
    }

    public void CreateGUI()
    {
        DisposePropertyTree();
        rootVisualElement.Clear();
        rootVisualElement.style.flexDirection = FlexDirection.Column;
        rootVisualElement.style.backgroundColor =
            new Color(0.075f, 0.085f, 0.09f);

        PS260714LocalizationKeyField.Refresh();
        BuildToolbar();

        VisualElement body = new();
        body.style.flexGrow = 1f;
        body.style.flexDirection = FlexDirection.Row;
        rootVisualElement.Add(body);
        body.Add(BuildAssetList());

        _preview = new IMGUIContainer(DrawPreview);
        _preview.style.flexGrow = 1f;
        _preview.style.minWidth = 420f;
        body.Add(_preview);
        body.Add(BuildInspector());
        RefreshAssets(_selectedRest);
    }

    private void OnDisable()
    {
        DisposePropertyTree();
    }

    private void OnDestroy()
    {
        DisposePropertyTree();
    }

    private void OnSelectionChange()
    {
        if (Selection.activeObject is DungeonRestSO rest &&
            rest != _selectedRest)
        {
            SelectRest(rest);
        }
    }

    private void BuildToolbar()
    {
        _assetToolbar = new PS260714UIToolkitAssetToolbar(
            CreateRest,
            Save,
            DuplicateSelected,
            BeginRename,
            DeleteSelected,
            PingSelected,
            () => RefreshAssets(_selectedRest),
            "Select or create a RestSO.");
        _statusLabel = _assetToolbar.StatusLabel;
        rootVisualElement.Add(_assetToolbar);

        Toolbar tools = new();
        tools.Add(new ToolbarButton(ValidateSelected)
        {
            text = "Validate",
        });
        tools.Add(new ToolbarSpacer { flex = true });
        tools.Add(new ToolbarButton(ApplyToClientScene)
        {
            text = "Apply To Client Scene",
        });
        rootVisualElement.Add(tools);
    }

    private VisualElement BuildAssetList()
    {
        _restList = new PS260714UIToolkitAssetList<DungeonRestSO>(
            "REST ASSETS",
            PS260714AssetEditorList.Width,
            item => item.name,
            item => item.RoomId,
            item => $"{item.name}\n{item.RoomId}\n{item.DisplayName}",
            SelectRest);
        _renameRow = new PS260714UIToolkitRenameRow(
            RenameSelected,
            CancelRename);
        _restList.HeaderExtras.Add(_renameRow);
        return _restList;
    }

    private VisualElement BuildInspector()
    {
        VisualElement panel = new();
        panel.style.width = InspectorWidth;
        panel.style.flexShrink = 0f;
        panel.style.borderLeftWidth = 1f;
        panel.style.borderLeftColor = new Color(0.2f, 0.22f, 0.24f);

        Label title = new("REST INSPECTOR");
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.paddingLeft = 8f;
        title.style.paddingTop = 8f;
        title.style.paddingBottom = 6f;
        panel.Add(title);

        _inspector = new IMGUIContainer(DrawInspector);
        _inspector.style.flexGrow = 1f;
        _inspector.style.paddingLeft = 10f;
        _inspector.style.paddingRight = 10f;
        _inspector.style.paddingBottom = 10f;
        panel.Add(_inspector);
        return panel;
    }

    private void DrawInspector()
    {
        float previousLabelWidth = EditorGUIUtility.labelWidth;
        float previousFieldWidth = EditorGUIUtility.fieldWidth;
        bool previousWideMode = EditorGUIUtility.wideMode;
        EditorGUIUtility.labelWidth = InspectorLabelWidth;
        EditorGUIUtility.fieldWidth = 220f;
        EditorGUIUtility.wideMode = true;
        _inspectorScroll = EditorGUILayout.BeginScrollView(
            _inspectorScroll,
            GUILayout.ExpandWidth(true),
            GUILayout.ExpandHeight(true));
        try
        {
            if (_selectedRest == null)
            {
                EditorGUILayout.HelpBox(
                    "Select a RestSO from the list.",
                    MessageType.Info);
                return;
            }

            SerializedObject serialized = new(_selectedRest);
            serialized.UpdateIfRequiredOrScript();
            EditorGUILayout.LabelField(
                "Localization",
                EditorStyles.boldLabel);
            PS260714LocalizationKeyField.Draw(
                serialized.FindProperty("titleLocalizationKey"),
                "Title Key",
                HandleInspectorChanged);
            PS260714LocalizationKeyField.Draw(
                serialized.FindProperty("descriptionLocalizationKey"),
                "Description Key",
                HandleInspectorChanged);

            SerializedProperty actions = serialized.FindProperty("actions");
            if (actions != null)
            {
                for (int index = 0; index < actions.arraySize; index++)
                {
                    SerializedProperty choice = actions
                        .GetArrayElementAtIndex(index)
                        .FindPropertyRelative("choice");
                    if (choice == null)
                        continue;
                    EditorGUILayout.Space(8f);
                    EditorGUILayout.LabelField(
                        $"Action {index + 1} Localization",
                        EditorStyles.miniBoldLabel);
                    PS260714LocalizationKeyField.Draw(
                        choice.FindPropertyRelative(
                            "titleLocalizationKey"),
                        "Label Key",
                        HandleInspectorChanged);
                    PS260714LocalizationKeyField.Draw(
                        choice.FindPropertyRelative(
                            "descriptionLocalizationKey"),
                        "Description Key",
                        HandleInspectorChanged);
                }
            }
            PS260714LocalizationKeyField.DrawLoadError();
            EditorGUILayout.Space(14f);
            DrawInspectorSeparator();
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField(
                "Rest Definition",
                EditorStyles.boldLabel);
            EditorGUILayout.Space(6f);

            EditorGUI.BeginChangeCheck();
            Undo.RecordObject(_selectedRest, "Edit Dungeon Rest");
            _propertyTree?.Draw(false);
            if (EditorGUI.EndChangeCheck())
                HandleInspectorChanged();
            EditorGUILayout.Space(16f);
        }
        finally
        {
            EditorGUILayout.EndScrollView();
            EditorGUIUtility.labelWidth = previousLabelWidth;
            EditorGUIUtility.fieldWidth = previousFieldWidth;
            EditorGUIUtility.wideMode = previousWideMode;
        }
    }

    private static void DrawInspectorSeparator()
    {
        Rect rect = EditorGUILayout.GetControlRect(false, 1f);
        EditorGUI.DrawRect(rect, new Color(0.28f, 0.3f, 0.31f, 1f));
    }

    private void DrawPreview()
    {
        Rect area = GUILayoutUtility.GetRect(
            320f,
            10000f,
            260f,
            10000f,
            GUILayout.ExpandWidth(true),
            GUILayout.ExpandHeight(true));
        EditorGUI.DrawRect(area, new Color(0.018f, 0.024f, 0.024f));
        if (_selectedRest == null)
        {
            GUI.Label(area, "Select a RestSO to preview.",
                EditorStyles.centeredGreyMiniLabel);
            return;
        }

        Rect canvas = FitAspectRect(
            new Rect(
                area.x + PreviewMargin,
                area.y + PreviewMargin,
                Mathf.Max(1f, area.width - PreviewMargin * 2f),
                Mathf.Max(1f, area.height - PreviewMargin * 2f)),
            PreviewAspect);
        EditorGUI.DrawRect(
            new Rect(
                canvas.x - 1f,
                canvas.y - 1f,
                canvas.width + 2f,
                canvas.height + 2f),
            new Color(0.22f, 0.26f, 0.25f, 1f));
        EditorGUI.DrawRect(canvas, Color.black);

        Sprite banner = _selectedRest.Banner;
        if (banner != null && banner.texture != null)
        {
            GUI.DrawTexture(
                canvas,
                banner.texture,
                ScaleMode.ScaleToFit,
                false);
        }

        Rect shade = new(
            canvas.x + canvas.width * 0.64f,
            canvas.y,
            canvas.width * 0.36f,
            canvas.height);
        EditorGUI.DrawRect(shade, new Color(0.025f, 0.045f, 0.04f, 0.82f));

        GUIStyle title = new(EditorStyles.boldLabel)
        {
            fontSize = 22,
            wordWrap = true,
            normal = { textColor = new Color(0.94f, 0.91f, 0.78f) },
        };
        GUIStyle body = new(EditorStyles.label)
        {
            fontSize = 13,
            wordWrap = true,
            normal = { textColor = Color.white },
        };
        float left = shade.x + 18f;
        float width = shade.width - 36f;
        GUI.Label(new Rect(left, canvas.y + 20f, width, 42f),
            _selectedRest.DisplayName, title);
        GUI.Label(new Rect(left, canvas.y + 66f, width, 86f),
            _selectedRest.Description, body);

        float y = canvas.y + 170f;
        for (int index = 0; index < _selectedRest.Actions.Count; index++)
        {
            DungeonRestActionDefinition action =
                _selectedRest.Actions[index];
            Rect choice = new(left, y, width, 58f);
            EditorGUI.DrawRect(choice, new Color(0.12f, 0.18f, 0.16f, 0.95f));
            GUI.Label(new Rect(
                    choice.x + 10f,
                    choice.y + 7f,
                    choice.width - 20f,
                    choice.height - 14f),
                action?.Choice?.Title ?? "INVALID ACTION",
                body);
            y += 66f;
        }

        GUI.Label(new Rect(
                canvas.x + 18f,
                canvas.yMax - 48f,
                canvas.width * 0.6f,
                28f),
            "PARTY SD PREVIEW · SELECT TARGET",
            body);
    }

    internal static Rect FitAspectRect(Rect bounds, float aspect)
    {
        if (bounds.width <= 0f || bounds.height <= 0f || aspect <= 0f)
            return bounds;

        float width = bounds.width;
        float height = width / aspect;
        if (height > bounds.height)
        {
            height = bounds.height;
            width = height * aspect;
        }

        return new Rect(
            bounds.x + (bounds.width - width) * 0.5f,
            bounds.y + (bounds.height - height) * 0.5f,
            width,
            height);
    }

    private void RefreshAssets(DungeonRestSO preferred)
    {
        if (_restList == null)
            return;

        string selectedPath = PS260714EditorAssetUtility.CapturePath(
            preferred != null ? preferred : _selectedRest);
        PS260714EditorAssetUtility.LoadAssets(_rests, "t:DungeonRestSO");
        DungeonRestSO next = PS260714EditorAssetUtility.RestoreSelection(
            selectedPath,
            _rests,
            preferred);
        _restList.SetItems(_rests, next);
        SelectRest(next);
    }

    private void SelectRest(DungeonRestSO rest)
    {
        if (_selectedRest != rest)
            CancelRename();
        DisposePropertyTree();
        _selectedRest = rest;
        _inspectorScroll = Vector2.zero;
        if (rest != null)
        {
            _propertyTree = PropertyTree.Create(
                rest,
                SerializationBackend.Unity);
            Selection.activeObject = rest;
            if (_rests.IndexOf(rest) >= 0)
                _restList?.SelectWithoutNotify(rest);
            _statusLabel.text = $"Selected {rest.name}.";
        }
        else if (_statusLabel != null)
        {
            _statusLabel.text = "Select or create a RestSO.";
        }

        _assetToolbar?.SetHasSelection(rest != null);
        _preview?.MarkDirtyRepaint();
        _inspector?.MarkDirtyRepaint();
    }

    private void DisposePropertyTree()
    {
        if (_propertyTree == null)
            return;
        _propertyTree.Dispose();
        _propertyTree = null;
    }

    private void HandleInspectorChanged()
    {
        if (_selectedRest == null)
            return;
        EditorUtility.SetDirty(_selectedRest);
        _preview?.MarkDirtyRepaint();
        Repaint();
    }

    private void CreateRest()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Create Dungeon Rest",
            "DungeonRest",
            "asset",
            "Choose where to save the RestSO.");
        if (string.IsNullOrWhiteSpace(path))
            return;

        DungeonRestSO rest = CreateInstance<DungeonRestSO>();
        rest.name = Path.GetFileNameWithoutExtension(path);
        SerializedObject serialized = new(rest);
        serialized.FindProperty("roomId").stringValue =
            CreateUniqueRoomId(
                rest.name.Replace(' ', '_').ToLowerInvariant());
        serialized.FindProperty("displayName").stringValue = rest.name;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(rest, path);
        AssetDatabase.SaveAssets();
        RefreshAssets(rest);
        EditorGUIUtility.PingObject(rest);
    }

    private void Save()
    {
        if (_selectedRest == null)
            return;
        PS260714EditorAssetUtility.Save(_selectedRest);
        _statusLabel.text = $"Saved {_selectedRest.name}.";
    }

    private void DuplicateSelected()
    {
        if (_selectedRest == null)
            return;
        Save();
        if (!PS260714EditorAssetUtility.TryDuplicate(
                _selectedRest,
                null,
                " Copy",
                out DungeonRestSO duplicate,
                out string error))
        {
            EditorUtility.DisplayDialog("Duplicate Rest", error, "OK");
            return;
        }

        SerializedObject serialized = new(duplicate);
        serialized.FindProperty("roomId").stringValue =
            CreateUniqueRoomId(_selectedRest.RoomId + "_copy");
        SerializedProperty display = serialized.FindProperty("displayName");
        if (display != null && !string.IsNullOrWhiteSpace(display.stringValue))
            display.stringValue += " Copy";
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(duplicate);
        AssetDatabase.SaveAssetIfDirty(duplicate);
        RefreshAssets(duplicate);
    }

    private void BeginRename()
    {
        if (_selectedRest != null)
            _renameRow?.Show(_selectedRest);
    }

    private void CancelRename()
    {
        _renameRow?.Hide();
    }

    private void RenameSelected()
    {
        if (_selectedRest == null)
            return;
        string requested = _renameRow?.Field.value;
        if (!PS260714EditorAssetUtility.TryRename(
                _selectedRest,
                requested,
                out string error))
        {
            EditorUtility.DisplayDialog("Rename Rest", error, "OK");
            return;
        }

        CancelRename();
        RefreshAssets(_selectedRest);
    }

    private void DeleteSelected()
    {
        if (_selectedRest == null)
            return;
        DungeonRestSO fallback =
            PS260714EditorAssetUtility.GetNeighborAfterDelete(
                _rests,
                _selectedRest);
        string deletedName = _selectedRest.name;
        DisposePropertyTree();
        if (!PS260714SafeAssetDelete.TryMoveToTrash(
                _selectedRest,
                "Dungeon Rest"))
        {
            SelectRest(_selectedRest);
            return;
        }

        _selectedRest = null;
        RefreshAssets(fallback);
        _statusLabel.text = $"Moved {deletedName} to the system trash.";
    }

    private void PingSelected()
    {
        if (_selectedRest == null)
            return;
        Selection.activeObject = _selectedRest;
        PS260714AssetEditorList.Ping(_selectedRest);
    }

    private void ValidateSelected()
    {
        if (_selectedRest == null)
            return;
        bool valid = _selectedRest.TryValidate(out string error);
        _statusLabel.text = valid ? "Rest data is valid." : error;
        _statusLabel.style.color = valid
            ? new Color(0.45f, 0.9f, 0.55f)
            : new Color(1f, 0.45f, 0.4f);
    }

    private void ApplyToClientScene()
    {
        if (_selectedRest == null)
            return;
        Save();
        if (!TryApplyToScene(_selectedRest, out string error))
        {
            _statusLabel.text = error;
            EditorUtility.DisplayDialog("Apply Rest UI", error, "OK");
            return;
        }

        _statusLabel.text =
            $"Applied {_selectedRest.name} preview to ClientScene.";
    }

    internal static bool TryApplyToScene(
        DungeonRestSO rest,
        out string error)
    {
        error = string.Empty;
        if (rest == null)
        {
            error = "A RestSO is required.";
            return false;
        }
        if (!File.Exists(ClientScenePath))
        {
            error = $"Scene not found: {ClientScenePath}";
            return false;
        }

        Scene previous = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(ClientScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened)
        {
            scene = EditorSceneManager.OpenScene(
                ClientScenePath,
                OpenSceneMode.Additive);
        }

        try
        {
            Transform panel = FindTransform(scene, "grpRestRoomPanel");
            if (panel == null)
            {
                error = "ClientScene requires grpRestRoomPanel.";
                return false;
            }

            UguiImage banner = panel.Find("imgRoomBanner")
                ?.GetComponent<UguiImage>();
            Transform content = panel.Find("grpRoomContent");
            TextMeshProUGUI title = content?.Find("txtRoomTitle")
                ?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI description = content
                ?.Find("txtRoomDescription")
                ?.GetComponent<TextMeshProUGUI>();
            Transform sdRoot = panel.Find("grpRestCharacterSds");
            Transform choiceRoot = content?.Find("grpRoomChoices");
            if (banner == null || title == null || description == null ||
                sdRoot == null || choiceRoot == null)
            {
                error = "Rest fixed UI is incomplete in ClientScene.";
                return false;
            }

            Undo.RecordObjects(
                new UnityEngine.Object[] { banner, title, description },
                "Apply Rest Preview To Scene");
            banner.sprite = rest.Banner;
            banner.color = rest.Banner != null
                ? Color.white
                : new Color(0.16f, 0.3f, 0.24f, 1f);
            title.text = rest.DisplayName;
            description.text = rest.Description;
            ApplySdPreview(sdRoot);
            ApplyChoicePreview(choiceRoot, rest);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                error = "Failed to save ClientScene.";
                return false;
            }
            return true;
        }
        finally
        {
            if (opened && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid() && previous.isLoaded)
                SceneManager.SetActiveScene(previous);
        }
    }

    private static void ApplySdPreview(Transform root)
    {
        UguiImage preview = root.GetComponentInChildren<UguiImage>(true);
        if (preview == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                RestSdPrefabPath);
            if (prefab != null)
            {
                GameObject instance = PrefabUtility.InstantiatePrefab(
                    prefab,
                    root) as GameObject;
                preview = instance != null
                    ? instance.GetComponent<UguiImage>()
                    : null;
            }
        }
        if (preview == null)
            return;

        CharacterSO[] characters = Resources.LoadAll<CharacterSO>(
            "Characters");
        Sprite sprite = null;
        for (int index = 0; index < characters.Length; index++)
        {
            if (characters[index] != null &&
                characters[index].SittingSdSprite != null)
            {
                sprite = characters[index].SittingSdSprite;
                break;
            }
        }
        preview.name = "imgRestCharacterSd_Preview";
        preview.sprite = sprite;
        preview.color = sprite != null
            ? Color.white
            : new Color(1f, 1f, 1f, 0.15f);
        preview.gameObject.SetActive(true);
        EditorUtility.SetDirty(preview);
    }

    private static void ApplyChoicePreview(
        Transform root,
        DungeonRestSO rest)
    {
        DungeonDynamicChoiceButtonView preview =
            root.GetComponentInChildren<DungeonDynamicChoiceButtonView>(true);
        if (preview == null)
            return;
        TextMeshProUGUI label = preview.GetComponentInChildren<
            TextMeshProUGUI>(true);
        if (label != null)
        {
            label.text = rest.Actions.Count > 0
                ? rest.Actions[0]?.Choice?.Title ?? "REST ACTION"
                : "REST ACTION";
            EditorUtility.SetDirty(label);
        }
        preview.name = "btnRoomChoice_Preview";
        preview.gameObject.SetActive(true);
        EditorUtility.SetDirty(preview);
    }

    private static Transform FindTransform(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = FindTransform(root.transform, objectName);
            if (found != null)
                return found;
        }
        return null;
    }

    private static Transform FindTransform(Transform root, string objectName)
    {
        if (root.name == objectName)
            return root;
        for (int index = 0; index < root.childCount; index++)
        {
            Transform found = FindTransform(
                root.GetChild(index),
                objectName);
            if (found != null)
                return found;
        }
        return null;
    }

    private string CreateUniqueRoomId(string baseId)
    {
        string root = string.IsNullOrWhiteSpace(baseId)
            ? "dungeon_rest"
            : baseId.Trim();
        string candidate = root;
        int suffix = 2;
        while (_rests.Exists(item => item != null && string.Equals(
                   item.RoomId,
                   candidate,
                   StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{root}_{suffix++}";
        }
        return candidate;
    }
}
