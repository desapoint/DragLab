# Independent review: Parts card-window layout readiness

## Decision

The latest source follows the approved post-attach readiness model for stale input, modal interruption, and persistent invalid geometry. It keeps the action guard strict and does not expose the pending ticket's card, callback, or surface registries. The previously reported `partsEmpty` layout mutation is fixed: query completion establishes its final text/visibility before creating a window ticket, pending updates leave it unchanged while a window exists, and attach/Ready change only the toolbar count. The one-row fixture now uses a visible real asset ID with a copied current query and invokes the real guarded card callback.

The verifier forces one invalid-layout condition by setting the real ScrollView viewport's `style.display` to `None`, and now labels it explicitly as `synthetic-ui-layout-fault`. It also reports `PARTS_WINDOW_NATURAL_INVALID_GEOMETRY,status=OPEN` because no natural user-path reproduction is known. This is consistent with the approved contract: the injected case can test fail-closed robustness but does not count as native/user-path acceptance. Source review is approved; compilation, runtime checks, screenshots, and native frame acceptance remain pending. Full S06 remains open.

## Verified state-machine behavior

- `PublishPartWindow` attaches the fully built staged grid with its buttons disabled, clears accepted-window authority, leaves action/surface registries private, and records the attach frame. `AdvancePartWindowLayout` waits for a later frame before it proceeds.
- On the later step, the code checks ticket, data and presentation generations, active Parts route, current browser and accepted facts, visible page/scroll, cart state, and the full typed `TryFreshPartFence` before reading live viewport geometry. It validates finite geometry, the scroll range, panel attachment, card count, exact `partMatches` entry references, and button parent/panel identity.
- It recomputes `NativeCardWindow` from the current result count, scroll position and viewport. If scroll or range differs, it retains only a disabled geometry fallback and builds a new ticket. A width-only change can be adopted when the scroll/range and attached card identities remain current. It registers callback/surface maps and enables actions only after the checks succeed.
- A modal encountered while a card window is building or awaiting layout cancels the staged window/surfaces, keeps actions disabled, sets a paused state, and starts a fresh build after the modal closes. The modal check runs before the step-held return, so a test hold does not block cancellation.
- Invalid geometry/attachment checks count only unheld rendered-step attempts. The fourth failure enters `FailPartBrowserBuild`, which cancels the private window and latches a terminal failure against the idle snapshot. It does not spin or auto-retry the unchanged snapshot; the retry seam is development/test-only. The documentation accurately leaves native behavior for these cases pending.

## Post-fence layout mutation review

`partsEmpty` is a child of `partsScroll` (`NativeParts.cs` around line 249 in the current source). The new flow sets final no-results text/visibility when `AdvancePartQuery` installs `partMatches`, before `EnsurePartWindowTicket` creates a ticket. `ShowPartsPending` leaves the label alone while a window is building, attached or retained as a geometry fallback. `PublishPartWindow` and successful readiness update only `partsCount`; they no longer change `partsEmpty` after the geometry fence. This addresses the original post-fence scroll-height/scroller-width risk. The short-result test verifies that the hidden-label state remains unchanged through the real card callback.

The failure predicate named `AttachedPartWindowHasFiniteGeometry` also checks attached card count/identity, parent, and panel membership. Its four-attempt terminal message says only that layout did not settle. This is fail-closed and not a correctness blocker, but the diagnostic is broad for structural card/attachment mismatches.

## Verifier scope note

The verifier's short-query fixture now finds a current visible entry with a nonempty `AssetId`, tests that exact string against a copy of the current `NativePartQuery` in the current section and cash/filter context, assigns it to the live TextField to run the production `ValueChanged`/query path, and checks a real current card callback. It avoids assumptions about display names, does not fabricate an offer, and does not bypass readiness.

The invalid-geometry test sets `viewport.style.display=DisplayStyle.None` programmatically. This is synthetic geometry injection even though it uses the real ScrollView element. The updated verifier comments/report call it `synthetic-ui-layout-fault` and emit `PARTS_WINDOW_NATURAL_INVALID_GEOMETRY,status=OPEN`. Keep that distinction in any results: the test may demonstrate the production four-attempt fail-closed behavior under the injected fault, but the natural native invalid-geometry case remains OPEN.

## Required verification before clearing the layout gate

- Verify zero, one, short, and long Parts results. The final empty-label state must be established before the post-attach check and remain unchanged through Ready. Record the viewport and range after Ready and invoke a real visible card action; the guard must accept it.
- Retain the strict stale-action guard and attach/hold, same-revision freshness, newest-ticket, resize/scroll, and modal-cancel cases. Keep invalid geometry acceptance OPEN unless the real native UI produces it without a test-created geometry mutation. A synthetic robustness probe may not be reported as native UI evidence.
- Capture the scrollbar and window at 720p, 1080p, and 4K, including resolved scroller width and arrow visibility. Run the fresh Windows build and native frame trace on the frozen source; preserve every full-frame tail and do not infer performance from nested timings.

The design and source audit alone do not clear the phase-0 action lifecycle gate, the frame-budget gate, or the full S06 preview gate.
