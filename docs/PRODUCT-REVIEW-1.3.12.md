# Launcher 1.3.12: product inventory and improvements

Reviewed 14 November 2026 against build 22 source, the previous video comparison and archived native captures. This is a source and product review, not a new functional test or complete security audit. No app code or published release changed during this review.

## Current inventory

The early video comparison describes version 1.0.1. Many of its missing features have shipped since then; it is not an accurate current backlog.

| Area | Implemented | Main remaining opportunity |
|---|---|---|
| Apps | Drawers, full-catalogue name/package search, pinned/hidden apps, labels, usage, goals, per-app tile appearance, icon libraries/packs, biometric launch gates, app-display grayscale | Direct organisation, action search and profile-aware app enumeration |
| Appearance | Dark/Light/Sun, accent, optical icon sizing, geometry, wallpaper dimming, light-sensor themes, minimal status row, sheets, layering and optional tilt | Preview editor, named user presets and consistent selected states |
| Root | Collapsible/pinnable/reorderable cards, notes, timers, calculator, forecast, feeds, resources, journeys and Android widgets | Agenda, richer content and direct arrangement |
| Conversations | Current notifications, app filters, reply actions, dates, source icons and optional local message history | Threads, deferred items and a consistent preview privacy policy |
| Feeds | Multiple sources, newest-first merge, cache, images, item count, Show more and refresh retention | Reading states, bookmarks and related-story grouping |
| Media | Artwork, source label, progress, transport and source-app opening | Explicit session selection and capability-aware controls |
| Companion | Windows LAN pairing, notes and selected-folder transfer, currently capped at 16 MB per file | Capture handoff and explicit transfers that can survive leaving Launcher |
| Maintenance | Verified updates, JSON backup/restore, permission controls and a recovery copy of unreadable state | Undo, selective restore, health reporting and broader performance/accessibility checks |
| Vision and ML | No OpenCV, OCR, embedding or general ML integration found | All proposed vision/ML capabilities below require new work |

## Findings in the implementation

- The shared choice sheet has no selected-value argument. It cannot consistently indicate the active theme or setting.
- Prompt closes before its callback validates the value. Some invalid entries can lose their editing context.
- App locks gate launching and actions. Notification bodies and history overview snippets are not automatically redacted. A preview privacy policy would extend the existing launch gate; this finding is not proof of bypassing that gate.
- App search matches names and packages; Root parses a small command set; Conversations searches current notification text. There is no shared content index.
- Today displays the date and opens Calendar. It is not an agenda.
- Focus changes presentation and has a timer. It is not an OS-wide application blocker.
- Drawer and card ordering still relies heavily on menus.
- General Save runs on each note text change, serialising the overall state and synchronising feature rules. Some live events still rebuild page bodies. These are opportunities to measure and reduce work, not measured claims about latency or battery cost.

## 45 specific improvements

Effort is relative: S is a focused extension, M is work across several components, L is substantial integration. These are not time or cost estimates. Related items should share foundations rather than produce dozens of disconnected settings.

### UI and interaction

1. **Live appearance editor [M].** Preview the real Apps/Root/Conversations composition while adjusting density, headings, accent roles and corners. Keep a draft with Apply, Cancel and Reset rather than immediately overwriting the saved layout.
2. **Selected states in every picker [S].** Mark the active option with a check and subtle accent treatment. Restore keyboard/accessibility focus to that option.
3. **Validation before dismissal [S].** Keep invalid fields open with an inline explanation and preserve the input. Make disabled, busy and successful actions visually distinct.
4. **Explicit layout editing [L].** Add drag grips, placement previews and Undo for drawers and cards. Enable dragging only in edit mode, leaving normal scrolling and page swipes alone. Retain accessible move buttons.
5. **Bulk organisation [M].** Filter and select several apps to move, pin, hide or restyle together. Authenticate protected rule changes before committing them.
6. **Named appearance presets [M].** Save personal Minimal, Outdoor or Large text presets. Export appearance independently of notes and other private data.
7. **Better collapsed summaries [M].** Extend the existing summaries into next-appointment rows, compact resource values, unread headlines, checklist progress and upcoming ticket details. Collapsing should retain useful information.
8. **Reachability controls [M].** Add Jump to drawer search results and optional lower filter/action placement. Preserve existing search and gesture conventions.
9. **Restrained haptics [S].** Provide feedback for committing a fold, placing a card and switching mode. Respect Android preferences and offer one application switch.
10. **A full accessibility layout matrix [M].** Cover 200% text, display scaling, TalkBack, keyboard focus, RTL, cutouts and landscape. Return focus to the invoking control after closing sheets.

### Workflows that make the home screen more useful

11. **User-defined modes [L].** Work, Home, Travel and Evening can select drawer order, Root cards, notification filters, appearance and grayscale rules. Start with manual selection; add schedules and explicitly selected connection triggers. Define override priority and an obvious way back.
12. **Project workspaces [L].** Combine an app action, note, selected document, link and conversation reference in one named workspace. A client or trip gets its relevant tools without rearranging the entire phone.
13. **A temporary shelf [M].** Keep a small expiring stack for a ticket, shopping list, document or timer. Allow expiry after the session or at a chosen time, returning to the normal minimal home screen.
14. **Unified local search [L].** Index apps, notes, tickets, saved stories and explicitly chosen files. Label result types. Include saved messages only with explicit history/search settings and protected-result filtering.
15. **Pinnable app actions [M].** Native shortcuts already appear inside app actions. Make supported shortcuts pinnable as tiles and searchable. Handle source apps removing or disabling them.
16. **Personal aliases [S].** Let bank, scan or work mail resolve to a selected app or supported shortcut. Explicit aliases provide immediate value without a model.
17. **A Share capture inbox [L].** Receive text, URLs, images and documents through Android Share. Offer Note, Read later, Journey or Keep original; hold undecided imports in an inbox.
18. **An actual agenda [M].** Show upcoming events from user-selected calendars, ongoing-event countdowns and available location/join links. Ask for calendar access only when enabling the card.
19. **Reminders and checklists [M].** Add dated tasks, recurrence and checkable items alongside duration timers. Parse phrases into an editable confirmation before scheduling anything.
20. **Richer notes [M].** Add selected images, file/link attachments and lightweight formatting. Keep editing fast and support plain-text export.
21. **Feed read states and bookmarks [M].** Add Read later, unread-only filtering and explicit mark-read actions. Keep newest-first order as the default.
22. **Related-story grouping [M/L].** Normalise tracking variants first. Optionally group reporting about the same event with separate publisher links, preserving distinct perspectives and chronological sorting.
23. **Explainable feed filters [M].** Offer keyword filters and per-source limits, with an explanation for filtered items and an easy unfiltered chronological view.
24. **Real notification threads [L].** Group using conversation identifiers the source app exposes. Expand into available recent messages; do not merge people across apps merely because their names match.
25. **A later queue [M].** Pin or defer notification-derived items in Launcher. Clearly distinguish this from Android-supported system snoozing.
26. **Media session selection [M].** Use the source label to choose between eligible podcast, music and browser sessions. Expose only controls supported by the chosen session.

### Computer vision and imported material

27. **Screenshot OCR [L].** Make text in selected/imported images selectable and searchable. Preserve the original and allow corrections. This requires OCR, not OpenCV alone.
28. **Ticket extraction [L].** Propose date, route and reference from a shared ticket image/document into the existing Journey form. Show a source crop for each extracted field and require review before saving.
29. **Document capture and correction [L].** Detect edges, correct perspective, deskew and improve contrast on an imported or explicitly captured document. Keep original and corrected copies. OpenCV is a credible option for this processing.
30. **Keep QR and barcodes usable [M].** Find codes in selected images and preserve a convenient view of the original. Decode supported formats; do not fabricate replacements for signed or unsupported tickets.
31. **Visual duplicate detection [M].** Group repeated scans/screenshots despite resizing, using perceptual similarity. Keep originals until the user chooses what to remove.
32. **Contrast-aware wallpaper composition [M].** For a bitmap explicitly imported into Launcher, identify busy regions under text and apply local scrims rather than uniformly darkening the image. Ordinary system wallpaper display remains a separate path.
33. **Prepared foreground/background layers [L, experimental].** Optionally segment a chosen wallpaper once, cache the masks and use restrained perspective. General subject separation needs an appropriate model. Keep black/flat themes and the existing master-off control fully supported.

### Small models with a clear purpose

34. **Semantic search [L].** Find saved parking instructions using a related phrase, even when the exact words differ. Return the original saved material and source rather than generating an unsupported answer.
35. **Drawer suggestions that learn corrections [M].** Initial placement currently uses keywords and Android categories. Learn from moves and propose better placement for new apps; never silently reshuffle existing drawers.
36. **Useful actions from text [M/L].** Offer Copy reference, Open link, Map address or Create reminder where the source content supports it. Use parsing first and entity models only where useful. Confirm scheduling/sharing actions.
37. **One stable suggestion position [M].** An opt-in fixed slot can offer an available meeting link before an event or a media app after headphones connect. Learn locally, explain the reason and leave existing app positions fixed.

### Trust, platform support and reliability

38. **Protected preview policy [M, high priority].** Provide Hide preview, Unlock to reveal and Exclude from history/search controls. Apply them consistently across Conversations, quick panel, saved history and future search.
39. **Work profiles and Android Private Space [L].** Use profile-aware launcher APIs, distinguish instances and respect locked/paused states. Android Private Space support does not automatically include Samsung Secure Folder.
40. **Undo and selective recovery [L].** Preview backup contents and restore appearance, app organisation or notes separately. Add Undo for moves/hides/removals and optional encrypted exports for sensitive content. Widget bindings remain device-local.
41. **Incremental rendering and measured performance [L].** Keep drawer, weather and media components alive and update data in place. Profile cold starts, large app libraries, notification bursts and 60/90/120 Hz behaviour rather than assuming screenshot checks establish performance.
42. **Independent note persistence [M].** Avoid rewriting layout and synchronising unrelated rules on each keystroke. Coalesce writes, flush safely when leaving the editor and expose saving failures without losing the draft.
43. **Useful standard lock-screen notifications [M].** Offer an optional journey/session notification with chosen privacy and supported actions. Android owns its lock-screen presentation; this is not a replacement keyguard.
44. **Phone/desktop task handoff [L].** Send a selected note, document or link to the capture inbox and show a receipt. An explicitly started transfer session could survive leaving Launcher using an Android-compliant visible service.
45. **Feature health and provenance [M].** Show connected listeners, last successful weather/feed refreshes, cache states and optional model download state in one place. Include recovery actions, local-index deletion and a global ML-off switch.

## Choosing OpenCV or a model

| Job | Appropriate approach | Important qualification |
|---|---|---|
| Straighten a photographed page | OpenCV geometry/perspective correction | Does not read the text |
| Recognise text | Dedicated OCR SDK/model | Accuracy depends on input quality; keep source and corrections |
| Find an imported barcode | Barcode decoder, optionally preprocessing with OpenCV | Preserve the original code; unsupported formats need a fallback |
| Find similar saved text | Text embedder and local index | Retrieval needs evaluation; it does not establish truth or generate actions safely |
| Extract date/address entities | Rules first, optional entity model | ML Kit entity extraction is currently beta |
| Separate a wallpaper subject | Suitable segmentation model | Person-only models do not promise arbitrary-object depth |
| Suggest next app or placement | Local scoring rules or a small learned model | Stable explicit suggestions; avoid automatic grid rearrangement |

No camera needs to run continuously for these proposals. Imported material can be processed on demand. General animation, haptics and sensor-driven UI depth do not need OpenCV.

The application is native .NET for Android. Native Android libraries can be integrated through bindings/native interop; ABI coverage, dependencies, trimming and device behaviour still need verification. No particular OpenCV C# package or model has been proven compatible in this review.

No model size, RAM usage, energy cost or Samsung inference latency was measured. Candidate models need device evaluation, optional downloads, transparent storage controls and a way to disable them. A general-purpose local chatbot is a separate, more demanding proposition than OCR or retrieval.

## Boundaries

- Search can use Launcher's own data and explicitly granted sources, not arbitrary private data from other apps.
- Notification-derived conversations are not complete private chat-history synchronisation.
- Home privileges do not grant control of keyguard, system shade styling or system Recents transitions.
- Direct system wallpaper bitmap access is restricted on recent Android. Analyse user-imported images; preserve the existing system-wallpaper display path.
- New dragging belongs in edit mode. Existing navigation and the layer master switch must remain predictable.

## Recommended delivery order

1. Picker state, form validation, preview privacy, Undo and reliable note saving.
2. Direct layout editing, bulk organisation and live appearance previews.
3. Local content store, Share capture inbox and unified lexical search.
4. User-reviewed OCR, ticket/document import and code preservation.
5. Agenda, user modes and project workspaces.
6. Evaluate semantic search and learned suggestions against simpler baselines before shipping models.
7. Expand profile support and companion handoff with their lifecycle/permission work.

## Official capability references

- [OpenCV perspective correction](https://docs.opencv.org/4.x/d9/dab/tutorial_homography.html)
- [ML Kit text recognition on Android](https://developers.google.com/ml-kit/vision/text-recognition/v2/android)
- [ML Kit barcode scanning](https://developers.google.com/ml-kit/vision/barcode-scanning/android)
- [MediaPipe text embeddings](https://developers.google.com/edge/mediapipe/solutions/text/text_embedder)
- [ML Kit entity extraction, beta](https://developers.google.com/ml-kit/language/entity-extraction)
- [MediaPipe image segmentation](https://developers.google.com/edge/mediapipe/solutions/vision/image_segmenter)
- [.NET Android Java library binding](https://learn.microsoft.com/dotnet/android/binding-libs/binding-java-libs/binding-java-library)
- [Android launcher/profile APIs](https://developer.android.com/reference/android/content/pm/LauncherApps)
- [Android wallpaper access](https://developer.android.com/reference/android/app/WallpaperManager)
- [Android rendering performance](https://developer.android.com/topic/performance/issues/render)

## Representative source evidence

- [src/Launcher/MainActivity.AppSearch.cs](E:/Repository/launcher/src/Launcher/MainActivity.AppSearch.cs:13)
- [src/Launcher/MainActivity.Settings.cs](E:/Repository/launcher/src/Launcher/MainActivity.Settings.cs:37)
- [src/Launcher/MainActivity.Settings.cs](E:/Repository/launcher/src/Launcher/MainActivity.Settings.cs:48)
- [src/Launcher/MainActivity.Workspace.cs](E:/Repository/launcher/src/Launcher/MainActivity.Workspace.cs:13)
- [src/Launcher/MainActivity.Workspace.cs](E:/Repository/launcher/src/Launcher/MainActivity.Workspace.cs:94)
- [src/Launcher/MainActivity.Notifications.cs](E:/Repository/launcher/src/Launcher/MainActivity.Notifications.cs:16)
- [src/Launcher/MainActivity.MessageHistory.cs](E:/Repository/launcher/src/Launcher/MainActivity.MessageHistory.cs:18)
- [src/Launcher/Services/AppCatalog.cs](E:/Repository/launcher/src/Launcher/Services/AppCatalog.cs:12)
- [src/Launcher/Services/MediaMonitor.cs](E:/Repository/launcher/src/Launcher/Services/MediaMonitor.cs:35)
- [src/Launcher/Services/StateStore.cs](E:/Repository/launcher/src/Launcher/Services/StateStore.cs:25)
- [src/Launcher.Core/LauncherState.cs](E:/Repository/launcher/src/Launcher.Core/LauncherState.cs:228)
