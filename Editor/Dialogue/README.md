# Dialogue authoring (ARC-313)

Open **Tools > Moonlight > Dialogue Graph**, then **New**, or create a
**Moonlight > Dialogue Document** asset. Double-click a document to reopen it.
No packages are added: this editor uses public IMGUI/Handles, SerializedObject,
AssetDatabase and Undo APIs already installed with Unity.

The left canvas adds and drags nodes. Select a node, then choose its outgoing
targets in the middle inspector; the canvas draws the resulting connections.
Set an entry node explicitly. Node and choice IDs are generated once and read-only
in the editor. Deleting a node leaves incoming targets visible as errors so authors
can deliberately reconnect them. Undo/redo includes content, links and layout.

Expand **Participants, text and allowed identifiers** to author speaker IDs and
optional preview portraits, localization keys with preview text, condition IDs,
allowed action IDs and intentional loop policies. Select **Illustrated Conversation**
or **Soft Bark** explicitly. Both edit the unchanged production schema-1 graph;
mode, portraits, preview text, layout and loop policy are editor metadata. The
runtime JSON export contains only the production Graph DTO. Consumers must provide
their own presentation and localization/action adapters; exporting does not install
an action handler or create NPC/tutorial content.

An optional editor host can subscribe to `DialogueGraphWindow.PublishRequested`.
Arclighters' **Publish game asset** button creates or updates its existing
`DialogueGraphAsset` directly after validation; it preserves the destination asset
GUID and supports Undo/Redo. It never overwrites an asset of a different type.

Validation includes the production runner's schema/target/automatic-cycle rules,
catalogue membership, reachability and intentional interactive loop exits. Every
node participating in a cycle needs a nonempty exit policy and a route to End.
Loop choices need an unconditional option outside the cycle. A policy can never
waive an automatic-only cycle. Structural reachability does not prove that arbitrary
game conditions will permit an exit; inspect fixture conditions in preview.

Preview uses the production `DialogueRunner` and a sandbox `IDialogueAdapter`.
Toggle fixture conditions, pick the trigger, start, advance or choose an option.
Allowed actions appear in the action log and never reach gameplay handlers.
Graph edits or undo stop preview; canvas movement alone preserves runtime revision.
**Validate** reports errors; runtime JSON export refuses invalid documents. **Save**
persists assets, including incomplete work, so designers can resume later.

Focused fixture: `DialogueAuthoringTests`; it covers invalid controls, production
path parity, isolated preview action delivery, native save/reopen and Undo/Redo.
Human usability and final portrait presentation acceptance are separate from these
automated checks.
