using UnityEngine;

namespace Moonlight.Interaction
{
    public interface IInteractable
    {
        void Interact(GameObject interactor);
    }
}