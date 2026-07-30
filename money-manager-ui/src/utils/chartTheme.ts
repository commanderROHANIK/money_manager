// Resolves the app's design tokens (src/style.css) into literal color strings for Chart.js,
// since canvas fillStyle/strokeStyle needs a real color value rather than a CSS var() reference
// that only the DOM's cascade would otherwise resolve.
function token(name: string): string {
  return getComputedStyle(document.documentElement).getPropertyValue(name).trim();
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
