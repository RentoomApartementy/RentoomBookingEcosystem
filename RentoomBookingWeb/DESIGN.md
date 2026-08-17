---
name: RentoomBookingWeb
description: Rentoom's public booking + partner-cooperation site — a boutique property operator's prospectus, not a startup landing page.
colors:
  forest-green: "#1f5a41"
  bronze: "#945f28"
  bronze-hover: "#a66e38"
  cream: "#f7f3ec"
  sage-tint: "#eef4ef"
  sage-line: "#8b9b8c"
  paper: "#f7f7f7"
  white: "#ffffff"
  ink: "#111111"
  border-neutral: "#e5e7eb"
typography:
  display:
    fontFamily: "'Helvetica Neue', Helvetica, Arial, sans-serif"
    fontSize: "clamp(2.5rem, 4.2vw, 4.25rem)"
    fontWeight: 850
    lineHeight: 0.98
    letterSpacing: "-0.045em"
  eyebrow:
    fontFamily: "'Helvetica Neue', Helvetica, Arial, sans-serif"
    fontSize: "0.78rem"
    fontWeight: 800
    lineHeight: 1.4
    letterSpacing: "0.16em"
  body:
    fontFamily: "'Helvetica Neue', Helvetica, Arial, sans-serif"
    fontSize: "1.08rem"
    fontWeight: 400
    lineHeight: 1.65
rounded:
  sm: "10px"
  md: "16px"
  lg: "20px"
  pill: "999px"
  circle: "50%"
spacing:
  sm: "1rem"
  md: "2rem"
  lg: "4rem"
  section-y: "7rem"
components:
  button-primary:
    backgroundColor: "{colors.bronze}"
    textColor: "{colors.white}"
    rounded: "{rounded.pill}"
    padding: "0 2rem"
    height: "42px"
  button-primary-hover:
    backgroundColor: "{colors.bronze-hover}"
  button-secondary:
    backgroundColor: "{colors.white}"
    textColor: "{colors.forest-green}"
    rounded: "{rounded.pill}"
    padding: "0 2rem"
    height: "42px"
  button-tertiary:
    backgroundColor: "{colors.forest-green}"
    textColor: "{colors.white}"
    rounded: "{rounded.pill}"
    padding: "0 2rem"
    height: "42px"
  card:
    backgroundColor: "{colors.white}"
    textColor: "{colors.ink}"
    rounded: "{rounded.lg}"
    padding: "2rem"
---

# Design System: RentoomBookingWeb

## Overview

**Creative North Star: "The Owner's Prospectus"**

RentoomBookingWeb is a boutique property operator's investor prospectus, not a scrappy startup landing page: a huge, tightly-tracked headline (weight 850, letter-spacing -0.045em) states the claim with total confidence, an uppercase bronze eyebrow label frames each section like a document header, and the page alternates warm cream and sage bands the way a printed brochure alternates paper stock. Every shadow in the system is tinted to the color of the element casting it — green cards cast green-tinted shadows, the bronze floating CTA casts a brown-tinted one — so depth reads as material, not generic UI chrome. This is confident and editorial where StayWell (the guest app under the same brand) is warm and tactile: the two apps share a bronze/forest-green DNA but express it in completely different registers, because one is selling trust to a prospective partner or booker and the other is helping someone already in an apartment finish a task.

The site is also unusually data-forward for a marketing surface: the cooperation funnel's payout calculators and cost-comparison tables (see `design-qa.md`) are central to the pitch, not a footnote — the editorial confidence exists to make financial claims feel credible, not just to look good.

**Key Characteristics:**
- Bronze (`#945f28`) is the action color; forest green (`#1f5a41`) is the trust/heading/secondary color — the inverse of StayWell, where green is primary.
- Huge, heavy, tightly-tracked display type (clamp 2.5–4.25rem, weight 850, -0.045em tracking, 0.98 line-height) on the plain system sans stack — no custom display webfont is loaded, only a Material Symbols icon font.
- Uppercase bronze eyebrow labels (0.78rem, weight 800, 0.16em tracking) precede every major section heading.
- Sections alternate white / cream (`#f7f3ec`) / sage (`#eef4ef`) backgrounds as a rhythm device, not randomly.
- Every non-trivial shadow is tinted to match its element's own hue (green elements → green shadow, bronze elements → brown shadow) — shadows are never neutral black.
- Pill-shaped buttons (999px) and circular numbered step badges are the only two "shape signatures"; cards use a more restrained 16–20px radius than StayWell's looser range.

## Colors

Warm-neutral paper backgrounds carrying a two-color brand system: bronze for action, forest green for trust and headings.

### Primary
- **Bronze** (`#945f28`): the site's action color. Primary button fill, eyebrow labels, numbered highlights. Hover state lightens to `#a66e38`. (Two older near-variants, `#8d6133` and `#724e29`, exist in `Home/Components/BlogSection.razor.css` predating the V3 token standardization in `CooperationLandingV3.razor.css` — converge on `#945f28`.)

### Secondary
- **Forest Green** (`#1f5a41`): headings, secondary/tertiary buttons, links, icon accents, and the dominant data-table color (per `design-qa.md`: "pierwszą kolumną danych jest zielony Rentoom" — the first data column in comparison tables is always Rentoom's green). This is a near-identical brand green to StayWell's `#21573e` — the same real brand color, independently drifted between the two codebases; do not "fix" one to match the other without a deliberate cross-app token unification effort.

### Neutral
- **Cream** (`#f7f3ec`): warm paper-toned section background, alternated with white and sage bands.
- **Sage Tint** (`#eef4ef`): cool paper-toned section background, alternated with cream and white.
- **Sage Line** (`#8b9b8c`): the muted border color on secondary/tertiary buttons and form fields — not a fill color, only ever a stroke.
- **Paper** (`#f7f7f7`): the site's plain page background outside the V3 cooperation sections.
- **Ink** (`#111111`): headline and heading text; body copy runs slightly lighter (`#333`–`#353535`), muted/support text lighter still (`#555`–`#666`).

### Named Rules
**The Tinted Shadow Rule.** Shadows take the hue of the element casting them: green cards and panels cast `rgba(31,90,65,…)` shadows, the bronze floating CTA casts `rgba(67,43,20,…)`. A neutral black shadow on a colored surface is a deviation from the system, not a safe default.

## Typography

**Body Font:** 'Helvetica Neue', Helvetica, Arial, sans-serif (system stack; no custom webfont is loaded for text — only the Material Symbols icon font comes from Google Fonts).

**Character:** Confidence is built entirely from weight, size, and negative tracking on a plain system sans — there is no serif, no display face, no italic voice. A 850-weight, -0.045em-tracked headline next to a 400-weight, wide-line-height paragraph does all the hierarchy work.

### Hierarchy
- **Display** (850, `clamp(2.5rem, 4.2vw, 4.25rem)`, 0.98 line-height, -0.045em tracking): hero and section H1s. Note: 850 is a fractional weight that only variable-font-capable system stacks render precisely; most platforms will clamp visually to ~800/900 — treat "as heavy as the platform allows" as the intent, not the literal number.
- **Eyebrow** (800, 0.78rem, 0.16em tracking, uppercase, bronze): the label that precedes every major section heading.
- **Section Title** (weight ~700–800, 2rem): sub-section headers within a page.
- **Body** (400, 1.08rem, 1.65 line-height): paragraph copy; kept wide and airy, contrasting with the tight display type.
- **Support** (400–500, 0.85–1rem, `#555`–`#666`): captions, helper text, muted labels.

### Named Rules
**The Weight-Over-Face Rule.** Every type contrast in this system comes from weight/size/tracking on one system sans stack — introducing a second typeface would undercut the "confident prospectus" read, which depends on typographic restraint.

## Layout

Content is capped at a **1240px** container (confirmed in `design-qa.md` across 1440px, 2203px, and 3440px viewports — the grid width does not grow past 1240px on ultrawide, it just gains margin). Sections are full-bleed bands that alternate white/cream/sage backgrounds for rhythm, each with generous vertical padding (~7rem desktop). The cooperation hero uses an asymmetric two-column grid (copy left, image right) rather than a centered layout. Mobile collapses to a single column with a fixed bottom CTA bar below 767px, and the CTA switches from a full-width bar to a pill above 767px — the mobile-hero breakpoint is a firm 767px boundary, not fluid.

## Elevation & Depth

Layered, not flat: cards and panels lift off their band with soft, wide shadows, and — per the Tinted Shadow Rule — every shadow's color matches the hue of the surface casting it rather than defaulting to neutral black.

### Shadow Vocabulary
- **Card lift, green** (`0 18px 48px rgba(31,90,65,.07)` to `0 24px 60px rgba(31,90,65,.14)`): calculators, feature cards, technology panels.
- **Hero image frame** (`0 18px 50px rgba(28,45,37,.12)`): the framed photo/stat card inside the hero.
- **Bronze CTA lift** (`0 14px 34px rgba(67,43,20,.28)`, deepening to `0 17px 40px rgba(67,43,20,.34)` on hover): the floating audit/cooperation CTA — brown-tinted because the element itself is bronze.

### Named Rules
**The Tinted Shadow Rule.** (See Colors.) Repeated here because it governs Elevation as much as Color: never substitute a neutral black shadow for the hue-matched one an element already has.

## Shapes

More restrained than StayWell: cards and panels use **16–20px** radius (rarely smaller, rarely larger), buttons and the floating CTA are true pills (**999px**/**100px**), and numbered step markers are perfect circles (**50%**). There is no neumorphism, no glassmorphism/blur, and no sharp 0-radius surface anywhere in the V3 cooperation system — corners are rounded but restrained, not the looser, wider StayWell range (12–25px with frequent pill/circle use for small controls too).

## Components

### Buttons
Three semantic variants, all pill-shaped (999px), 42px tall, defined once in `Components/Shared/Button.razor.css`:
- **Primary:** solid bronze (`#945f28`) fill, white text; hover lightens to `#a66e38`.
- **Secondary:** white fill, `1px solid #8b9b8c` border, forest-green text; hover fills light gray.
- **Tertiary:** solid forest-green (`#1f5a41`) fill, `1px solid #8b9b8c` border, white text; hover deepens to `#277051`.
- **Disabled:** flat `#7a7a7a` fill, sage-line border, `cursor: not-allowed`.
- **Interaction:** a genuine ripple effect on click (expanding circle from the click point, `rgba(255,255,255,0.4)` on colored buttons, `rgba(31,90,65,0.2)` on the secondary/white button) — a deliberate tactile touch worth preserving in new button work.
- **Small variant:** 34px height, 0.85rem text, same shape language.

### Cards / Panels
- **Corner Style:** 16–20px radius.
- **Background:** solid white, or the section's cream/sage band color.
- **Shadow Strategy:** hue-tinted (see Elevation) — never neutral.
- **Border:** thin (1px) neutral (`#e5e7eb`) or brand-tinted at low opacity (`rgba(31,90,65,.08–.14)`), used to separate panels within a section more often than to frame a single card.

### Eyebrow Label
- **Style:** uppercase, 0.78rem, weight 800, 0.16em letter-spacing, bronze text, block-level, sits directly above a section's Display headline. This is the system's primary "we are entering a new topic" signal — use it, don't invent a different section-intro pattern.

### Numbered Step Badge
- **Style:** perfect circle, solid forest-green fill, white bold number, often ringed by a matching-color halo (`box-shadow: 0 0 0 10px [section background]`) that visually punches the circle through the section's band color.

### Navigation
- **Style:** transparent over the hero image with white icons/text; once scrolled, the bar becomes solid `#f7f7f7` and icons/text switch to forest green. This transparent-to-solid transition is the header's defining behavior — don't ship a permanently-solid or permanently-transparent header variant without deliberately overriding it.

## Do's and Don'ts

### Do:
- **Do** use bronze (`#945f28`) for the primary action and forest green (`#1f5a41`) for secondary/trust/data — that assignment is inverted from StayWell and both are correct for their own app.
- **Do** tint every non-trivial shadow to the hue of the element casting it (The Tinted Shadow Rule); never fall back to neutral black.
- **Do** precede a new major section with an uppercase bronze eyebrow label above the Display headline.
- **Do** keep card/panel radius in the 16–20px range; reserve 999px/100px for pill buttons and CTAs only.
- **Do** alternate white/cream/sage section backgrounds for rhythm rather than stacking same-color sections back to back.

### Don't:
- **Don't** introduce a second typeface or a custom display webfont — the system's confidence comes from weight/tracking restraint on one system sans, not typographic variety.
- **Don't** borrow StayWell's frosted-glass/backdrop-blur or neumorphic treatments here; this system is flat-surface-with-tinted-shadow, not glass-and-glow.
- **Don't** treat the two brand greens (`#1f5a41` here, `#21573e` in StayWell) as the same token — they're independently maintained today even though they represent the same brand color.
- **Don't** ship a header that's permanently solid or permanently transparent; the scroll transition is load-bearing for the hero's full-bleed image treatment.
