# Persistent selection highlight — 2026-10-16

The shared clickable panel renderer accepted a `selected` state but rendered the same face and corner treatment as unselected panels. It now draws a red perimeter, left accent and restrained red face wash for an actual selected value. Static panels retain their existing theme. Race, opponent and other selected choice cards reuse it; hover/shine/press and no-focus/no-Tab rules stay intact.

The native race-selection suite checks rendered red selection after the pointer leaves and the shine ends, exclusive career selection, free opponent/distance, F8, Settings return, resolutions, entry failure/fee authority and durable reload. The rebuilt Windows player passes **53 checks / 0 runtime errors**, including the pixel check after hover ends and compact/1080p/4K bounds. The career/free selection screenshots were reviewed locally. No race or save behavior is changed in this checkpoint.
