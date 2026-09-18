/**
 * The math behind `PieChart.vue`, pulled out of the component because jsdom's CSS parser does
 * not understand `conic-gradient()` — a value set through it is silently dropped, so a mounted
 * component can't be used to verify what string was actually built. Kept pure and tested
 * directly instead, the same reason `PropertyAnalyticsCalculator` is pure.
 */
export interface PieChartSegment {
  label: string;
  value: number;
  color: string;
}

export interface PieChartSlice extends PieChartSegment {
  /** This segment's share of the total, as a 0-1 fraction. */
  percent: number;
}

/**
 * Zero-value segments are dropped — a "0%" legend row tells the reader nothing a missing row
 * wouldn't — and the rest are sorted largest first, matching the design system mockup's
 * Checking 55% / Savings 25% / Investing 20% ordering.
 */
export function pieChartSlices(segments: PieChartSegment[]): PieChartSlice[] {
  const withValue = segments.filter((segment) => segment.value > 0).sort((a, b) => b.value - a.value);
  const total = withValue.reduce((sum, segment) => sum + segment.value, 0);

  if (total === 0) return [];

  return withValue.map((segment) => ({ ...segment, percent: segment.value / total }));
}

/** A plain CSS conic-gradient — the design system's own mockup is exactly this, a filled circle
 * with no donut hole, built the same way rather than through canvas. */
export function pieChartGradient(slices: PieChartSlice[]): string {
  if (slices.length === 0) return 'transparent';

  // Rounded rather than left as raw floats: repeated addition drifts (a third of 100 summed
  // three times lands on 99.99999999999999, not 100), which would leave a stray sub-pixel gap in
  // the rendered wheel. The very last stop is forced to exactly 100 for the same reason — the
  // slices' percentages sum to 1 by construction, so anything else is drift, not data.
  let cumulative = 0;
  const stops = slices.map((slice, index) => {
    const start = round(cumulative);
    cumulative += slice.percent * 100;
    const isLast = index === slices.length - 1;
    return `${slice.color} ${start}% ${isLast ? 100 : round(cumulative)}%`;
  });

  return `conic-gradient(${stops.join(', ')})`;
}

function round(percent: number): number {
  return Math.round(percent * 10000) / 10000;
}
