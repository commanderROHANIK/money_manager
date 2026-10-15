/**
 * The math behind `PieChart.vue`'s donut ring, pulled out of the component so it's directly
 * testable without mounting: a mounted-component test would only see the SVG attribute strings
 * this module builds, not verify the arc-length/offset arithmetic behind them, so there is
 * nothing a mount would check that a pure-function test does not already check better. Kept pure
 * for the same reason `PropertyAnalyticsCalculator` is pure.
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

export interface PieChartRingSegment extends PieChartSlice {
  /** SVG stroke-dasharray: "<visible arc length> <remainder>", both in viewBox units. */
  dashArray: string;
  /** SVG stroke-dashoffset: this slice's cumulative offset (negative, so the pattern advances
   *  clockwise from the 3-o'clock start `<circle>` paths begin at). */
  dashOffset: number;
}

export interface PieChartRing {
  radius: number;
  /** 2 * PI * radius — exposed so a no-data placeholder ring can be drawn from it too. */
  circumference: number;
  segments: PieChartRingSegment[];
}

/**
 * One `<circle>` per segment (sharing cx/cy/r), each drawing its own arc via stroke-dasharray
 * then treating the rest of the circumference as a gap — this sidesteps the large-arc-flag
 * branching a `<path>` arc command would need for wraparound/>180° segments, since the browser's
 * own circle renderer handles that. The whole `<svg>` gets rotated -90deg once by the caller
 * (not per-segment) so the shared 3-o'clock circle start becomes 12 o'clock, clockwise — matching
 * the design system mockup's conic-gradient convention.
 *
 * `stroke-linecap="round"` on the rendered circles adds a strokeWidth/2 semicircle past each arc
 * endpoint, so gapLength must stay greater than strokeWidth or neighboring caps touch and the
 * "clear divide" the gap exists for disappears.
 */
export function pieChartRing(
  slices: PieChartSlice[],
  options: { radius?: number; gapLength?: number } = {}
): PieChartRing {
  const radius = options.radius ?? 38;
  const gapLength = options.gapLength ?? 14;
  const circumference = 2 * Math.PI * radius;

  if (slices.length === 0) return { radius, circumference, segments: [] };

  // A single slice covers the full ring — a gap only means something between two neighbors.
  const gap = slices.length > 1 ? gapLength : 0;
  const usableCircumference = Math.max(0, circumference - gap * slices.length);

  let cursor = 0;
  const segments = slices.map((slice) => {
    const arcLength = round(slice.percent * usableCircumference);
    const dashOffset = round(-cursor);
    const dashArray = `${arcLength} ${round(circumference - arcLength)}`;
    cursor += arcLength + gap;
    return { ...slice, dashArray, dashOffset };
  });

  return { radius, circumference, segments };
}

function round(value: number): number {
  return Math.round(value * 1000) / 1000;
}
