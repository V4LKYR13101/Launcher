# Launcher 1.3.9, build 19

14 November 2026. App search and a smaller resource card.

- Removed the Phone resources history graph, its tabs, session sample buffer and unused chart renderer. Battery/memory/storage donuts, exact-reading details, temperature and foreground ten-second updates remain.
- App search filters the same complete in-memory app catalogue used by the drawers. It has no display limit or Show more button.
- Native ListView row recycling keeps only on-screen rows populated. The result count and full match set update without rebuilding the page or replacing the text input.
- Search matches custom names, original names and package names, supports multiple terms, trims whitespace and ranks name prefixes first. Hidden-app preferences remain respected. The keyboard's search action uses the same result ordering.
- App enumeration runs on initial load or after package/availability/locale changes, rather than every resume. Usage refresh is separate from the decision to reload the catalogue.
- A dynamic catalogue watcher stays registered for the activity lifetime so installs/removals in the background invalidate the cache. Change notifications are debounced; changes during an active load trigger another refresh.
- Catalogue completion refreshes an already-open app search. Startup searches no longer need a subsequent interaction to pick up completed enumeration.
- A failed app launch forces a catalogue refresh. Receivers and pending callbacks are removed on destruction.
- Existing input focus, fixed bottom search, app launch/edit actions, badges and pin markers are retained. Existing gesture recognition is unchanged.

No device or functional tests were run, at the owner's request. Build, source review and publication checks are separate from owner acceptance testing. Website interface covers and editorial focus are retained.
