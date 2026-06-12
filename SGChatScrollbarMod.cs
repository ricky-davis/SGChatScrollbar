using MelonLoader;
using UnityEngine;
using Il2CppTankAndHealerStudioAssets;

[assembly: MelonInfo(typeof(SGChatScrollbar.SGChatScrollbarMod), "SGChatScrollbar", "1.0.0", "Spyci")]
[assembly: MelonColor]

namespace SGChatScrollbar
{
    /// <summary>
    /// Adds a working scrollbar to the in-game chat (the "UltimateChatBox" asset).
    ///
    /// How the asset actually scrolls (verified against the decompiled GameAssembly):
    ///   • chatContentBox.anchoredPosition.y IS the scroll position, valid range [0, overflow] where
    ///     overflow = chatContentBox.sizeDelta.y - visibleChatBoundingBox.sizeDelta.y. y == overflow is the
    ///     newest/bottom, y == 0 is the oldest/top.
    ///   • ConstrainContentBox() clamps that y to [0, overflow], re-virtualizes the pooled TextMeshPro objects
    ///     for the new window (via UpdateChatBoxVisibility), AND derives the scrollbar handle position from the
    ///     content (handle = Lerp(0, scrollbarBottomPosition, y/overflow)). So the handle always follows content.
    ///
    /// We therefore drive ONLY chatContentBox.anchoredPosition.y and call ConstrainContentBox(). We do NOT touch
    /// ScrollValue / isDraggingScrollHandle / ConstrainScrollbarHandle / RepositionAllChats — those belong to the
    /// asset's own input pipeline and driving them in parallel is what made the handle desync from the content.
    /// We disable the asset's built-in wheel (useScrollWheel = false) so it can't double-apply against us, and
    /// keep the scrollbar always visible (useScrollbar = true, visibleOnlyOnHover = false).
    /// </summary>
    public class SGChatScrollbarMod : MelonMod
    {
        private const int   MaxMessages   = 100;   // retained-message cap (history depth)
        private const float WheelPageFrac = 0.25f; // fraction of the viewport scrolled per wheel notch
        private const float Eps           = 0.5f;  // "at bottom" tolerance, in content units

        private UltimateChatBox _chat;
        private RectTransform _content, _viewport, _handle, _track;
        private CanvasGroup _scrollGroup;
        private Canvas _canvas;

        private float _targetY;       // desired chatContentBox.anchoredPosition.y
        private bool  _follow = true; // pinned to the newest message (auto-scroll on new messages)
        private bool  _dragging;
        private float _findCooldown;

        public override void OnUpdate()
        {
            if (_chat == null)
            {
                _findCooldown -= Time.unscaledDeltaTime;
                if (_findCooldown > 0f) return;
                _findCooldown = 1f;
                if (!TryBind()) return;
            }
            try { Tick(); }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[SGChatScrollbar] {ex.GetType().Name}: {ex.Message}");
                _chat = null; _dragging = false;
            }
        }

        private bool TryBind()
        {
            _chat = UnityEngine.Object.FindObjectOfType<UltimateChatBox>();
            if (_chat == null) return false;
            _content  = _chat.chatContentBox;
            _viewport = _chat.visibleChatBoundingBox;
            _handle   = _chat.scrollbarHandle;
            _track    = _chat.scrollbarBase;
            _scrollGroup = _chat.scrollbarCanvasGroup;
            _canvas   = _chat.GetComponentInParent<Canvas>();
            if (_content == null || _viewport == null || _handle == null || _track == null) { _chat = null; return false; }

            try
            {
                _chat.maxTextInChatBox  = MaxMessages;  // keep more history than the default
                _chat.useScrollbar      = true;         // render the handle + let ConstrainContentBox drive it
                _chat.useScrollWheel    = false;         // we own the wheel; stop the asset double-scrolling
                _chat.visibleOnlyOnHover = false;        // keep the handle visible (asset hides it without fed input)
            }
            catch (System.Exception ex) { MelonLogger.Warning($"[SGChatScrollbar] config: {ex.Message}"); }

            _follow = true;
            MelonLogger.Msg("[SGChatScrollbar] Bound to chat box — driving native content scroll.");
            return true;
        }

        private void Tick()
        {
            float overflow = _content.sizeDelta.y - _viewport.sizeDelta.y;
            if (overflow <= Eps)
            {
                // Everything fits — nothing to scroll. Let the asset rest at the bottom.
                _follow = true;
                return;
            }

            float cur = _content.anchoredPosition.y;
            _targetY = _follow ? overflow : Mathf.Clamp(_targetY, 0f, overflow);

            HandleWheel(overflow);
            HandleDrag(overflow);

            if (_follow) _targetY = overflow;
            _targetY = Mathf.Clamp(_targetY, 0f, overflow);

            if (Mathf.Abs(cur - _targetY) > 0.01f || _follow)
            {
                var ap = _content.anchoredPosition;
                ap.y = _targetY;
                _content.anchoredPosition = ap;
            }
            // Always re-constrain so the handle + virtualized window track the content (and any new message).
            _chat.ConstrainContentBox();

            // The asset's own visibility logic is neutered (visibleOnlyOnHover=false) and what's left
            // (UpdateChatBoxComponentSizes on AddChat) toggles the alpha 0/1 per message — so force the
            // scrollbar visible ourselves every frame.
            ForceScrollbarVisible();
        }

        private void ForceScrollbarVisible()
        {
            try { _chat.ScrollbarActive = true; } catch { }
            if (_scrollGroup != null)
            {
                if (_scrollGroup.alpha < 1f) _scrollGroup.alpha = 1f;
                if (!_scrollGroup.gameObject.activeSelf) _scrollGroup.gameObject.SetActive(true);
            }
            if (!_track.gameObject.activeSelf)  _track.gameObject.SetActive(true);
            if (!_handle.gameObject.activeSelf) _handle.gameObject.SetActive(true);
        }

        private void HandleWheel(float overflow)
        {
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) < 0.01f || !PointerOver(_viewport)) return;
            float step = Mathf.Max(20f, _viewport.rect.height * WheelPageFrac);
            // Wheel up (positive) -> older -> toward y = 0.
            _targetY = Mathf.Clamp(_targetY - wheel * step, 0f, overflow);
            _follow = _targetY >= overflow - Eps;
        }

        private void HandleDrag(float overflow)
        {
            if (!_dragging)
            {
                // Begin on press over the handle OR anywhere on the track (click-to-jump).
                if (Input.GetMouseButtonDown(0) && (PointerOver(_handle) || PointerOver(_track)))
                    _dragging = true;
                else
                    return;
            }
            if (!Input.GetMouseButton(0)) { _dragging = false; return; }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_track, Input.mousePosition, Cam(), out var lp))
                return;
            // Normalize within the track regardless of its pivot: norm.y 0 = bottom, 1 = top.
            Vector2 norm = Rect.PointToNormalized(_track.rect, lp);
            float frac = Mathf.Clamp01(1f - norm.y);   // 0 = top/oldest, 1 = bottom/newest
            _targetY = frac * overflow;
            _follow = frac >= 1f - 0.001f;
        }

        private Camera Cam() => (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? _canvas.worldCamera : null;
        private bool PointerOver(RectTransform rt) => rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition, Cam());
    }
}
