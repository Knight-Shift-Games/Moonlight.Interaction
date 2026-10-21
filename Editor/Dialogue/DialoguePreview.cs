using System;
using System.Collections.Generic;

namespace Moonlight.Interaction.Dialogue.Editor
{
    /// <summary>Production runner with a sandbox adapter: actions are recorded, never delivered to gameplay.</summary>
    public sealed class DialoguePreview : IDialogueAdapter
    {
        readonly DialogueDocument _document;
        readonly HashSet<string> _conditions = new(StringComparer.Ordinal);
        readonly HashSet<string> _deliveries = new(StringComparer.Ordinal);
        public readonly List<string> Actions = new();
        public DialogueRunner Runner { get; }
        public DialoguePreview(DialogueDocument document)
        { _document = document; Runner = new DialogueRunner(this); }
        public void SetCondition(string id, bool value) { if (value) _conditions.Add(id); else _conditions.Remove(id); }
        public bool Start(DialogueTrigger trigger)
        {
            if (DialogueAuthoringValidation.Validate(_document).Count != 0) return false;
            Actions.Clear(); _deliveries.Clear();
            return Runner.Start(_document.Graph, trigger, new DialogueContext("preview-actor", "preview-target"), DialogueDocument.NewId());
        }
        public bool IsConditionMet(string condition, DialogueContext context) => _conditions.Contains(condition);
        public string ResolveText(string key)
        {
            foreach (var text in _document.Texts) if (text.Key == key) return text.Text ?? "";
            return "[" + key + "]";
        }
        public bool TryExecute(string action, string argument, string deliveryId, DialogueContext context)
        {
            if (Array.IndexOf(_document.AllowedActions, action) < 0) return false;
            if (_deliveries.Add(deliveryId)) Actions.Add(action + "(" + argument + ")");
            return true;
        }
    }
}
