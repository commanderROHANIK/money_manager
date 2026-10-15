import { describe, it, expect } from 'vitest';
import { pieChartSlices, pieChartRing } from './pieChart';

const DEFAULT_RADIUS = 38;
const DEFAULT_GAP = 14;
const circumference = (radius: number) => 2 * Math.PI * radius;

function arcLength(dashArray: string): number {
  return Number(dashArray.split(' ')[0]);
}

describe('pieChartSlices', () => {
  it('filters out zero-value segments', () => {
    const slices = pieChartSlices([
      { label: 'Has value', value: 10, color: '#111111' },
      { label: 'Empty', value: 0, color: '#222222' },
    ]);

    expect(slices.map((s) => s.label)).toEqual(['Has value']);
  });

  it('sorts largest first, matching the design system mockup', () => {
    const slices = pieChartSlices([
      { label: 'Smallest', value: 20, color: '#111111' },
      { label: 'Largest', value: 55, color: '#222222' },
      { label: 'Middle', value: 25, color: '#333333' },
    ]);

    expect(slices.map((s) => s.label)).toEqual(['Largest', 'Middle', 'Smallest']);
  });

  it('computes each slice as a fraction of the total', () => {
    const slices = pieChartSlices([
      { label: 'A', value: 25, color: '#111111' },
      { label: 'B', value: 75, color: '#222222' },
    ]);

    expect(slices.find((s) => s.label === 'A')?.percent).toBe(0.25);
    expect(slices.find((s) => s.label === 'B')?.percent).toBe(0.75);
  });

  it('returns an empty list rather than dividing by zero when every segment is zero', () => {
    expect(pieChartSlices([{ label: 'Nothing', value: 0, color: '#111111' }])).toEqual([]);
    expect(pieChartSlices([])).toEqual([]);
  });
});

describe('pieChartRing', () => {
  it('gives a single slice the full circumference with no gap', () => {
    const slices = pieChartSlices([{ label: 'Only', value: 42, color: '#123456' }]);
    const ring = pieChartRing(slices);

    expect(ring.segments).toHaveLength(1);
    expect(ring.segments[0].dashOffset).toBeCloseTo(0, 2);
    expect(arcLength(ring.segments[0].dashArray)).toBeCloseTo(circumference(DEFAULT_RADIUS), 2);
  });

  it('gives two equal slices equal arc lengths, chaining the second off the first', () => {
    const slices = pieChartSlices([
      { label: 'A', value: 1, color: 'a' },
      { label: 'B', value: 1, color: 'b' },
    ]);
    const ring = pieChartRing(slices);
    const [first, second] = ring.segments;

    expect(arcLength(first.dashArray)).toBeCloseTo(arcLength(second.dashArray), 2);
    expect(second.dashOffset).toBeCloseTo(-(arcLength(first.dashArray) + DEFAULT_GAP), 2);
  });

  it('preserves the mockup ratios (55/25/20) after gap subtraction', () => {
    // The design system mockup's own proportions — Checking 55%, Savings 25%, Investing 20%.
    const slices = pieChartSlices([
      { label: 'Checking', value: 55, color: 'primary' },
      { label: 'Savings', value: 25, color: 'accent' },
      { label: 'Investing', value: 20, color: 'neutral' },
    ]);
    const [checking, savings, investing] = pieChartRing(slices).segments;

    expect(arcLength(checking.dashArray) / arcLength(savings.dashArray)).toBeCloseTo(55 / 25, 2);
    expect(arcLength(savings.dashArray) / arcLength(investing.dashArray)).toBeCloseTo(25 / 20, 2);
  });

  it('returns no segments for no data, without NaN or Infinity leaking into circumference', () => {
    const ring = pieChartRing([]);

    expect(ring.segments).toEqual([]);
    expect(Number.isFinite(ring.circumference)).toBe(true);
    expect(ring.circumference).toBeGreaterThan(0);
  });

  it('degenerates back to abutting arcs when gapLength is 0', () => {
    const slices = pieChartSlices([
      { label: 'A', value: 1, color: 'a' },
      { label: 'B', value: 1, color: 'b' },
      { label: 'C', value: 1, color: 'c' },
    ]);
    const ring = pieChartRing(slices, { gapLength: 0 });
    const total = ring.segments.reduce((sum, segment) => sum + arcLength(segment.dashArray), 0);

    expect(total).toBeCloseTo(ring.circumference, 2);
  });

  it('scales the circumference with a custom radius, without changing relative proportions', () => {
    const slices = pieChartSlices([
      { label: 'Checking', value: 55, color: 'primary' },
      { label: 'Savings', value: 25, color: 'accent' },
      { label: 'Investing', value: 20, color: 'neutral' },
    ]);
    const small = pieChartRing(slices, { radius: 10 });

    expect(small.circumference).toBeCloseTo(circumference(10), 2);
    expect(arcLength(small.segments[0].dashArray) / arcLength(small.segments[1].dashArray)).toBeCloseTo(55 / 25, 2);
  });

  it('does not lose a slice to rounding when values do not split evenly, and closes back to the circumference', () => {
    const slices = pieChartSlices([
      { label: 'A', value: 1, color: 'a' },
      { label: 'B', value: 1, color: 'b' },
      { label: 'C', value: 1, color: 'c' },
    ]);
    const ring = pieChartRing(slices);

    for (const segment of ring.segments) {
      expect(arcLength(segment.dashArray)).toBeGreaterThan(0);
      expect(arcLength(segment.dashArray)).toBeCloseTo(arcLength(ring.segments[0].dashArray), 2);
    }

    const last = ring.segments[ring.segments.length - 1];
    const closesAt = -last.dashOffset + arcLength(last.dashArray) + DEFAULT_GAP;
    expect(closesAt).toBeCloseTo(ring.circumference, 2);
  });
});
