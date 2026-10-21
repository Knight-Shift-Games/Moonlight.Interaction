using System;
using System.Collections.Generic;
using Moonlight.Interaction.Dialogue;
using NUnit.Framework;

namespace Moonlight.Interaction.Tests
{
    public sealed class DialogueRunnerTests
    {
        sealed class Adapter : IDialogueAdapter
        {
            public bool Condition;
            public bool Reject;
            public bool Throw;
            public bool ThrowCondition;
            public bool ThrowText;
            public int Effects;
            public readonly HashSet<string> Deliveries = new();
            public Action Callback;
            public bool IsConditionMet(string key, DialogueContext context)
            { Callback?.Invoke(); if (ThrowCondition) throw new InvalidOperationException("condition failure"); return Condition; }
            public string ResolveText(string key)
            { Callback?.Invoke(); if (ThrowText) throw new InvalidOperationException("text failure"); return "localized:" + key; }
            public bool TryExecute(string action, string argument, string id, DialogueContext context)
            {
                Callback?.Invoke();
                if (Throw) throw new InvalidOperationException("adapter exception");
                if (Reject) return false;
                if (Deliveries.Add(id)) ++Effects;
                context.ActorId = "mutated";
                return true;
            }
        }

        static DialogueGraph Graph(params DialogueNode[] nodes) => new()
        { Id = "npc.trainer", Revision = "1", Entry = nodes[0].Id, Nodes = nodes,
            Triggers = DialogueTrigger.Automatic | DialogueTrigger.Interaction };
        static DialogueNode Line(string id = "line", string next = "end") => new()
        { Id = id, Kind = DialogueNodeKind.Line, SpeakerId = "trainer", TextKey = "dialogue_training_name", Next = next };
        static DialogueNode End() => new() { Id = "end", Kind = DialogueNodeKind.End };
        static DialogueNode Action(string id = "action", string next = "end") => new()
        { Id = id, Kind = DialogueNodeKind.Action, Action = "training.unlock", Argument = "dash", Next = next };
        static DialogueContext Context => new("player-1", "npc-1");

        [Test]
        public void ReplayingCheckpointAlongDifferentChoiceKeepsBothActionIdentities()
        {
            var adapter = new Adapter();
            var runner = new DialogueRunner(adapter);
            var graph = Graph(new DialogueNode { Id = "choose", Kind = DialogueNodeKind.Choice, Choices = new[] {
                new DialogueChoice { Id = "a", TextKey = "a", Next = "reward" },
                new DialogueChoice { Id = "b", TextKey = "b", Next = "take" } } },
                Action("reward"), Action("take"), End());
            graph.Nodes[2].Action = "item.take";
            runner.Start(graph, DialogueTrigger.Interaction, Context, "s");
            var snapshot = runner.Suspend();
            runner.Resume(graph, snapshot); runner.Choose("a");
            runner.Resume(graph, snapshot); runner.Choose("b");
            Assert.That(adapter.Effects, Is.EqualTo(2));
            Assert.That(adapter.Deliveries, Does.Contain("v1:1:s:0:11:npc.trainer:6:reward:15:training.unlock:4:dash"));
            Assert.That(adapter.Deliveries, Does.Contain("v1:1:s:0:11:npc.trainer:4:take:9:item.take:4:dash"));
            runner.Resume(graph, snapshot); runner.Choose("a");
            Assert.That(adapter.Effects, Is.EqualTo(2));
        }

        [Test]
        public void CancelBeforeFirstActionPreventsEffectWithoutRelyingOnDeduplication()
        {
            var adapter = new Adapter();
            var runner = new DialogueRunner(adapter);
            runner.Start(Graph(Line("line", "action"), Action(), End()), DialogueTrigger.Interaction, Context, "fresh");
            Assert.That(runner.Cancel(), Is.True);
            Assert.That(runner.State, Is.EqualTo(DialogueState.Cancelled));
            Assert.That(runner.Advance(), Is.False);
            Assert.That(adapter.Effects, Is.Zero);
            Assert.That(adapter.Deliveries, Is.Empty);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void InvalidSessionIdsFailClearly(string session)
        {
            var runner = new DialogueRunner(new Adapter());
            Assert.That(runner.Start(Graph(Line(), End()), DialogueTrigger.Interaction, Context, session), Is.False);
            Assert.That(runner.Error, Is.EqualTo("A durable session ID is required."));
            Assert.That(runner.Start(Graph(Line(), End()), DialogueTrigger.Interaction, Context, "valid"), Is.True);
        }

        [Test]
        public void NullAdapterAndContextsAreArgumentErrorsWithValidControls()
        {
            Assert.Throws<ArgumentNullException>(() => new DialogueRunner(null));
            var runner = new DialogueRunner(new Adapter());
            var graph = Graph(Line(), End());
            Assert.Throws<ArgumentNullException>(() => runner.CanStart(graph, DialogueTrigger.Interaction, null));
            Assert.Throws<ArgumentNullException>(() => runner.Start(graph, DialogueTrigger.Interaction, null, "s"));
            Assert.That(runner.CanStart(graph, DialogueTrigger.Interaction, Context), Is.True);
            Assert.That(runner.Start(graph, DialogueTrigger.Interaction, Context, "s"), Is.True);
        }

        [TestCase("null")]
        [TestCase("context")]
        [TestCase("branch")]
        [TestCase("action")]
        [TestCase("missing")]
        public void SnapshotNullAndNonwaitingGuardsHaveMatchingControls(string mutation)
        {
            var runner = new DialogueRunner(new Adapter());
            var graph = Graph(Line(), Action(), new DialogueNode { Id = "branch", Kind = DialogueNodeKind.Branch,
                Condition = "ready", Next = "end", Otherwise = "end" }, End());
            runner.Start(graph, DialogueTrigger.Interaction, Context, "s");
            var snapshot = runner.Suspend();
            Assert.That(runner.Resume(graph, snapshot), Is.True);
            snapshot = runner.Suspend();
            switch (mutation) {
                case "null": snapshot = null; break;
                case "context": snapshot.Context = null; break;
                default: snapshot.NodeId = mutation; break;
            }
            Assert.That(runner.Resume(graph, snapshot), Is.False);
            Assert.That(runner.Error, Is.EqualTo(mutation == "null" || mutation == "context"
                ? "Snapshot does not match this graph/schema." : "Snapshot must identify a waiting line or choice."));
        }

        [Test]
        public void ResumeCannotReplaceRunningSessionAndWrongInputKindDoesNothing()
        {
            var runner = new DialogueRunner(new Adapter());
            var graph = Graph(Line(), End());
            runner.Start(graph, DialogueTrigger.Interaction, Context, "s");
            var snapshot = runner.Suspend();
            runner.Resume(graph, snapshot);
            Assert.That(runner.Resume(graph, snapshot), Is.False);
            Assert.That(runner.Choose("any"), Is.False);
            Assert.That(runner.NodeId, Is.EqualTo("line"));
            runner.Cancel();
            graph = Graph(new DialogueNode { Id = "choose", Kind = DialogueNodeKind.Choice, Choices = new[] {
                new DialogueChoice { Id = "yes", TextKey = "yes", Condition = "blocked", Next = "end" } } }, End());
            runner.Start(graph, DialogueTrigger.Interaction, Context, "s2");
            Assert.That(runner.Advance(), Is.False);
            Assert.That(runner.WaitingForChoice, Is.True);
            Assert.That(runner.GetChoices(), Is.Empty);
            Assert.That(runner.NodeId, Is.EqualTo("choose"));
            Assert.That(runner.Cancel(), Is.True);
        }

        [TestCase(long.MaxValue, false)]
        [TestCase(long.MaxValue - 1, true)]
        public void ActionSequenceExhaustionFailsBeforeDelivery(long sequence, bool succeeds)
        {
            var adapter = new Adapter();
            var runner = new DialogueRunner(adapter);
            var graph = Graph(Line("line", "action"), Action(), End());
            runner.Start(graph, DialogueTrigger.Interaction, Context, "s");
            var snapshot = runner.Suspend(); snapshot.ActionSequence = sequence;
            Assert.That(runner.Resume(graph, snapshot), Is.True);
            Assert.That(runner.Advance(), Is.EqualTo(succeeds));
            Assert.That(adapter.Effects, Is.EqualTo(succeeds ? 1 : 0));
            if (!succeeds) Assert.That(runner.Error, Is.EqualTo("Action sequence exhausted."));
        }

        [Test]
        public void ConditionProbeThrowsWithoutChangingStateButStartRecordsException()
        {
            var adapter = new Adapter { ThrowCondition = true };
            var runner = new DialogueRunner(adapter);
            var graph = Graph(Line(), End()); graph.Conditions = new[] { "ready" };
            Assert.Throws<InvalidOperationException>(() => runner.CanStart(graph, DialogueTrigger.Interaction, Context));
            Assert.That(runner.State, Is.EqualTo(DialogueState.Idle));
            Assert.That(runner.Error, Is.Null);
            Assert.That(runner.Start(graph, DialogueTrigger.Interaction, Context, "s"), Is.False);
            Assert.That(runner.Error, Is.EqualTo("Dialogue adapter failed: condition failure"));
            Assert.That(runner.LastException, Is.TypeOf<InvalidOperationException>());
            adapter.ThrowCondition = false; adapter.Condition = true;
            Assert.That(runner.Start(graph, DialogueTrigger.Interaction, Context, "s2"), Is.True);
            Assert.That(runner.LastException, Is.Null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TextAndChoiceProbesPropagateWithoutFaulting(bool choices)
        {
            var adapter = new Adapter();
            var runner = new DialogueRunner(adapter);
            var graph = choices ? Graph(new DialogueNode { Id = "choose", Kind = DialogueNodeKind.Choice, Choices = new[] {
                new DialogueChoice { Id = "yes", TextKey = "yes", Condition = "ready", Next = "end" } } }, End()) : Graph(Line(), End());
            runner.Start(graph, DialogueTrigger.Interaction, Context, "s");
            adapter.ThrowText = true; adapter.Condition = true;
            if (choices) Assert.Throws<InvalidOperationException>(() => runner.GetChoices());
            else Assert.Throws<InvalidOperationException>(() => runner.GetText());
            Assert.That(runner.State, Is.EqualTo(DialogueState.Running));
            Assert.That(runner.Error, Is.Null);
            adapter.ThrowText = false;
            if (choices) {
                adapter.ThrowCondition = true;
                Assert.Throws<InvalidOperationException>(() => runner.GetChoices());
                Assert.That(runner.State, Is.EqualTo(DialogueState.Running));
                adapter.ThrowCondition = false;
                Assert.That(runner.GetChoices().Count, Is.EqualTo(1));
            } else Assert.That(runner.GetText(), Is.EqualTo("localized:dialogue_training_name"));
        }

        [Test]
        public void FingerprintNormalizesNullStringsAcrossUnitySerialization()
        {
            var runner = new DialogueRunner(new Adapter());
            var graph = Graph(Line(), End());
            runner.Start(graph, DialogueTrigger.Interaction, Context, "s");
            var snapshot = runner.Suspend();
            foreach (var node in graph.Nodes) { node.Argument ??= ""; node.Action ??= "";
                node.Condition ??= ""; node.Otherwise ??= ""; node.Next ??= ""; node.TextKey ??= "";
                node.SpeakerId ??= ""; node.ExpressionId ??= ""; }
            Assert.That(runner.Resume(graph, snapshot), Is.True);
        }

        [Test]
        public void StartFreezesValidatedDefinitionBeforeConditionCallbacks()
        {
            var adapter = new Adapter { Condition = true };
            var graph = Graph(Line(), End()); graph.Conditions = new[] { "ready" };
            var context = Context;
            adapter.Callback = () => { graph.Nodes[0].Next = "missing"; context.ActorId = "changed"; };
            var runner = new DialogueRunner(adapter);
            Assert.That(runner.Start(graph, DialogueTrigger.Interaction, context, "s"), Is.True);
            Assert.That(runner.Advance(), Is.True);
            Assert.That(runner.State, Is.EqualTo(DialogueState.Completed));
        }

        [Test]
        public void LocalizedLineAdvancesToCompletionAndCannotAdvanceAgain()
        {
            var runner = new DialogueRunner(new Adapter());
            Assert.That(runner.Start(Graph(Line(), End()), DialogueTrigger.Interaction, Context, "session-1"), Is.True);
            Assert.That(runner.SpeakerId, Is.EqualTo("trainer"));
            Assert.That(runner.GetText(), Is.EqualTo("localized:dialogue_training_name"));
            Assert.That(runner.Advance(), Is.True);
            Assert.That(runner.State, Is.EqualTo(DialogueState.Completed));
            Assert.That(runner.Advance(), Is.False);
            Assert.That(runner.NodeId, Is.Null);
        }

        [TestCase(false, "end")]
        [TestCase(true, "line")]
        public void BranchEvaluatesBothPaths(bool condition, string expected)
        {
            var runner = new DialogueRunner(new Adapter { Condition = condition });
            var graph = Graph(new DialogueNode { Id = "branch", Kind = DialogueNodeKind.Branch,
                Condition = "training.ready", Next = "line", Otherwise = "end" }, Line(), End());
            Assert.That(runner.Start(graph, DialogueTrigger.Automatic, Context, "s"), Is.True);
            Assert.That(runner.State, Is.EqualTo(expected == "end" ? DialogueState.Completed : DialogueState.Running));
            if (condition) Assert.That(runner.NodeId, Is.EqualTo(expected));
        }

        [Test]
        public void ChoiceEligibilityIsRevalidatedAtSelection()
        {
            var adapter = new Adapter { Condition = true };
            var runner = new DialogueRunner(adapter);
            var graph = Graph(new DialogueNode { Id = "choose", Kind = DialogueNodeKind.Choice,
                Choices = new[] { new DialogueChoice { Id = "accept", TextKey = "accept", Condition = "ready", Next = "action" },
                    new DialogueChoice { Id = "leave", TextKey = "leave", Next = "end" } } }, Action(), End());
            Assert.That(runner.Start(graph, DialogueTrigger.Interaction, Context, "s"), Is.True);
            Assert.That(runner.GetChoices().Count, Is.EqualTo(2));
            adapter.Condition = false;
            Assert.That(runner.GetChoices().Count, Is.EqualTo(1));
            Assert.That(runner.Choose("accept"), Is.False);
            Assert.That(runner.Choose("unknown"), Is.False);
            Assert.That(adapter.Effects, Is.Zero);
            Assert.That(runner.Choose("leave"), Is.True);
        }

        [Test]
        public void TriggerMaskAndConditionsHavePairedEligibilityControls()
        {
            var adapter = new Adapter();
            var runner = new DialogueRunner(adapter);
            var graph = Graph(Line(), End());
            graph.Triggers = DialogueTrigger.Automatic;
            graph.Conditions = new[] { "ready" };
            Assert.That(runner.CanStart(graph, DialogueTrigger.Automatic, Context), Is.False);
            Assert.That(runner.Start(graph, DialogueTrigger.Automatic, Context, "s"), Is.False);
            adapter.Condition = true;
            Assert.That(runner.CanStart(graph, DialogueTrigger.Automatic, Context), Is.True);
            Assert.That(runner.Start(graph, DialogueTrigger.Interaction, Context, "s"), Is.False);
            Assert.That(runner.Start(graph, (DialogueTrigger)3, Context, "s"), Is.False);
            Assert.That(runner.Start(graph, DialogueTrigger.Automatic, Context, "s"), Is.True);
            Assert.That(runner.Start(graph, DialogueTrigger.Automatic, Context, "other"), Is.False);
        }

        [Test]
        public void CancelStopsDeliveryAndSuspendRestoresWithoutReplayingPriorAction()
        {
            var adapter = new Adapter();
            var graph = Graph(Action("before", "line"), Line("line", "after"), Action("after"), End());
            var runner = new DialogueRunner(adapter);
            Assert.That(runner.Start(graph, DialogueTrigger.Automatic, Context, "s"), Is.True);
            var snapshot = runner.Suspend();
            Assert.That(snapshot.Context.ActorId, Is.EqualTo("player-1"));
            Assert.That(runner.Advance(), Is.False);
            var resumed = new DialogueRunner(adapter);
            Assert.That(resumed.Resume(graph, snapshot), Is.True);
            Assert.That(adapter.Effects, Is.EqualTo(1));
            Assert.That(resumed.Advance(), Is.True);
            Assert.That(adapter.Effects, Is.EqualTo(2));
            // Restore the same older checkpoint: atomic adapter IDs make the effect exactly once.
            Assert.That(new DialogueRunner(adapter).Resume(graph, snapshot), Is.True);
            var replay = new DialogueRunner(adapter);
            Assert.That(replay.Resume(graph, snapshot), Is.True);
            replay.Advance();
            Assert.That(adapter.Effects, Is.EqualTo(2));
            Assert.That(runner.Resume(graph, snapshot), Is.True);
            Assert.That(runner.Cancel(), Is.True);
            Assert.That(runner.Cancel(), Is.False);
            Assert.That(runner.Advance(), Is.False);
            Assert.That(adapter.Effects, Is.EqualTo(2));
        }

        [Test]
        public void InteractiveLoopsHaveDistinctActionDeliveries()
        {
            var adapter = new Adapter();
            var runner = new DialogueRunner(adapter);
            runner.Start(Graph(Line("line", "action"), Action("action", "line")), DialogueTrigger.Interaction, Context, "s");
            Assert.That(runner.Advance(), Is.True);
            Assert.That(runner.Advance(), Is.True);
            Assert.That(adapter.Effects, Is.EqualTo(2));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RejectedOrThrowingActionFaultsWithoutAutomaticRetry(bool throwing)
        {
            var adapter = new Adapter { Reject = !throwing, Throw = throwing };
            var runner = new DialogueRunner(adapter);
            Assert.That(runner.Start(Graph(Action(), End()), DialogueTrigger.Automatic, Context, "s"), Is.False);
            Assert.That(runner.State, Is.EqualTo(DialogueState.Faulted));
            Assert.That(runner.Error, Is.EqualTo(throwing ? "Dialogue adapter failed: adapter exception" : "Action rejected at action"));
            if (throwing) Assert.That(runner.LastException, Is.TypeOf<InvalidOperationException>());
            Assert.That(runner.Advance(), Is.False);
            Assert.That(adapter.Effects, Is.Zero);
        }

        [Test]
        public void AutomaticCycleFaultsWhileFiniteActionChainWorks()
        {
            var adapter = new Adapter();
            var runner = new DialogueRunner(adapter);
            Assert.That(runner.Start(Graph(Action("action", "action")), DialogueTrigger.Automatic, Context, "s"), Is.False);
            Assert.That(runner.Error, Is.EqualTo("Automatic transition cycle."));
            Assert.That(adapter.Effects, Is.Zero);
            Assert.That(runner.Start(Graph(Action("a", "b"), Action("b", "a")), DialogueTrigger.Automatic, Context, "s"), Is.False);
            Assert.That(adapter.Effects, Is.Zero);
            var branchCycle = Graph(new DialogueNode { Id = "branch", Kind = DialogueNodeKind.Branch,
                Condition = "ready", Next = "end", Otherwise = "action" }, Action("action", "branch"), End());
            Assert.That(DialogueRunner.Validate(branchCycle, out var error), Is.False);
            Assert.That(error, Is.EqualTo("Automatic transition cycle."));
            Assert.That(runner.Start(Graph(Action(), End()), DialogueTrigger.Automatic, Context, "s2"), Is.True);
            Assert.That(adapter.Effects, Is.EqualTo(1));
        }

        [Test]
        public void AdapterCallbacksCannotReenterTransitionsOrCancel()
        {
            var adapter = new Adapter();
            var runner = new DialogueRunner(adapter);
            adapter.Callback = () => { Assert.That(runner.Cancel(), Is.False); Assert.That(runner.Advance(), Is.False);
                Assert.That(runner.Suspend(), Is.Null);
                Assert.That(runner.Start(Graph(Line(), End()), DialogueTrigger.Interaction, Context, "nested"), Is.False);
                Assert.That(runner.CanStart(Graph(Line(), End()), DialogueTrigger.Interaction, Context), Is.False); };
            Assert.That(runner.Start(Graph(Action("a", "line"), Line(), End()), DialogueTrigger.Interaction, Context, "s"), Is.True);
            Assert.That(runner.GetText(), Is.Not.Null);
            Assert.That(runner.State, Is.EqualTo(DialogueState.Running));
            Assert.That(adapter.Effects, Is.EqualTo(1));
        }

        [Test]
        public void AuthoredGraphAndContextMutationsCannotChangeRunningSession()
        {
            var graph = Graph(Line("line", "action"), Action(), End());
            var context = Context;
            var adapter = new Adapter();
            var runner = new DialogueRunner(adapter);
            runner.Start(graph, DialogueTrigger.Interaction, context, "s");
            graph.Nodes[0].Next = "missing";
            context.ActorId = "changed";
            Assert.That(runner.Suspend().Context.ActorId, Is.EqualTo("player-1"));
            graph.Nodes[0].Next = "action";
            runner.Start(graph, DialogueTrigger.Interaction, Context, "s2");
            graph.Nodes[0].Next = "missing";
            Assert.That(runner.Advance(), Is.True);
            Assert.That(adapter.Effects, Is.EqualTo(1));
        }

        [TestCase("schema")]
        [TestCase("entry")]
        [TestCase("duplicate")]
        [TestCase("next")]
        [TestCase("speaker")]
        [TestCase("text")]
        [TestCase("triggers")]
        [TestCase("revision")]
        [TestCase("condition")]
        [TestCase("kind")]
        [TestCase("null-nodes")]
        [TestCase("null-node")]
        [TestCase("empty-id")]
        [TestCase("null-conditions")]
        [TestCase("zero-triggers")]
        [TestCase("missing-kind")]
        public void InvalidGraphsFailBeforeAnyEffectsWithValidNegativeControl(string mutation)
        {
            var graph = Graph(Line(), End());
            Assert.That(DialogueRunner.Validate(graph, out _), Is.True);
            switch (mutation)
            {
                case "schema": graph.Schema = 2; break;
                case "entry": graph.Entry = "absent"; break;
                case "duplicate": graph.Nodes[1].Id = "line"; break;
                case "next": graph.Nodes[0].Next = "absent"; break;
                case "speaker": graph.Nodes[0].SpeakerId = ""; break;
                case "text": graph.Nodes[0].TextKey = ""; break;
                case "triggers": graph.Triggers = (DialogueTrigger)8; break;
                case "revision": graph.Revision = null; break;
                case "condition": graph.Conditions = new[] { "" }; break;
                case "kind": graph.Nodes[0].Kind = (DialogueNodeKind)99; break;
                case "null-nodes": graph.Nodes = null; break;
                case "null-node": graph.Nodes[0] = null; break;
                case "empty-id": graph.Id = " "; break;
                case "null-conditions": graph.Conditions = null; break;
                case "zero-triggers": graph.Triggers = 0; break;
                case "missing-kind": graph.Nodes[0].Kind = DialogueNodeKind.Invalid; break;
            }
            var adapter = new Adapter();
            var runner = new DialogueRunner(adapter);
            Assert.That(DialogueRunner.Validate(graph, out var validationError), Is.False);
            var expected = mutation switch {
                "schema" => "Unsupported dialogue graph schema.",
                "entry" => "Dangling graph entry.",
                "duplicate" or "kind" or "missing-kind" or "null-node" => "Invalid or duplicate node ID/kind.",
                "next" => "Dangling transition at line",
                "speaker" or "text" => "Line requires speaker/text IDs at line",
                "condition" => "Empty trigger condition.",
                _ => "Graph requires IDs, revision, nodes, conditions and supported triggers." };
            Assert.That(validationError, Is.EqualTo(expected));
            Assert.That(runner.Start(graph, DialogueTrigger.Interaction, Context, "s"), Is.False);
            Assert.That(runner.Error, Is.EqualTo(expected));
            Assert.That(adapter.Effects, Is.Zero);
        }

        [TestCase("branch-condition")]
        [TestCase("branch-target")]
        [TestCase("action-id")]
        [TestCase("empty-choices")]
        [TestCase("null-choice")]
        [TestCase("duplicate-choice")]
        [TestCase("choice-target")]
        [TestCase("choice-text")]
        public void SpecializedNodeGuardsRejectMalformedDefinitions(string mutation)
        {
            var choice = new DialogueNode { Id = "choose", Kind = DialogueNodeKind.Choice, Choices = new[] {
                new DialogueChoice { Id = "yes", TextKey = "yes", Next = "end" },
                new DialogueChoice { Id = "no", TextKey = "no", Next = "end" } } };
            var branch = new DialogueNode { Id = "branch", Kind = DialogueNodeKind.Branch, Condition = "ready",
                Next = "choose", Otherwise = "end" };
            var action = Action("action", "branch");
            var graph = Graph(action, branch, choice, End());
            Assert.That(DialogueRunner.Validate(graph, out _), Is.True);
            switch (mutation)
            {
                case "branch-condition": branch.Condition = ""; break;
                case "branch-target": branch.Otherwise = "missing"; break;
                case "action-id": action.Action = ""; break;
                case "empty-choices": choice.Choices = Array.Empty<DialogueChoice>(); break;
                case "null-choice": choice.Choices[0] = null; break;
                case "duplicate-choice": choice.Choices[1].Id = "yes"; break;
                case "choice-target": choice.Choices[0].Next = "missing"; break;
                case "choice-text": choice.Choices[0].TextKey = " "; break;
            }
            Assert.That(DialogueRunner.Validate(graph, out var error), Is.False);
            Assert.That(error, Is.EqualTo(mutation switch {
                "branch-condition" or "branch-target" => "Branch requires condition and both targets at branch",
                "action-id" => "Action ID required at action",
                "empty-choices" => "Choice options required at choose",
                _ => "Invalid choice ID/text/target at choose" }));
        }

        [TestCase("schema")]
        [TestCase("graph")]
        [TestCase("revision")]
        [TestCase("node")]
        [TestCase("sequence")]
        [TestCase("session")]
        [TestCase("content")]
        public void MismatchedSnapshotsFailWithMatchingSnapshotNegativeControl(string mutation)
        {
            var graph = Graph(Line(), End());
            var runner = new DialogueRunner(new Adapter());
            runner.Start(graph, DialogueTrigger.Interaction, Context, "s");
            var snapshot = runner.Suspend();
            Assert.That(runner.Resume(graph, snapshot), Is.True);
            snapshot = runner.Suspend();
            switch (mutation)
            {
                case "schema": snapshot.Schema = 99; break;
                case "graph": snapshot.GraphId = "other"; break;
                case "revision": snapshot.Revision = "other"; break;
                case "node": snapshot.NodeId = "end"; break;
                case "sequence": snapshot.ActionSequence = -1; break;
                case "session": snapshot.SessionId = ""; break;
                case "content": graph.Nodes[0].TextKey = "changed-without-revision"; break;
            }
            Assert.That(runner.Resume(graph, snapshot), Is.False);
            Assert.That(runner.Error, Is.EqualTo(mutation == "node"
                ? "Snapshot must identify a waiting line or choice." : "Snapshot does not match this graph/schema."));
        }
    }
}
