# Words application icon concepts

These are exploratory concept boards, not production assets. Each board shows a proposed
family across four roles: full-colour application icon, isolated foreground mark,
single-colour mark, and splash treatment.

**Selected direction:** Pattern Lens. Its production SVG sources now live in
`src/Words.Maui/Resources/AppIcon` and `src/Words.Maui/Resources/Splash`. The
`pattern-lens-final.png` file is the 1024 px raster produced by MAUI's own resizetizer.

The concepts use the application's established "ink on newsprint" palette:

- ink blue: `#2A4B8D`
- near-black: `#14161A`
- paper: `#F2F3F5`
- white: `#FFFFFF`

## 1. Ink Grid

A literal crossword grid and pen. This is the clearest crossword-specific direction, but
also the most detailed at small sizes.

## 2. Shuffle Tiles

Four `W O R D` tiles in a rearrangement loop. This makes the anagram activity unmistakable
and has the friendliest character of the set.

## 3. Puzzle Fold

A compact W-shaped chain of crossword cells with one tile returning to place. This is the
most abstract direction and the strongest candidate for a distinctive small-size mark.

## 4. Pattern Lens

A search lens containing a grid, with its handle becoming a pen nib. This communicates
search and solving immediately, though the lens metaphor is less ownable than Puzzle Fold.

Once a direction is selected, it should be redrawn as clean SVG geometry and adapted into
the production MAUI app-icon foreground/background and splash assets. Platform exports and
small-size checks should be made from that deterministic vector source rather than by
cropping these boards.

## Generation brief

All four boards were generated with the built-in image-generation tool as `logo-brand`
concepts. The common prompt asked for a square board containing four unlabelled treatments
of one consistent mark, large platform-safe margins, a clear silhouette at 24–32 px, the
palette above, and no device frame, watermark, third-party branding, or .NET template
imagery.

The direction-specific prompts were:

- **Ink Grid:** a compact crossword grid whose negative space suggests a W, completed by a
  confident blue pen stroke.
- **Shuffle Tiles:** exactly four tiles spelling `W O R D`, arranged in a compact loop with
  a blue motion path to express rearrangement.
- **Puzzle Fold:** a bold abstract W made from a connected ribbon of crossword cells, with
  one blue cell displaced and returning to the row.
- **Pattern Lens:** a search lens containing a simple three-by-three crossword grid, with
  one blue found cell and a handle that echoes a fountain-pen nib.
