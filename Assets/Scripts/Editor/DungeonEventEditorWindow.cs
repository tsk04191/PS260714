using System;
using System.Collections.Generic;
using System.IO;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class DungeonEventEditorWindow : EditorWindow
{
    public const string MenuPath = PS260714EditorMenu.EventEditor;

    private const float InspectorWidth = 380f;

    private readonly List<DungeonEventSO> _events = new();
    private PS260714UIToolkitAssetToolbar _assetToolbar;
    private PS260714UIToolkitAssetList<DungeonEventSO> _eventList;
    private Label _statusLabel;
    private Label _selectionLabel;
    private VisualElement _centerHost;
    private IMGUIContainer _inspector;
    private PS260714UIToolkitRenameRow _renameRow;
    private DungeonEventGraphView _graph;
    private DungeonEventPreview _preview;
    private DungeonEventSO _selectedEvent;
    private DungeonEventChoiceNodeDefinition _selectedNode;
    private PropertyTree _eventTree;
    private PropertyTree _nodeTree;
    private Vector2 _inspectorScroll;
    private bool _showPreview;

    public static void Open(DungeonEventSO selected = null)
    {
        DungeonEventEditorWindow window = GetWindow<DungeonEventEditorWindow>();
        if (selected != null)
            window._selectedEvent = selected;
        window.titleContent = new GUIContent("Event Editor");
        window.minSize = new Vector2(1100f, 650f);
        window.Show();
        window.RefreshAssets(selected);
    }

    public void CreateGUI()
    {
        DisposePropertyTrees();
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

        _centerHost = new VisualElement();
        _centerHost.style.flexGrow = 1f;
        _centerHost.style.minWidth = 420f;
        _centerHost.style.position = Position.Relative;
        body.Add(_centerHost);

        _graph = new DungeonEventGraphView(this);
        _preview = new DungeonEventPreview();
        _centerHost.Add(_graph);
        _centerHost.Add(_preview);

        body.Add(BuildInspector());
        SetPreviewVisible(false);
        RefreshAssets(_selectedEvent);
    }

    private void OnDisable()
    {
        DisposePropertyTrees();
    }

    private void OnDestroy()
    {
        DisposePropertyTrees();
    }

    private void OnSelectionChange()
    {
        if (Selection.activeObject is DungeonEventSO dungeonEvent &&
            dungeonEvent != _selectedEvent)
        {
            SelectEvent(dungeonEvent);
        }
    }

    private void BuildToolbar()
    {
        _assetToolbar = new PS260714UIToolkitAssetToolbar(
            CreateEvent,
            Save,
            DuplicateSelected,
            BeginRename,
            DeleteSelected,
            PingSelected,
            () => RefreshAssets(_selectedEvent),
            "Select or create an EventSO.");
        _statusLabel = _assetToolbar.StatusLabel;
        rootVisualElement.Add(_assetToolbar);

        Toolbar graphToolbar = new();
        graphToolbar.Add(CreateToolbarButton("Add Choice", AddNode));
        graphToolbar.Add(CreateToolbarButton("Entry", ToggleSelectedEntry));
        graphToolbar.Add(CreateToolbarButton("Clear Links", ClearSelectedLinks));
        graphToolbar.Add(CreateToolbarButton("Delete Choice", DeleteSelectedNode));
        graphToolbar.Add(new ToolbarSpacer { flex = true });
        graphToolbar.Add(CreateToolbarButton("Frame All", () =>
            _graph?.FrameAll()));
        graphToolbar.Add(CreateToolbarButton("Validate", ValidateSelected));
        graphToolbar.Add(CreateToolbarButton("Graph / Preview", () =>
            SetPreviewVisible(!_showPreview)));
        rootVisualElement.Add(graphToolbar);
    }

    private VisualElement BuildAssetList()
    {
        _eventList = new PS260714UIToolkitAssetList<DungeonEventSO>(
            "EVENT ASSETS",
            PS260714AssetEditorList.Width,
            item => item.name,
            item => item.EventId,
            item => $"{item.name}\\n{item.EventId}\\n{item.DisplayName}",
            SelectEvent);
        _renameRow = new PS260714UIToolkitRenameRow(
            RenameSelected,
            CancelRename);
        _eventList.HeaderExtras.Add(_renameRow);
        return _eventList;
    }

    private VisualElement BuildInspector()
    {
        VisualElement panel = new();
        panel.style.width = InspectorWidth;
        panel.style.flexShrink = 0f;
        panel.style.borderLeftWidth = 1f;
        panel.style.borderLeftColor = new Color(0.2f, 0.22f, 0.24f);

        _selectionLabel = new Label("EVENT / NODE INSPECTOR");
        _selectionLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _selectionLabel.style.paddingLeft = 8f;
        _selectionLabel.style.paddingTop = 8f;
        _selectionLabel.style.paddingBottom = 6f;
        panel.Add(_selectionLabel);

        _inspector = new IMGUIContainer(DrawOdinInspector);
        _inspector.style.flexGrow = 1f;
        panel.Add(_inspector);
        return panel;
    }

    private static ToolbarButton CreateToolbarButton(
        string text,
        Action action)
    {
        ToolbarButton button = new(action) { text = text };
        return button;
    }

    private void DrawOdinInspector()
    {
        _inspectorScroll = EditorGUILayout.BeginScrollView(
            _inspectorScroll,
            GUILayout.ExpandWidth(true),
            GUILayout.ExpandHeight(true));
        try
        {
            DrawOdinInspectorContents();
        }
        finally
        {
            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawOdinInspectorContents()
    {
        if (_selectedEvent == null)
        {
            EditorGUILayout.HelpBox(
                "Select an EventSO from the list.",
                MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Event", EditorStyles.boldLabel);
        SerializedObject localization = new(_selectedEvent);
        localization.UpdateIfRequiredOrScript();
        DrawLocalizationHeader();
        PS260714LocalizationKeyField.Draw(
            localization.FindProperty("titleLocalizationKey"),
            "Title Key",
            HandleLocalizationChanged);
        PS260714LocalizationKeyField.Draw(
            localization.FindProperty("descriptionLocalizationKey"),
            "Description Key",
            HandleLocalizationChanged);
        PS260714LocalizationKeyField.DrawLoadError();
        EditorGUILayout.Space(5f);

        EditorGUI.BeginChangeCheck();
        Undo.RecordObject(_selectedEvent, "Edit Dungeon Event");
        _eventTree?.Draw(false);
        bool eventChanged = EditorGUI.EndChangeCheck();

        EditorGUILayout.Space(8f);
        if (_selectedNode == null)
        {
            EditorGUILayout.HelpBox(
                "Select a choice node in the graph to edit its label, " +
                "conditions and rewards.",
                MessageType.Info);
        }
        else
        {
            EditorGUILayout.LabelField(
                $"Choice Node · {_selectedNode.Title}",
                EditorStyles.boldLabel);
            SerializedProperty selectedNode = FindNodeProperty(
                localization,
                _selectedNode.NodeId,
                out _);
            if (selectedNode != null)
            {
                PS260714LocalizationKeyField.Draw(
                    selectedNode.FindPropertyRelative(
                        "titleLocalizationKey"),
                    "Choice Title Key",
                    HandleLocalizationChanged);
                PS260714LocalizationKeyField.Draw(
                    selectedNode.FindPropertyRelative(
                        "descriptionLocalizationKey"),
                    "Choice Description Key",
                    HandleLocalizationChanged);
                PS260714LocalizationKeyField.Draw(
                    selectedNode.FindPropertyRelative(
                        "resultDescriptionLocalizationKey"),
                    "Result Description Key",
                    HandleLocalizationChanged);
                EditorGUILayout.Space(5f);
            }
            EditorGUI.BeginChangeCheck();
            Undo.RecordObject(_selectedEvent, "Edit Event Choice Node");
            _nodeTree?.Draw(false);
            eventChanged |= EditorGUI.EndChangeCheck();
        }

        if (!eventChanged)
            return;

        EditorUtility.SetDirty(_selectedEvent);
        _graph?.RefreshGraph(false);
        _preview?.Bind(_selectedEvent);
    }

    private void DrawLocalizationHeader()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(
                "Localization",
                EditorStyles.boldLabel);
            if (GUILayout.Button(
                    "Refresh Keys",
                    EditorStyles.miniButton,
                    GUILayout.Width(88f)))
            {
                PS260714LocalizationKeyField.Refresh();
            }
        }
    }

    private void HandleLocalizationChanged()
    {
        EditorApplication.delayCall += () =>
        {
            if (this == null || _selectedEvent == null)
                return;
            RebuildSelectedTrees();
            Repaint();
        };
    }

    private void RefreshAssets(DungeonEventSO preferred)
    {
        if (_eventList == null)
            return;

        string selectedPath = PS260714EditorAssetUtility.CapturePath(
            preferred != null ? preferred : _selectedEvent);
        PS260714EditorAssetUtility.LoadAssets(
            _events,
            "t:DungeonEventSO");
        DungeonEventSO next =
            PS260714EditorAssetUtility.RestoreSelection(
                selectedPath,
                _events,
                preferred);
        _eventList.SetItems(_events, next);
        SelectEvent(next);
    }

    private void SelectEvent(DungeonEventSO dungeonEvent)
    {
        if (_selectedEvent != dungeonEvent)
            CancelRename();
        if (dungeonEvent == null)
        {
            DisposePropertyTrees();
            _selectedEvent = null;
            _selectedNode = null;
            _graph?.Bind(null);
            _preview?.Bind(null);
            _statusLabel.text = "Select or create an EventSO.";
            _assetToolbar?.SetHasSelection(false);
            return;
        }

        DisposePropertyTrees();
        _selectedEvent = dungeonEvent;
        EnsureGraphData(dungeonEvent);
        _eventTree = PropertyTree.Create(
            dungeonEvent,
            SerializationBackend.Unity);
        Selection.activeObject = dungeonEvent;
        if (_events.IndexOf(dungeonEvent) >= 0)
            _eventList?.SelectWithoutNotify(dungeonEvent);
        _assetToolbar?.SetHasSelection(true);

        _graph?.Bind(dungeonEvent);
        _preview?.Bind(dungeonEvent);
        DungeonEventChoiceNodeDefinition nextNode =
            dungeonEvent.Choices.Count > 0
                ? dungeonEvent.Choices[0]
                : null;
        SelectNode(nextNode);
        ValidateSelected(false);
    }

    internal void SelectNode(DungeonEventChoiceNodeDefinition node)
    {
        if (node != null && ReferenceEquals(_selectedNode, node) &&
            _nodeTree != null)
        {
            if (_selectionLabel != null)
                _selectionLabel.text = $"NODE · {node.Title}";
            _graph?.SetSelectedNode(node != null ? node.NodeId : null);
            return;
        }

        if (!ReferenceEquals(_selectedNode, node))
            _inspectorScroll = Vector2.zero;
        DisposeNodeTree();
        _selectedNode = node;
        _nodeTree = node != null
            ? PropertyTree.Create(node)
            : null;
        if (_selectionLabel != null)
        {
            _selectionLabel.text = node != null
                ? $"NODE · {node.Title}"
                : "EVENT / NODE INSPECTOR";
        }
        _inspector?.MarkDirtyRepaint();
        _graph?.SetSelectedNode(node != null ? node.NodeId : null);
    }

    private void DisposePropertyTrees()
    {
        DisposeNodeTree();
        if (_eventTree == null)
            return;

        _eventTree.Dispose();
        _eventTree = null;
    }

    private void DisposeNodeTree()
    {
        if (_nodeTree == null)
            return;

        _nodeTree.Dispose();
        _nodeTree = null;
    }

    private void CreateEvent()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Create Dungeon Event",
            "DungeonEvent",
            "asset",
            "Choose where to save the EventSO.");
        if (string.IsNullOrWhiteSpace(path))
            return;

        DungeonEventSO dungeonEvent = CreateInstance<DungeonEventSO>();
        dungeonEvent.name = Path.GetFileNameWithoutExtension(path);
        SerializedObject serialized = new(dungeonEvent);
        serialized.FindProperty("roomId").stringValue =
            CreateUniqueEventId(
                dungeonEvent.name.Replace(' ', '_').ToLowerInvariant());
        serialized.FindProperty("displayName").stringValue =
            dungeonEvent.name;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(dungeonEvent, path);
        AssetDatabase.SaveAssets();
        RefreshAssets(dungeonEvent);
        EditorGUIUtility.PingObject(dungeonEvent);
    }

    private void Save()
    {
        if (_selectedEvent == null)
            return;
        PS260714EditorAssetUtility.Save(_selectedEvent);
        _statusLabel.text = $"Saved {_selectedEvent.name}.";
    }

    private void DuplicateSelected()
    {
        if (_selectedEvent == null)
            return;

        Save();
        if (!PS260714EditorAssetUtility.TryDuplicate(
                _selectedEvent,
                null,
                " Copy",
                out DungeonEventSO duplicate,
                out string duplicateError))
        {
            EditorUtility.DisplayDialog(
                "Duplicate Event",
                duplicateError,
                "OK");
            return;
        }

        if (duplicate != null)
        {
            SerializedObject serialized = new(duplicate);
            serialized.FindProperty("roomId").stringValue =
                CreateUniqueEventId(_selectedEvent.EventId + "_copy");
            SerializedProperty displayName =
                serialized.FindProperty("displayName");
            if (displayName != null &&
                !string.IsNullOrWhiteSpace(displayName.stringValue))
            {
                displayName.stringValue += " Copy";
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(duplicate);
            AssetDatabase.SaveAssetIfDirty(duplicate);
        }

        RefreshAssets(duplicate);
        _statusLabel.text = duplicate != null
            ? $"Duplicated {_selectedEvent.name}."
            : "Event duplication failed.";
    }

    private void BeginRename()
    {
        if (_selectedEvent == null || _renameRow == null)
            return;
        _renameRow.Show(_selectedEvent);
    }

    private void CancelRename()
    {
        _renameRow?.Hide();
    }

    private void RenameSelected()
    {
        if (_selectedEvent == null)
        {
            CancelRename();
            return;
        }

        string requested = _renameRow?.Field.value;
        if (!PS260714EditorAssetUtility.TryRename(
                _selectedEvent,
                requested,
                out string error))
        {
            EditorUtility.DisplayDialog("Rename Event", error, "OK");
            _renameRow?.Field.Focus();
            return;
        }

        CancelRename();
        RefreshAssets(_selectedEvent);
        _statusLabel.text =
            $"Renamed asset to {(requested ?? string.Empty).Trim()}.";
    }

    private void DeleteSelected()
    {
        if (_selectedEvent == null)
            return;

        string deletedName = _selectedEvent.name;
        DungeonEventSO fallback =
            PS260714EditorAssetUtility.GetNeighborAfterDelete(
                _events,
                _selectedEvent);
        DisposePropertyTrees();
        if (!DungeonEventAssetDelete.TryMoveToTrash(
                _selectedEvent,
                true,
                out string error))
        {
            RebuildSelectedTrees();
            if (!string.IsNullOrWhiteSpace(error))
            {
                _statusLabel.text = error;
                EditorUtility.DisplayDialog(
                    "Delete Dungeon Event",
                    error,
                    "OK");
            }
            return;
        }

        CancelRename();
        _selectedEvent = null;
        _selectedNode = null;
        RefreshAssets(fallback);
        _statusLabel.text = $"Moved {deletedName} to the system trash.";
    }

    private void PingSelected()
    {
        if (_selectedEvent == null)
            return;
        Selection.activeObject = _selectedEvent;
        PS260714AssetEditorList.Ping(_selectedEvent);
    }

    private string CreateUniqueEventId(string baseId)
    {
        string root = string.IsNullOrWhiteSpace(baseId)
            ? "dungeon_event"
            : baseId.Trim();
        string candidate = root;
        int suffix = 2;
        while (_events.Exists(item => item != null && string.Equals(
                   item.EventId,
                   candidate,
                   StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{root}_{suffix++}";
        }
        return candidate;
    }

    private void ValidateSelected()
    {
        ValidateSelected(true);
    }

    private void ValidateSelected(bool showDialog)
    {
        if (_selectedEvent == null)
            return;

        bool valid = _selectedEvent.TryValidate(out string error);
        _statusLabel.text = valid ? "Graph is valid." : error;
        _statusLabel.style.color = valid
            ? new Color(0.45f, 0.9f, 0.55f)
            : new Color(1f, 0.45f, 0.4f);
        if (showDialog)
        {
            EditorUtility.DisplayDialog(
                valid ? "Event Graph Valid" : "Event Graph Error",
                valid ? "No validation errors were found." : error,
                "OK");
        }
    }

    private void SetPreviewVisible(bool visible)
    {
        _showPreview = visible;
        if (_graph != null)
            _graph.style.display = visible ? DisplayStyle.None : DisplayStyle.Flex;
        if (_preview != null)
        {
            _preview.style.display = visible
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            if (visible)
                _preview.Bind(_selectedEvent);
        }
    }

    private void AddNode()
    {
        if (_selectedEvent == null)
            return;

        Vector2 graphPosition = _graph != null
            ? _graph.GetSuggestedNodePosition()
            : new Vector2(100f, 100f);
        DisposePropertyTrees();
        SerializedObject serialized = new(_selectedEvent);
        SerializedProperty choices = serialized.FindProperty("choices");
        Undo.RecordObject(_selectedEvent, "Add Event Choice Node");
        int index = choices.arraySize;
        choices.InsertArrayElementAtIndex(index);
        SerializedProperty node = choices.GetArrayElementAtIndex(index);
        ResetNodeProperty(node, index, graphPosition);
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(_selectedEvent);

        EnsureGraphData(_selectedEvent);
        _selectedNode = _selectedEvent.Choices[index];
        RebuildSelectedTrees();
    }

    private void ToggleSelectedEntry()
    {
        if (_selectedEvent == null || _selectedNode == null)
            return;

        SerializedObject serialized = new(_selectedEvent);
        SerializedProperty entries =
            serialized.FindProperty("entryChoiceNodeIds");
        int index = FindString(entries, _selectedNode.NodeId);
        if (index >= 0)
        {
            if (entries.arraySize == 1)
            {
                _statusLabel.text =
                    "An event graph must keep at least one entry choice.";
                return;
            }
        }

        DisposePropertyTrees();
        Undo.RecordObject(_selectedEvent, "Toggle Event Entry Choice");
        if (index >= 0)
        {
            entries.DeleteArrayElementAtIndex(index);
        }
        else
        {
            int next = entries.arraySize;
            entries.InsertArrayElementAtIndex(next);
            entries.GetArrayElementAtIndex(next).stringValue =
                _selectedNode.NodeId;
        }
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(_selectedEvent);
        RebuildSelectedTrees();
    }

    private void ClearSelectedLinks()
    {
        if (_selectedEvent == null || _selectedNode == null)
            return;

        SerializedObject serialized = new(_selectedEvent);
        SerializedProperty node = FindNodeProperty(
            serialized,
            _selectedNode.NodeId,
            out _);
        if (node == null)
            return;

        DisposePropertyTrees();
        Undo.RecordObject(_selectedEvent, "Clear Event Choice Links");
        node.FindPropertyRelative("nextChoiceNodeIds").arraySize = 0;
        node.FindPropertyRelative("endsEvent").boolValue = true;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(_selectedEvent);
        RebuildSelectedTrees();
    }

    private void DeleteSelectedNode()
    {
        if (_selectedEvent == null || _selectedNode == null)
            return;

        string nodeId = _selectedNode.NodeId;
        SerializedObject serialized = new(_selectedEvent);
        SerializedProperty choices = serialized.FindProperty("choices");
        SerializedProperty node = FindNodeProperty(
            serialized,
            nodeId,
            out int removeIndex);
        if (node == null || removeIndex < 0)
            return;

        DisposePropertyTrees();
        Undo.RecordObject(_selectedEvent, "Delete Event Choice Node");
        choices.DeleteArrayElementAtIndex(removeIndex);
        RemoveString(
            serialized.FindProperty("entryChoiceNodeIds"),
            nodeId);
        for (int index = 0; index < choices.arraySize; index++)
        {
            SerializedProperty candidate = choices.GetArrayElementAtIndex(index);
            SerializedProperty nextIds =
                candidate.FindPropertyRelative("nextChoiceNodeIds");
            RemoveString(nextIds, nodeId);
            if (nextIds.arraySize == 0)
                candidate.FindPropertyRelative("endsEvent").boolValue = true;
        }
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(_selectedEvent);
        _selectedNode = _selectedEvent.Choices.Count > 0
            ? _selectedEvent.Choices[Mathf.Min(
                removeIndex,
                _selectedEvent.Choices.Count - 1)]
            : null;
        RebuildSelectedTrees();
    }

    internal void SetNodePosition(string nodeId, Vector2 position)
    {
        if (_selectedEvent == null || !IsFinite(position))
            return;

        SerializedObject serialized = new(_selectedEvent);
        SerializedProperty node = FindNodeProperty(serialized, nodeId, out _);
        if (node == null)
            return;
        Undo.RecordObject(_selectedEvent, "Move Event Choice Node");
        node.FindPropertyRelative("editorPosition").vector2Value = position;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(_selectedEvent);
    }

    internal void AddConnection(string sourceNodeId, string targetNodeId)
    {
        if (_selectedEvent == null || sourceNodeId == targetNodeId)
            return;

        SerializedObject serialized = new(_selectedEvent);
        SerializedProperty source = FindNodeProperty(
            serialized,
            sourceNodeId,
            out _);
        if (source == null || FindNodeProperty(
                serialized,
                targetNodeId,
                out _) == null)
        {
            return;
        }

        SerializedProperty nextIds =
            source.FindPropertyRelative("nextChoiceNodeIds");
        if (FindString(nextIds, targetNodeId) >= 0)
            return;

        DisposePropertyTrees();
        Undo.RecordObject(_selectedEvent, "Connect Event Choice Nodes");
        int index = nextIds.arraySize;
        nextIds.InsertArrayElementAtIndex(index);
        nextIds.GetArrayElementAtIndex(index).stringValue = targetNodeId;
        source.FindPropertyRelative("endsEvent").boolValue = false;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(_selectedEvent);
        RebuildSelectedTrees();
    }

    private void RebuildSelectedTrees()
    {
        string selectedId = _selectedNode != null
            ? _selectedNode.NodeId
            : null;
        DisposePropertyTrees();
        _eventTree = PropertyTree.Create(
            _selectedEvent,
            SerializationBackend.Unity);
        _selectedEvent.TryGetChoiceNode(selectedId, out var selected);
        _selectedNode = null;
        SelectNode(selected);
        _graph.RefreshGraph(false);
        _preview.Bind(_selectedEvent);
        _inspector.MarkDirtyRepaint();
        ValidateSelected(false);
    }

    internal static void EnsureGraphData(DungeonEventSO dungeonEvent)
    {
        SerializedObject serialized = new(dungeonEvent);
        serialized.UpdateIfRequiredOrScript();
        SerializedProperty choices = serialized.FindProperty("choices");
        SerializedProperty entries =
            serialized.FindProperty("entryChoiceNodeIds");
        Undo.RecordObject(dungeonEvent, "Initialize Event Choice Graph");

        if (choices.arraySize == 0)
        {
            choices.InsertArrayElementAtIndex(0);
            ResetNodeProperty(
                choices.GetArrayElementAtIndex(0),
                0,
                new Vector2(90f, 90f));
        }

        HashSet<string> ids = new(StringComparer.Ordinal);
        bool positionsMissing = true;
        for (int index = 0; index < choices.arraySize; index++)
        {
            SerializedProperty node = choices.GetArrayElementAtIndex(index);
            SerializedProperty nodeId = node.FindPropertyRelative("nodeId");
            if (string.IsNullOrWhiteSpace(nodeId.stringValue) ||
                !ids.Add(nodeId.stringValue))
            {
                nodeId.stringValue = Guid.NewGuid().ToString("N");
                ids.Add(nodeId.stringValue);
            }

            SerializedProperty choiceId =
                node.FindPropertyRelative("choiceId");
            if (string.IsNullOrWhiteSpace(choiceId.stringValue))
                choiceId.stringValue = $"choice_{index + 1}";
            SerializedProperty fallbackTitle =
                node.FindPropertyRelative("fallbackTitle");
            if (string.IsNullOrWhiteSpace(fallbackTitle.stringValue))
                fallbackTitle.stringValue = $"CHOICE {index + 1}";

            SerializedProperty position =
                node.FindPropertyRelative("editorPosition");
            Vector2 positionValue = position.vector2Value;
            if (!IsFinite(positionValue))
            {
                positionValue = GetDefaultNodePosition(index);
                position.vector2Value = positionValue;
            }
            if (positionValue != Vector2.zero)
                positionsMissing = false;

            SerializedProperty next =
                node.FindPropertyRelative("nextChoiceNodeIds");
            if (next.arraySize == 0)
                node.FindPropertyRelative("endsEvent").boolValue = true;
        }

        if (positionsMissing)
        {
            for (int index = 0; index < choices.arraySize; index++)
            {
                choices.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("editorPosition").vector2Value =
                    GetDefaultNodePosition(index);
            }
        }

        if (entries.arraySize == 0)
        {
            for (int index = 0; index < choices.arraySize; index++)
            {
                entries.InsertArrayElementAtIndex(index);
                entries.GetArrayElementAtIndex(index).stringValue =
                    choices.GetArrayElementAtIndex(index)
                        .FindPropertyRelative("nodeId").stringValue;
            }
        }

        serialized.FindProperty("choiceGraphVersion").intValue =
            DungeonEventSO.CurrentChoiceGraphVersion;
        if (serialized.ApplyModifiedProperties())
            EditorUtility.SetDirty(dungeonEvent);
    }

    private static Vector2 GetDefaultNodePosition(int index)
    {
        int safeIndex = Mathf.Max(0, index);
        return new Vector2(
            90f + (safeIndex % 3) * 300f,
            90f + (safeIndex / 3) * 205f);
    }

    private static bool IsFinite(Vector2 value)
    {
        return !float.IsNaN(value.x) &&
               !float.IsInfinity(value.x) &&
               !float.IsNaN(value.y) &&
               !float.IsInfinity(value.y);
    }

    private static void ResetNodeProperty(
        SerializedProperty node,
        int index,
        Vector2 position)
    {
        node.FindPropertyRelative("nodeId").stringValue =
            Guid.NewGuid().ToString("N");
        node.FindPropertyRelative("choiceId").stringValue =
            $"choice_{index + 1}";
        node.FindPropertyRelative("titleLocalizationKey").stringValue =
            string.Empty;
        node.FindPropertyRelative("fallbackTitle").stringValue =
            $"CHOICE {index + 1}";
        node.FindPropertyRelative("descriptionLocalizationKey").stringValue =
            string.Empty;
        node.FindPropertyRelative("fallbackDescription").stringValue =
            string.Empty;
        node.FindPropertyRelative("runCurrencyCost").intValue = 0;
        node.FindPropertyRelative("conditions").arraySize = 0;
        node.FindPropertyRelative("singlePurchase").boolValue = true;
        node.FindPropertyRelative("effects").arraySize = 0;
        node.FindPropertyRelative("resultDescriptionLocalizationKey")
            .stringValue = string.Empty;
        node.FindPropertyRelative("fallbackResultDescription").stringValue =
            string.Empty;
        node.FindPropertyRelative("endsEvent").boolValue = true;
        node.FindPropertyRelative("nextChoiceNodeIds").arraySize = 0;
        node.FindPropertyRelative("editorPosition").vector2Value = position;
    }

    private static SerializedProperty FindNodeProperty(
        SerializedObject serialized,
        string nodeId,
        out int nodeIndex)
    {
        SerializedProperty choices = serialized.FindProperty("choices");
        for (int index = 0; index < choices.arraySize; index++)
        {
            SerializedProperty node = choices.GetArrayElementAtIndex(index);
            string candidate = node.FindPropertyRelative("nodeId").stringValue;
            if (string.IsNullOrWhiteSpace(candidate))
            {
                candidate = node.FindPropertyRelative("choiceId").stringValue;
            }
            if (!string.Equals(candidate, nodeId, StringComparison.Ordinal))
                continue;

            nodeIndex = index;
            return node;
        }

        nodeIndex = -1;
        return null;
    }

    private static int FindString(SerializedProperty array, string value)
    {
        for (int index = 0; index < array.arraySize; index++)
        {
            if (string.Equals(
                    array.GetArrayElementAtIndex(index).stringValue,
                    value,
                    StringComparison.Ordinal))
            {
                return index;
            }
        }
        return -1;
    }

    private static void RemoveString(SerializedProperty array, string value)
    {
        for (int index = array.arraySize - 1; index >= 0; index--)
        {
            if (string.Equals(
                    array.GetArrayElementAtIndex(index).stringValue,
                    value,
                    StringComparison.Ordinal))
            {
                array.DeleteArrayElementAtIndex(index);
            }
        }
    }

    private sealed class DungeonEventGraphView : VisualElement
    {
        private const float NodeWidth = 240f;
        private const float NodeHeight = 154f;
        private const float MinimumZoom = 0.35f;
        private const float MaximumZoom = 1.6f;

        private readonly DungeonEventEditorWindow _owner;
        private readonly Dictionary<string, EventNodeElement> _elements = new();
        private readonly Dictionary<string, Vector2> _positions = new();
        private DungeonEventSO _event;
        private Vector2 _pan = new(40f, 40f);
        private float _zoom = 1f;
        private string _selectedNodeId;
        private string _dragNodeId;
        private Vector2 _dragStartPointer;
        private Vector2 _dragStartPosition;
        private bool _panning;
        private Vector2 _panStartPointer;
        private Vector2 _panStart;
        private string _connectionSourceId;
        private Vector2 _connectionEnd;

        internal DungeonEventGraphView(DungeonEventEditorWindow owner)
        {
            _owner = owner;
            style.flexGrow = 1f;
            style.position = Position.Absolute;
            style.left = 0f;
            style.right = 0f;
            style.top = 0f;
            style.bottom = 0f;
            style.overflow = Overflow.Hidden;
            focusable = true;
            generateVisualContent += DrawCanvas;
            RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        internal void Bind(DungeonEventSO dungeonEvent)
        {
            _event = dungeonEvent;
            _selectedNodeId = null;
            RefreshGraph(true);
        }

        internal void RefreshGraph(bool frameAll)
        {
            Clear();
            _elements.Clear();
            _positions.Clear();
            if (_event == null)
            {
                MarkDirtyRepaint();
                return;
            }

            for (int index = 0; index < _event.Choices.Count; index++)
            {
                DungeonEventChoiceNodeDefinition node = _event.Choices[index];
                if (node == null)
                    continue;
                _positions[node.NodeId] = IsFinite(node.EditorPosition)
                    ? node.EditorPosition
                    : GetDefaultNodePosition(index);
                EventNodeElement element = new(this, node);
                element.SetSelected(node.NodeId == _selectedNodeId);
                _elements[node.NodeId] = element;
                Add(element);
            }
            UpdateNodeLayouts();
            if (frameAll)
                schedule.Execute(FrameAll);
            MarkDirtyRepaint();
        }

        internal void SetSelectedNode(string nodeId)
        {
            _selectedNodeId = nodeId;
            foreach (var pair in _elements)
                pair.Value.SetSelected(pair.Key == nodeId);
            MarkDirtyRepaint();
        }

        internal Vector2 GetSuggestedNodePosition()
        {
            float width = resolvedStyle.width;
            float height = resolvedStyle.height;
            if (float.IsNaN(width) || float.IsInfinity(width) ||
                float.IsNaN(height) || float.IsInfinity(height) ||
                width <= 1f || height <= 1f || !IsFinite(_pan) ||
                float.IsNaN(_zoom) || float.IsInfinity(_zoom))
            {
                return GetDefaultNodePosition(_positions.Count);
            }

            Vector2 center = new(
                width * 0.5f,
                height * 0.5f);
            Vector2 position =
                (center - _pan) / Mathf.Max(MinimumZoom, _zoom);
            return IsFinite(position)
                ? position
                : GetDefaultNodePosition(_positions.Count);
        }

        internal void FrameAll()
        {
            if (_positions.Count == 0 || resolvedStyle.width <= 1f ||
                resolvedStyle.height <= 1f)
            {
                return;
            }

            Vector2 min = new(float.MaxValue, float.MaxValue);
            Vector2 max = new(float.MinValue, float.MinValue);
            foreach (Vector2 position in _positions.Values)
            {
                min = Vector2.Min(min, position);
                max = Vector2.Max(max, position +
                    new Vector2(NodeWidth, NodeHeight));
            }

            Vector2 graphSize = max - min;
            float availableWidth = Mathf.Max(1f, resolvedStyle.width - 100f);
            float availableHeight = Mathf.Max(1f, resolvedStyle.height - 100f);
            _zoom = Mathf.Clamp(
                Mathf.Min(
                    availableWidth / Mathf.Max(NodeWidth, graphSize.x),
                    availableHeight / Mathf.Max(NodeHeight, graphSize.y)),
                MinimumZoom,
                MaximumZoom);
            Vector2 graphCenter = (min + max) * 0.5f;
            Vector2 viewCenter = new(
                resolvedStyle.width * 0.5f,
                resolvedStyle.height * 0.5f);
            _pan = viewCenter - graphCenter * _zoom;
            UpdateNodeLayouts();
        }

        private void OnWheel(WheelEvent evt)
        {
            Vector2 pointer = evt.localMousePosition;
            Vector2 graphPoint = (pointer - _pan) / _zoom;
            float factor = Mathf.Pow(1.1f, -evt.delta.y);
            _zoom = Mathf.Clamp(
                _zoom * factor,
                MinimumZoom,
                MaximumZoom);
            _pan = pointer - graphPoint * _zoom;
            UpdateNodeLayouts();
            evt.StopPropagation();
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 2 && !(evt.button == 0 && evt.altKey))
                return;
            _panning = true;
            _panStartPointer = ToLocal(evt.position);
            _panStart = _pan;
            PointerCaptureHelper.CapturePointer(this, evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (_panning)
            {
                Vector2 pointer = ToLocal(evt.position);
                _pan = _panStart + pointer - _panStartPointer;
                UpdateNodeLayouts();
                return;
            }

            if (!string.IsNullOrEmpty(_dragNodeId))
            {
                _positions[_dragNodeId] = _dragStartPosition +
                    (ToLocal(evt.position) - _dragStartPointer) / _zoom;
                UpdateNodeLayouts();
                return;
            }

            if (!string.IsNullOrEmpty(_connectionSourceId))
            {
                _connectionEnd = ToLocal(evt.position);
                MarkDirtyRepaint();
            }
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (_panning)
            {
                _panning = false;
                PointerCaptureHelper.ReleasePointer(this, evt.pointerId);
                return;
            }

            if (!string.IsNullOrEmpty(_dragNodeId))
            {
                string nodeId = _dragNodeId;
                _dragNodeId = null;
                PointerCaptureHelper.ReleasePointer(this, evt.pointerId);
                if (_positions.TryGetValue(nodeId, out Vector2 position))
                    _owner.SetNodePosition(nodeId, position);
                return;
            }

            if (string.IsNullOrEmpty(_connectionSourceId))
                return;

            string sourceId = _connectionSourceId;
            _connectionSourceId = null;
            PointerCaptureHelper.ReleasePointer(this, evt.pointerId);
            string targetId = FindInputNodeAt(evt.position);
            if (!string.IsNullOrEmpty(targetId))
                _owner.AddConnection(sourceId, targetId);
            MarkDirtyRepaint();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.F)
            {
                FrameAll();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.Delete &&
                     !string.IsNullOrEmpty(_selectedNodeId))
            {
                _owner.DeleteSelectedNode();
                evt.StopPropagation();
            }
        }

        internal void BeginNodeDrag(
            DungeonEventChoiceNodeDefinition node,
            PointerDownEvent evt)
        {
            Focus();
            _dragNodeId = node.NodeId;
            _dragStartPointer = ToLocal(evt.position);
            _dragStartPosition = _positions[node.NodeId];
            PointerCaptureHelper.CapturePointer(this, evt.pointerId);
            _owner.SelectNode(node);
        }

        internal void BeginConnection(
            DungeonEventChoiceNodeDefinition node,
            PointerDownEvent evt)
        {
            Focus();
            _connectionSourceId = node.NodeId;
            _connectionEnd = ToLocal(evt.position);
            PointerCaptureHelper.CapturePointer(this, evt.pointerId);
            _owner.SelectNode(node);
            MarkDirtyRepaint();
        }

        private string FindInputNodeAt(Vector2 panelPosition)
        {
            foreach (var pair in _elements)
            {
                if (pair.Value.InputPort.worldBound.Contains(panelPosition))
                    return pair.Key;
            }
            return null;
        }

        private void UpdateNodeLayouts()
        {
            foreach (var pair in _elements)
            {
                if (!_positions.TryGetValue(pair.Key, out Vector2 position))
                    continue;
                EventNodeElement element = pair.Value;
                element.style.left = _pan.x + position.x * _zoom;
                element.style.top = _pan.y + position.y * _zoom;
                element.style.scale = new Scale(Vector3.one * _zoom);
            }
            MarkDirtyRepaint();
        }

        private Vector2 ToLocal(Vector3 panelPosition)
        {
            return VisualElementExtensions.WorldToLocal(
                this,
                new Vector2(panelPosition.x, panelPosition.y));
        }

        private Vector2 GetOutputPosition(string nodeId)
        {
            Vector2 position = _positions[nodeId];
            return _pan + (position +
                new Vector2(NodeWidth, NodeHeight * 0.5f)) * _zoom;
        }

        private Vector2 GetInputPosition(string nodeId)
        {
            Vector2 position = _positions[nodeId];
            return _pan + (position +
                new Vector2(0f, NodeHeight * 0.5f)) * _zoom;
        }

        private void DrawCanvas(MeshGenerationContext context)
        {
            Painter2D painter = context.painter2D;
            DrawGrid(painter);
            if (_event == null)
                return;

            painter.lineWidth = Mathf.Max(1.5f, 2.5f * _zoom);
            foreach (DungeonEventChoiceNodeDefinition node in _event.Choices)
            {
                if (node == null || !_positions.ContainsKey(node.NodeId))
                    continue;
                foreach (string nextId in node.NextChoiceNodeIds)
                {
                    if (!_positions.ContainsKey(nextId))
                        continue;
                    DrawEdge(
                        painter,
                        GetOutputPosition(node.NodeId),
                        GetInputPosition(nextId),
                        node.NodeId == _selectedNodeId
                            ? new Color(0.98f, 0.74f, 0.25f)
                            : new Color(0.25f, 0.72f, 0.68f));
                }
            }

            if (!string.IsNullOrEmpty(_connectionSourceId) &&
                _positions.ContainsKey(_connectionSourceId))
            {
                DrawEdge(
                    painter,
                    GetOutputPosition(_connectionSourceId),
                    _connectionEnd,
                    new Color(1f, 0.8f, 0.3f));
            }
        }

        private void DrawGrid(Painter2D painter)
        {
            float step = Mathf.Max(14f, 32f * _zoom);
            Color grid = new(0.16f, 0.18f, 0.19f, 0.8f);
            painter.strokeColor = grid;
            painter.lineWidth = 1f;
            float startX = Mathf.Repeat(_pan.x, step);
            for (float x = startX; x < resolvedStyle.width; x += step)
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, 0f));
                painter.LineTo(new Vector2(x, resolvedStyle.height));
                painter.Stroke();
            }
            float startY = Mathf.Repeat(_pan.y, step);
            for (float y = startY; y < resolvedStyle.height; y += step)
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(0f, y));
                painter.LineTo(new Vector2(resolvedStyle.width, y));
                painter.Stroke();
            }
        }

        private static void DrawEdge(
            Painter2D painter,
            Vector2 start,
            Vector2 end,
            Color color)
        {
            float tangent = Mathf.Max(55f, Mathf.Abs(end.x - start.x) * 0.45f);
            painter.strokeColor = color;
            painter.BeginPath();
            painter.MoveTo(start);
            painter.BezierCurveTo(
                start + Vector2.right * tangent,
                end + Vector2.left * tangent,
                end);
            painter.Stroke();
        }

        private sealed class EventNodeElement : VisualElement
        {
            internal VisualElement InputPort { get; }

            internal EventNodeElement(
                DungeonEventGraphView graph,
                DungeonEventChoiceNodeDefinition node)
            {
                style.position = Position.Absolute;
                style.width = NodeWidth;
                style.height = NodeHeight;
                style.backgroundColor = new Color(0.1f, 0.14f, 0.14f, 0.98f);
                style.borderLeftWidth = 2f;
                style.borderRightWidth = 2f;
                style.borderTopWidth = 2f;
                style.borderBottomWidth = 2f;
                style.borderTopLeftRadius = 5f;
                style.borderTopRightRadius = 5f;
                style.borderBottomLeftRadius = 5f;
                style.borderBottomRightRadius = 5f;
                style.paddingLeft = 12f;
                style.paddingRight = 12f;
                style.paddingTop = 9f;
                style.paddingBottom = 8f;

                Label entry = new(graph._event.IsEntryChoice(node.NodeId)
                    ? "ENTRY"
                    : "CHOICE");
                entry.style.fontSize = 10f;
                entry.style.color = graph._event.IsEntryChoice(node.NodeId)
                    ? new Color(0.45f, 1f, 0.75f)
                    : new Color(0.6f, 0.65f, 0.66f);
                Add(entry);

                Label title = new(string.IsNullOrWhiteSpace(node.Title)
                    ? node.ChoiceId
                    : node.Title);
                title.style.unityFontStyleAndWeight = FontStyle.Bold;
                title.style.fontSize = 14f;
                title.style.marginTop = 4f;
                title.style.whiteSpace = WhiteSpace.Normal;
                Add(title);

                Label summary = new(
                    $"Conditions {node.Conditions.Count}  ·  " +
                    $"Rewards {node.Effects.Count}\n" +
                    (node.EndsEvent
                        ? "END EVENT"
                        : $"NEXT {node.NextChoiceNodeIds.Count}"));
                summary.style.fontSize = 11f;
                summary.style.color = new Color(0.72f, 0.77f, 0.74f);
                summary.style.marginTop = 10f;
                summary.style.whiteSpace = WhiteSpace.Normal;
                Add(summary);

                InputPort = CreatePort("◀", false);
                InputPort.style.left = -12f;
                Add(InputPort);

                VisualElement output = CreatePort("▶", true);
                output.style.right = -12f;
                output.tooltip = "Drag to another node's left port.";
                output.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 0)
                        return;
                    graph.BeginConnection(node, evt);
                    evt.StopImmediatePropagation();
                });
                Add(output);

                RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 0)
                        return;
                    graph.BeginNodeDrag(node, evt);
                    evt.StopPropagation();
                });
            }

            internal void SetSelected(bool selected)
            {
                Color border = selected
                    ? new Color(1f, 0.72f, 0.22f)
                    : new Color(0.22f, 0.55f, 0.5f);
                style.borderLeftColor = border;
                style.borderRightColor = border;
                style.borderTopColor = border;
                style.borderBottomColor = border;
            }

            private static VisualElement CreatePort(string text, bool output)
            {
                Label port = new(text);
                port.style.position = Position.Absolute;
                port.style.top = NodeHeight * 0.5f - 12f;
                port.style.width = 24f;
                port.style.height = 24f;
                port.style.unityTextAlign = TextAnchor.MiddleCenter;
                port.style.backgroundColor = output
                    ? new Color(0.2f, 0.68f, 0.62f)
                    : new Color(0.26f, 0.45f, 0.58f);
                port.style.borderTopLeftRadius = 12f;
                port.style.borderTopRightRadius = 12f;
                port.style.borderBottomLeftRadius = 12f;
                port.style.borderBottomRightRadius = 12f;
                return port;
            }
        }
    }

    private sealed class DungeonEventPreview : VisualElement
    {
        private readonly UnityEngine.UIElements.Image _banner;
        private readonly Label _title;
        private readonly Label _description;
        private readonly VisualElement _choices;

        internal DungeonEventPreview()
        {
            style.flexGrow = 1f;
            style.position = Position.Absolute;
            style.left = 0f;
            style.right = 0f;
            style.top = 0f;
            style.bottom = 0f;
            style.backgroundColor = Color.black;

            VisualElement frame = new();
            frame.style.position = Position.Absolute;
            frame.style.left = 16f;
            frame.style.right = 16f;
            frame.style.top = 16f;
            frame.style.bottom = 16f;
            frame.style.backgroundColor = new Color(0.04f, 0.05f, 0.05f);
            frame.style.overflow = Overflow.Hidden;
            Add(frame);

            _banner = new UnityEngine.UIElements.Image
            {
                scaleMode = ScaleMode.ScaleAndCrop
            };
            _banner.style.position = Position.Absolute;
            _banner.style.left = 0f;
            _banner.style.right = 0f;
            _banner.style.top = 0f;
            _banner.style.bottom = 0f;
            frame.Add(_banner);

            VisualElement content = new();
            content.style.position = Position.Absolute;
            content.style.right = 0f;
            content.style.top = 0f;
            content.style.bottom = 0f;
            content.style.width = new Length(34f, LengthUnit.Percent);
            content.style.paddingLeft = 22f;
            content.style.paddingRight = 22f;
            content.style.paddingTop = 28f;
            content.style.paddingBottom = 24f;
            content.style.backgroundColor = new Color(0.025f, 0.055f, 0.05f, 0.9f);
            frame.Add(content);

            Label previewLabel = new("1920 × 1080 PREVIEW");
            previewLabel.style.fontSize = 10f;
            previewLabel.style.color = new Color(0.45f, 0.8f, 0.75f);
            content.Add(previewLabel);

            _title = new Label();
            _title.style.fontSize = 25f;
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _title.style.marginTop = 12f;
            _title.style.whiteSpace = WhiteSpace.Normal;
            content.Add(_title);

            _description = new Label();
            _description.style.fontSize = 14f;
            _description.style.marginTop = 16f;
            _description.style.marginBottom = 22f;
            _description.style.whiteSpace = WhiteSpace.Normal;
            content.Add(_description);

            ScrollView scroll = new(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1f;
            content.Add(scroll);
            _choices = scroll.contentContainer;
        }

        internal void Bind(DungeonEventSO dungeonEvent)
        {
            _choices.Clear();
            if (dungeonEvent == null)
            {
                _banner.image = null;
                _title.text = "NO EVENT SELECTED";
                _description.text = string.Empty;
                return;
            }

            _banner.sprite = dungeonEvent.Banner;
            _banner.tintColor = dungeonEvent.Banner != null
                ? Color.white
                : new Color(0.12f, 0.17f, 0.16f);
            _title.text = dungeonEvent.DisplayName;
            _description.text = dungeonEvent.Description;

            List<DungeonEventChoiceNodeDefinition> entries = new();
            dungeonEvent.GetEntryChoices(entries);
            for (int index = 0; index < entries.Count; index++)
            {
                DungeonEventChoiceNodeDefinition node = entries[index];
                Label choice = new(node.Title +
                    (string.IsNullOrWhiteSpace(node.Description)
                        ? string.Empty
                        : "\n" + node.Description));
                choice.style.whiteSpace = WhiteSpace.Normal;
                choice.style.paddingLeft = 14f;
                choice.style.paddingRight = 14f;
                choice.style.paddingTop = 11f;
                choice.style.paddingBottom = 11f;
                choice.style.marginBottom = 8f;
                choice.style.backgroundColor = new Color(0.12f, 0.25f, 0.2f, 0.96f);
                choice.style.borderLeftWidth = 1f;
                choice.style.borderRightWidth = 1f;
                choice.style.borderTopWidth = 1f;
                choice.style.borderBottomWidth = 1f;
                Color border = new(0.25f, 0.55f, 0.47f);
                choice.style.borderLeftColor = border;
                choice.style.borderRightColor = border;
                choice.style.borderTopColor = border;
                choice.style.borderBottomColor = border;
                _choices.Add(choice);
            }
        }
    }
}

internal static class DungeonEventAssetDelete
{
    private const int VisibleReferenceLimit = 8;

    internal static bool TryMoveToTrash(
        DungeonEventSO dungeonEvent,
        bool askForConfirmation,
        out string error,
        Func<string, bool> moveToTrash = null)
    {
        error = string.Empty;
        if (!TryGetAssetPath(dungeonEvent, out string eventPath))
        {
            error = "The selected object is not a deletable EventSO asset.";
            return false;
        }

        IReadOnlyList<string> references =
            PS260714SafeAssetDelete.FindReferences(dungeonEvent);
        List<DungeonReferenceSnapshot> dungeonReferences = new();
        List<string> unsupportedReferences = new();
        foreach (string referencePath in references)
        {
            DungeonDefinition definition =
                AssetDatabase.LoadAssetAtPath<DungeonDefinition>(
                    referencePath);
            if (definition == null ||
                !DungeonReferenceSnapshot.TryCreate(
                    definition,
                    dungeonEvent,
                    out DungeonReferenceSnapshot snapshot))
            {
                unsupportedReferences.Add(referencePath);
                continue;
            }

            dungeonReferences.Add(snapshot);
        }

        if (unsupportedReferences.Count > 0)
        {
            error = BuildUnsupportedReferenceMessage(
                unsupportedReferences);
            return false;
        }

        if (askForConfirmation && !EditorUtility.DisplayDialog(
                "Delete Dungeon Event",
                BuildConfirmationMessage(
                    dungeonEvent,
                    eventPath,
                    dungeonReferences),
                "Move to Trash",
                "Cancel"))
        {
            return false;
        }

        try
        {
            foreach (DungeonReferenceSnapshot snapshot in dungeonReferences)
                snapshot.Clear(dungeonEvent);
            AssetDatabase.SaveAssets();

            Func<string, bool> delete =
                moveToTrash ?? AssetDatabase.MoveAssetToTrash;
            if (!delete(eventPath))
            {
                RestoreReferences(dungeonReferences, dungeonEvent);
                error = "Failed to move the EventSO to the system trash.";
                return false;
            }

            DungeonDefinitionCatalog.Invalidate();
            AssetDatabase.SaveAssets();
            return true;
        }
        catch (Exception exception)
        {
            RestoreReferences(dungeonReferences, dungeonEvent);
            error =
                "Failed to delete the EventSO safely. " +
                exception.Message;
            return false;
        }
    }

    private static bool TryGetAssetPath(
        DungeonEventSO dungeonEvent,
        out string path)
    {
        path = dungeonEvent != null
            ? AssetDatabase.GetAssetPath(dungeonEvent)
            : string.Empty;
        return !string.IsNullOrWhiteSpace(path) &&
               path.StartsWith("Assets/", StringComparison.Ordinal) &&
               AssetDatabase.LoadMainAssetAtPath(path) == dungeonEvent;
    }

    private static string BuildConfirmationMessage(
        DungeonEventSO dungeonEvent,
        string eventPath,
        IReadOnlyList<DungeonReferenceSnapshot> references)
    {
        string message =
            $"Move '{dungeonEvent.name}' to the system trash?\n\n" +
            $"{eventPath}\n\n";
        if (references.Count > 0)
        {
            message +=
                "This event is assigned to the following dungeon " +
                "definition(s). Their default/fixed event fields will " +
                "be cleared before deletion:\n";
            int visibleCount = Math.Min(
                references.Count,
                VisibleReferenceLimit);
            for (int index = 0; index < visibleCount; index++)
                message += $"- {references[index].AssetPath}\n";
            if (references.Count > visibleCount)
            {
                message +=
                    $"- {references.Count - visibleCount} more\n";
            }
            message += "\n";
        }

        return message +
               "The EventSO can be restored from the system trash.";
    }

    private static string BuildUnsupportedReferenceMessage(
        IReadOnlyList<string> references)
    {
        string message =
            "The EventSO is referenced outside DungeonDefinition event " +
            "fields, so automatic deletion was stopped.\n\n";
        int visibleCount = Math.Min(
            references.Count,
            VisibleReferenceLimit);
        for (int index = 0; index < visibleCount; index++)
            message += $"- {references[index]}\n";
        if (references.Count > visibleCount)
        {
            message +=
                $"- {references.Count - visibleCount} more reference(s)";
        }
        return message;
    }

    private static void RestoreReferences(
        IReadOnlyList<DungeonReferenceSnapshot> references,
        DungeonEventSO dungeonEvent)
    {
        foreach (DungeonReferenceSnapshot snapshot in references)
            snapshot.Restore(dungeonEvent);
        DungeonDefinitionCatalog.Invalidate();
        AssetDatabase.SaveAssets();
    }

    private sealed class DungeonReferenceSnapshot
    {
        private readonly DungeonDefinition _definition;
        private readonly bool _usesDefault;
        private readonly List<int> _fixedIndices;

        internal string AssetPath =>
            AssetDatabase.GetAssetPath(_definition);

        private DungeonReferenceSnapshot(
            DungeonDefinition definition,
            bool usesDefault,
            List<int> fixedIndices)
        {
            _definition = definition;
            _usesDefault = usesDefault;
            _fixedIndices = fixedIndices;
        }

        internal static bool TryCreate(
            DungeonDefinition definition,
            DungeonEventSO dungeonEvent,
            out DungeonReferenceSnapshot snapshot)
        {
            SerializedObject serialized = new(definition);
            serialized.UpdateIfRequiredOrScript();
            SerializedProperty defaultEvent =
                serialized.FindProperty("defaultEvent");
            SerializedProperty fixedEvents =
                serialized.FindProperty("fixedEvents");
            bool usesDefault = defaultEvent != null &&
                               defaultEvent.objectReferenceValue ==
                               dungeonEvent;
            List<int> fixedIndices = new();
            if (fixedEvents != null && fixedEvents.isArray)
            {
                for (int index = 0;
                     index < fixedEvents.arraySize;
                     index++)
                {
                    if (fixedEvents.GetArrayElementAtIndex(index)
                            .objectReferenceValue == dungeonEvent)
                    {
                        fixedIndices.Add(index);
                    }
                }
            }

            if (!usesDefault && fixedIndices.Count == 0)
            {
                snapshot = null;
                return false;
            }

            snapshot = new DungeonReferenceSnapshot(
                definition,
                usesDefault,
                fixedIndices);
            return true;
        }

        internal void Clear(DungeonEventSO dungeonEvent)
        {
            Apply(dungeonEvent, null, "Remove Dungeon Event Reference");
        }

        internal void Restore(DungeonEventSO dungeonEvent)
        {
            Apply(null, dungeonEvent, "Restore Dungeon Event Reference");
        }

        private void Apply(
            DungeonEventSO expectedValue,
            DungeonEventSO replacement,
            string undoName)
        {
            if (_definition == null)
                return;

            Undo.RecordObject(_definition, undoName);
            SerializedObject serialized = new(_definition);
            serialized.UpdateIfRequiredOrScript();
            if (_usesDefault)
            {
                SerializedProperty defaultEvent =
                    serialized.FindProperty("defaultEvent");
                if (defaultEvent != null &&
                    defaultEvent.objectReferenceValue == expectedValue)
                {
                    defaultEvent.objectReferenceValue = replacement;
                }
            }

            SerializedProperty fixedEvents =
                serialized.FindProperty("fixedEvents");
            if (fixedEvents != null && fixedEvents.isArray)
            {
                foreach (int index in _fixedIndices)
                {
                    if (index < 0 || index >= fixedEvents.arraySize)
                        continue;
                    SerializedProperty element =
                        fixedEvents.GetArrayElementAtIndex(index);
                    if (element.objectReferenceValue == expectedValue)
                        element.objectReferenceValue = replacement;
                }
            }

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(_definition);
        }
    }
}
