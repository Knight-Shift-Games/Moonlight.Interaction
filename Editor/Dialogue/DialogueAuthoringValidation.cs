using System;
using System.Collections.Generic;

namespace Moonlight.Interaction.Dialogue.Editor
{
    public static class DialogueAuthoringValidation
    {
        public static List<string> Validate(DialogueDocument document)
        {
            var errors = new List<string>();
            if (document == null) { errors.Add("Choose a dialogue document."); return errors; }
            var graph = document.Graph;
            if (!DialogueRunner.Validate(graph, out var runtimeError)) errors.Add(runtimeError);
            if (graph?.Nodes == null) return errors;
            if (!Enum.IsDefined(typeof(DialoguePresentation), document.Presentation)) errors.Add("Choose a supported presentation mode.");
            var participants = Catalog(document.Participants, p => p?.Id, "participant", errors);
            var texts = Catalog(document.Texts, t => t?.Key, "localization key", errors);
            var conditions = Catalog(document.AllowedConditions, s => s, "condition", errors);
            var actions = Catalog(document.AllowedActions, s => s, "action", errors);
            void Condition(string key, string owner) { if (!string.IsNullOrEmpty(key) && !conditions.Contains(key)) errors.Add(owner + ": condition is not allowed: " + key); }
            foreach (var key in graph.Conditions ?? Array.Empty<string>()) Condition(key, "Graph");
            var nodes = new Dictionary<string, DialogueNode>(StringComparer.Ordinal);
            foreach (var node in graph.Nodes)
            {
                if (node == null || string.IsNullOrWhiteSpace(node.Id)) continue;
                if (!nodes.ContainsKey(node.Id)) nodes.Add(node.Id, node);
                if (node.Kind == DialogueNodeKind.Line)
                {
                    if (!participants.Contains(node.SpeakerId ?? "")) errors.Add(node.Id + ": missing participant: " + node.SpeakerId);
                    if (!texts.Contains(node.TextKey ?? "")) errors.Add(node.Id + ": missing localization key: " + node.TextKey);
                }
                if (node.Kind == DialogueNodeKind.Branch) Condition(node.Condition, node.Id);
                if (node.Kind == DialogueNodeKind.Action && !actions.Contains(node.Action ?? "")) errors.Add(node.Id + ": action is not allowed: " + node.Action);
                if (node.Kind == DialogueNodeKind.Choice)
                {
                    var choiceIds = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var choice in node.Choices ?? Array.Empty<DialogueChoice>())
                    {
                        if (choice == null) continue;
                        if (!choiceIds.Add(choice.Id ?? "")) errors.Add(node.Id + ": duplicate choice ID: " + choice.Id);
                        if (!Array.Exists(graph.Nodes, target => target != null && target.Id == choice.Next)) errors.Add(node.Id + "/" + choice.Id + ": missing choice target: " + choice.Next);
                        if (!texts.Contains(choice.TextKey ?? "")) errors.Add(node.Id + ": missing choice localization key: " + choice.TextKey);
                        Condition(choice.Condition, node.Id);
                    }
                }
            }
            // Structural checks do not execute adapters or assume which conditions will hold.
            var reachable = Reach(graph.Entry, nodes);
            foreach (var id in nodes.Keys) if (nodes.ContainsKey(graph.Entry ?? "") && !reachable.Contains(id)) errors.Add(id + ": unreachable from entry.");
            var policies = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var policy in document.Loops ?? Array.Empty<DialogueLoopPolicy>())
            {
                if (policy == null) { errors.Add("Missing loop policy."); continue; }
                if (string.IsNullOrWhiteSpace(policy.NodeId)) { errors.Add("Loop policy requires a node ID."); continue; }
                if (!nodes.ContainsKey(policy.NodeId)) { errors.Add("Loop policy references missing node: " + policy.NodeId); continue; }
                if (policies.ContainsKey(policy.NodeId)) { errors.Add("Duplicate loop policy: " + policy.NodeId); continue; }
                if (string.IsNullOrWhiteSpace(policy.ExitPolicy)) { errors.Add(policy.NodeId + ": loop policy requires an exit description."); continue; }
                policies.Add(policy.NodeId, policy.ExitPolicy);
            }
            // For every cyclic node require an explicit policy and a structural route to End.
            // Runtime separately rejects automatic-only cycles, even when a policy is supplied.
            foreach (var pair in nodes)
            {
                var cycle = false;
                foreach (var target in Targets(pair.Value)) if (Reach(target, nodes).Contains(pair.Key)) { cycle = true; break; }
                if (!cycle) continue;
                if (!policies.ContainsKey(pair.Key)) errors.Add(pair.Key + ": intentional loop requires an exit policy.");
                var hasEnd = false;
                foreach (var target in Reach(pair.Key, nodes)) if (nodes[target].Kind == DialogueNodeKind.End) { hasEnd = true; break; }
                if (!hasEnd) errors.Add(pair.Key + ": loop has no route to End.");
                if (pair.Value.Kind == DialogueNodeKind.Choice)
                {
                    var unconditionalExit = false;
                    foreach (var choice in pair.Value.Choices ?? Array.Empty<DialogueChoice>())
                        if (choice != null && string.IsNullOrEmpty(choice.Condition)
                            && !Reach(choice.Next, nodes).Contains(pair.Key)) unconditionalExit = true;
                    if (!unconditionalExit) errors.Add(pair.Key + ": loop choice requires an unconditional exit outside the cycle.");
                }
            }
            return errors;
        }

        static HashSet<string> Catalog<T>(T[] entries, Func<T, string> key, string label, List<string> errors)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (entries == null) { errors.Add("Missing " + label + " catalogue."); return set; }
            foreach (var entry in entries)
            { var id = key(entry); if (string.IsNullOrWhiteSpace(id)) errors.Add("Empty " + label + "."); else if (!set.Add(id)) errors.Add("Duplicate " + label + ": " + id); }
            return set;
        }
        public static IEnumerable<string> Targets(DialogueNode node)
        {
            if (node.Kind == DialogueNodeKind.Choice)
            { foreach (var choice in node.Choices ?? Array.Empty<DialogueChoice>()) if (choice != null) yield return choice.Next; }
            else if (node.Kind != DialogueNodeKind.End)
            { yield return node.Next; if (node.Kind == DialogueNodeKind.Branch) yield return node.Otherwise; }
        }
        static HashSet<string> Reach(string start, Dictionary<string, DialogueNode> nodes)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal); var pending = new Stack<string>();
            if (start != null) pending.Push(start);
            while (pending.Count > 0)
            {
                var id = pending.Pop();
                if (id == null || !nodes.TryGetValue(id, out var node) || !visited.Add(id)) continue;
                foreach (var next in Targets(node)) if (next != null) pending.Push(next);
            }
            return visited;
        }
    }
}
