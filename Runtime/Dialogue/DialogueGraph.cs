using System;

namespace Moonlight.Interaction.Dialogue
{
    // Schema 1 is shared by authoring, portraits, barks and save data. IDs are authored,
    // ordinal, nonempty strings; a revision changes whenever graph content changes.
    [Serializable]
    public sealed class DialogueGraph
    {
        public const int CurrentSchema = 1;
        public int Schema = CurrentSchema;
        public string Id;
        public string Revision;
        public string Entry;
        public DialogueTrigger Triggers = DialogueTrigger.Interaction;
        public string[] Conditions = Array.Empty<string>();
        public DialogueNode[] Nodes = Array.Empty<DialogueNode>();
    }

    [Flags]
    public enum DialogueTrigger { Interaction = 1, Automatic = 2 }
    public enum DialogueNodeKind { Invalid = 0, Line = 1, Choice = 2, Branch = 3, Action = 4, End = 5 }
    public enum DialogueState { Idle = 0, Running = 1, Completed = 2, Cancelled = 3, Suspended = 4, Faulted = 5 }

    [Serializable]
    public sealed class DialogueNode
    {
        public string Id;
        public DialogueNodeKind Kind;
        public string SpeakerId;
        public string ExpressionId;
        public string TextKey;
        public string Next;
        public string Condition;
        public string Otherwise;
        public string Action;
        public string Argument;
        public DialogueChoice[] Choices = Array.Empty<DialogueChoice>();
    }

    [Serializable]
    public sealed class DialogueChoice
    {
        public string Id;
        public string TextKey;
        public string Condition;
        public string Next;
    }

    [Serializable]
    public sealed class DialogueContext
    {
        public string ActorId;
        public string TargetId;
        public DialogueContext() { }
        public DialogueContext(string actorId, string targetId) { ActorId = actorId; TargetId = targetId; }
    }

    [Serializable]
    public sealed class DialogueSnapshot
    {
        public int Schema = DialogueGraph.CurrentSchema;
        public string GraphId;
        public string Revision;
        public string Fingerprint;
        public string SessionId;
        public string NodeId;
        public long ActionSequence;
        public DialogueContext Context;
    }

    public sealed class DialogueOption
    {
        public string Id { get; }
        public string Text { get; }
        public DialogueOption(string id, string text) { Id = id; Text = text; }
    }

    /// <summary>Implementations own condition evaluation, text resolution and validated actions.
    /// TryExecute must atomically deduplicate deliveryId before applying its effect. This makes
    /// replay of an older saved checkpoint safe, including across runner/process instances.</summary>
    public interface IDialogueAdapter
    {
        bool IsConditionMet(string condition, DialogueContext context);
        string ResolveText(string key);
        bool TryExecute(string action, string argument, string deliveryId, DialogueContext context);
    }
}
