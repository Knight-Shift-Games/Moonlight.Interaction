using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Moonlight.Core;
using Sirenix.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Moonlight.Interaction
{
    public interface IPlayerGetter
    {
        Transform Transform { get; set; }
    }

    public sealed class DefaultPlayerGetter : IPlayerGetter
    {
        private Transform _transform;

        public Transform Transform
        
        {
            get => _transform;
            set => _transform = value;
        }
    }
    
    [DisallowMultipleComponent]
    public class ProximityPrompt : MonoBehaviour
    {
        [Header("Detection")] [Tooltip("Player transform. If null, will try GameObject tagged 'Player'.")]
        public Transform player;
        
        [Tooltip("How close the player must be to show the prompt.")]
        public float activationRadius = 3f;

        [Tooltip("Optional extra height to place prompt above this object.")]
        public Vector3 worldOffset = new(0, 1.2f, 0);

        [Header("Input (New Input System)")] [Tooltip("Action the player uses to interact (e.g., 'Player/Interact').")]
        public InputActionReference interactAction;

        [Tooltip("Optional PlayerInput to read current control scheme for nicer glyphs.")]
        public PlayerInput playerInput;

        [Tooltip("Press = single tap; Hold = require holding for the given duration.")]
        public InteractionMode interactionMode = InteractionMode.Press;

        [Tooltip("Seconds the player must hold the key if Hold mode.")]
        public float holdDuration = 0.5f;

        [Header("UI")] [Tooltip("World-space canvas or screen-space container that holds the prompt visuals.")]
        public RectTransform visualRoot;

        [Tooltip("CanvasGroup for fade/visibility control.")]
        public CanvasGroup canvasGroup;

        [Tooltip("Icon image for the key glyph. Optional if you only want text.")]
        public Image keyIcon;

        public TMP_Text keyLabel;
        public TMP_Text titleLabel; // e.g., 'Press E to Open'

        [Tooltip("Optional radial/linear progress (Image.type = Filled) for Hold mode.")]
        public Image holdFill;

        [Header("Glyphs (Optional)")] [Tooltip("Map binding display strings to sprites for nice-looking key icons.")]
        public KeyGlyphLibrary glyphLibrary;

        [Header("Tweening")] [Tooltip("Scale at which the prompt appears.")]
        public float showScale = 1.0f;

        [Tooltip("Scale when hidden.")] public float hiddenScale = 0.9f;
        [Tooltip("Tween time for in/out.")] public float tweenDuration = 0.18f;
        [Tooltip("Curve for scale tween.")] public Ease scaleEase = Ease.OutBack;
        [Tooltip("Curve for fade tween.")] public Ease fadeEase = Ease.OutQuad;

        [SerializeField] private List<Interactable> _interactables = new();
        
        public enum InteractionMode
        {
            Press,
            Hold
        }

        private bool _visible;
        private float _holdT;
        private Tween _alphaTween, _scaleTween;

        private Camera _cam;
        private Vector3 _initialLocalPos;
        
        private InteractionLockController LockController { get; set; }
        
        private void Awake()
        {
            if (!canvasGroup && visualRoot)
            {
                canvasGroup = visualRoot.GetComponent<CanvasGroup>();
            }
            
            if (!canvasGroup && visualRoot)
            {
                canvasGroup = visualRoot.gameObject.AddComponent<CanvasGroup>();
            }
            
            if (visualRoot)
            {
                _initialLocalPos = visualRoot.localPosition;
                visualRoot.localScale = Vector3.one * hiddenScale;
            }

            if (canvasGroup) canvasGroup.alpha = 0f;

            _cam = Camera.main;

            if (interactAction && interactAction.action != null)
            {
                interactAction.action.Enable();
            }

            // Start hidden
            SetVisible(false, true);
            UpdateBindingGlyph(); // pre-cache label/icon
        }

        private void OnEnable()
        {
            if (playerInput != null)
                playerInput.onControlsChanged += OnControlsChanged;
        }

        private void OnDisable()
        {
            if (playerInput != null)
                playerInput.onControlsChanged -= OnControlsChanged;

            KillTweens();
        }

        private void OnControlsChanged(PlayerInput pi)
        {
            UpdateBindingGlyph();
        }

        private void Update()
        {
            if (!player) return;

            // Face camera (simple billboard)
            if (visualRoot && _cam)
            {
                // Keep it hovering above the object
                visualRoot.position = transform.position + worldOffset;
                var dir = visualRoot.position - _cam.transform.position;
                dir.y = 0; // yaw-only billboard
                if (dir.sqrMagnitude > 0.0001f)
                {
                    visualRoot.forward = dir.normalized;
                }
            }

            var dist = Vector3.Distance(player.position, transform.position);
            var shouldShow = dist <= activationRadius;

            if (shouldShow != _visible)
            {
                SetVisible(shouldShow);
            }

            // Early out if not visible
            if (!_visible || interactAction == null || interactAction.action == null) return;

            // Handle input
            var action = interactAction.action;
            if (interactionMode == InteractionMode.Press)
            {
                // Triggered = performed this frame
                if (action.triggered)
                {
                    CompleteInteraction();
                }
            }
            else
            {
                // Hold logic
                var pressed = action.IsPressed();
                if (pressed)
                {
                    _holdT += Time.deltaTime;
                }
                else
                {
                    _holdT = Mathf.MoveTowards(_holdT, 0f, Time.deltaTime * 2f);
                }

                if (holdFill)
                {
                    holdFill.fillAmount = Mathf.Clamp01(_holdT / holdDuration);
                }

                if (_holdT >= holdDuration)
                {
                    _holdT = 0;
                    if (holdFill) holdFill.fillAmount = 0f;
                    CompleteInteraction();
                }
            }
        }

        private void CompleteInteraction()
        {
            _interactables.ForEach(x => x.Interact(player.gameObject));
            // Optionally auto-hide after interact:
            SetVisible(false);
        }

        private void SetVisible(bool show, bool instant = false)
        {
            _visible = show;
            if (!canvasGroup || !visualRoot) return;

            KillTweens();

            var targetAlpha = show ? 1f : 0f;
            var targetScale = show ? showScale : hiddenScale;

            if (instant)
            {
                canvasGroup.alpha = targetAlpha;
                visualRoot.localScale = Vector3.one * targetScale;
                return;
            }

            _alphaTween = canvasGroup.DOFade(targetAlpha, tweenDuration).SetEase(fadeEase).SetLink(gameObject);
            _scaleTween = visualRoot.DOScale(targetScale, tweenDuration).SetEase(scaleEase).SetLink(gameObject);
        }

        private void KillTweens()
        {
            _alphaTween?.Kill();
            _scaleTween?.Kill();
            _alphaTween = null;
            _scaleTween = null;
        }

        [ContextMenu("Refresh Binding Glyph")]
        public void UpdateBindingGlyph()
        {
            if (interactAction == null || interactAction.action == null) return;

            string display = null;

            var action = interactAction.action;

            // If a PlayerInput is provided, try to pick a binding for the current scheme.
            if (playerInput != null && !string.IsNullOrEmpty(playerInput.currentControlScheme))
            {
                var scheme = playerInput.currentControlScheme;
                // Find a binding matching the scheme if possible
                var bindingIndex = FindBindingIndexForControlScheme(action, scheme);
                display = bindingIndex >= 0
                    ? action.GetBindingDisplayString(bindingIndex)
                    : action.GetBindingDisplayString(); // fallback
            }
            else
            {
                display = action.GetBindingDisplayString();
            }

            if (keyLabel) keyLabel.text = display;

            if (glyphLibrary && keyIcon)
            {
                var sprite = glyphLibrary.GetSpriteForDisplay(display);
                keyIcon.enabled = sprite != null;
                if (sprite != null) keyIcon.sprite = sprite;
            }

            // Optional title like "Press E to Interact"
            if (titleLabel)
            {
                if (interactionMode == InteractionMode.Press)
                    titleLabel.text = $"Press {display} to Interact";
                else
                    titleLabel.text = $"Hold {display} to Interact";
            }
        }

        private int FindBindingIndexForControlScheme(InputAction action, string controlScheme)
        {
            // Simple heuristic: find the first binding whose groups include the scheme name.
            // (Assumes your bindings have their 'Groups' set via InputActionAsset control schemes.)
            for (var i = 0; i < action.bindings.Count; i++)
            {
                var b = action.bindings[i];
                if (string.IsNullOrEmpty(b.groups)) continue;

                // groups is a semicolon-separated list; check contains (case-insensitive)
                if (b.groups.IndexOf(controlScheme, StringComparison.OrdinalIgnoreCase) >= 0) return i;
            }

            return -1;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
            Gizmos.DrawSphere(transform.position, activationRadius);
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, activationRadius);
        }
    }
}