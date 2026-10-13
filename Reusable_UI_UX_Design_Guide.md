# Reusable UI UX Design Guide

## Purpose and use

This specification translates the supplied Wand desktop screenshot into a reusable visual and interaction system. Use it to give a different software product the same calm, dense, dark workspace character while preserving that product's own purpose, terminology, data, and workflows. The intended reader is a designer or coding agent working in an existing or new application.

The defining composition is a persistent navigation rail, a contextual workspace header, a broad primary work area, and optional supporting panels. Charcoal surfaces, rounded containers, compact typography, restrained borders, a white primary action, and selective atmospheric imagery create the visual identity. The result should feel like a working application with useful tools close at hand.

The guide has three evidence labels. **Observed** means directly visible in the screenshot. **Estimated** means visually approximated from the image, including dimensions and typography. **Prescribed** means a reusable implementation decision or extension; it does not claim that the original software behaves that way. Default behavior, hover states, responsiveness, backend operations, and accessibility semantics cannot be established from a single screenshot. Normative words such as must and should apply to the reusable system defined here.

This is a design guide, not an installable skill or a requirement to build the reference application's features. The Markdown edition is intended for coding agents. The Word edition presents the same specification for human review.

## 1 Design identity and priorities

### 1.1 The visual character

**Observed.** The reference combines a near-black working surface with an atmospheric image behind the top portion of the page. The sidebar and work panels remain legible through dark overlays. Most controls use gray rather than vivid brand colors. Rounded rectangular cards coexist with pill buttons and circular icon controls. Selected navigation and active filters are filled gray. The most prominent task action is a white pill near the upper right.

**Observed.** Visual weight follows task importance. The page title and primary action lead; the map and tool panel hold the central workspace; the assistant and media area form a right support column. Small badges and colorful map pins add limited saturation. Interface chrome is secondary to content.

**Prescribed.** Preserve these relationships before matching individual pixels. A dark theme alone is insufficient. The implementation should retain the compact spacing, asymmetric working layout, soft corner language, monochrome action hierarchy, shallow surface layering, and clear contextual identity.

### 1.2 Fixed traits and adaptable traits

| Trait | Preserve across products | Adapt to the target product |
| --- | --- | --- |
| Surfaces | Charcoal canvas with a small number of nearby tones | Where panels are needed and how many appear |
| Geometry | Rounded panels, pills, circular utility controls | Size according to content and input method |
| Hierarchy | Bright task action, muted support controls | The action label and its actual operation |
| Composition | Navigation plus a contextual working canvas | Number of columns and the primary work object |
| Density | Compact but readable rows and toolbars | Spacious variants for forms and touch |
| Imagery | Optional subdued context image above work content | Product-specific, appropriate imagery or none |
| Accent | Small deliberate color highlights | Brand, status, categories, and accessibility |
| Content | Short functional labels and visible context | All nouns, records, options, messages, and features |

### 1.3 Priorities when requirements conflict

Keep the target product's task and information hierarchy first, accessibility and usable behavior second, then the reference's visual relationships. Exact source geometry is the lowest priority outside a matching desktop viewport. A spreadsheet may need a wide grid; a messaging product may need a conversation column; a medical records product may need prominent critical alerts. Apply the theme to those needs instead of inserting irrelevant modules.

## 2 Reference composition and measurements

### 2.1 Coordinate convention

The supplied image is 1435 by 820 pixels. Coordinates below use the upper-left corner as x 0 and y 0. Rectangles are estimated visible bounds in image pixels, not CSS measurements or verified implementation values. Display scaling and browser zoom are unknown. Parts of the bottom content extend beyond the captured image.

| Region | Approximate bounds x y width height | Relationship |
| --- | --- | --- |
| Global top bar | 0 0 1435 58 | Full-width utility strip |
| Left navigation rail | 0 58 262 762 | About 18 percent of image width |
| Workspace header | 286 78 1117 51 | Context on left and actions on right |
| Main content origin | 286 146 | About 24 px from the rail boundary |
| Main and middle group | 286 146 705 674 plus | Primary working column |
| Large visual panel | 286 146 465 251 | Dominant visual in top row |
| Compact media panel | 767 146 224 251 | Secondary panel beside visual |
| Tool settings panel | 286 412 705 408 plus | Full width below the top row |
| Right support column | 1007 146 400 674 plus | About 36 percent of usable width |
| Assistant panel | 1007 146 400 312 | Top support card |
| Media and promotion panel | 1011 479 400 341 plus | Lower support card |

### 2.2 Spatial system

**Estimated.** The header-to-content separation is about 16 to 20 px. The main content uses about 16 px gaps between cards and between columns. The right edge has about 28 px breathing room. The rail has 12 to 16 px inner padding. The top-left visual and its neighboring small card share a common height and top edge. The settings panel shares the left edge and full width of the pair above it.

**Prescribed.** Use a flexible grid rather than absolute placement. At a similar width, start with a 256 to 264 px navigation rail, 24 px workspace padding, 16 px gutters, and a 2-column workspace split near 64 to 36. Split the primary area's top row roughly 2.1 to 1 only when two appropriate modules exist. Let long content determine height. Do not force the screenshot's partially cropped content into fixed-height cards.

### 2.3 Surface depth

**Observed.** The top is brighter and more translucent than the lower work area. Cards are separated mainly by thin outlines, slight fill changes, corner radius, and spacing. Deep drop shadows are not a defining feature. Rows inside the settings panel appear marginally lighter than the canvas.

**Prescribed.** Use no more than five semantic surface levels: canvas, navigation, panel, inset group, and interactive fill. The atmospheric image occupies a separate decorative layer behind the workspace header. It should fade before it competes with long reading or detailed controls.

## 3 Complete visible component inventory

Every visible category is listed below. Several symbols communicate a probable purpose, but the screenshot does not verify click behavior or hidden menus. Do not assign unknown utilities an invented function.

### 3.1 Global bar

| ID | Visible element | Appearance and transferable role |
| --- | --- | --- |
| G01 | Wand wordmark | White compact logo at far left; target product identity |
| G02 | Back arrow | Muted icon control; history navigation if appropriate |
| G03 | Forward arrow | More muted arrow; history availability state is unverified |
| G04 | Home circle | Circular filled utility control before search |
| G05 | Search field | Wide rounded field with magnifier and Search placeholder |
| G06 | Help icon | Small outlined question circle in right utility cluster |
| G07 | Yellow display symbol | Small display-shaped utility; exact function unknown |
| G08 | Half-light circle | Appearance-related symbol; exact function unknown |
| G09 | Profile avatar | Purple circular avatar containing a letter |
| G10 | Support and upgrade pill | Compact adjoining support label and white upgrade portion |
| G11 | Vertical separator | Thin boundary before native window controls |
| G12 | Window controls | Minimize, maximize, and close at far right |

### 3.2 Navigation rail

| ID | Visible element | Appearance and transferable role |
| --- | --- | --- |
| N01 | Current context capsule | Rounded gray container with small thumbnail and truncated title |
| N02 | Context downward mark | Small mark under context text; interaction is unverified |
| N03 | Context overflow | Circular ellipsis button inside context capsule |
| N04 | Context quick action | White circular button with dark play symbol |
| N05 | Primary navigation list | Five icon and label entries in a vertical stack |
| N06 | Colored navigation icon | Magenta flame-like symbol on one otherwise neutral item |
| N07 | Collection heading | Uppercase My Games label, small symbol, count of 6 |
| N08 | Collection utility | Small opposing-arrow symbol near collection heading |
| N09 | Entity list | Six thumbnail and title rows; long text truncates |
| N10 | Active entity row | Rounded medium-gray filled row with brighter text |
| N11 | Rail divider | Thin vertical line between sidebar and workspace |

### 3.3 Workspace header

| ID | Visible element | Appearance and transferable role |
| --- | --- | --- |
| H01 | Context thumbnail | Small portrait-oriented image next to page title |
| H02 | Page title | Large bright bold entity name |
| H03 | Favorite symbol | Outlined star adjacent to title |
| H04 | Notification symbol | Small outlined bell next to favorite |
| H05 | Background artwork | Cropped image across the header and upper workspace |
| H06 | Help circle | Large muted round control with question mark |
| H07 | Adjustment circle | Round utility with slider-like icon |
| H08 | Video circle | Round utility with camera-like icon |
| H09 | Feature capsule | Dark pill with lightning symbol and small Pro badge |
| H10 | Primary action | White rounded Play button with dark icon and label |
| H11 | Adjacent chevron | Dark segment or adjoining button for action alternatives |

### 3.4 Visual and media modules

| ID | Visible element | Appearance and transferable role |
| --- | --- | --- |
| V01 | Visual panel frame | Large rounded rectangle with clipped content |
| V02 | Visual panel header | Medium-gray band with map icon, title, and chevron |
| V03 | Map-like canvas | Pale detailed geographic image inside dark workspace |
| V04 | Category markers | Pink, orange, and purple pin-like symbols |
| V05 | Zoom control | Stacked white plus and minus controls on right |
| V06 | Location-like control | Separate white circular target/pin control below zoom |
| V07 | Visual watermark | Small white wordmark at upper-right of map image |
| V08 | Media panel header | Icon and Highlights label |
| V09 | First media thumbnail | Rounded landscape image with centered play circle |
| V10 | Second media tile | Darkened tile and faint centered play triangle |
| V11 | Media panel scrollbar | Thin internal vertical thumb near panel's right edge |

### 3.5 Tool settings module

| ID | Visible element | Appearance and transferable role |
| --- | --- | --- |
| T01 | Parent panel | Dark rounded outlined container below visual row |
| T02 | Panel heading | Adjustment icon, Customize label, and chevron |
| T03 | Segmented category group | Four compact icon and label choices in one pill-like track |
| T04 | Active category | Filled gray All segment, brighter label and icon |
| T05 | New capability pill | Plus, New label, and Pro badge adjoining filters |
| T06 | First settings group | Inset rounded section headed Player with person icon |
| T07 | Group collapse symbol | Very small right-side chevron-like indicator |
| T08 | Repeated setting rows | Lightning-like icon, short label, right-aligned control |
| T09 | Info indicators | Small filled circles with i next to some setting labels |
| T10 | Binary controls | Dark pill controls showing Off and On text |
| T11 | Numeric control | Minus, current value 0, and plus in a rounded track |
| T12 | Following group | Inventory icon and heading visible at image bottom |

### 3.6 Assistant and lower support module

| ID | Visible element | Appearance and transferable role |
| --- | --- | --- |
| S01 | Assistant frame | Rounded outlined panel with reading and input regions |
| S02 | Assistant heading | Book-like icon, Game Guide label, small Beta badge |
| S03 | Time separator | Today label between thin horizontal rules |
| S04 | Assistant identity | Small white symbol at start of introduction |
| S05 | Introduction | Two-line contextual invitation; main context name is bold |
| S06 | Suggested prompt | Large rounded dark card with one short starter request |
| S07 | Composer | Wide gray pill with placeholder and circular upward-arrow button |
| S08 | Accuracy note | Tiny centered low-contrast line below composer |
| S09 | Assistant scrollbar | Narrow internal vertical scroll indicator on right |
| S10 | Lower support container | Tall dark panel beginning with blank dark space |
| S11 | Media title band | Narrow overlay identifying upcoming content |
| S12 | Large video region | Landscape image spanning support panel width |
| S13 | Timeline | Bright yellow-green progress track near video bottom |
| S14 | Playback controls | Play, muted audio icon, volume slider, remaining time, fullscreen |
| S15 | Promotional footer | Ad-removal text, Pro badge, and outlined upgrade pill |
| S16 | Workspace scroll indicator | Very thin bar near far-right window edge |

### 3.7 How to use the inventory

Treat the IDs as a reference checklist, not mandatory features. A target app may use G05 as global entity search, N09 as project navigation, H10 as Publish, V03 as a document preview, T08 as preferences, and S01 as contextual help. Components with no useful target-product role should be omitted. Native window controls belong to a desktop host; do not reproduce a fake title bar inside an ordinary website.

## 4 Semantic tokens and exact implementation defaults

### 4.1 Color evidence and palette

**Observed sample values.** Single-pixel samples at non-text locations returned canvas and sidebar #0D0F12, an inset row #101113, a category track #191B1E, a selected entity background #3D3F41, a composer background #313335, and primary action white #FFFFFF. These values describe those sampled pixels only; overlays, nearby artwork, and compression can affect appearance. They are not recovered source CSS.

**Prescribed defaults.** The palette below keeps the reference's relationships while raising the readability of subdued controls. Resolve color through semantic variables so the target product can override its brand and status tones without rewriting components.

| Token | Default | Use |
| --- | --- | --- |
| color.canvas | #0D0F12 | App background |
| color.navigation | #101215 | Sidebar base |
| color.panel | #111316 | Outer work cards |
| color.inset | #17191D | Inner groups and secondary tiles |
| color.control | #292C31 | Inputs and neutral utility controls |
| color.controlHover | #35383E | Pointer hover on neutral controls |
| color.selected | #3D3F44 | Selected navigation or active segment |
| color.borderSubtle | #2A2D33 | Decorative panel separators |
| color.borderControl | #707783 | Boundaries when needed to identify controls |
| color.textPrimary | #F4F5F7 | Titles and principal labels |
| color.textSecondary | #B8BCC5 | Supporting text and icons |
| color.textMuted | #969DA9 | Metadata and helper text |
| color.primaryFill | #FFFFFF | Highest-priority task action |
| color.primaryText | #20242C | Text and icons on white action |
| color.focus | #ADB8FF | Visible focus outline |
| color.accent | #A78BFA | Limited product accents |
| color.success | #6ED9A0 | Success text or icon |
| color.warning | #FFD37A | Warning text or icon |
| color.danger | #FF8A9A | Errors and destructive emphasis |
| color.info | #8CC8FF | Informational text or icon |

Borders on decorative panels may be subtle. If a boundary alone communicates that an element is interactive, use sufficient contrast or combine it with a visible label, icon, and distinct fill. Status colors above are foreground candidates on dark surfaces; do not assume white text on these fills is readable. Validate every rendered color pairing.

### 4.2 Spacing and geometry

| Token group | Default values | Application |
| --- | --- | --- |
| Spacing | 4 8 12 16 24 32 48 px | Use semantic spacing from this scale |
| Page padding | 24 px desktop, 16 px compact | Main content inset |
| Card gap | 16 px | Related panels in grid |
| Panel padding | 16 to 20 px | Inner panel content |
| Row padding | 12 to 16 px horizontal | Align labels across groups |
| Small radius | 6 px | Thumbnails and small tags |
| Medium radius | 10 to 12 px | Navigation selection and inner groups |
| Panel radius | 18 px | Main cards; reference estimate 18 to 22 px |
| Pill radius | 999 px | Buttons, segmented tracks, composer |
| Border width | 1 px | Panel outlines and separators |
| Focus outline | 2 px with 2 px offset | Keyboard focus visibility |
| Icon | 16 to 20 px standard, 24 px prominent | One consistent icon family |
| Navigation row | 32 to 36 px desktop | Compact entity list |
| Setting row | 48 to 52 px | Label and control pairs |
| Standard control | 36 px desktop, 44 px touch | Button and input height |
| Primary action | 44 px height | Header task action |
| Circular utility | 40 to 44 px | Main header controls |

Visible icons may be small while the interactive hit area remains larger. Use at least 24 by 24 CSS px as a baseline target, subject to applicable spacing exceptions, and prefer 44 by 44 for touch. Reference pixel estimates are not proof of CSS target sizes.

### 4.3 Typography

**Estimated.** The screenshot uses a neutral sans serif with substantial weight for headings and labels. The page title appears about 24 px and most labels about 14 px. Some metadata and badges are about 10 to 12 px. The exact family and weights are unknown.

**Prescribed.** Use the target app's existing neutral sans serif if compatible, otherwise Inter if available, with system UI fallbacks. Do not download fonts merely to match an unverified family. Use 24/30 px semibold for desktop context title, 16/22 px semibold for panel headings, 14/20 px medium for controls, 14/21 px regular for body text, and 12/18 px for metadata. Badges may use 10/14 px only for short supplemental labels. Essential instructions must remain comfortably readable.

Use weight and brightness to establish hierarchy before increasing size. Avoid condensed body type, decorative headings, and excessive uppercase. Uppercase is appropriate for short collection labels or compact category tabs; longer navigation and controls use normal capitalization. Use tabular numbers in timestamps, metrics, and numeric inputs.

### 4.4 Icons and elevation

Use one consistent icon family, mostly 1.5 to 2 px strokes or consistently filled glyphs. The reference mixes outlined utility symbols with heavier filled navigation icons; this is a role distinction, not permission to mix arbitrary icon styles. Pair unfamiliar symbols with labels or tooltips. Keep decorative imagery out of the icon system.

Cards normally have no shadow or a very subtle one. Overlays may use 0 12px 32px rgba(0,0,0,.35) and a visible border. Raising every card with a heavy shadow would change the visual character. Use opaque surfaces as the fallback when translucent effects are unavailable.

## 5 Layout and navigation contracts

### 5.1 App shell

The shell owns global navigation, context selection, global search, and the workspace frame. Its content area must allow children to shrink with min-width 0 and must not create accidental horizontal overflow. On a reference-sized desktop, use a roughly 58 px top utility bar and a 260 px rail. These are prescribed starting values based on the captured geometry.

The shell should normally provide one page scroll container. If the product requires independently scrolling conversations or entity lists, give them explicit containment and keep essential controls reachable. Avoid making every card a nested scroll area. Sticky positions must account for the global bar and avoid covering focused elements or anchor targets.

### 5.2 Global utility bar

The product logo remains compact and does not compete with the context title. History buttons only appear if the application has meaningful history behavior. Search occupies a calm rounded field and may have a keyboard shortcut hint if that shortcut exists. Profile, help, and preferences are grouped at the right, with adequate spacing between unrelated actions.

Use a consistent height and center all controls vertically. When width is limited, collapse secondary utilities into an overflow menu before truncating the primary task action. The bright action remains in the contextual header, not duplicated throughout the shell without a reason.

### 5.3 Context selector and entity navigation

The context capsule combines an image or initial, a short label, and optional quick actions. A context menu should reveal the full current name and alternatives. Overflow actions must not activate context navigation accidentally. Keep separate interactive elements as separate buttons; never nest a button in a button.

The entity list uses stable keys and consistent thumbnail sizes. Selected state uses the filled gray background, stronger text, and aria-current when the item is a route. Hover uses a weaker fill and does not look selected. Truncate only after preserving meaningful identity. Reveal a full name through a tooltip, expanded label, or accessible name. Search or grouping should be added for long collections based on actual product needs.

### 5.4 Context header

The header combines entity identity, optional metadata or utility icons, and a task action cluster. Keep the title adjacent to the thumbnail. Place favorite or subscription controls only when the product supports them. On narrow widths, wrap the action group below identity and preserve a sensible reading order.

Use a neutral placeholder image or initial when an entity lacks artwork. If the title is unusually long, allow up to two lines on compact layouts or provide a full accessible name when desktop truncation is necessary. Do not shrink the title to make an unbounded utility cluster fit.

### 5.5 Atmospheric image layer

Artwork is optional and contextual. Crop with cover positioning, place it behind the header, and apply a dark overlay plus a downward fade into the canvas. A suggested desktop layer height is 220 to 280 px. Add a stronger local overlay if a title or control crosses a bright part of the image. Keep panels mostly opaque where text density is high.

Treat purely atmospheric images as decorative. Content-bearing images require alternative text or an equivalent description. Use the target product's permitted assets, not the reference's logo or game imagery. In a business app without useful artwork, use a quiet gradient or solid background while keeping the same panel geometry and hierarchy.

### 5.6 Workspace composition

The primary area supports the main task. The optional right area supports guidance, context, previews, recent activity, or status. On desktop, a 64/36 split is a starting point, not an invariant. A target app can use a single large panel when a canvas, document, or grid needs room.

Align neighboring panel tops and use shared grid gutters. A primary canvas can be light inside a dark frame, as the reference map demonstrates. Keep controls legible against that canvas using a local surface treatment. Add a secondary column only if its content helps the current task.

## 6 Component behavior and styling contracts

All behavior in this section is prescribed. A coding agent should implement only components that the target product needs and supports.

### 6.1 Panel and panel header

A panel is a rounded container with subtle outline, consistent padding, and an optional header. Its header contains icon, concise title, optional metadata, and actions aligned to the right. Use either a distinct header band for a canvas module or an integrated dark header for a text/settings module. Never use a large bright card heading background solely for decoration.

The panel should handle long headings and missing icons without shifting the content grid. Loading, empty, unavailable, and failed states belong inside the panel boundary. Header actions remain available where useful during loading. If the panel itself collapses, the title area is a semantic button with expanded state; an ordinary chevron must not imply collapse when no collapse action exists.

### 6.2 Primary action and split action

Use a white pill with dark label and optional leading icon for the highest-priority operation. Keep one clearly dominant action in the active task region. Secondary operations use dark fills or outlines. A destructive action should communicate its consequences and use a distinct danger treatment where necessary.

A split action has two sibling controls: the main operation and an adjacent menu trigger. The trigger has an accessible name such as More publish options. Opening the menu must not execute the primary operation. Menu Escape returns focus to the trigger; choosing an alternative may execute it or change the default only when the product defines that behavior.

During execution, retain button width, show a progress indicator and useful label, and prevent duplicate submission. A long-running operation should expose meaningful status. Success should update the relevant content. Failure should preserve user input and provide a specific retry or recovery action.

### 6.3 Secondary button and icon button

Secondary buttons use charcoal fills with text-secondary or text-primary according to importance. Small tertiary actions may be text or icon-only. Utility circles use the same circle size and icon alignment across the header. Every icon-only control has a programmatic name and a discoverable tooltip when its meaning is not obvious.

Hover brightens fill slightly; pressed darkens or strengthens the selection feel. Keyboard focus adds the independent visible outline. A tooltip supplements the name and is not the sole way to understand a critical action. Disabled buttons do not respond to click and expose an explanation nearby when lack of availability would be confusing.

### 6.4 Search field and search results

Use a magnifier, readable placeholder, and a rounded fill. The accessible label must identify scope, such as Search projects, even when the visible placeholder is simply Search. Results should state their type and enough context to distinguish similar names. Provide a clear reset when a query exists.

Preserve the last useful query when results close. Show loading, no results, and failed search distinctly. Keyboard users can move through result options, select a result, and dismiss the popup. A keyboard shortcut may open search but must avoid conflicts with typing and platform commands. Do not invent a functioning global search if no searchable data source exists.

### 6.5 Segmented filters and tabs

The reference shows categories in a shared dark rounded track with a lighter selected segment. Match the compact arrangement and restrained uppercase labels if suitable. Keep segment padding consistent and do not let a tiny badge resize a whole row unpredictably.

Choose semantics by behavior. A category that filters the same collection can use buttons with pressed state. A mutually exclusive view selector may use a radio group. Distinct content panels can use tabs with correct relationships and arrow-key navigation. Do not apply tab semantics simply because the control looks segmented. Wrap or scroll a long group on small screens; do not reduce labels to unreadable abbreviations.

### 6.6 Badge and capability label

Use short labels in small rounded shapes. Neutral release labels use a light fill with dark text. A limited purple-to-blue gradient can identify a premium capability when the target product actually has one. Status badges use text plus meaning, not color alone.

Badges are supplementary, never the only indicator that an operation is restricted or unfinished. Explain restricted functionality at the point of use. Do not add Pro, Beta, upgrade prompts, or paywalls to products without those concepts. A badge is not automatically interactive.

### 6.7 Accordion group

The settings groups suggest a header and right-side collapse indicator. Implement a clear header button with title, optional icon, and expanded state. Put the caret in a stable right slot. Allow the product to choose single-open or multi-open behavior deliberately; do not change it between similar sections.

Preserve edited values when collapsing. A collapsed group can show concise status or a count of non-default settings if useful. Expansion must not unexpectedly move focus. If a save validation error occurs inside a collapsed group, expand it and identify the error before focusing the relevant input.

### 6.8 Setting row

Use a predictable grid: optional leading icon, flexible label and helper area, and trailing control. Default height is about 50 px, but rows grow when labels or descriptions wrap. Align all trailing controls within a group. Avoid fixed widths on labels that clip translations.

The screenshot's very dark controls may represent a restricted or inactive state; that reason cannot be established. In the target app, distinguish disabled, read-only, off, unavailable, and loading states explicitly. If a dependency disables a setting, show the requirement near the row or through accessible help. Do not copy unreadably low contrast into actionable controls.

### 6.9 Binary control

The screenshot uses a pill with Off and On labels. Use a compact two-choice control with mutually exclusive values, or use an accessible switch if the target's existing semantics are better. Keep selection visible through fill, label weight, and position, not color alone.

Changing a value either applies immediately or remains unsaved until a visible Save action. Choose one policy for each related group and communicate it. If the change is asynchronous, show pending state, avoid duplicates, and restore the last confirmed value on failure with an explanation. Do not show the UI as successfully enabled before an operation actually completes without a defined optimistic rollback strategy.

### 6.10 Numeric stepper

Use a rounded track with minus, centered numeric value, and plus. Support typed entry where users may need more than a few steps. Define units, precision, minimum, maximum, and step from domain requirements. The guide's appearance does not establish valid values.

Disable decrement/increment at bounds, preserve focus, and announce changes appropriately. Invalid typed values should produce a useful message and should not silently become a different value. For high-impact changes, use a deliberate Apply or Save operation rather than applying every keystroke. Avoid tiny controls that are difficult to target.

### 6.11 Info tooltip and contextual help

An info icon belongs adjacent to the concept it explains. For short supplementary help, use a tooltip available on hover and keyboard focus. For longer help, use a popover with a clear heading and dismiss action. Critical instructions and errors must remain visible rather than hidden behind the info icon.

Constrain popovers to the viewport and allow readable wrapping. Do not include essential clickable content in a plain hover tooltip. Opening help should not erase edits or move the entire workspace unexpectedly.

### 6.12 Visual canvas and floating tools

Maps, diagrams, document previews, charts, or images can occupy the large visual panel. The panel frame stays dark while the canvas can use its own appropriate colors. Floating tools should cluster along an edge, as the reference's zoom controls do, without covering critical content.

Only show zoom, reset, locate, layers, or selection tools when those operations exist. Keep pan and zoom usable with keyboard alternatives and touch where supported. Do not trap ordinary page scrolling in a canvas by default. Provide a textual or tabular equivalent when a visual conveys essential information. A loading canvas preserves its aspect ratio and tool placement; an error exposes recovery without clearing the context header.

### 6.13 Category marker

The reference uses small brightly colored pins on a pale map. For a target visual canvas, use category colors only when there are meaningful categories. Combine color with symbol, label, legend, or shape. Markers need larger hit areas than their visible glyphs and sensible collision handling.

Selecting a marker can reveal detail in a popover or supporting panel. Keep the selected marker distinct and preserve map/diagram position when details close. Do not invent spatial data or use scattered colored pins to decorate an unrelated app.

### 6.14 Media tile and media player

Media tiles use a clipped thumbnail with a centered play affordance. Show a title, duration, or accessible description when needed. Keep a dark neutral fallback for unavailable imagery. A play button requires a clear name specifying the content.

For an actual player, expose play/pause, sound state, seek position, duration, and fullscreen where supported. Keep controls legible over video with a scrim. Provide captions when available and controls with accessible labels. Do not autoplay audio. Advertising and promotions are optional product decisions, not part of the reusable visual core. For products without media, reuse the tile treatment for documents, previews, or activity rather than implementing a fake player.

### 6.15 Assistant and support panel

The support panel can host contextual help, notes, a conversation, activity, or an inspector. Its structure is header, scrollable content when needed, and a stable bottom action/input area. A time separator is useful for conversation history; do not use one in unrelated static help.

If an assistant exists, its opening message should identify the current context without making unsupported claims. Suggested prompts should be relevant, clearly clickable, and short enough to scan. The composer remains reachable while content grows. The screenshot does not establish whether clicking a suggestion sends it immediately; a safe default is to populate the composer and let the user send unless product requirements say otherwise.

### 6.16 Composer

The reference has a wide pill input with a circular upward-arrow send control. Use a compact single-line appearance that grows to a bounded multi-line input when needed. Provide a visible or programmatic input label. Empty input disables sending; non-empty input makes availability clear. Placeholder text is secondary and never replaces a label.

For desktop chat, Enter may send and Shift+Enter may insert a newline if the product makes the behavior discoverable. Do not submit during input-method composition. Multi-line forms should keep ordinary Enter behavior. Handle draft preservation, sending progress, cancellation where supported, and failure with retry. Keep the draft after a failed send.

### 6.17 Avatar and thumbnail

Use circular avatars for people or accounts and small rounded rectangles for entities. Keep consistent sizes within each role. Image loading should not shift the title or row. Show initials or a neutral symbol when an image is absent. Alternative text depends on whether the adjacent label already communicates identity; avoid repeated redundant announcements.

### 6.18 Overflow menu

Group infrequent actions in a small anchored menu with the same dark surface language. Use short verb-led labels. Separate destructive actions and show confirmation only where consequences justify it. Maintain focus management and dismiss on Escape or outside activation. If the menu includes non-action choices, use semantics appropriate to those choices.

## 7 Interaction and state system

### 7.1 Universal state matrix

| State | Visual treatment | Required behavior |
| --- | --- | --- |
| Rest | Neutral surface and secondary or primary text | Available controls remain identifiable |
| Hover | Slightly lighter fill or boundary | No layout shift and no state change |
| Focus | Independent 2 px visible outline | Visible with keyboard and not obscured |
| Pressed | Stronger fill or subtle feedback | Matches actual activation timing |
| Selected | Persistent gray fill plus strong label | Reflects current selection or route |
| Off | Neutral control with explicit value | Still interactive when enabled |
| Disabled | Subdued but interpretable | No activation and clear reason if needed |
| Read only | Value stays readable | Can inspect or copy where appropriate |
| Pending | Stable geometry and progress feedback | Prevent duplicate conflicting operations |
| Error | Text and icon with specific recovery | Preserve input and last confirmed data |
| Success | Local confirmation where useful | Update content and avoid unnecessary interruption |

Do not collapse these states into one low-opacity treatment. In particular, an off setting is not a disabled setting, and a selected navigation row is not hover. A focused selected item must show both conditions.

### 7.2 Loading and skeletons

Use skeletons for known layouts, such as entity rows and panel content, with the same eventual geometry. Use a spinner or textual progress for actions and operations. Avoid flashing the whole app on local refresh. Respect reduced motion; static placeholders are acceptable. Long waits need context or progress, not an indefinitely spinning decoration.

### 7.3 Empty and error states

Empty states should explain the relevant absence and provide one useful next action. No records, no search matches, no permissions, and network failure are different conditions. A panel failure should remain localized when the rest of the page still works. Preserve filters and drafts through retry. Use domain-specific messages supplied by the target product; do not insert gaming-related examples as runtime copy.

### 7.4 Notifications and persistence

Use toasts for brief noncritical confirmation, inline messages for problems tied to a control, and persistent banners for problems affecting the workspace. Alerts must remain readable long enough and be available to assistive technology. Destructive or irreversible actions should name their effect rather than using vague Are you sure wording.

Preserve context selection, filters, panel expansion, and drafts where useful. Respect the target product's privacy and retention rules. Changes that alter permissions, billing, publishing, or external communication need the target product's appropriate review flow; the white pill treatment does not imply immediate execution is always suitable.

### 7.5 Motion

Use 120 to 160 ms for hover and pressed transitions and 180 to 240 ms for small panel reveals. Prefer opacity and modest translation. Avoid parallax, continuous glow, bounce, and animated full-screen backgrounds. Reduced motion should remove nonessential animation and smooth scrolling while preserving state communication.

## 8 Extensions for software not represented in the screenshot

Everything in this section is prescribed. These extensions carry the reference's character to component families that are not visible in the supplied image.

### 8.1 Forms

Forms use dark panels, labels above inputs, compact but comfortable vertical rhythm, and clear helper/error text. Use 40 to 44 px input height for longer data-entry flows even when nearby toolbar controls are denser. Group related fields using heading and spacing. Align widths to expected input length rather than making every field full width.

Required fields, validation, saved state, and submit behavior come from the product's real rules. Show errors near their fields and an error summary for long forms. Preserve values after failure. Use a bright submit action only for the form's primary operation. Checkbox, radio, select, date/time, file upload, and textarea controls should use the same tokens without replacing native semantics with decorative div elements.

### 8.2 Tables and record lists

Use a subtly differentiated header, 40 to 48 px rows by default, fine horizontal dividers, and a stronger selected-row treatment. Keep numeric values aligned consistently, normally right aligned with tabular figures. Show sort direction and an accessible sortable heading. Bulk selection must communicate count and available actions.

Keep primary row identity visible when columns overflow. On small screens, hide only genuinely secondary columns, use an intentional horizontally scrollable grid, or present a card list with labels. Do not shrink every column to illegibility. Pagination, virtualization, filters, and export should be driven by data scale and existing functionality.

### 8.3 Charts and metrics

Use the charcoal panel frame and readable titles. Place units and time range near the metric or chart. Use a limited series palette and distinguish lines with labels, patterns, or symbols in addition to color. Reduce gridline prominence while keeping axes readable. Tooltips should expose values through keyboard interaction where feasible.

A single white primary button does not mean charts must be monochrome. Data needs can justify more color than the chrome. Offer an equivalent table or summary when exact values matter. Do not invent KPI cards or charts merely to fill the reference's layout.

### 8.4 Dialogs and drawers

Use the same rounded dark surfaces with a clearer boundary and restrained overlay. Dialogs need a title, dismiss behavior, appropriate focus containment, and focus restoration. Important confirmations explicitly name the operation. A drawer is useful for secondary details without losing the working context.

At small widths, drawers may become full-screen panels. Avoid nesting modal dialogs or placing multi-step critical workflows in tiny popovers. Scroll lengthy dialog content while keeping key actions reachable and focus visible.

### 8.5 Editor and canvas applications

Give the main editor most of the available width. The left rail becomes a file, object, or project navigator. The right column becomes an inspector or contextual help area. Maintain the reference's subdued tool groups and bright primary action, but allow the central canvas to use the background best suited to the task.

Selection, zoom, undo/redo, shortcuts, and save state must work according to the editor's model. Never add a map-style locate button just because the source has one. A minimal editor may use only two columns and still preserve the visual character.

### 8.6 Authentication and onboarding

Use a focused single panel with the same surfaces, typography, input shapes, and action hierarchy. Avoid surrounding a sign-in form with inactive app controls. Show clear progress for onboarding only when multiple steps exist. Explain permission requests in the target product's language. These pages are optional routes, not requirements inferred from the screenshot.

## 9 Responsive behavior

### 9.1 Breakpoint defaults

The reference provides one desktop size. The breakpoints below are prescribed starting values. Adapt to real content and test the product's smallest useful layout rather than treating a device name as a guarantee.

| Available viewport width | Layout default | Behavior |
| --- | --- | --- |
| 1440 px and above | Rail plus main/support columns | Flexible panels and bounded long text widths |
| 1180 to 1439 px | Same composition if content fits | Support column about 320 to 400 px; reduce utilities first |
| 900 to 1179 px | Main area with optional support drawer | Collapse rail to about 72 px or a drawer |
| 600 to 899 px | Single primary work column | Navigation drawer, full-width panels, support below or in drawer |
| Under 600 px | Single-column touch layout | 16 px insets, 44 px controls, wrapped header actions |

### 9.2 Layout collapse order

First remove or move low-priority utilities into overflow. Next collapse the navigation rail while preserving an accessible way to reach all destinations. Then move the secondary support column into a drawer or below the main task. Finally stack the top-row modules. The main task and its primary action remain visible and understandable throughout.

The product's actual task can change this order. A chat application should prioritize conversation and composer. An editor should prioritize canvas and essential tools. A data application should prioritize records and filters. Do not use CSS visual reordering that contradicts keyboard or screen-reader reading order.

### 9.3 Content resilience

Test long titles, translations, missing images, many navigation items, large numeric values, and browser text zoom. Avoid fixed-height text cards. Components must reflow at 200 percent zoom. For ordinary reading and forms, aim to reflow at 320 CSS px without two-direction scrolling; inherently two-dimensional canvases and data grids may need intentional exceptions.

On mobile, keep the composer above the on-screen keyboard and account for safe-area insets. Focused fields should remain visible when the keyboard opens. Avoid hover-only information and actions. Put labels beside or below controls when horizontal row geometry becomes too tight.

## 10 Accessibility and usability requirements

### 10.1 Readability and controls

Use WCAG 2.2 AA contrast targets as implementation goals: at least 4.5 to 1 for normal text, 3 to 1 for qualifying large text, and 3 to 1 for applicable non-text control and state indicators. Verify final computed styles over actual backgrounds, including artwork and gradients. This guide defines targets; it does not certify either the reference or a future implementation.

Do not copy the screenshot's tiny subdued accuracy line for essential instructions. Do not assume the very dark Off/On controls are usable while enabled. Keep active controls distinguishable without relying on hover. Support high-contrast or forced-color environments and do not remove platform focus indications without an adequate replacement.

### 10.2 Semantics and keyboard

Use landmarks for navigation and main content, meaningful heading levels, native buttons for actions, links for navigation, and labels for fields. Expose current route, pressed state, selected value, and expanded state through appropriate semantics. Add names to every icon-only control. A whole row can be a link, but its separate action controls must remain distinct targets.

Maintain logical focus order. Menus and dialogs support Escape and return focus. Avoid keyboard traps in maps, grids, and media. Focus must not sit behind sticky bars or off-screen scroll boundaries. Provide a skip link for repetitive navigation in a web app.

### 10.3 Content and feedback

Use text or symbols alongside status colors. Present errors with specific correction steps. Announce important asynchronous status without repeatedly interrupting users; use appropriately scoped live regions. Keep helper text associated with its control. Alternative content should convey the purpose of meaningful visuals, not repeat every decorative element.

### 10.4 Reference usability findings

**Observed strengths.** The left rail provides persistent navigation and visible current context. The title and white action make the active task recognizable. Rounded panels group complex content. The top-row cards align cleanly. The support panel gives guidance a stable location. Category filters and repeated setting rows make a dense tool area easier to scan.

**Potential risks requiring interaction review.** Several utility icons lack visible labels. The meaning and availability of dim controls are unclear in the still image. Small helper text may be hard to read. Multiple visible scroll indicators could produce confusing nested scrolling. The lower media/promotion area competes for task space. A single screenshot cannot establish whether tooltips, keyboard operation, explanations, or alternative layouts already address these issues.

**Prescribed response.** Keep the successful spatial hierarchy while making action meaning, state, contrast, and scrolling clearer. Remove promotions from the transferable design unless the target product explicitly needs them. This preserves the visual character while improving task focus.

## 11 Cross product mapping

### 11.1 Map roles instead of source nouns

| Reference role | Task management | Finance workspace | Document editor | Customer support |
| --- | --- | --- | --- | --- |
| Current context | Workspace or project | Account or portfolio | Folder or document | Queue or customer |
| Entity list | Projects | Accounts | Documents | Conversations |
| Context title | Project name | Account name | Document title | Ticket subject |
| Main visual | Timeline or board | Chart or transaction area | Editor or preview | Conversation history |
| Tool settings | Project options | Filters or planning inputs | Formatting or properties | Ticket fields |
| Support column | Notes or activity | Context and explanation | Inspector or outline | Customer details |
| Primary action | Create or complete | Relevant permitted action | Save or publish | Reply or resolve |
| Suggested prompt | Optional task help | Optional contextual help | Optional writing help | Optional reply guidance |
| Media tile | Optional attachment preview | Optional report preview | Optional asset preview | Optional attachment preview |

These are role examples, not a feature list or financial workflow recommendation. Select the operation already required by the product. Any row can be omitted if it has no meaningful equivalent.

### 11.2 Minimum visual recipe

For a product with very different architecture, retain at least these relationships: a charcoal base; a restrained hierarchy of darker panels and lighter interactive fills; rounded panel geometry; compact neutral sans serif; strong context identity; one bright task action; muted icons and labels; consistent row rhythm; and limited accent color. A three-column page, assistant, map, video player, and premium labels are not necessary to achieve the style.

### 11.3 Existing product adaptation

Inspect the current routes, components, design tokens, data sources, and user flows. Preserve working behavior and public interfaces unless change is expressly requested. Apply the guide through shared styles and reusable components before replacing entire pages. Prefer existing component primitives that already handle keyboard and accessibility behavior.

For a new app, let requirements define the features and routes first. Use this guide as the presentation layer. Missing requirements are not permission to invent persistent data, billing, artificial intelligence, advertising, account systems, or external actions.

## 12 Framework neutral implementation blueprint

### 12.1 Suggested component boundaries

Use equivalents of AppShell, GlobalBar, NavigationRail, ContextSelector, EntityNavigationItem, ContextHeader, AtmosphericBackdrop, WorkspaceGrid, Panel, PanelHeader, ActionButton, IconButton, SegmentedSelector, SettingsGroup, SettingRow, BinaryControl, NumericStepper, SupportPanel, Composer, Badge, Tooltip, Menu, Dialog, and Drawer as needed. These are conceptual responsibilities; follow the repository's own naming and organization.

AppShell and WorkspaceGrid own geometry. Tokens own shared values. Panel owns visual grouping and header slots. Controls own interaction semantics and state feedback. Domain containers supply labels, data, permissions, and operation handlers. Avoid domain strings embedded in generic UI components.

### 12.2 Configuration contract

The shell accepts navigation items, current context, global utilities, and main content. The context header accepts identity, optional imagery, optional metadata, utilities, and one primary action. Panels accept title, icon, actions, body, and optional footer. They should not automatically create a premium badge, collapse action, or menu.

Represent an action with a label, accessible name where different, optional icon, handler or link, availability, pending state, and optional alternatives. Represent navigation with stable identity, label, destination, optional thumbnail, and current state. Represent settings with domain value, allowed values or bounds, helper text, validation, availability reason, and apply policy.

### 12.3 CSS token starter

The following is a prescribed implementation starter. Adapt naming to the target project's style system. It uses semantic variables rather than source-product names.

```css
:root {
  color-scheme: dark;
  --ui-canvas: #0d0f12;
  --ui-nav: #101215;
  --ui-panel: #111316;
  --ui-inset: #17191d;
  --ui-control: #292c31;
  --ui-control-hover: #35383e;
  --ui-selected: #3d3f44;
  --ui-border: #2a2d33;
  --ui-control-border: #707783;
  --ui-text: #f4f5f7;
  --ui-text-secondary: #b8bcc5;
  --ui-text-muted: #969da9;
  --ui-primary: #ffffff;
  --ui-on-primary: #20242c;
  --ui-focus: #adb8ff;
  --ui-accent: #a78bfa;
  --ui-radius-sm: 6px;
  --ui-radius-md: 12px;
  --ui-radius-panel: 18px;
  --ui-radius-pill: 999px;
  --ui-gap: 16px;
  --ui-page-pad: 24px;
  --ui-rail-width: 260px;
  --ui-topbar-height: 58px;
  --ui-fast: 140ms;
  --ui-reveal: 200ms;
}

.ui-panel {
  min-width: 0;
  border: 1px solid var(--ui-border);
  border-radius: var(--ui-radius-panel);
  background: var(--ui-panel);
}

.ui-workspace-grid {
  display: grid;
  grid-template-columns: minmax(0, 1.76fr) minmax(320px, 1fr);
  gap: var(--ui-gap);
}

.ui-primary-action {
  min-height: 44px;
  padding-inline: 18px;
  border: 0;
  border-radius: var(--ui-radius-pill);
  background: var(--ui-primary);
  color: var(--ui-on-primary);
  font: inherit;
  font-weight: 600;
}

:where(button, a, input, select, textarea, [tabindex]):focus-visible {
  outline: 2px solid var(--ui-focus);
  outline-offset: 2px;
}

@media (max-width: 1179px) {
  .ui-workspace-grid { grid-template-columns: minmax(0, 1fr); }
}

@media (max-width: 599px) {
  :root { --ui-page-pad: 16px; }
}

@media (prefers-reduced-motion: reduce) {
  :root { --ui-fast: 0ms; --ui-reveal: 0ms; }
}
```

The starter styles do not create semantics, keyboard behavior, loading states, or a navigation drawer. Implement those through the target framework's components. Components should use the motion variables for them to affect transitions. Add responsive rules to the shell itself according to the layout contract.

### 12.4 Data and performance

Separate presentation from remote data. Use actual records where available and clearly identified development fixtures otherwise. Do not make a decorative control look like it successfully operates a backend when it does not. Handle partial data, permissions, timeouts, and stale state explicitly.

Reserve image dimensions to avoid layout shift. Lazy-load off-screen media and optimize large atmospheric assets. Avoid broad blur layers that hurt performance without improving readability. Virtualize very large lists only when necessary and maintain usable keyboard and screen-reader behavior. Measure with the target application's actual content rather than a single static demo.

## 13 Coding agent execution contract

### 13.1 Ordered workflow

1. Read this guide and inspect the target application's requirements, routes, components, tokens, state management, and available assets. Identify the main user task and the current primary action.
2. Build a role mapping from the reference to the target application. Mark unsupported modules as omitted. Distinguish existing functionality from any explicitly requested new behavior.
3. Define semantic tokens and shared geometry in the target's established styling approach. Preserve existing accessible primitives where practical.
4. Implement the shell and one representative main task view. Apply panels, actions, labels, navigation selection, and compact rows consistently before expanding to every route.
5. Extend the same system to forms, tables, dialogs, or canvases actually present. Keep domain data and operations separate from generic visual components.
6. Implement all states needed by each working flow, including keyboard focus, loading, empty, error, disabled, and success. Do not create decorative controls without useful behavior.
7. Verify the representative desktop composition, then compact layouts and content edge cases. Compare visual relationships to the supplied screenshot rather than only checking colors.
8. Validate existing functionality and relevant interactions with appropriate checks. Report changes, omitted reference modules, unresolved dependencies, and material limits honestly.

For a presentation-only prototype explicitly requested by the user, fixtures and simulated interactions are acceptable if labeled as such. For a real software adaptation, use the existing data and operations.

### 13.2 Definition of acceptable visual fidelity

The result has a nearly black canvas, subtly differentiated dark surfaces, rounded cards, muted utilities, a compact sans serif hierarchy, aligned grid gutters, and a dominant white action. The primary task occupies most of the useful workspace. Context identity is obvious. Color is reserved mainly for content, status, and a small number of accents. Artwork, if used, fades behind work content.

The result should not drift into a bright dashboard, a colorful marketing page, oversized floating cards, neon borders, excessive blur, large gradient buttons, or a copy of gaming-specific labels. Fidelity means recognizable visual relationships combined with the target product's correct behavior.

### 13.3 Failure patterns to prevent

Do not apply one black background and leave all existing spacing, buttons, and rows unrelated. Do not preserve the screenshot's low-contrast areas for enabled controls. Do not hardcode reference titles, branding, map geography, category names, or premium labels. Do not introduce artificial assistants or video modules. Do not fake browser/window controls. Do not make all buttons white. Do not add hidden interactions only discoverable through hover. Do not replace real workflows with mock data without explicit prototype scope.

### 13.4 Quality review matrix

| Area | Pass condition | Useful verification |
| --- | --- | --- |
| Visual identity | Token, radius, typography, and action hierarchy are consistent | Desktop screenshot beside reference |
| Composition | Main task dominates; support is useful and subordinate | Review at 1435 or 1440 px |
| Navigation | Current context and route are identifiable | Navigate and inspect current state |
| Controls | Available, selected, off, pending, and disabled are distinct | Exercise each applicable state |
| Domain fit | Labels, modules, and actions serve target workflows | Review role mapping |
| Accessibility | Labels, focus, contrast, and semantics are usable | Keyboard pass and appropriate audit |
| Responsiveness | Main task survives layout collapse | Check 1440, 1024, 768, and 390 px |
| Content resilience | Long text, missing images, and empty data do not break layout | Edge-case fixture review |
| Async behavior | Pending, retry, and rollback preserve useful context | Relevant failure-path checks |
| Existing behavior | Previously working flows remain functional | Targeted regression checks |

### 13.5 Suggested visual review weights

For a design review, assign 25 percent to composition and information hierarchy, 20 percent to surfaces and contrast, 15 percent to typography, 15 percent to spacing and geometry, 10 percent to control and state consistency, 10 percent to responsive behavior, and 5 percent to imagery and restrained accents. These are suggested review weights, not an automated quality score. A serious accessibility or workflow failure cannot be compensated for by a high visual score.

## 14 Ready to use coding agent instruction

Copy the following instruction into the target project's coding-agent session and attach this guide. It intentionally names no target software or reference-specific content.

```text
Read the attached Reusable UI UX Design Guide and apply its visual system
to this project. Inspect the project before editing. Determine its actual
purpose, routes, components, data sources, and main user workflows.

Preserve the project's domain language, working behavior, data model,
permissions, and real operations. Map the guide's component roles to the
project's existing needs. Omit reference modules with no useful equivalent.
Do not copy the source product's branding, game content, map, media,
assistant, advertising, premium labels, or native window controls unless
this project independently requires the corresponding functionality.

Use a charcoal canvas, subtly layered dark panels, rounded containers,
compact neutral typography, restrained borders, muted utility controls,
filled gray selection, consistent row rhythm, and one prominent white
task action. Use contextual atmospheric imagery only when appropriate.
Preserve the guide's visual relationships without forcing its exact
desktop column arrangement onto every route or device.

Implement semantic design tokens and reusable components through the
project's established stack. Use the guide's prescribed defaults as the
starting point and distinguish them from observations about the source.
Keep hover, focus, selected, off, disabled, pending, error, and success
states distinct. Maintain readable contrast and keyboard accessibility.

Complete the applicable responsive layouts, real interactions, content
edge cases, and loading/empty/error behavior. Do not leave nonfunctional
decorative controls or silently substitute mock data in a working app.
Verify the result visually at desktop and compact widths and check
relevant workflows. Report what changed, what was verified, which
reference modules were intentionally omitted, and any material limits.
```

## 15 Final implementation checklist

- The target product's own context, terminology, records, and workflows are retained.
- Every included reference role has a useful purpose in the target application.
- Canvas, panel, inset, control, and selected surfaces are deliberately distinct.
- Panel corners, pills, circular controls, and icon sizes follow shared tokens.
- Titles, panel headings, labels, body text, and metadata have a consistent hierarchy.
- The primary action is visually dominant and has the correct real operation.
- Navigation selection, hover, keyboard focus, and off/disabled states are distinct.
- Text over artwork remains readable and content panels have sufficient opacity.
- The main work area receives the space the target task needs.
- Tool rows, filters, inputs, and optional support panels handle long content.
- Applicable loading, empty, error, pending, success, and permission states exist.
- Controls have semantic labels, appropriate roles, and keyboard operation.
- Mobile layouts preserve the main task and touch usability.
- Essential information survives missing imagery, failed requests, and text zoom.
- No unrelated source branding, artificial features, or promotional modules were introduced.
- Visual review and relevant behavioral verification have been completed.

## Appendix Reference image

The Word edition includes the supplied reference image for visual comparison. The reusable rules above are sufficient to apply the system without embedding source-product imagery in the target software.

## Appendix Accessibility references

The accessibility targets in Sections 4 and 10 follow W3C WCAG 2.2 guidance. Consult the full criteria and their exceptions when evaluating the target implementation. They supplement the screenshot analysis and do not establish the reference application's compliance.

W3C Web Content Accessibility Guidelines 2.2, including text contrast, non-text contrast, resizing, reflow, keyboard access, and focus requirements: https://www.w3.org/TR/WCAG22/

W3C Understanding Contrast Minimum, criterion 1.4.3: https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html

W3C Understanding Target Size Minimum, criterion 2.5.8: https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html

W3C Understanding Non Text Contrast, criterion 1.4.11: https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html

W3C Understanding Reflow, criterion 1.4.10: https://www.w3.org/WAI/WCAG22/Understanding/reflow.html
