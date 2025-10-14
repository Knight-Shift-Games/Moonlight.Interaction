using System;
using System.Collections.Generic;
using UnityEngine;

namespace Moonlight.Interaction
{
    // KeyGlyphLibrary.cs
    // Create an asset via: Right-click in Project → Create → Input → Key Glyph Library
    // Add entries mapping display strings (or fragments) to sprites.
    // Example matches: "E", "F", "Space", "A", "B", "Cross", "Circle", "X", "Y", "Left Mouse", "Right Mouse", etc.
    [CreateAssetMenu(menuName = "Moonlight/Interaction/Key Glyph Library")]
    public class KeyGlyphLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("Case-insensitive fragments to match against Input System display string (e.g., \"E\", \"Space\", \"Left Mouse\", \"South\", \"Cross\").")]
            public string[] matches;
            public Sprite sprite;
        }

        public List<Entry> entries = new();

        public Sprite GetSpriteForDisplay(string displayString)
        {
            if (string.IsNullOrEmpty(displayString))
            {
                return null;
            }
            
            var s = displayString.Trim().ToLowerInvariant();

            foreach (var e in entries)
            {
                if (e.sprite == null || e.matches == null)
                {
                    continue;
                }
                
                foreach (var m in e.matches)
                {
                    if (string.IsNullOrWhiteSpace(m))
                    {
                        continue;
                    }
                    
                    if (s.Contains(m.Trim().ToLowerInvariant()))
                    {
                        return e.sprite;
                    }
                }
            }
            
            return null;
        }
    }
}