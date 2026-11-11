using UnityEngine;

public interface IInteractable
{
    string Prompt(PlayerInteractor player);
    bool CanInteract(PlayerInteractor player);
    void Interact(PlayerInteractor player);
    Transform transform { get; }
}

