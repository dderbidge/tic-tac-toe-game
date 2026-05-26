# Visual Assets — Phase 2 PNG Swap

Drop PNGs into this folder matching the slot names below, then update the
matching `--asset-*` lines in `web/wwwroot/css/theme.css`. No code changes.

| Slot                      | Phase 1 default     | Phase 2 file expected            | Min size                | Notes                         |
|---------------------------|---------------------|----------------------------------|-------------------------|-------------------------------|
| Ascendant sigil           | `ascendant-sigil.svg` (sun) | `ascendant-sigil.png`    | 256×256, transparent    | Round bounding circle ideal   |
| Vanguard sigil            | `vanguard-sigil.svg` (sparkle) | `vanguard-sigil.png`  | 256×256, transparent    | Round bounding circle ideal   |
| Backdrop                  | (none — pure CSS)   | `backdrop.png` or `.jpg`         | 1920×1080               | Optional; covers full viewport at 60% opacity |
| App icon (192px)          | placeholder         | `icons/icon-192.png`             | 192×192                 | Solid background OK           |
| App icon (512px)          | placeholder         | `icons/icon-512.png`             | 512×512                 | Used for splash on Android    |
| Maskable 192              | placeholder         | `icons/icon-maskable-192.png`    | 192×192                 | 80% safe area inside          |
| Maskable 512              | placeholder         | `icons/icon-maskable-512.png`    | 512×512                 | 80% safe area inside          |
| Apple touch icon          | placeholder         | `icons/apple-touch-icon.png`     | 180×180                 | No transparency               |
| Favicon                   | inline SVG          | `favicon.ico` (already in wwwroot root) | 32×32 ICO         | Multiple sizes optional       |

After swapping, edit `web/wwwroot/css/theme.css`:

```css
:root {
    --asset-ascendant-sigil: url('/img/ascendant-sigil.png');
    --asset-vanguard-sigil:  url('/img/vanguard-sigil.png');
    --asset-backdrop:        url('/img/backdrop.png');  /* enables the body::before layer */
}
```

The `body::before` layer in `theme.css` paints the backdrop image at 60% opacity behind the CSS starfield — the starfield stays visible on top.
