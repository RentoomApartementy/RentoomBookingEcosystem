---
name: StayWell
description: Rentoom's guest self-check-in app — a warm lodge keycard, not a corporate kiosk.
colors:
  forest-green: "#21573e"
  cabin-brown: "#965f28"
  sage: "#8b9c8c"
  lantern-gold: "#f9b806"
  ink: "#222222"
  paper: "#f7f7f7"
  white: "#ffffff"
  confirm-green: "#249223"
  alert-red: "#dc2626"
typography:
  title:
    fontFamily: "Montserrat, sans-serif"
    fontSize: "1.3rem"
    fontWeight: 700
    lineHeight: 1.2
  body:
    fontFamily: "Montserrat, sans-serif"
    fontSize: "0.95rem"
    fontWeight: 400
    lineHeight: 1.4
  label:
    fontFamily: "Montserrat, sans-serif"
    fontSize: "0.8rem"
    fontWeight: 700
    lineHeight: 1
    letterSpacing: "normal"
rounded:
  sm: "8px"
  md: "12px"
  lg: "15px"
  xl: "20px"
  pill: "999px"
  circle: "50%"
spacing:
  xs: "8px"
  sm: "12px"
  md: "16px"
  lg: "24px"
components:
  button-primary:
    backgroundColor: "{colors.white}"
    textColor: "{colors.ink}"
    rounded: "{rounded.lg}"
    padding: "8px 12px"
    height: "40px"
  button-signature:
    backgroundColor: "{colors.forest-green}"
    textColor: "{colors.lantern-gold}"
    rounded: "{rounded.lg}"
    padding: "8px 12px"
    height: "40px"
  card-tile:
    backgroundColor: "{colors.white}"
    textColor: "{colors.ink}"
    rounded: "{rounded.lg}"
    padding: "18px"
---

# Design System: StayWell

## Overview

**Creative North Star: "The Lodge Keycard"**

StayWell is the moment a guest arrives at a Rentoom apartment with no host in sight, and the app has to feel like a warm, competent concierge handing over a physical keycard — not a corporate self-service kiosk. The palette pulls from timber and forest (deep forest green, cabin brown, sage), every card and button is heavily rounded like a river stone or a rounded keycard corner, and the primary buttons are glossy and frosted — a gradient-and-glass surface with an inset white highlight that reads as something you press, not just tap. The one moment the whole app has been building to — unlocking the door — gets its own singular treatment: a neumorphic, pressed-metal dial unlike any other control in the app, and the one button style allowed to glow (a pulsing lantern-gold ring around a forest-green fill).

Nothing in StayWell reaches for a big marketing hero moment: type stays compact (rarely above 1.3rem), density is high, and screens are built to be finished quickly by someone standing in a hallway with a suitcase, not lingered in. Warm materials, efficient task flow.

**Key Characteristics:**
- Forest green + cabin brown + sage: an earthy, lodge palette, not a corporate blue/gray one.
- Heavy, consistent rounding (12–18px on cards and buttons, pill/999px on badges, perfect circles on icon controls) — almost nothing in the app has a sharp corner.
- Frosted glass surfaces: buttons and tiles use semi-transparent white gradients with `backdrop-filter: blur(16px)` over whatever sits behind them, not flat opaque fills.
- Buttons feel physically pressable: a glossy top-lit gradient, an inset white highlight, and a genuinely unusual pressed state (color inversion + downward shift) rather than a simple opacity or darken.
- Lantern-gold glow is reserved for exactly one thing: the signature "open the door" action. It does not appear anywhere else.
- Compact, utilitarian type scale — no large display type; the largest text in the app is a 1.3–1.6rem bold title.

## Colors

Warm and earthy, built around one dominant forest green with brown/sage as its supporting cast, and a gold accent kept deliberately rare.

### Primary
- **Forest Green** (`#21573e`): the app's one true action color — primary CTAs, active nav states, icon accents, selected-state borders/backgrounds (at low opacity, e.g. `rgba(33,87,62,0.1–0.45)`). Component-local variants (`#1F5A41`, `#1E4D40`, `#1a4532`, `#1c4a35`, `#1f5a42`) drift slightly from screen to screen; treat `#21573e` as the canonical value and the others as drift to converge, not intentional variation.

### Secondary
- **Cabin Brown** (`#965f28`): the "Well" half of the StayWell wordmark ([Header.razor.css:41](Header.razor.css)), the AI-chat bot bubble, and warm secondary accents. Lighter tint `#a97949` exists as a root variable (`--rentoom-brown-light`) but is rarely referenced directly in components today.

### Tertiary
- **Sage** (`#8b9c8c`): a quiet, muted support tone — seen in the live-chat header background. Underused relative to how often it's defined; a candidate for more secondary-surface duty rather than staying a one-off.

### Neutral
- **Ink** (`#222222`): primary text and dark UI chrome (button borders, headings). Actual text color is inconsistent across components — `#000`, `#111`, `#222`, `#333` and `rgba(0,0,0,0.9)` are all used for "dark text" in different files; `#222` / `rgba(0,0,0,0.9)` is the most common and should be treated as canonical.
- **Paper** (`#f7f7f7`): the app's off-white neutral background (`--rentoom-neutral-white`), used behind the footer nav and skeleton-loading gradients.
- **White** (`#ffffff`): card and button surfaces, almost always at reduced opacity (`0.6`–`0.96`) with a blur behind them rather than solid white.

### Signature
- **Lantern Gold** (`#f9b806`): appears in exactly one place in the codebase — the "benefits-club" signature button variant in [Button.razor.css:82-107](Components/Button.razor.css) — as the border/text color and the color of a pulsing glow (`0 0 8px…0 0 32px rgba(249,184,6,…)` animating over 6s). This is the app's most important color precisely because of how rarely it appears.

### Semantic
- **Confirm Green** (`#249223`): success/selected states in checkout and registration flows (border + tinted background). Distinct from Forest Green — don't merge the two; Confirm Green means "this choice is accepted," Forest Green means "this is the brand action."
- **Alert Red** (`#dc2626`): warnings, destructive actions, form validation errors. Observed with drift (`#dc3545`, `#d32f2f`, `#e53935`, `#d00`) across different components; converge new work on `#dc2626`.

### Named Rules
**The One Glow Rule.** Lantern-gold glow is reserved for the app's single signature call-to-action (the "open apartment"/benefits moment). No other button, badge, or state may use the gold glow treatment — its rarity is what makes it register as "the important one."

## Typography

**Body Font:** Montserrat (with system sans-serif fallback) — the only typeface used across the entire app; no serif or display face exists.

**Character:** A single, workmanlike sans across every weight from 400 to 800. Hierarchy is carried by weight and size, not by switching families — nothing in StayWell should introduce a second typeface.

### Hierarchy
- **Title** (700, 1.3–1.6rem, 1.2 line-height): screen and section headers, the reservation/apartment name.
- **Emphasis** (700–800, 0.9–1.2rem): prices, quantities, and other numbers the guest needs to register at a glance — 800 weight is reserved for the single most important number on a given card (e.g. total price).
- **Body** (400–500, 0.85–1rem, 1.4 line-height): descriptive copy, instructions, form labels.
- **Label** (600–700, 0.65–0.85rem): tags, category chips, badge text; frequently paired with the pill radius.

### Named Rules
**The Weight-Not-Family Rule.** Every type distinction in StayWell is expressed through `font-weight` and `font-size` on Montserrat — never introduce a second font family for hierarchy.

## Layout

Single-column, mobile-first screens (this is a phone-in-hand app, not a responsive desktop site) built from stacked cards with consistent 16–24px outer padding. Content sits inside frosted-glass panels over a plain background rather than a bordered grid. Spacing is dense and functional: 8px between related items, 16–24px between distinct sections. Sticky/floating elements are common — a bottom footer nav, a floating action button for the upsell cart, and modal sheets that slide up from the bottom — reflecting that this is a task tool used one-handed while standing.

## Elevation & Depth

Two distinct elevation systems coexist, deliberately:

1. **Everyday soft-drop shadows** for nearly everything — cards, tiles, buttons, modals. The near-universal card shadow is `0 2px 12px 0 rgba(0,0,0,0.1)`; buttons get a livelier `0 4px 16px -2px rgba(0,0,0,0.12), 0 1.5px 0 0 #fff inset` with a brighter `0 8px 24px -4px rgba(0,0,0,0.18)` on hover. This is ambient lift, not structural — it says "this is a surface," not "this is far above the page."
2. **One neumorphic exception**: the door-unlock control on [OpenApartmentPage.razor.css:98-108](Pages/OpenApartmentPage.razor.css) uses a pressed dual-shadow (`10px 10px 20px #bebebe, -10px -10px 20px #ffffff`, inset on press) found nowhere else in the app. This is intentional, not an inconsistency to fix — it marks the one control that should feel like a real physical dial you turn, distinct from every flat-surface button around it. Do not generalize this treatment to other components.

### Shadow Vocabulary
- **Card lift** (`box-shadow: 0 2px 12px 0 rgba(0,0,0,0.1)`): default resting elevation for cards, tiles, and panels.
- **Button rest** (`box-shadow: 0 4px 16px -2px rgba(0,0,0,0.12), 0 1.5px 0 0 #fff inset`): default button elevation, including the inset gloss highlight.
- **Button hover** (`box-shadow: 0 8px 24px -4px rgba(0,0,0,0.18), 0 2px 0 0 #fff inset`): lift on hover/focus.
- **Signature glow** (`box-shadow: 0 0 8px–32px rgba(249,184,6,0.3–0.7), 0 4px 16px -2px rgba(33,87,62,0.18)`, animated): the lantern-gold pulse, exclusive to the signature button.
- **Unlock dial** (`box-shadow: 10px 10px 20px #bebebe, -10px -10px 20px #ffffff`, inset on press): the one neumorphic control.

### Named Rules
**The Single Dial Rule.** Neumorphism is not a system-wide material; it belongs to exactly one control (the door unlock). Everything else uses soft ambient drop-shadow.

## Shapes

Rounded almost everywhere, in a fairly wide but consistent range: **15px** is the default card/tile/button radius (the single most common value in the codebase), **12px** for smaller inline elements, **18–22px** for larger feature cards and the header's reservation pill, **999px** for pill badges and toggle tracks, and true circles (**50%**) for icon-only buttons and avatars. Sharp (0-radius) corners appear only on a couple of full-bleed modal variants where the sheet meets the screen edge. There is no square/sharp design language anywhere in the primary UI — a new component with hard corners would immediately read as foreign.

## Components

### Buttons
- **Shape:** 15px radius, 40px height, full-width by default.
- **Primary (default):** Frosted glass — `linear-gradient(180deg, rgba(255,255,255,0.85) 80%, rgba(255,255,255,0.65) 100%)` over `backdrop-filter: blur(16px)`, `1.75px solid #222` border, black text, gloss-highlight shadow.
- **Hover:** Deepens the lift shadow and shifts the gradient cooler (`#f8f8ff → #e0e0f8`).
- **Active/Pressed:** A distinctive `filter: invert(1)` plus `translateY(2px)` and a flattened shadow — buttons visibly invert color when pressed, not just darken. This is a deliberate, unusual signature; preserve it rather than "fixing" it to a conventional darken-on-press.
- **Disabled:** Flat gray gradient (`#eaeaea → #d5d5d5`), `#bbb` border, `#aaa` text, no shadow.
- **Signature ("benefits-club" / unlock-adjacent):** Forest-green gradient fill, lantern-gold border and text, animated gold glow (see Elevation). Reserve for the single most important action per screen.

### Cards / Containers
- **Corner Style:** 15px radius (the default), occasionally 12px for denser inline cards.
- **Background:** Frosted white (`rgba(255,255,255,0.6–0.96)`) with `backdrop-filter: blur(16px)`, not solid white — content behind cards should remain faintly perceptible.
- **Shadow Strategy:** Card-lift shadow (see Elevation). Selected state adds a forest-green-tinted border and a deeper forest-green-tinted shadow rather than a color change to the fill.
- **Border:** 2px transparent by default, becoming a tinted brand-color border only on selected/active state.
- **Internal Padding:** 18px is standard for a tile; larger feature cards use 24px+.

### Badges / Chips
- **Style:** Pill radius (999px), small (`0.65–0.85rem`) bold label text, tinted background at 8–12% opacity of the relevant semantic color (forest green for neutral tags, red for alerts).
- **Notification badge:** small solid-red circle (`#e53935`) with white bold micro-text, absolutely positioned over an icon — used for unread chat counts.

### Inputs / Fields
- **Style:** Light gray/white background, thin (1–1.5px) neutral border, 8–16px radius depending on context.
- **Focus/Valid:** Border shifts toward the relevant semantic color (green for valid/selected, red for invalid) with a soft tinted background wash; no heavy glow ring except the deliberate `outline: 3px solid rgba(59,130,246,0.35)` accessibility focus ring on a couple of interactive controls (toggle, action card) — keep that ring for keyboard-focus visibility, don't remove it as "off-brand blue."
- **Error/Disabled:** Alert-red border and text for errors; reduced opacity plus `cursor: not-allowed` for disabled.

### Navigation
- **Footer nav:** Frosted white bar, pill/rounded-top container, forest-green active-state icon/label with a tinted rounded background behind the active item — no underline-style active indicators anywhere in the app.
- **Header:** Transparent background, two-tone "Stay"(green)/"Well"(brown) wordmark, a rounded reservation-ID pill on light gray.

### Unlock Dial (signature component)
The circular door-unlock control is StayWell's one deliberately different object: neumorphic (pressed-metal) shadow instead of drop-shadow, true circle instead of a rounded-rect, and it's the only control in the app built to feel like a physical mechanism rather than a screen element. Any redesign of the reservation/unlock flow should protect this control's distinctness rather than folding it into the standard card/button language.

## Do's and Don'ts

### Do:
- **Do** keep lantern-gold glow exclusive to the single signature action per screen (The One Glow Rule).
- **Do** use 15px as the default radius for new cards/buttons/tiles unless the element is a badge (999px) or icon control (50%).
- **Do** use `backdrop-filter: blur(16px)` with a translucent white fill for new surface-level components, not solid opaque backgrounds.
- **Do** preserve the invert-on-press button interaction as StayWell's signature press feedback.
- **Do** keep the unlock dial's neumorphic shadow unique to that one control.
- **Do** reference the existing `--rentoom-green-dark`, `--rentoom-brown-dark`, `--rentoom-brown-light`, `--rentoom-green-sage` custom properties (defined in `wwwroot/css/app.css`) in new CSS instead of re-typing the hex — most current components hardcode the hex directly, which is drift to fix opportunistically, not a pattern to keep extending.

### Don't:
- **Don't** introduce a second typeface; all hierarchy comes from Montserrat weight/size.
- **Don't** add large display/hero type — the largest text anywhere in the app is ~1.6rem; this is a task tool, not a marketing surface.
- **Don't** apply the neumorphic shadow to any control other than the unlock dial.
- **Don't** use flat, opaque white cards where the app's convention is translucent frosted glass.
- **Don't** treat Confirm Green (`#249223`) and Forest Green (`#21573e`) as interchangeable — one means "brand action," the other means "this choice is accepted."
