# S06D2A attached window and layout checks

This verifier increment covers the native Parts card window after its virtualized card tree is attached to the real UI Toolkit panel. It preserves the existing base-model, query, scroll, detail, and action checks and adds checks at the boundary between attaching the private tree and publishing its callbacks and surfaces.

## Attached-window lifecycle

The new lifecycle checks use an accepted real Parts list and trigger actual visible-window work by changing the list scroll position. `HoldPartBrowserAfterWindowAttachForChecks` stops at the attached `WindowLayout` state. At that boundary, the verifier checks that actions remain disabled, the target card callback is unavailable, and the registered surface count contains only persistent surfaces after subtracting the previous accepted card window.

After resuming, the verifier changes selected-car fuel without changing profile revision. The layout’s owner-thread fence must observe the changed physical input, invalidate the old data generation, and keep actions unavailable before the fixture restores the original fuel. A fresh A build must then reach Ready.

A second held attached window is built after a real 1024×768 live window resize, then receives a real scroll change while attached. The old ticket must be superseded, and only a later complete window may become actionable. The verifier requires the exact current-versus-accepted geometry predicate after publication, along with the current scroll. It restores the user’s original window dimensions in `finally` and waits for the restored layout to settle.

The modal case opens the real Parts sort dialog while an attached window is held. It captures the exact pending card-surface references and observes their registry membership after cancellation. The dialog legitimately adds its own surfaces, so the total registry count must not be compared to the pre-dialog count; native93863 exposed that fixture mistake after 424 checks with zero runtime errors. It checks that the pending tree is canceled without publishing callbacks or registering staged surfaces, then closes the dialog and requires a fresh Ready window.

The **synthetic UI-layout fault-injection** case deliberately hides the actual ScrollView content viewport while the attached ticket is held. It observes the invalid geometry, resumes the normal owner-thread checks, and requires the existing four-rendered-check terminal failure and its exact diagnostic. The authored display style is restored in `finally`; the verifier then uses the existing explicit retry path and waits for Ready. This is injected invalid layout, not a natural user-path reproduction. It exercises the production geometry predicate, fresh fences and terminal bound without overriding their results, readiness, or thresholds. The natural invalid-geometry acceptance case remains OPEN and is recorded separately; this synthetic robustness evidence must never be presented as closing it.

## Owned-unit scrollbar captures

While an actual temporary owned piston unit detail remains mounted, the verifier captures its scrollbar at 1920×1080 and at a 3840×2160 render target. Both checks require the existing themed eight-point scrollbar, its themed slider, and hidden stock arrow buttons. Capture names are flat `bounded-parts-owned-unit-detail-1920x1080.png` and `bounded-parts-owned-unit-detail-3840x2160.png`, so the gallery can classify them with the other bounded Parts captures. The 4K path uses a 3840×2160 offscreen native panel target and shared 2× panel scale while the physical window remains at 1920×1080; the report labels it as an offscreen panel capture. The original screen resolution, fullscreen state, panel render target, and active render target are restored.

After terminal-geometry recovery, a short query chooses an actual visible entry whose full asset ID has exactly one match under a copy of the current production query, category and cash/filter context and confirms the final list contains exactly one card. It verifies current accepted geometry and the hidden empty-label state after layout settles, invokes that card’s actual guarded callback, checks the same label state after entering detail, and restores the previous search. The real TextField.value setter dispatches the production search change for IDs with punctuation; the test does not set readiness or fabricate a product.

## Gate and evidence

These checks are part of the Windows development native verifier. Their evidence is emitted under `PARTS_WINDOW_HELD`, `PARTS_WINDOW_SUPERSEDE`, `PARTS_WINDOW_GEOMETRY_FAILURE`, and `PARTS_CAPTURE` report labels; existing `PARTS_PHASE` measurements remain in place. The terminal geometry probe must remain visible in its own result and is followed only by the explicit recovery needed to continue the suite.

The author ran no Unity while preparing the change. Parent-owned final Windows2937 and native94925 subsequently pass:451 checks/0 runtime errors, including the four-check synthetic failure and the screenshot/query cases. Full frame responsiveness still fails13/6324; natural invalid-geometry acceptance remains OPEN. Independent final review approves only the functional component, as recorded in LIVE_RESULTS and S06D2A_LIVE_FINAL_REVIEW.md.

A zero-result query is first proven to match no actual catalogue entry under the current query/category/cash context. The real search field event builds the empty window; the verifier requires Ready with zero cards, exact accepted/current geometry and the final visible `No matching parts` label. It does not invoke a nonexistent card or weaken the nonempty-list readiness helper. The original search is restored afterward.
