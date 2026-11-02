using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Moonlight.Interaction.Dialogue.Editor
{
    public sealed class DialogueGraphWindow : EditorWindow
    {
        public static event Action<DialogueDocument> PublishRequested;
        [SerializeField] DialogueDocument _document;
        [SerializeField] string _selected;
        Vector2 _canvasScroll, _inspectorScroll, _previewScroll, _errorScroll;
        string _dragging;
        Vector2 _dragOffset;
        bool _catalogues, _dragRecorded;
        List<string> _errors = new();
        DialoguePreview _preview;
        readonly HashSet<string> _trueConditions = new(StringComparer.Ordinal);
        DialogueTrigger _trigger = DialogueTrigger.Interaction;

        [MenuItem("Tools/Moonlight/Dialogue Graph")]
        public static void Open() => GetWindow<DialogueGraphWindow>("Dialogue Graph");

        [MenuItem("Assets/Create/Moonlight/Dialogue Document")]
        public static void CreateDocument()
        {
            var path = EditorUtility.SaveFilePanelInProject("New dialogue", "Dialogue", "asset", "Choose a project asset path.");
            if (string.IsNullOrEmpty(path)) return;
            var document = DialogueDocument.Create(); AssetDatabase.CreateAsset(document, path); AssetDatabase.SaveAssets();
            var window = GetWindow<DialogueGraphWindow>("Dialogue Graph"); window.SelectDocument(document);
            Selection.activeObject = document;
        }

        [OnOpenAsset]
        public static bool OpenAsset(EntityId instanceId, int line)
        {
            if (EditorUtility.EntityIdToObject(instanceId) is not DialogueDocument document) return false;
            GetWindow<DialogueGraphWindow>("Dialogue Graph").SelectDocument(document); return true;
        }
        void OnEnable() { minSize = new Vector2(1000, 650); Undo.undoRedoPerformed += UndoRedo; Validate(); }
        void OnDisable() { Undo.undoRedoPerformed -= UndoRedo; _preview?.Runner.Cancel(); }
        void UndoRedo() { StopPreview(); Validate(); Repaint(); }
        void SelectDocument(DialogueDocument document)
        { StopPreview(); _document = document; _selected = document?.Graph?.Entry; _trueConditions.Clear(); Validate(); Repaint(); }
        void Validate() => _errors = DialogueAuthoringValidation.Validate(_document);
        void StopPreview() { _preview?.Runner.Cancel(); _preview = null; }

        void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var picked = (DialogueDocument)EditorGUILayout.ObjectField(_document, typeof(DialogueDocument), false, GUILayout.Width(250));
                if (picked != _document) SelectDocument(picked);
                if (GUILayout.Button("New", EditorStyles.toolbarButton)) CreateDocument();
                using (new EditorGUI.DisabledScope(_document == null))
                {
                    if (GUILayout.Button("Save", EditorStyles.toolbarButton)) { AssetDatabase.SaveAssets(); Validate(); }
                    if (GUILayout.Button("Validate", EditorStyles.toolbarButton)) Validate();
                    if (GUILayout.Button("Export runtime JSON", EditorStyles.toolbarButton)) Export();
                    using (new EditorGUI.DisabledScope(PublishRequested == null))
                        if (GUILayout.Button("Publish game asset", EditorStyles.toolbarButton))
                        { if (!TryPublish()) ShowNotification(new GUIContent("Fix validation errors before publishing.")); }
                    if (GUILayout.Button("Undo", EditorStyles.toolbarButton)) Undo.PerformUndo();
                    if (GUILayout.Button("Redo", EditorStyles.toolbarButton)) Undo.PerformRedo();
                }
            }
            if (_document == null || _document.Graph?.Nodes == null) { EditorGUILayout.HelpBox("Create a dialogue document or open an existing asset.", MessageType.Info); return; }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
                {
                    using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                    {
                        foreach (var kind in new[] { DialogueNodeKind.Line, DialogueNodeKind.Choice, DialogueNodeKind.Branch, DialogueNodeKind.Action, DialogueNodeKind.End })
                            if (GUILayout.Button("+ " + kind, EditorStyles.toolbarButton))
                            { _selected = _document.AddNode(kind, _canvasScroll + new Vector2(80 + _document.Graph.Nodes.Length % 4 * 40, 80 + _document.Graph.Nodes.Length % 4 * 40)).Id; StopPreview(); Validate(); }
                    }
                    DrawCanvas();
                    EditorGUILayout.LabelField(_errors.Count == 0 ? "Valid graph" : _errors.Count + " validation errors", EditorStyles.boldLabel);
                    var diagnosticHeight = Mathf.Min(150, Mathf.Max(35, _errors.Count * 20));
                    _errorScroll = EditorGUILayout.BeginScrollView(_errorScroll, GUILayout.Height(diagnosticHeight));
                    foreach (var error in _errors) EditorGUILayout.LabelField(error, EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.EndScrollView();
                }
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(330))) DrawInspector();
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(270))) DrawPreview();
            }
        }

        void DrawCanvas()
        {
            var viewport = GUILayoutUtility.GetRect(200, 100, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            var extent = new Vector2(Mathf.Max(viewport.width, 1800), Mathf.Max(viewport.height, 1400));
            foreach (var p in _document.Placements ?? Array.Empty<DialoguePlacement>()) if (p != null) extent = Vector2.Max(extent, p.Position + new Vector2(300, 200));
            _canvasScroll = GUI.BeginScrollView(viewport, _canvasScroll, new Rect(Vector2.zero, extent));
            Handles.BeginGUI();
            foreach (var node in _document.Graph.Nodes)
                foreach (var target in DialogueAuthoringValidation.Targets(node))
                {
                    if (!Array.Exists(_document.Graph.Nodes, n => n.Id == target)) continue;
                    var from = _document.Position(node.Id) + new Vector2(180, 35);
                    var to = _document.Position(target) + new Vector2(0, 35);
                    Handles.DrawBezier(from, to, from + Vector2.right * 60, to - Vector2.right * 60, Color.gray, null, 2);
                }
            Handles.EndGUI();
            var ev = Event.current;
            foreach (var node in _document.Graph.Nodes)
            {
                var rect = new Rect(_document.Position(node.Id), new Vector2(180, 76));
                var before = GUI.backgroundColor; GUI.backgroundColor = node.Id == _selected ? new Color(.45f, .75f, 1) : Color.white;
                GUI.Box(rect, GUIContent.none, EditorStyles.helpBox); GUI.backgroundColor = before;
                GUI.Label(new Rect(rect.x + 8, rect.y + 5, 165, 20), (node.Id == _document.Graph.Entry ? "ENTRY · " : "") + node.Kind, EditorStyles.boldLabel);
                GUI.Label(new Rect(rect.x + 8, rect.y + 26, 165, 20), NodeLabel(node), EditorStyles.miniLabel);
                GUI.Label(new Rect(rect.x + 8, rect.y + 48, 165, 20), node.Id, EditorStyles.miniLabel);
                if (ev.type == EventType.MouseDown && ev.button == 0 && rect.Contains(ev.mousePosition))
                { _selected = node.Id; _dragging = node.Id; _dragOffset = ev.mousePosition - rect.position; _dragRecorded = false; ev.Use(); Repaint(); }
            }
            if (ev.type == EventType.MouseDrag && _dragging != null)
            { if (!_dragRecorded) { _document.Record("Move dialogue node"); _dragRecorded = true; } _document.Move(_dragging, Vector2.Max(Vector2.zero, ev.mousePosition - _dragOffset)); ev.Use(); Repaint(); }
            if (ev.type == EventType.MouseUp) _dragging = null;
            GUI.EndScrollView();
        }

        void DrawInspector()
        {
            _inspectorScroll = EditorGUILayout.BeginScrollView(_inspectorScroll);
            var beforeGraph = JsonUtility.ToJson(_document.Graph);
            var serialized = new SerializedObject(_document); serialized.Update();
            EditorGUILayout.LabelField("Document", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serialized.FindProperty("Presentation"));
            var graph = serialized.FindProperty("Graph");
            using (new EditorGUI.DisabledScope(true)) { EditorGUILayout.PropertyField(graph.FindPropertyRelative("Id")); EditorGUILayout.PropertyField(graph.FindPropertyRelative("Revision")); }
            EditorGUILayout.PropertyField(graph.FindPropertyRelative("Triggers"));
            EditorGUILayout.PropertyField(graph.FindPropertyRelative("Conditions"), true);
            _catalogues = EditorGUILayout.Foldout(_catalogues, "Participants, text and allowed identifiers", true);
            if (_catalogues)
                foreach (var name in new[] { "Participants", "Texts", "AllowedConditions", "AllowedActions", "Loops" }) EditorGUILayout.PropertyField(serialized.FindProperty(name), true);
            var nodes = graph.FindPropertyRelative("Nodes");
            var index = Array.FindIndex(_document.Graph.Nodes, n => n.Id == _selected);
            if (index >= 0)
            {
                var node = nodes.GetArrayElementAtIndex(index);
                EditorGUILayout.Space(); EditorGUILayout.LabelField("Selected " + _document.Graph.Nodes[index].Kind, EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(node.FindPropertyRelative("Id"));
                var kind = _document.Graph.Nodes[index].Kind;
                if (kind == DialogueNodeKind.Line)
                {
                    SelectKey(node.FindPropertyRelative("SpeakerId"), Array.ConvertAll(_document.Participants ?? Array.Empty<DialogueParticipant>(), p => p?.Id));
                    EditorGUILayout.PropertyField(node.FindPropertyRelative("ExpressionId"));
                    SelectKey(node.FindPropertyRelative("TextKey"), Array.ConvertAll(_document.Texts ?? Array.Empty<DialogueText>(), p => p?.Key));
                }
                if (kind == DialogueNodeKind.Branch) SelectKey(node.FindPropertyRelative("Condition"), _document.AllowedConditions);
                if (kind == DialogueNodeKind.Action)
                { SelectKey(node.FindPropertyRelative("Action"), _document.AllowedActions); EditorGUILayout.PropertyField(node.FindPropertyRelative("Argument")); }
                var ids = Array.ConvertAll(_document.Graph.Nodes, n => n.Id);
                if (kind != DialogueNodeKind.End && kind != DialogueNodeKind.Choice) SelectTarget(node.FindPropertyRelative("Next"), ids);
                if (kind == DialogueNodeKind.Branch) SelectTarget(node.FindPropertyRelative("Otherwise"), ids);
                if (kind == DialogueNodeKind.Choice) DrawChoices(node.FindPropertyRelative("Choices"), ids);
                if (GUILayout.Button("Set as entry")) graph.FindPropertyRelative("Entry").stringValue = _selected;
            }
            if (serialized.ApplyModifiedProperties()) { if (JsonUtility.ToJson(_document.Graph) != beforeGraph) _document.Changed(); StopPreview(); Validate(); }
            if (index >= 0 && GUILayout.Button("Delete selected node")) { _document.RemoveNode(_selected); _selected = null; StopPreview(); Validate(); }
            EditorGUILayout.EndScrollView();
        }

        void DrawChoices(SerializedProperty choices, string[] nodeIds)
        {
            for (var i = 0; i < choices.arraySize; ++i)
            {
                var choice = choices.GetArrayElementAtIndex(i);
                EditorGUILayout.LabelField("Option " + (i + 1), EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(choice.FindPropertyRelative("Id"));
                SelectKey(choice.FindPropertyRelative("TextKey"), Array.ConvertAll(_document.Texts ?? Array.Empty<DialogueText>(), p => p?.Key));
                SelectKey(choice.FindPropertyRelative("Condition"), _document.AllowedConditions);
                SelectTarget(choice.FindPropertyRelative("Next"), nodeIds);
                if (GUILayout.Button("Remove option " + (i + 1))) { choices.DeleteArrayElementAtIndex(i); break; }
            }
            if (GUILayout.Button("Add option"))
            {
                var index = choices.arraySize; choices.InsertArrayElementAtIndex(index);
                var choice = choices.GetArrayElementAtIndex(index);
                choice.FindPropertyRelative("Id").stringValue = DialogueDocument.NewId();
                foreach (var name in new[] { "TextKey", "Condition", "Next" }) choice.FindPropertyRelative(name).stringValue = "";
            }
        }
        static string NodeLabel(DialogueNode node)
        {
            var content = node.Kind == DialogueNodeKind.Line ? node.TextKey : node.Kind == DialogueNodeKind.Action ? node.Action : node.Kind == DialogueNodeKind.Branch ? node.Condition : "";
            return node.Kind + (string.IsNullOrEmpty(content) ? "" : ": " + content);
        }
        void SelectTarget(SerializedProperty property, string[] ids) => SelectKey(property, ids, Array.ConvertAll(_document.Graph.Nodes, n => NodeLabel(n) + " [" + n.Id.Substring(0, Math.Min(8, n.Id.Length)) + "]"));
        static void SelectKey(SerializedProperty property, string[] options, string[] labels = null)
        {
            var list = new List<string> { "" };
            foreach (var option in options ?? Array.Empty<string>()) if (!string.IsNullOrEmpty(option) && !list.Contains(option)) list.Add(option);
            var current = property.stringValue;
            if (!list.Contains(current)) list.Add(current);
            var displayed = list.ToArray(); displayed[0] = "(none)";
            if (labels != null) for (var i = 1; i < displayed.Length; ++i)
            { var original = Array.IndexOf(options, list[i]); if (original >= 0) displayed[i] = labels[original]; }
            var selected = EditorGUILayout.Popup(property.displayName, list.IndexOf(current), displayed);
            if (selected >= 0) property.stringValue = list[selected];
        }

        void DrawPreview()
        {
            _previewScroll = EditorGUILayout.BeginScrollView(_previewScroll);
            EditorGUILayout.LabelField("Runner preview", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Actions are recorded here. No gameplay handlers run.", MessageType.Info);
            foreach (var condition in _document.AllowedConditions ?? Array.Empty<string>())
            {
                var value = EditorGUILayout.ToggleLeft(condition ?? "(missing ID)", _trueConditions.Contains(condition ?? ""));
                if (value) _trueConditions.Add(condition ?? ""); else _trueConditions.Remove(condition ?? "");
                if (condition != null) _preview?.SetCondition(condition, value);
            }
            _trigger = (DialogueTrigger)EditorGUILayout.EnumPopup("Trigger", _trigger);
            if (GUILayout.Button("Start / restart"))
            {
                Validate(); StopPreview(); _preview = new DialoguePreview(_document);
                foreach (var condition in _trueConditions) _preview.SetCondition(condition, true);
                if (!_preview.Start(_trigger) && _errors.Count == 0) _errors.Add(_preview.Runner.Error ?? "Start conditions or trigger are not eligible.");
            }
            if (_preview != null)
            {
                var runner = _preview.Runner;
                EditorGUILayout.LabelField(runner.State.ToString(), EditorStyles.boldLabel);
                if (runner.State == DialogueState.Running)
                {
                    EditorGUILayout.SelectableLabel(runner.NodeId ?? "", GUILayout.Height(20));
                    if (runner.WaitingForChoice)
                    { foreach (var option in runner.GetChoices()) if (GUILayout.Button(option.Text)) runner.Choose(option.Id); }
                    else
                    {
                        var participant = Array.Find(_document.Participants ?? Array.Empty<DialogueParticipant>(), p => p != null && p.Id == runner.SpeakerId);
                        EditorGUILayout.LabelField(participant?.DisplayName ?? runner.SpeakerId ?? "");
                        if (_document.Presentation == DialoguePresentation.IllustratedConversation && participant?.Portrait != null)
                            GUILayout.Label(participant.Portrait, GUILayout.Width(100), GUILayout.Height(100));
                        EditorGUILayout.LabelField(runner.GetText() ?? "", EditorStyles.wordWrappedLabel);
                        if (GUILayout.Button("Advance")) runner.Advance();
                    }
                    if (GUILayout.Button("Cancel")) runner.Cancel();
                }
                if (runner.Error != null) EditorGUILayout.HelpBox(runner.Error, MessageType.Error);
                foreach (var action in _preview.Actions) EditorGUILayout.LabelField(action, EditorStyles.wordWrappedMiniLabel);
            }
            EditorGUILayout.EndScrollView();
        }
        public bool TryPublish(Action<DialogueDocument> publish = null)
        {
            Validate(); if (_errors.Count != 0) return false;
            (publish ?? PublishRequested)?.Invoke(_document); return true;
        }
        public bool TryExport(string path)
        {
            Validate(); if (_errors.Count != 0) return false;
            File.WriteAllText(path, JsonUtility.ToJson(_document.Graph, true)); AssetDatabase.ImportAsset(path); return true;
        }
        void Export()
        {
            Validate(); if (_errors.Count != 0) { ShowNotification(new GUIContent("Fix validation errors before export.")); return; }
            var path = EditorUtility.SaveFilePanelInProject("Export schema-1 runtime graph", _document.name, "json", "Save runtime graph JSON.");
            if (string.IsNullOrEmpty(path)) return;
            TryExport(path);
        }
    }
}
