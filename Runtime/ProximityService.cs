using System.Collections.Generic;
using Moonlight.Core;
using UnityEngine;
using Zenject;

namespace Moonlight.Interaction
{
    public class ProximityService : ITickable
    {
        private readonly List<ProximityPrompt> _prompts = new();
        private Transform _playerTransform;
        private ProximityPrompt _closestPrompt;
        
        // Config
        private const float SQR_ACTIVATION_HYSTERESIS = 1.0f; // Prevent flickering at edge

        public void Register(ProximityPrompt prompt)
        {
            if (!_prompts.Contains(prompt))
            {
                _prompts.Add(prompt);
            }
        }

        public void Unregister(ProximityPrompt prompt)
        {
            _prompts.Remove(prompt);
            if (_closestPrompt == prompt)
            {
                _closestPrompt = null;
            }
        }

        public void Tick()
        {
            if (_playerTransform == null)
            {
                // Try to find player (can be optimized later with a PlayerService)
                var playerObj = GameObject.FindWithTag("Player");
                if (playerObj) _playerTransform = playerObj.transform;
                else return;
            }

            Vector3 playerPos = _playerTransform.position;
            ProximityPrompt newClosest = null;
            float closestDistSqr = float.MaxValue;

            // Single loop for all prompts
            foreach (var prompt in _prompts)
            {
                if (prompt == null) continue;

                float sqrDist = (prompt.transform.position - playerPos).sqrMagnitude;
                float activationSqr = prompt.activationRadius * prompt.activationRadius;
                
                // Check visibility
                bool inRange = sqrDist <= activationSqr;
                
                // Hysteresis for hiding
                if (prompt.IsVisible && sqrDist > activationSqr + SQR_ACTIVATION_HYSTERESIS)
                {
                    prompt.SetVisible(false);
                }
                else if (!prompt.IsVisible && inRange)
                {
                    prompt.SetVisible(true);
                }

                // Track closest
                if (inRange && sqrDist < closestDistSqr)
                {
                    closestDistSqr = sqrDist;
                    newClosest = prompt;
                }
            }

            // Handle Input Focus
            if (newClosest != _closestPrompt)
            {
                _closestPrompt = newClosest;
            }
            
            // Delegate input handling to the closest prompt
            if (_closestPrompt != null && _closestPrompt.IsVisible)
            {
                _closestPrompt.HandleInput();
            }
        }
    }
}
