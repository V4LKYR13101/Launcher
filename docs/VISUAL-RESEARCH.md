# Ratio visual research · 13 November 2026

## Reference set

All downloaded reference images and three paginated contact sheets are in
`featureImages/`. `featureImages/sources.json` maps the second collection to its
original URL, page, dimensions and content hash. The initial eight Play Store
screenshots use `play-01.jpg` through `play-08.jpg`; their URLs are recorded in
`tools/research_images.py`. The original user-provided files are preserved.
Images are research references, not bundled application assets.

## Sources and versions

- [Current Play listing](https://play.google.com/store/apps/details?id=com.bllocosn&hl=en_ZA): eight promotional screenshots, current grouping, search, widgets, drawer picker and conversations.
- [Ratio product designer SingYu Lam](https://singyulam.com/blloc-ratio): component boards, Root wrapper anatomy, semantic colors, icon system, onboarding and 2022 redesign. This is the most useful primary visual source. The designer specifies 20dp icons and four theme variants.
- [Blloc’s Ratio 6 announcement](https://medium.com/blloc/announcing-ratio-6-46b76617c764): drawer alignment, icon packs, alphabetical scrolling and conversation filters.
- [2020 photographs and screens](https://www.yankodesign.com/2020/05/02/spending-more-time-on-screen-this-premium-andriod-launcher-makes-your-smartphone-less-addictive/): black and sun themes, expanded tiles, app controls, Root, messaging.
- [2020 hands-on walkthrough](https://www.xatakandroid.com/aplicaciones-android/ratio-minimalismo-al-extremo-launcher-super-personalizable): three-screen organization, settings, permission screens and Root.
- [2022 review](https://9to5google.com/2022/03/11/the-best-android-launchers-to-use-with-your-smartphone-in-2022-video/): newer Root and drawer photographs.
- [Screenshot gallery](https://www.malavida.com/en/soft/ratio/android/): onboarding, icon style and usage goal screens.
- [2024 user screenshot](https://nothing.community/d/11687-has-anyone-tried-ratio-launcher-on-nothing-phone): unobstructed five-column layout.
- [Sun theme user screenshot](https://forum.fairphone.com/t/minimalistic-android-launcher/60302): light collapsed drawers and search.

## Observed visual anatomy

These are observations from the saved images, not claims of access to Ratio's source or exact design tokens.

| Surface | Observations | Useful files |
| --- | --- | --- |
| Classic Tiles | Bold left-aligned headings; four columns; tall narrow cards; double-width cards containing pill actions; white line icons; sparse fluorescent yellow; small usage text | user `images.jpg`, `Ratio_by_Blloc…06.jpg`, `yanko-392b8c9e11.jpg` |
| Later Tiles | Often five columns; centered headings; small category marker at left; compact label and line icon; rounded dark panels over monochrome wallpaper; favorites tray above search | `nothing-home-4f6e1194c2.jpg`, `designer-5f1400fef5.jpg` |
| Collapsed drawers | Heading remains visible; tiny category usage at opposite edge in classic view; no surrounding large card | `yanko-bf4822c46a.jpg`, `fairphone-caf40c003f.jpg` |
| App editing | App name at top; prominent usage chart; tile appearance panel; compact action controls; full-width rounded save control at bottom | `yanko-30a5712447.jpg`, `overview.jpg` |
| Root | Vertically stacked collapsible wrappers, persistent icon/name header; rounded inner primary and secondary panels; no giant page clock | `designer-23028883e2.jpg`, `designer-5c869a597e.jpg`, `xataka-tour-7118ede05.jpg` |
| Conversations | Bold title, compact stacked rounded previews, avatar/name/time, small app marker, filter pills immediately above bottom search | `conversation-filters-f5f3c2a1e.gif`, `yanko-0fb61bf301.jpg` |
| Search | Persistent pill at bottom; hint aligned left and search/control icon right; results replace surface above; keyboard-aware layout | `designer-5f1400fef5.jpg`, `nothing-home-4f6e1194c2.jpg` |
| Theme controls | Small vertical quick action stack anchored near bottom control: Sun, Light, Focus, add drawer/widget, settings | `xataka-tour-7118ede05.jpg`, `play-06.jpg` |
| Settings | Branded top followed by bold text rows with small outline icons; General, Tiles, Conversations, Root, Search, Wellbeing, Permissions, About | `settings-six-8ecf262cba.jpg` |
| Colors | Near-black base; charcoal primary; lighter grey inset panel; off-white primary text, grey secondary text, fluorescent yellow highlight | `designer-5c869a597e.jpg` |

## Implementation decisions

Support two explicit presets instead of silently combining versions: **Classic**
(four columns, left headings, black base) and **Studio** (five columns, centered
headings, optional system wallpaper). Keep Root on the left, Tiles at center,
Conversations on the right; retain the bottom search pill on all three. Use
swipes and small page indicators, with page selection also available by tapping.
Long-press opens tile/drawer editing and bottom quick controls. App icons must
preserve app identity: use an app's own themed monochrome icon when provided,
otherwise its real icon desaturated. Category symbols are unsuitable substitutes
for app icons. Earlier exploratory category-symbol app icons were replaced.

Use original code, original drawn outline glyphs, and the name Launcher. Research
photos, Ratio trademarks, wallpapers and proprietary icon art are not copied into
the APK. Drawers, theme, tile geometry, labels, highlights, pinned apps, hidden
apps, card visibility and notes persist locally. Usage requires an optional
Android usage grant. Conversations use current message notifications after an
optional notification-access grant; they cannot recreate historical chat data.

Native Android widget hosting sits inside collapsible Root wrappers. A minimal
status row and Launcher-owned pull-down quick panel complement the home screen.
The Android system notification shade is OS-owned and retains the device theme.
Do not invent weather, messages, usage values or installed apps to make the screen
match promotional images. Device data determines actual contents.
