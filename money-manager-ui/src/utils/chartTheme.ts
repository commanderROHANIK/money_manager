// Resolves the app's design tokens (src/style.css) into literal color strings for Chart.js.
//
// Two things force the resolution to go all the way down to hex rather than handing Chart.js
// the token's own value:
//   1. canvas fillStyle/strokeStyle needs a real color, not a var() reference that only the
//      DOM cascade would resolve;
//   2. the tokens are authored in oklch, and Chart.js derives hover/active colors internally
//      via @kurkle/color, which has no oklch parser — it returns undefined for those and the
//      hovered arc/bar/point then paints black.
// Painting the color into a 1x1 canvas and reading the pixel back gives us the hex equivalent
// of whatever the browser resolved, which every layer of Chart.js can then parse.
const cache = new Map<string, string>();
let probe: CanvasRenderingContext2D | null = null;

function toHex(color: string): string {
  if (!probe) {
    const canvas = document.createElement('canvas');
    canvas.width = canvas.height = 1;
    probe = canvas.getContext('2d', { willReadFrequently: true });
  }
  if (!probe) return color;

  // Reset to a known value first: an unparseable color leaves fillStyle untouched, and without
  // this we would silently inherit the previously converted color instead of noticing.
  probe.fillStyle = '#000000';
  probe.fillStyle = color;
  probe.clearRect(0, 0, 1, 1);
  probe.fillRect(0, 0, 1, 1);

  const [r, g, b] = probe.getImageData(0, 0, 1, 1).data;
  return '#' + [r, g, b].map((channel) => channel.toString(16).padStart(2, '0')).join('');
}

function token(name: string): string {
  const cached = cache.get(name);
  if (cached !== undefined) return cached;

  const raw = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
  const hex = raw ? toHex(raw) : raw;
  cache.set(name, hex);
  return hex;
}

// Token values are cached because resolving one costs a getComputedStyle plus a canvas readback.
// A theme switch (the dark-mode follow-up) has to clear the cache and rebuild any live charts —
// Chart.js copies these colors into its own config when the dataset is built.
export function resetChartColorCache(): void {
  cache.clear();
}

export const chartColors = {
  get primary() { return token('--mm-primary'); },
  get primaryStrong() { return token('--mm-primary-strong'); },
  get primarySoft() { return token('--mm-primary-soft'); },
  get accent() { return token('--mm-accent'); },
  get accentSoft() { return token('--mm-accent-soft'); },
  get danger() { return token('--mm-danger'); },
  get dangerSoft() { return token('--mm-danger-soft'); },
  get surface() { return token('--mm-surface'); },
  get surface2() { return token('--mm-surface-2'); },
  get border() { return token('--mm-border'); },
  get text() { return token('--mm-text'); },
  get textMuted() { return token('--mm-text-muted'); },
};

// Categorical palette for charts with more series than primary/accent/danger cover
// (sector/holding breakdowns, per-account distributions, etc.).
export function chartCategoricalPalette(): string[] {
  return [1, 2, 3, 4, 5, 6].map((n) => token(`--mm-chart-${n}`));
}

export function chartColor(index: number): string {
  const palette = chartCategoricalPalette();
  return palette[index % palette.length];
}
