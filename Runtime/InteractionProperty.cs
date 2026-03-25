using System;
using Moonlight.Inventory;
using Moonlight.Localization;

namespace Moonlight.Interaction
{
    [Serializable]
    public sealed class InteractionProperty : EntryProperty
    {
        [AlwaysFromBlueprint] public L10nString Message = new();
        
        public override object Clone()
        {
            return new InteractionProperty
            {
                Message = this.Message
            };
        }
    }
}