using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Moonlight.Interaction.Dialogue
{
    /// <summary>Synchronous local orchestration. Only Line and Choice nodes wait for input.
    /// Callers supply a unique, durable session ID. No gameplay state is mutated by this runner.</summary>
    public sealed class DialogueRunner : IDialogueService
    {
        readonly IDialogueAdapter _adapter;
        readonly Dictionary<string, DialogueNode> _nodes = new(StringComparer.Ordinal);
        DialogueGraph _graph;
        DialogueContext _context;
        DialogueNode _node;
        string _session;
        string _fingerprint;
        long _sequence;
        bool _busy;
        public DialogueState State { get; private set; }
        public string Error { get; private set; }
        public string NodeId => State == DialogueState.Running ? _node?.Id : null;
        public string SpeakerId => State == DialogueState.Running ? _node?.SpeakerId : null;
        public string Text
        {
            get
            {
                if (_busy || State != DialogueState.Running || _node.Kind != DialogueNodeKind.Line) return null;
                _busy = true;
                try { return _adapter.ResolveText(_node.TextKey); }
                catch (Exception ex) { Fail("Dialogue text adapter failed: " + ex.Message); return null; }
                finally { _busy = false; }
            }
        }

        public DialogueRunner(IDialogueAdapter adapter) => _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));

        public bool CanStart(DialogueGraph graph, DialogueTrigger trigger, DialogueContext context)
        {
            if (_busy || State == DialogueState.Running || !Validate(graph, out _)) return false;
            _busy = true;
            try
            {
                if (context == null || (trigger != DialogueTrigger.Interaction && trigger != DialogueTrigger.Automatic)
                    || (graph.Triggers & trigger) == 0) return false;
                foreach (var condition in graph.Conditions)
                    if (!_adapter.IsConditionMet(condition, Copy(context))) return false;
                return true;
            }
            catch (Exception ex) { return Fail("Dialogue condition adapter failed: " + ex.Message); }
            finally { _busy = false; }
        }

        public bool Start(DialogueGraph graph, DialogueTrigger trigger, DialogueContext context, string sessionId)
        {
            if (_busy || State == DialogueState.Running) return false;
            if (!Validate(graph, out var error)) return Fail(error);
            if (string.IsNullOrWhiteSpace(sessionId)) return Fail("A durable session ID is required.");
            _busy = true;
            try
            {
                // CanStart is intentionally not used here: the reentrancy guard must already hold
                // while user adapters evaluate start conditions.
                if (context == null || (trigger != DialogueTrigger.Interaction && trigger != DialogueTrigger.Automatic)
                    || (graph.Triggers & trigger) == 0) return false;
                foreach (var condition in graph.Conditions)
                    if (!_adapter.IsConditionMet(condition, Copy(context))) return false;
                Load(graph, context, sessionId, 0);
                return Enter(_graph.Entry);
            }
            catch (Exception ex) { return Fail("Dialogue adapter failed: " + ex.Message); }
            finally { _busy = false; }
        }

        public bool Advance() => Transition(() => _node.Kind == DialogueNodeKind.Line && Enter(_node.Next));

        public IReadOnlyList<DialogueOption> GetChoices()
        {
            var options = new List<DialogueOption>();
            if (_busy || State != DialogueState.Running || _node.Kind != DialogueNodeKind.Choice) return options;
            _busy = true;
            try
            {
                foreach (var choice in _node.Choices)
                    if (Eligible(choice.Condition)) options.Add(new DialogueOption(choice.Id, _adapter.ResolveText(choice.TextKey)));
            }
            catch (Exception ex) { Fail("Dialogue choice adapter failed: " + ex.Message); options.Clear(); }
            finally { _busy = false; }
            return options;
        }

        public bool Choose(string choiceId) => Transition(() =>
        {
            if (_node.Kind != DialogueNodeKind.Choice) return false;
            foreach (var choice in _node.Choices)
                if (choice.Id == choiceId && Eligible(choice.Condition)) return Enter(choice.Next);
            return false;
        });

        public bool Cancel()
        {
            if (_busy || State != DialogueState.Running) return false;
            State = DialogueState.Cancelled;
            _node = null;
            return true;
        }

        public DialogueSnapshot Suspend()
        {
            if (_busy || State != DialogueState.Running) return null;
            var snapshot = new DialogueSnapshot { GraphId = _graph.Id, Revision = _graph.Revision,
                Fingerprint = _fingerprint, SessionId = _session, NodeId = _node.Id,
                ActionSequence = _sequence, Context = Copy(_context) };
            State = DialogueState.Suspended;
            _node = null;
            return snapshot;
        }

        public bool Resume(DialogueGraph graph, DialogueSnapshot snapshot)
        {
            if (_busy || State == DialogueState.Running) return false;
            if (!Validate(graph, out var error)) return Fail(error);
            if (snapshot == null || snapshot.Schema != DialogueGraph.CurrentSchema || snapshot.Context == null
                || string.IsNullOrWhiteSpace(snapshot.SessionId) || snapshot.ActionSequence < 0
                || snapshot.GraphId != graph.Id || snapshot.Revision != graph.Revision
                || snapshot.Fingerprint != Fingerprint(graph)) return Fail("Snapshot does not match this graph/schema.");
            Load(graph, snapshot.Context, snapshot.SessionId, snapshot.ActionSequence);
            if (!_nodes.TryGetValue(snapshot.NodeId ?? "", out var node)
                || (node.Kind != DialogueNodeKind.Line && node.Kind != DialogueNodeKind.Choice))
                return Fail("Snapshot must identify a waiting line or choice.");
            _node = node;
            return true;
        }

        bool Transition(Func<bool> step)
        {
            if (_busy || State != DialogueState.Running) return false;
            _busy = true;
            try { return step(); }
            catch (Exception ex) { return Fail("Dialogue adapter failed: " + ex.Message); }
            finally { _busy = false; }
        }

        bool Enter(string id)
        {
            // Automatic-only cycles fail without hanging the main thread. Interactive loops are valid.
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (true)
            {
                if (!visited.Add(id)) return Fail("Automatic transition cycle at " + id);
                _node = _nodes[id];
                switch (_node.Kind)
                {
                    case DialogueNodeKind.Line:
                    case DialogueNodeKind.Choice: return true;
                    case DialogueNodeKind.End: State = DialogueState.Completed; _node = null; return true;
                    case DialogueNodeKind.Branch: id = Eligible(_node.Condition) ? _node.Next : _node.Otherwise; break;
                    case DialogueNodeKind.Action:
                        var delivery = _session.Length + ":" + _session + ":" + _sequence;
                        if (_sequence == long.MaxValue) return Fail("Action sequence exhausted.");
                        ++_sequence; // reserve before delivery; failed actions fault, never retry implicitly
                        if (!_adapter.TryExecute(_node.Action, _node.Argument, delivery, Copy(_context)))
                            return Fail("Action rejected at " + _node.Id);
                        id = _node.Next;
                        break;
                }
            }
        }

        bool Eligible(string condition) => string.IsNullOrEmpty(condition) || _adapter.IsConditionMet(condition, Copy(_context));
        bool Fail(string error) { Error = error; State = DialogueState.Faulted; _node = null; return false; }
        static DialogueContext Copy(DialogueContext context) => new(context.ActorId, context.TargetId);

        void Load(DialogueGraph graph, DialogueContext context, string session, long sequence)
        {
            // Isolate running sessions from editor and consumer mutations of serialized DTOs.
            _graph = Clone(graph);
            _fingerprint = Fingerprint(_graph);
            _nodes.Clear();
            foreach (var node in _graph.Nodes) _nodes.Add(node.Id, node);
            _context = Copy(context); _session = session; _sequence = sequence;
            State = DialogueState.Running; Error = null;
        }

        public static bool Validate(DialogueGraph graph, out string error)
        {
            error = null;
            if (graph == null || graph.Schema != DialogueGraph.CurrentSchema) error = "Unsupported dialogue graph schema.";
            else if (string.IsNullOrWhiteSpace(graph.Id) || string.IsNullOrWhiteSpace(graph.Revision)
                || string.IsNullOrWhiteSpace(graph.Entry) || graph.Nodes == null || graph.Nodes.Length == 0
                || graph.Conditions == null || graph.Triggers == 0 || ((int)graph.Triggers & ~3) != 0)
                error = "Graph requires IDs, revision, nodes, conditions and supported triggers.";
            if (error != null) return false;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var condition in graph.Conditions)
                if (string.IsNullOrWhiteSpace(condition)) { error = "Empty trigger condition."; return false; }
            foreach (var node in graph.Nodes)
                if (node == null || string.IsNullOrWhiteSpace(node.Id) || !ids.Add(node.Id)
                    || !Enum.IsDefined(typeof(DialogueNodeKind), node.Kind))
                { error = "Invalid or duplicate node ID/kind."; return false; }
            if (!ids.Contains(graph.Entry)) { error = "Dangling graph entry."; return false; }
            foreach (var node in graph.Nodes)
            {
                if (node.Kind != DialogueNodeKind.End && node.Kind != DialogueNodeKind.Choice
                    && !ids.Contains(node.Next ?? "")) error = "Dangling transition at " + node.Id;
                else if (node.Kind == DialogueNodeKind.Line && (string.IsNullOrWhiteSpace(node.SpeakerId)
                    || string.IsNullOrWhiteSpace(node.TextKey))) error = "Line requires speaker/text IDs at " + node.Id;
                else if (node.Kind == DialogueNodeKind.Branch && (string.IsNullOrWhiteSpace(node.Condition)
                    || !ids.Contains(node.Otherwise ?? ""))) error = "Branch requires condition and both targets at " + node.Id;
                else if (node.Kind == DialogueNodeKind.Action && string.IsNullOrWhiteSpace(node.Action))
                    error = "Action ID required at " + node.Id;
                else if (node.Kind == DialogueNodeKind.Choice)
                {
                    var choices = new HashSet<string>(StringComparer.Ordinal);
                    if (node.Choices == null || node.Choices.Length == 0) error = "Choice options required at " + node.Id;
                    else foreach (var choice in node.Choices)
                        if (choice == null || string.IsNullOrWhiteSpace(choice.Id) || !choices.Add(choice.Id)
                            || string.IsNullOrWhiteSpace(choice.TextKey) || !ids.Contains(choice.Next ?? ""))
                        { error = "Invalid choice ID/text/target at " + node.Id; break; }
                }
                if (error != null) return false;
            }
            return true;
        }

        static DialogueGraph Clone(DialogueGraph source)
        {
            var copy = new DialogueGraph { Schema = source.Schema, Id = source.Id, Revision = source.Revision,
                Entry = source.Entry, Triggers = source.Triggers, Conditions = (string[])source.Conditions.Clone(),
                Nodes = new DialogueNode[source.Nodes.Length] };
            for (var i = 0; i < copy.Nodes.Length; ++i)
            {
                var n = source.Nodes[i];
                var choices = n.Choices ?? Array.Empty<DialogueChoice>();
                var c = new DialogueNode { Id = n.Id, Kind = n.Kind, SpeakerId = n.SpeakerId, TextKey = n.TextKey,
                    Next = n.Next, Otherwise = n.Otherwise, Condition = n.Condition, Action = n.Action,
                    Argument = n.Argument, Choices = new DialogueChoice[choices.Length] };
                for (var j = 0; j < choices.Length; ++j)
                {
                    var choice = choices[j];
                    if (choice != null) c.Choices[j] = new DialogueChoice { Id = choice.Id,
                        TextKey = choice.TextKey, Condition = choice.Condition, Next = choice.Next };
                }
                copy.Nodes[i] = c;
            }
            return copy;
        }

        static string Fingerprint(DialogueGraph graph)
        {
            using var stream = new MemoryStream();
            using (var w = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                void S(string s) { w.Write(s != null); if (s != null) w.Write(s); }
                w.Write(graph.Schema); S(graph.Id); S(graph.Revision); S(graph.Entry); w.Write((int)graph.Triggers);
                w.Write(graph.Conditions.Length); foreach (var condition in graph.Conditions) S(condition);
                w.Write(graph.Nodes.Length);
                foreach (var node in graph.Nodes)
                {
                    S(node.Id); w.Write((int)node.Kind); S(node.SpeakerId); S(node.TextKey); S(node.Next);
                    S(node.Condition); S(node.Otherwise); S(node.Action); S(node.Argument);
                    var choices = node.Choices ?? Array.Empty<DialogueChoice>();
                    w.Write(choices.Length);
                    foreach (var choice in choices)
                    {
                        w.Write(choice != null);
                        if (choice != null) { S(choice.Id); S(choice.TextKey); S(choice.Condition); S(choice.Next); }
                    }
                }
            }
            using var sha = SHA256.Create();
            return Convert.ToBase64String(sha.ComputeHash(stream.ToArray()));
        }
    }
}
