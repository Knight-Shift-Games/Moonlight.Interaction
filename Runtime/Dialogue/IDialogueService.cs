using System.Collections.Generic;
using Moonlight.Interaction.Dialogue;

namespace Moonlight.Interaction
{
    public interface IDialogueService
    {
        DialogueState State { get; }
        string Error { get; }
        string NodeId { get; }
        string SpeakerId { get; }
        string Text { get; }
        bool CanStart(DialogueGraph graph, DialogueTrigger trigger, DialogueContext context);
        bool Start(DialogueGraph graph, DialogueTrigger trigger, DialogueContext context, string sessionId);
        bool Advance();
        IReadOnlyList<DialogueOption> GetChoices();
        bool Choose(string choiceId);
        DialogueSnapshot Suspend();
        bool Resume(DialogueGraph graph, DialogueSnapshot snapshot);
        bool Cancel();
    }
}
