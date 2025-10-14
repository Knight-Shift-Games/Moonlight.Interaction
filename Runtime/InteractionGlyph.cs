using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Moonlight.Interaction
{
    [Serializable]
    public struct IndexedInteractionGlyph
    {
        public string Key;
        public InteractionGlyph Glyph;
    }
    
    [Serializable, InlineProperty]
    public struct InteractionGlyph
    {
        public Sprite Icon;
        public Color Color;
    }
}