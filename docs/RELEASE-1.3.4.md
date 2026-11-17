# Launcher 1.3.4, build 14

14 November 2026. News & feeds refinement.

- Feed settings includes an initial story count from 1 to 50, defaulting to 12. Show more adds the chosen count.
- Article thumbnails can be enabled or disabled. Hidden, missing or failed images reserve no empty column.
- Publisher accents, clearer headlines, compact summaries, quieter dates and rounded thumbnails improve the reading layout.
- Existing stories remain visible during source refreshes. Results apply together once the batch completes, updating only the feed content rather than rebuilding the widgets page.
- Last successful stories are saved in app-private cache storage and displayed immediately on reopening, including after activity recreation. Android may clear disposable caches.
- Failed sources retain their previous stories. Changing one source does not clear the others.
- Images support Media RSS thumbnails/content, image enclosures, Atom enclosures, XHTML images and images embedded in descriptions/content. Relative image addresses respect XML base paths.
- Thumbnail downloads are HTTPS-only, limited to 2 MB and three concurrent requests. Decoding is sampled off the UI thread; disk cache is bounded to roughly 24 MB with seven-day reuse.
- Disabling images cancels outstanding thumbnail work. Detached page work is cancelled on body rebuild, and all pending feed/image work is cancelled on destruction.

No device or functional tests were run, at the owner's request. Design illustrations use sample stories and are not device screenshots.

Release compiled with zero warnings and zero errors. Package/version/signing identity, public updater and APK, previous APK removal, release notes, article, gallery preview and retained Windows companion download were verified.

APK SHA-256: a0916fd1ccf7b07b0ae09cdf00962533e273396f18fd2fbf6a8b8f0330253c80
