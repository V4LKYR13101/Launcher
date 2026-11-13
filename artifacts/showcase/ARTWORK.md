# Launcher showcase artwork

Ten product boards and twelve actual screenshots are published in the project
gallery. Screenshots come from the dedicated Android emulator with sample notes,
weather and a synthetic Android media session. No personal phone notifications,
network names or contacts were published.

Nine boards use reproducible HTML/CSS device frames around unmodified app
captures. Render them with `python tools/render_showcase.py`. Their sources and
PNG exports are in `artifacts/showcase/mockups`; published WebP exports are in
`artifacts/showcase/media`.

The studio background/phone scene was made with the built-in imagegen tool,
using `screenshots/home.png` as the screen reference. It is saved as
`mockups/studio-hero.png`. The gallery labels it as a generated product scene.
The cover board adds deterministic HTML typography to this scene.

Prompt used:

> Create a polished photorealistic product photograph for an Android app named
> Launcher. Edit/composite the supporting actual app screenshot into a single
> modern black Android phone, preserving all visible screenshot text, symbols
> and UI faithfully. Landscape 1536x1024 composition. Phone upright at a very
> slight three-quarter angle on the right half, entire phone visible, resting
> on a dark charcoal concrete plinth. Matte black studio background with
> restrained acid yellow light at far right. Soft professional lighting, crisp
> metal edge, believable contact shadow. Large quiet negative space on left.
> Do not add any text, logos, app names, slogans or graphics outside the actual
> phone screen. Do not invent additional interface elements. No hands. The
> supplied screenshot is the screen insert, not a style suggestion.
