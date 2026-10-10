# Native interface direction — grey/red and Settings first

Recorded 2026-10-09 from the user's instructions. This is the current design contract for new interfaces. **Documentation only: no widgets, navigation or settings behavior is implemented by this checkpoint.** The 4A HTML mockups remain historical; the user specifies native controls as development proceeds.

## Visual language

- Slick grey surfaces with red accents. Main attraction/primary action buttons, including Confirm and Save, are red. Secondary actions use a shade of grey.
- Most buttons have a visible border and small rounded corners. Use shading and depth on panels/controls, with consistent hover, pressed, selected, keyboard/controller focus and disabled states. Red is the primary-action treatment; errors/destructive meaning also need words/icons, rather than relying on that color alone.
- UI is not constrained to pixel sprites or the resolution/style of the 2D car art. Smooth shapes, shading, shadows and hover effects are allowed. Specific shades, radii, animation and effect strength will be reviewed in the actual game as their controls are introduced.
- Hover effects must have equivalent focus feedback for keyboard/controller use. Touch needs pressed/selected feedback without requiring hover. Cosmetic transitions must not delay actions, claim driving input, save values or affect simulation.

## Icons and progressive detail

- Prefer icons over visible text where the action or value is recognizable, including Settings, information, close and familiar navigation/resource indicators. Use one consistent icon style. Keep text where the meaning would otherwise be ambiguous, especially confirmation choices and consequences; icons retain accessible names and hover/focus labels. Do not rely on hover alone to explain an action on touch devices.
- Secondary information belongs behind a small info button opening a dialog. Deep part descriptions, detailed specifications and explanations remain available there instead of filling the primary card. The dialog uses the shared X/Close, focus and modal rules below; inspecting information never applies settings or spends resources.
- A part's primary summary shows **HP, torque, weight and part durability**, plus the effect of the selected part/change. Preserve current-to-proposed values, units and positive/negative comparison feedback. Use authoritative installed/draft calculations and part data, not invented bonuses or misleading universal strength ratings. Detailed condition, compatibility, hardware permissions and specifications can live in the info dialog; essential failure, incompatibility, cost or transaction warnings remain visible when needed to make the decision.
- Durability describes the part's actual applicable lifetime/robustness; distinguish it from its current wear/condition. If a stat is unavailable or inapplicable, show that clearly instead of fabricating a number. Exact labels and presentation are reviewed with the first part-summary control.

## Responsive sizing and anchors

- Layout must adapt to display resolution, aspect ratio and fullscreen/windowed mode, including live window resizing. Use a shared reference scale, spacing and sizing policy wherever possible; reference dimensions and minimum supported sizes will be validated with native implementation rather than assumed here. Avoid unrelated scale factors on each screen. Any necessary screen-specific scaling exception must be documented and reviewed.
- Anchor the navigation bar to the **top, full width** of its parent. Anchor the Settings control to the **right edge of the bar**, with its control/hit area filling **100% of the bar's height**. Keep the icon proportionate and centered with padding; filling the hit area does not stretch the icon. Reserve its width so navigation/resources cannot overlap it.
- Anchor the content area beneath the bar and let it use the remaining window space. Resize/reflow content before reducing readability; allow scrolling inside the relevant content panel while the top bar remains fixed. Keep shared buttons, sliders, text and interaction targets at consistent readable sizes. Dialogs fit the viewport and can scroll their content while close/confirmation actions remain reachable.
- Resolution/mode/size changes must not reset drafts, selected tabs, cart contents, focus ownership or simulation state, or publish Settings edits. Compact layouts retain access to navigation, Settings and cash/fuel/progression information.
- Each implemented control/screen gets layout checks at representative small and large windows, different aspect ratios and fullscreen/windowed transitions. Check for clipped/overlapping text, inaccessible actions, stretched icons and broken pointer/focus alignment. This documentation checkpoint does not claim device/layout validation has run.

## Full-screen menus and top navigation

- Most menu interfaces fill the window with their own opaque background; the racing strip is not a permanent background. An intentional car preview may remain on screens that need it without exposing the strip everywhere.
- Main-menu navigation runs across the top, in the same position and form across the usual menu screens. Keep it visible while content scrolls. Replace the old side-navigation proposal with this top-bar direction.
- The bar includes cash, fuel and progression. Use real selected-car fuel/tank information and actual progression (current reputation/career status); an empty garage shows unavailable fuel rather than inventing a tank. Do not introduce new XP/progression mechanics just to populate the header.
- Settings sits at the far top right. Resource/progression information must not push it into a scrolling panel or a different position on each screen. Compact-window/device arrangements must retain readable access to the same information and Settings.
- Some interfaces may use a simple Back action or context-specific navigation. Race uses its HUD/pause menu instead of the persistent main-menu bar. Going into Settings from a paused race retains the paused context and returns there; it does not resume driving automatically.

## Unified Settings

- Settings is a full-screen interface with category tabs and the same top-right entry point from normal menus. All player settings belong here. The new navigation has **no separate Driving menu**.
- Categories initially cover **Difficulty / driving**, **Sound**, **Units**, **Controls / input**, and **General / game data** (including the existing progression-reset flow). Category names/ordering can be refined while implementing them. Support additional necessary categories later without rebuilding the page.
- Difficulty/driving contains existing launch/shift/driver assists, opponent level and simulation rules: wear, fuel/liquids, thermals and start-temperature dependencies. Sound contains the shared master and opponent levels. Units preserves current speed/pressure/torque choices. Moving settings does not remove advanced options or change their meaning.
- The first new page is Settings. It introduces the shared components needed elsewhere: styled buttons, tabs, labels/help text, numeric/text inputs, sliders, choices and flexible confirmation dialogs. Build/review these in control-sized increments, using only the minimum common infrastructure each increment needs. This is not authorization to implement every widget or tab at once.

## Explicit settings Save contract

This supersedes the earlier new-UI proposal to save completed settings gestures. Existing legacy behavior remains unchanged until its migration is explicitly implemented.

1. On entry, create a draft from the currently applied preferences. All tabs edit **one shared draft**, and changing category preserves pending edits.
2. Edits update the page's draft/validation feedback only. **No game setting takes effect and no preference is saved on slider movement, numeric entry, hover, tab change or widget refresh.** This includes volume, displayed units and difficulty; there is no implicit live audio preview exception in this requirement.
3. Show that changes are unsaved and provide a red **Save settings** action plus grey **Discard/Revert**. Save opens a confirmation dialog explaining the actual pending changes. The dialog may contain inputs/sliders/other relevant content, not just a text summary.
4. Red **Confirm save** validates the final draft/dialog input and publishes/persists the whole intended settings change through a recoverable command boundary. Validation/write failure keeps the previous applied settings and the editable draft, with an error. Disabled or dependent options must retain their remembered preferences under the existing thermal/assist rules.
5. Grey Cancel or the dialog's top-right X closes only that dialog, leaving the page draft available; it does not apply changes. Discard/Revert restores currently applied preferences and never resets progression.
6. Leaving Settings with unsaved edits offers **Save / Discard / Stay**. Navigation must not silently save or apply. Save follows the same validation/confirmation flow; Discard leaves with applied values intact; Stay keeps editing. Clean exits need no prompt.
7. F8 presentation comparison preserves the draft and selected category. An open dialog continues to block F8. Legacy and new screens must not become separate preference authorities: if another definitive action changes applied preferences during comparison, detect/reconcile the stale draft instead of overwriting unrelated newer values.

Saving from a paused race changes preferences under existing safe application rules without restarting the run or silently changing its captured entry terms. Exact effects of mid-run difficulty/driver settings must be checked against current race-session semantics in their control checkpoint; do not rebuild/heal/reprice the running car to make a preference apply. A single shared applied preference set still serves all interfaces and benches.

## Reusable dialogs

- Destructive actions, purchases and sales require an explicit dialog above the current interface. Other actions can use one when appropriate. Existing transaction, quote, identity and reservation guards remain authoritative.
- Dialogs can contain text, forms, sliders, choices, previews, costs and other content needed to confirm an operation. Confirm uses the current validated values, not an out-of-date summary captured before editing.
- Main confirmation action is red; secondary actions are grey. Include an X in the dialog's top-right corner and an explicit Cancel/Back/Close button. X and cancellation never execute the underlying action. Escape/controller Back should provide equivalent dismissal where safe.
- Cover/dim the underlying interface and prevent background clicks, navigation and game commands from leaking through the modal. Restore focus to the invoking control when closing. Do not add a second simulation/audio/input owner to implement a dialog.
- A dialog draft must not buy, sell, repair, reset, transfer or spend on editing. Validate again at confirmation and use the existing atomic transaction/quote/save boundaries. Settings confirmation follows its separate explicit-Save contract above.
- Progression reset remains its own destructive command and retains applied driving/audio/unit/input preferences. Editing Settings or choosing Discard must not reset the game; reset confirmation must not accidentally publish unsaved preference drafts.

## Implementation and review handoff

F8 Old ↔ New comparison (`67e09df`) remains the native review mechanism. The legacy layout is available while each replacement control is developed. Screen background/nav effects belong to presentation and must not pause, reset or otherwise change the authoritative simulation beyond explicit context/lifecycle rules.

Next implementation target: **the Settings page**, starting with its top-right entry/minimal screen foundation and the first user-selected control. Establish only necessary shared style/layout/dialog/draft infrastructure; do not complete the remaining 4B widget library or all 4C tabs in one checkpoint. Keep the lettered phases as coverage organizers, record the dependencies used and retain future controller/device gates.

Every requested native control or infrastructure increment gets appropriate checks, a runnable checkpoint, a local commit and a review handoff. Wait for user validation before the next control. No new runtime interface is delivered by this requirements note.
