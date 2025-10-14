using System;
using System.Collections.Generic;
using UnityEngine;

namespace Moonlight.Interaction
{
    [Serializable]
    public sealed class InteractionGlyphLibrary
    { 
        [SerializeField] private List<IndexedInteractionGlyph> Glyphs = new();

        public InteractionGlyph Get(string key)
        {
            return Glyphs.Find(x => x.Key == key).Glyph;
        }
    }
}