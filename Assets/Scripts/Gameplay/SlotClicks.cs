using UnityEngine;
using UnityEngine.EventSystems;

namespace DreamWorld.Gameplay
{
    // Left and right clicks on an inventory slot.
    public class SlotClicks : MonoBehaviour, IPointerClickHandler
    {
        public System.Action left, right;

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) left?.Invoke();
            else if (e.button == PointerEventData.InputButton.Right) right?.Invoke();
        }
    }
}
