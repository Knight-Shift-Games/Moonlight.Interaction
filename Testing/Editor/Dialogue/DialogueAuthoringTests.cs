using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Moonlight.Interaction.Dialogue.Editor.Tests
{
    public sealed class DialogueAuthoringTests
    {
        DialogueDocument _document;
        string _assetPath;
        [SetUp] public void SetUp() => _document = Fixture();
        [TearDown] public void TearDown()
        {
            Undo.ClearAll();
            if (!string.IsNullOrEmpty(_assetPath)) AssetDatabase.DeleteAsset(_assetPath);
            if (_document != null && !EditorUtility.IsPersistent(_document)) Object.DestroyImmediate(_document);
        }
        static DialogueDocument Fixture()
        {
            var d = DialogueDocument.Create();
            d.Participants = new[] { new DialogueParticipant { Id = "guide", DisplayName = "Guide" } };
            d.Texts = new[] { new DialogueText { Key = "intro", Text = "Welcome" }, new DialogueText { Key = "ready", Text = "Ready" }, new DialogueText { Key = "later", Text = "Later" } };
            d.AllowedConditions = new[] { "trained" }; d.AllowedActions = new[] { "record" };
            d.Graph = new DialogueGraph { Id = "fixture", Revision = "1", Entry = "branch", Triggers = DialogueTrigger.Interaction | DialogueTrigger.Automatic,
                Nodes = new[] {
                    new DialogueNode { Id = "branch", Kind = DialogueNodeKind.Branch, Condition = "trained", Next = "line", Otherwise = "choice" },
                    new DialogueNode { Id = "line", Kind = DialogueNodeKind.Line, SpeakerId = "guide", TextKey = "intro", Next = "end" },
                    new DialogueNode { Id = "choice", Kind = DialogueNodeKind.Choice, Choices = new[] {
                        new DialogueChoice { Id = "yes", TextKey = "ready", Next = "action" },
                        new DialogueChoice { Id = "no", TextKey = "later", Next = "end" } } },
                    new DialogueNode { Id = "action", Kind = DialogueNodeKind.Action, Action = "record", Argument = "fixture", Next = "end" },
                    new DialogueNode { Id = "end", Kind = DialogueNodeKind.End } } };
            return d;
        }
        DialogueNode Node(string id) => Array.Find(_document.Graph.Nodes, n => n.Id == id);
        void AssertError(string message) => Assert.That(DialogueAuthoringValidation.Validate(_document), Does.Contain(message));
        void Valid() => Assert.That(DialogueAuthoringValidation.Validate(_document), Is.Empty);

        [TestCase(DialoguePresentation.IllustratedConversation)]
        [TestCase(DialoguePresentation.SoftBark)]
        public void BothModesUseTheSameProductionSchema(DialoguePresentation mode)
        { _document.Presentation = mode; Valid(); Assert.That(_document.Graph.Schema, Is.EqualTo(1)); }
        [Test] public void MissingParticipantIsRejected() { Node("line").SpeakerId = "absent"; AssertError("line: missing participant: absent"); }
        [Test] public void MissingLineKeyIsRejected() { Node("line").TextKey = "absent"; AssertError("line: missing localization key: absent"); }
        [Test] public void MissingChoiceKeyIsRejected() { Node("choice").Choices[0].TextKey = "absent"; AssertError("choice: missing choice localization key: absent"); }
        [Test] public void MissingTargetIsRejected() { Node("line").Next = "absent"; AssertError("Dangling transition at line"); }
        [Test] public void MissingChoiceTargetIsRejected() { Node("choice").Choices[0].Next = "absent"; AssertError("choice/yes: missing choice target: absent"); }
        [Test] public void MissingEntryIsRejected() { _document.Graph.Entry = "absent"; AssertError("Dangling graph entry."); }
        [Test] public void DuplicateNodeIsRejected() { Node("line").Id = "end"; AssertError("Invalid or duplicate node ID/kind."); }
        [Test] public void DuplicateChoiceIsRejected() { Node("choice").Choices[0].Id = "no"; AssertError("choice: duplicate choice ID: no"); }
        [Test] public void UnreachableNodeIsRejected() { _document.Graph.Entry = "line"; AssertError("branch: unreachable from entry."); }
        [Test] public void UnknownActionIsRejected() { Node("action").Action = "unregistered"; AssertError("action: action is not allowed: unregistered"); }
        [Test] public void UnknownBranchConditionIsRejected() { Node("branch").Condition = "unregistered"; AssertError("branch: condition is not allowed: unregistered"); }
        [Test] public void UnknownTriggerConditionIsRejected() { _document.Graph.Conditions = new[] { "unregistered" }; AssertError("Graph: condition is not allowed: unregistered"); }
        [Test] public void UnknownChoiceConditionIsRejected() { Node("choice").Choices[0].Condition = "unregistered"; AssertError("choice: condition is not allowed: unregistered"); }
        [Test] public void DuplicateCataloguesAreRejected() { _document.AllowedActions = new[] { "record", "record" }; AssertError("Duplicate action: record"); }
        [Test] public void BlankCatalogueEntryIsRejected() { _document.AllowedConditions = new[] { "" }; AssertError("Empty condition."); }
        [Test] public void InvalidModeIsRejected() { _document.Presentation = (DialoguePresentation)42; AssertError("Choose a supported presentation mode."); }
        [Test] public void UnsupportedSchemaIsRejected() { _document.Graph.Schema = 99; AssertError("Unsupported dialogue graph schema."); }
        [Test] public void NullDocumentIsRejected() { Assert.That(DialogueAuthoringValidation.Validate(null), Is.EqualTo(new[] { "Choose a dialogue document." })); }

        [TestCase("participant")]
        [TestCase("localization key")]
        [TestCase("condition")]
        [TestCase("action")]
        public void MissingCatalogueIsRejected(string label)
        {
            SetCatalogue(label, null); AssertError("Missing " + label + " catalogue.");
        }
        [TestCase("participant", false)] [TestCase("participant", true)]
        [TestCase("localization key", false)] [TestCase("localization key", true)]
        [TestCase("condition", false)] [TestCase("condition", true)]
        [TestCase("action", false)] [TestCase("action", true)]
        public void CatalogueEmptyAndDuplicateHaveDistinctDiagnostics(string label, bool duplicate)
        {
            SetCatalogue(label, duplicate ? new[] { "duplicate", "duplicate" } : new[] { " " });
            AssertError(duplicate ? "Duplicate " + label + ": duplicate" : "Empty " + label + ".");
        }
        void SetCatalogue(string label, string[] values)
        {
            switch (label)
            {
                case "participant": _document.Participants = values == null ? null : Array.ConvertAll(values, id => new DialogueParticipant { Id = id }); break;
                case "localization key": _document.Texts = values == null ? null : Array.ConvertAll(values, key => new DialogueText { Key = key }); break;
                case "condition": _document.AllowedConditions = values; break;
                case "action": _document.AllowedActions = values; break;
            }
        }
        [Test] public void NullLoopPolicyIsRejected() { _document.Loops = new DialogueLoopPolicy[] { null }; AssertError("Missing loop policy."); }
        [Test] public void EmptyLoopNodeIdIsRejected() { _document.Loops = new[] { new DialogueLoopPolicy { NodeId = " ", ExitPolicy = "Exit" } }; AssertError("Loop policy requires a node ID."); }
        [Test] public void DanglingEntryDoesNotFloodUnreachableDiagnostics()
        { _document.Graph.Entry = "missing"; Assert.That(DialogueAuthoringValidation.Validate(_document), Is.EqualTo(new[] { "Dangling graph entry." })); }
        [Test] public void AddNodeRecoversMissingNodeAndPlacementArrays()
        {
            _document.Graph.Nodes = null; _document.Placements = null;
            var node = _document.AddNode(DialogueNodeKind.End, new Vector2(30, 40));
            Assert.That(_document.Graph.Nodes, Has.Length.EqualTo(1)); Assert.That(_document.Position(node.Id), Is.EqualTo(new Vector2(30, 40)));
        }
        [Test] public void InvalidExportAndPublishCannotWriteOrDeliver()
        {
            var window = ScriptableObject.CreateInstance<DialogueGraphWindow>();
            var serialized = new SerializedObject(window);
            serialized.FindProperty("_document").objectReferenceValue = _document; serialized.ApplyModifiedPropertiesWithoutUndo();
            _assetPath = "Assets/ARC313-export-" + Guid.NewGuid().ToString("N") + ".json";
            var calls = 0; Action<DialogueDocument> publish = d => calls++;
            DialogueGraphWindow.PublishRequested += publish;
            try
            {
                Node("line").Next = "missing";
                Assert.That(window.TryPublish(), Is.False); Assert.That(calls, Is.Zero);
                Assert.That(window.TryExport(_assetPath), Is.False); Assert.That(System.IO.File.Exists(_assetPath), Is.False);
                Node("line").Next = "end";
                Assert.That(window.TryPublish(publish), Is.True); Assert.That(calls, Is.EqualTo(1));
                Assert.That(window.TryExport(_assetPath), Is.True);
                Assert.That(JsonUtility.FromJson<DialogueGraph>(System.IO.File.ReadAllText(_assetPath)).Id, Is.EqualTo("fixture"));
            }
            finally { DialogueGraphWindow.PublishRequested -= publish; Object.DestroyImmediate(window); }
        }

        void InteractiveLoop()
        {
            Node("action").Next = "choice";
            _document.Loops = new[] { new DialogueLoopPolicy { NodeId = "action", ExitPolicy = "Choose Later" }, new DialogueLoopPolicy { NodeId = "choice", ExitPolicy = "Choose Later" } };
        }
        [Test] public void ExplicitInteractiveLoopWithUnconditionalExitIsAllowed() { InteractiveLoop(); Valid(); }
        [Test] public void InteractiveLoopWithoutPolicyIsRejected() { InteractiveLoop(); _document.Loops = Array.Empty<DialogueLoopPolicy>(); AssertError("action: intentional loop requires an exit policy."); }
        [Test] public void BlankLoopPolicyIsRejected() { InteractiveLoop(); _document.Loops[0].ExitPolicy = " "; AssertError("action: loop policy requires an exit description."); }
        [Test] public void DanglingLoopPolicyIsRejected() { InteractiveLoop(); _document.Loops[0].NodeId = "missing"; AssertError("Loop policy references missing node: missing"); }
        [Test] public void DuplicateLoopPolicyIsRejected() { InteractiveLoop(); _document.Loops[0].NodeId = "choice"; AssertError("Duplicate loop policy: choice"); }
        [Test] public void ClosedInteractiveLoopIsRejected() { InteractiveLoop(); Node("choice").Choices[1].Next = "action"; AssertError("action: loop has no route to End."); }
        [Test] public void ConditionalOnlyLoopExitIsRejected() { InteractiveLoop(); Node("choice").Choices[1].Condition = "trained"; AssertError("choice: loop choice requires an unconditional exit outside the cycle."); }
        [Test] public void AutomaticCycleCannotBeWaivedByPolicy()
        { Node("action").Next = "action"; _document.Loops = new[] { new DialogueLoopPolicy { NodeId = "action", ExitPolicy = "Condition changes" } }; AssertError("Automatic transition cycle."); }

        sealed class RuntimeAdapter : IDialogueAdapter
        {
            public bool Trained;
            public readonly List<string> Actions = new();
            public bool IsConditionMet(string condition, DialogueContext context) => Trained;
            public string ResolveText(string key) => key;
            public bool TryExecute(string action, string argument, string id, DialogueContext context) { Actions.Add(action + "(" + argument + ")"); return true; }
        }
        [TestCase(true, DialogueTrigger.Interaction)]
        [TestCase(false, DialogueTrigger.Interaction)]
        [TestCase(true, DialogueTrigger.Automatic)]
        [TestCase(false, DialogueTrigger.Automatic)]
        public void PreviewAndProductionChooseIdenticalPaths(bool trained, DialogueTrigger trigger)
        {
            var preview = new DialoguePreview(_document); preview.SetCondition("trained", trained);
            var adapter = new RuntimeAdapter { Trained = trained }; var runtime = new DialogueRunner(adapter);
            Assert.That(preview.Start(trigger), Is.True);
            Assert.That(runtime.Start(_document.Graph, trigger, new DialogueContext("actor", "npc"), "test-session"), Is.True);
            Assert.That(preview.Runner.NodeId, Is.EqualTo(runtime.NodeId));
            if (trained) { Assert.That(preview.Runner.GetText(), Is.EqualTo("Welcome")); preview.Runner.Advance(); runtime.Advance(); }
            else { Assert.That(preview.Runner.GetChoices().Count, Is.EqualTo(runtime.GetChoices().Count)); preview.Runner.Choose("yes"); runtime.Choose("yes"); }
            Assert.That(preview.Runner.State, Is.EqualTo(runtime.State));
            Assert.That(preview.Actions, Is.EqualTo(adapter.Actions));
        }
        [Test] public void InvalidPreviewCannotDeliverActions() { Node("action").Action = "unknown"; var preview = new DialoguePreview(_document); Assert.That(preview.Start(DialogueTrigger.Interaction), Is.False); Assert.That(preview.Actions, Is.Empty); }
        [Test] public void PreviewRespectsChangingChoiceConditions()
        {
            Node("choice").Choices[0].Condition = "trained";
            var preview = new DialoguePreview(_document); Assert.That(preview.Start(DialogueTrigger.Interaction), Is.True);
            Assert.That(preview.Runner.GetChoices().Count, Is.EqualTo(1)); preview.SetCondition("trained", true);
            Assert.That(preview.Runner.GetChoices().Count, Is.EqualTo(2)); Assert.That(preview.Runner.Choose("yes"), Is.True);
        }
        [Test] public void PreviewNeverInvokesUnregisteredAction() { var p = new DialoguePreview(_document); Assert.That(p.TryExecute("external", "payload", "delivery", new DialogueContext()), Is.False); Assert.That(p.Actions, Is.Empty); }
        [Test] public void PreviewDeduplicatesRecordedDeliveries() { var p = new DialoguePreview(_document); p.TryExecute("record", "x", "delivery", new DialogueContext()); p.TryExecute("record", "x", "delivery", new DialogueContext()); Assert.That(p.Actions.Count, Is.EqualTo(1)); }
        [Test] public void PreviewRespectsStartConditions() { _document.Graph.Conditions = new[] { "trained" }; var p = new DialoguePreview(_document); Assert.That(p.Start(DialogueTrigger.Interaction), Is.False); p.SetCondition("trained", true); Assert.That(p.Start(DialogueTrigger.Interaction), Is.True); }

        [Test] public void AddDeleteUndoRedoPreserveNodeAndChoiceIds()
        {
            var choiceId = Node("choice").Choices[0].Id; var node = _document.AddNode(DialogueNodeKind.Line, new Vector2(100, 200)); var id = node.Id;
            Undo.FlushUndoRecordObjects(); Undo.IncrementCurrentGroup();
            _document.RemoveNode(id); Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
            Assert.That(Array.Exists(_document.Graph.Nodes, n => n.Id == id), Is.True);
            Assert.That(Node("choice").Choices[0].Id, Is.EqualTo(choiceId));
            Undo.PerformRedo(); Assert.That(Array.Exists(_document.Graph.Nodes, n => n.Id == id), Is.False);
        }
        [Test] public void MovingNodePreservesRevisionAndSupportsUndo()
        {
            var revision = _document.Graph.Revision; _document.Move("line", new Vector2(30, 40));
            _document.Record("Move"); _document.Move("line", new Vector2(130, 140)); Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
            Assert.That(_document.Position("line"), Is.EqualTo(new Vector2(30, 40))); Assert.That(_document.Graph.Revision, Is.EqualTo(revision));
        }
        [Test] public void DeleteLeavesIncomingTargetVisibleForValidation() { _document.RemoveNode("line"); Assert.That(Node("branch").Next, Is.EqualTo("line")); AssertError("Dangling transition at branch"); }
        [Test] public void NewDocumentIsImmediatelyValid() { var d = DialogueDocument.Create(); try { Assert.That(DialogueAuthoringValidation.Validate(d), Is.Empty); } finally { Object.DestroyImmediate(d); } }
        [Test] public void SaveReopenAndRuntimeJsonPreserveAllStableIds()
        {
            var before = JsonUtility.ToJson(_document.Graph); _document.Move("line", new Vector2(10, 20)); _document.Presentation = DialoguePresentation.SoftBark;
            _assetPath = "Assets/ARC313-" + Guid.NewGuid().ToString("N") + ".asset";
            AssetDatabase.CreateAsset(_document, _assetPath); EditorUtility.SetDirty(_document); AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(_assetPath, ImportAssetOptions.ForceUpdate);
            Assert.That(System.IO.File.ReadAllText(_assetPath), Does.Contain("fixture"));
            Resources.UnloadAsset(_document); _document = null;
            var loaded = AssetDatabase.LoadAssetAtPath<DialogueDocument>(_assetPath);
            Assert.That(JsonUtility.ToJson(loaded.Graph), Is.EqualTo(before)); Assert.That(loaded.Position("line"), Is.EqualTo(new Vector2(10, 20)));
            Assert.That(loaded.Presentation, Is.EqualTo(DialoguePresentation.SoftBark));
            var runtime = JsonUtility.FromJson<DialogueGraph>(before); Assert.That(DialogueRunner.Validate(runtime, out var error), Is.True, error);
            Assert.That(runtime.Nodes[2].Choices[0].Id, Is.EqualTo("yes"));
        }
        [Test] public void EditorAssembliesAreIsolatedFromPlayers()
        {
            foreach (var name in new[] { "Moonlight.Interaction.Dialogue.Editor", "Moonlight.Interaction.Dialogue.Editor.Tests" })
            {
                var matches = Array.FindAll(AssetDatabase.FindAssets(name + " t:AssemblyDefinitionAsset"),
                    guid => System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)) == name);
                Assert.That(matches, Has.Length.EqualTo(1), name);
                var path = AssetDatabase.GUIDToAssetPath(matches[0]);
                Assert.That(System.IO.File.ReadAllText(path), Does.Contain("\"Editor\""), name);
            }
            Assert.That(typeof(DialogueRunner).Assembly.GetReferencedAssemblies(), Has.None.Property("Name").EqualTo(typeof(DialogueDocument).Assembly.GetName().Name));
        }
        [Test] public void NativeAssetOpenCallbackOpensTheDocumentWindow()
        {
            _assetPath = "Assets/ARC313-Open-" + Guid.NewGuid().ToString("N") + ".asset";
            AssetDatabase.CreateAsset(_document, _assetPath); AssetDatabase.SaveAssets();
            try
            {
                // Exercise this registered callback and native EntityId lookup directly. Global
                // AssetDatabase.OpenAsset also invokes a legacy Circuit callback with an invalid
                // pre-6000.6 signature, outside this module's ownership.
                if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    // Unity still creates/binds the EditorWindow object in -nographics, but
                    // reports these four view-initialization diagnostics for its native views.
                    UnityEngine.TestTools.LogAssert.Expect(LogType.Error, "No graphic device is available to initialize the view.");
                    UnityEngine.TestTools.LogAssert.Expect(LogType.Error, "No graphic device is available to show the window.");
                    UnityEngine.TestTools.LogAssert.Expect(LogType.Error, "No graphic device is available to initialize the view.");
                    UnityEngine.TestTools.LogAssert.Expect(LogType.Error, "No graphic device is available to initialize the view.");
                }
                Assert.That(DialogueGraphWindow.OpenAsset(_document.GetEntityId(), 0), Is.True);
                var windows = Resources.FindObjectsOfTypeAll<DialogueGraphWindow>();
                Assert.That(windows.Length, Is.EqualTo(1));
                var serialized = new SerializedObject(windows[0]);
                Assert.That(serialized.FindProperty("_document").objectReferenceValue, Is.EqualTo(_document));
            }
            finally { foreach (var window in Resources.FindObjectsOfTypeAll<DialogueGraphWindow>()) window.Close(); }
        }
        [Test] public void AssetOpenCallbackDoesNotClaimOtherAssetTypes()
        {
            var texture = new Texture2D(1, 1);
            try { Assert.That(DialogueGraphWindow.OpenAsset(texture.GetEntityId(), 0), Is.False); }
            finally { Object.DestroyImmediate(texture); }
        }
    }
}
