# 👆 Moonlight.Interaction

**Moonlight.Interaction** is a flexible system for handling player interactions with world objects. It decouples the "Interactor" from the "Interactable" and supports complex interaction options, validation, and UI feedback.

## ✨ Features

- **IInteractable**: Standard interface for any object that can be interacted with.
- **Interaction Context**: Passes relevant data (Player, Target) to interaction logic.
- **Proximity Prompts**: (Implied) System for displaying UI when close to interactables.
- **Glyph System**: `KeyGlyphLibrary` maps input actions to visual icons (e.g., "Press [E] to Interact").
- **Modular Strategies**: `IInteractionStrategy` allows for different interaction behaviors (Hold, Press, Multi-option).

## 📦 Installation & Dependencies

Ensure your project has the following dependencies installed:

- **Zenject** (Extenject)
- **R3** (Reactive Extensions)
- **DOTween**
- **TextMeshPro**
- **Moonlight.Core**
- **Moonlight.UI**
- **Moonlight.Localization**

## 🚀 Getting Started

### 1. Dependency Injection
Install the `InteractableModule` in your ProjectContext.

```csharp
Container.Install<InteractableModule>();
```

### 2. Configuration
- Create a `KeyGlyphLibrary` ScriptableObject to define your input icons.
- Assign the library to the `InteractableModule` installer.

### 3. Creating an Interactable
Implement `IInteractable` on a MonoBehaviour.

```csharp
public class Chest : MonoBehaviour, IInteractable
{
    public void Interact(GameObject interactor)
    {
        Debug.Log("Chest opened!");
        // Grant loot, play animation, etc.
    }
}
```

### 4. Triggering Interaction
The `InteractableController` (or your player controller) detects interactables (e.g., via Trigger Collider or Raycast) and calls `Interact()`.

## 🧩 Architecture

- **`IInteractable`**: The core contract.
- **`InteractionContext`**: Data packet containing the `Player` (who initiated) and `Target` (what is being interacted with).
- **`InteractableService`**: Central manager for interaction state (currently minimal).
- **`InteractionGlyphLibrary`**: Visual configuration for input prompts.

## 📂 Folder Structure

- **Runtime**: Core scripts.
