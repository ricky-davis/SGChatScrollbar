# Changelog

All notable changes to this project will be documented in this file.

## [1.0.0] - 2026-06-11

### Added

- Working scrollbar for the in-game chat box, with an always-visible draggable handle that sizes itself to how much history there is.
- Mouse-wheel scrolling over the chat (a quarter page per notch), plus click-to-jump and drag anywhere on the scrollbar track.
- Scrolls through the full retained message history (up to 100 messages) by driving the chat asset's own virtualized content.
- Auto-follow: stays pinned to the newest message when you are at the bottom, and holds your position when you have scrolled up.
