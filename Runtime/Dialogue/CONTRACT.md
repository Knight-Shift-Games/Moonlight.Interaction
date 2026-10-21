# Dialogue schema 1 (ARC-307)

`Moonlight.Interaction.Dialogue` is a separate, engine-free assembly inside the Interaction
module. It references no game, Quantum, localization, UI or dependency-injection assembly.
`Moonlight.Interaction.IDialogueService` replaces the former unused empty marker with the
runner contract. Consumers must reference `Moonlight.Interaction.Dialogue` explicitly.

Graph DTOs use public serializable fields and parameterless constructors. Persist schema,
graph ID, revision, stable node/choice IDs, speaker IDs and localization keys. Never regenerate
IDs when editing or reordering. Nodes are Line, Choice, Branch, Action or End. Line/Action
use Next; Branch uses Condition, Next and Otherwise; Choice uses stable option IDs, TextKey,
optional Condition and Next. Every target must exist. End is explicit. Schema changes require
a migration; this runner rejects unsupported versions instead of guessing. The game wraps
this data in DialogueGraphAsset; authoring can build DTOs without Unity.

Start validates the entire graph before calling adapters. Interaction and Automatic trigger
flags both use graph-level Conditions. Choices recheck eligibility when chosen. Lines and
choices wait for explicit input; branches/actions advance synchronously. Automatic-only
cycles fault; interactive loops are supported. Running sessions clone input data. Reentrant
adapter calls cannot advance, cancel, suspend or replace a session.

Pass a **globally unique durable session ID** for every new conversation. Reusing an ID means
replaying the same conversation. Each action gets a session/sequence delivery ID. The action
adapter must atomically deduplicate that ID with its validated effect, returning success for
an already-applied delivery. Persistent rewards need server-side idempotency; a process-local
set is insufficient. Exactly-once external effects cannot be guaranteed by a local runner
alone. Missing handlers, rejected effects and adapter exceptions stop the run as Faulted;
there is no implicit retry. The game adapter passes only explicitly registered action IDs to
validated handlers, which own account/entity authority and gameplay rules.

Suspend captures a waiting Line/Choice, action sequence, context and graph fingerprint,
then closes the running session. Resume restores that wait without replaying prior actions
or re-evaluating start conditions. It accepts only matching schema/ID/revision/content.
Snapshots are **trusted application save state**, not client authority or signed network
messages. The game must authorize stored actor/target identity before restoring a save.
Cancel ends locally and prevents future delivery; it does not undo effects already committed.

IDialogueAdapter resolves localized text and conditions; presentation polls State, Text,
SpeakerId and GetChoices. A game controller may acquire a presentation lease around Start
and dispose it on terminal/suspended states. The core changes neither game input nor time,
and is equally usable for barks. DialogueModule binds the game adapter and runner; install
it in the owning scene/ProjectContext when adding the first dialogue consumer. No authored
NPC conversations, editor UI or portrait stage are part of this foundation.
