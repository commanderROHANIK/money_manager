import { describe, it, expect } from 'vitest';
import { pieChartSlices, pieChartGradient } from './pieChart';

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

describe('pieChartGradient', () => {
  it('builds a single 0%-100% stop for one slice', () => {
    const slices = pieChartSlices([{ label: 'Only', value: 42, color: '#123456' }]);

    expect(pieChartGradient(slices)).toBe('conic-gradient(#123456 0% 100%)');
  });

  it('chains cumulative stops across slices, not per-slice widths', () => {
    // 55/25/20, in the design system mockup's own proportions.
    const slices = pieChartSlices([
      { label: 'Checking', value: 55, color: 'primary' },
      { label: 'Savings', value: 25, color: 'accent' },
      { label: 'Investing', value: 20, color: 'neutral' },
    ]);

    expect(pieChartGradient(slices)).toBe(
      'conic-gradient(primary 0% 55%, accent 55% 80%, neutral 80% 100%)'
    );
  });

  it('does not lose a slice to rounding when values do not split evenly', () => {
    const slices = pieChartSlices([
      { label: 'A', value: 1, color: 'a' },
      { label: 'B', value: 1, color: 'b' },
      { label: 'C', value: 1, color: 'c' },
    ]);

    // Three exact thirds: rounded to a clean 4 decimals rather than left as repeating floats,
    // and the final stop is forced to exactly 100% rather than whatever the drift rounds to.
    expect(pieChartGradient(slices)).toBe('conic-gradient(a 0% 33.3333%, b 33.3333% 66.6667%, c 66.6667% 100%)');
  });

  it('renders transparent rather than an empty gradient() call for no data', () => {
    expect(pieChartGradient([])).toBe('transparent');
  });
});
