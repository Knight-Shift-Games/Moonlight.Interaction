using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Moonlight.Interaction.Dialogue.Editor
{
    public enum DialoguePresentation { IllustratedConversation, SoftBark }

    [Serializable] public sealed class DialogueParticipant
    {
        public string Id;
        public string DisplayName;
        public Texture2D Portrait;
    }
    [Serializable] public sealed class DialogueText { public string Key; [TextArea] public string Text; }
    [Serializable] public sealed class DialoguePlacement { public string NodeId; public Vector2 Position; }
    [Serializable] public sealed class DialogueLoopPolicy
    {
        public string NodeId;
        [Tooltip("Describe how the player exits this intentional interactive loop.")] public string ExitPolicy;
    }

    /// <summary>Editor-only wrapper. Graph is the unchanged schema-1 production DTO.
    /// Catalogues are explicit authoring/preview contracts, not gameplay action handlers.</summary>
    public sealed class DialogueDocument : ScriptableObject
    {
        public DialoguePresentation Presentation;
        public DialogueGraph Graph = new();
        public DialogueParticipant[] Participants = Array.Empty<DialogueParticipant>();
        public DialogueText[] Texts = Array.Empty<DialogueText>();
        public string[] AllowedConditions = Array.Empty<string>();
        public string[] AllowedActions = Array.Empty<string>();
        public DialoguePlacement[] Placements = Array.Empty<DialoguePlacement>();
        public DialogueLoopPolicy[] Loops = Array.Empty<DialogueLoopPolicy>();

        public static DialogueDocument Create()
        {
            var document = CreateInstance<DialogueDocument>();
            var end = new DialogueNode { Id = NewId(), Kind = DialogueNodeKind.End };
            document.Graph = new DialogueGraph { Id = NewId(), Revision = NewId(), Entry = end.Id, Nodes = new[] { end } };
            document.Placements = new[] { new DialoguePlacement { NodeId = end.Id, Position = new Vector2(60, 60) } };
            return document;
        }
        public static string NewId() => Guid.NewGuid().ToString("N");

        public DialogueNode AddNode(DialogueNodeKind kind, Vector2 position)
        {
            Record("Add dialogue node");
            var node = new DialogueNode { Id = NewId(), Kind = kind };
            var nodes = new List<DialogueNode>(Graph.Nodes) { node }; Graph.Nodes = nodes.ToArray();
            var placements = new List<DialoguePlacement>(Placements) { new() { NodeId = node.Id, Position = position } };
            Placements = placements.ToArray(); Changed(); return node;
        }
        public void RemoveNode(string id)
        {
            Record("Delete dialogue node");
            Graph.Nodes = Array.FindAll(Graph.Nodes, n => n.Id != id);
            Placements = Array.FindAll(Placements, p => p.NodeId != id);
            Loops = Array.FindAll(Loops, p => p.NodeId != id);
            // Keep incoming targets explicit: validation highlights dangling links; never silently reroute.
            Changed();
        }
        public void Record(string label) => Undo.RegisterCompleteObjectUndo(this, label);
        public void Changed() { Graph.Revision = NewId(); EditorUtility.SetDirty(this); }
        public Vector2 Position(string id)
        {
            foreach (var p in Placements) if (p.NodeId == id) return p.Position;
            return new Vector2(60, 60);
        }
        public void Move(string id, Vector2 position)
        {
            var found = Array.Find(Placements, p => p.NodeId == id);
            if (found != null) found.Position = position;
            else { var list = new List<DialoguePlacement>(Placements) { new() { NodeId = id, Position = position } }; Placements = list.ToArray(); }
            EditorUtility.SetDirty(this); // Layout is not runtime content and does not invalidate checkpoints.
        }
    }
}
