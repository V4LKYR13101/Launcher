# Launcher 1.3.13 (23)

- Settings > Haptic feedback: Off, Subtle and Crisp, with previews and a visible current selection.
- Central semantic feedback for taps, holds, toggles, committed page/drawer/sheet/notification gestures and media interactions.
- No raw vibration patterns, new vibration permission or background vibration service. Native feedback respects system touch-vibration policy.
- Launcher Off also disables its own default long-press and input selection feedback. Keyboard, system and widget-provider feedback remain independently controlled.
- An 80 ms guard limits duplicate cues. Ordinary scrolling and background refreshes do not produce feedback. Movement cancels the blank-area long-press timer.
- Preference defaults to Subtle for older layouts, validates imported values and survives backup/restore.

Validation: clean Release compilation, 61 core checks and native emulator interaction checks. The old core test fixture was updated for the existing legacy-note migration and repaired Unicode input. Physical motor feel is not verified in an emulator. No physical phone touched.

Separate research: ANDROID-SYSTEM-UI-A26.md examines global status/shade customisation for the reported Galaxy A26 / One UI 9, without Good Lock. Audit-AndroidSystemUi.ps1 is read-only and does not apply themes or grant permissions. A global custom shade/status service is not part of this release.
