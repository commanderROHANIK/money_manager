/**
 * The token resolver behind every chart's colours.
 *
 * Two pieces of real logic live here and neither was covered. The cache is load-bearing by the
 * module's own account — resolving one token costs a `getComputedStyle` plus a canvas readback,
 * and the charts ask for a dozen of them — so a cache that silently stopped caching would be a
 * performance regression nothing would notice. And `chartColor` wraps with a modulo, which is
 * what stops a breakdown with more series than palette entries from painting `undefined`.
 *
 * `getComputedStyle` is stubbed rather than driven through real CSS: jsdom does not resolve
 * custom properties, so the real thing returns an empty string for every token and the
 * assertions below could not tell a working resolver from a broken one. Stubbing it also makes
 * the call count observable, which is the only way to assert the cache at all.
 *
 * The canvas conversion is deliberately not asserted. jsdom has no 2d context, so `toHex`
 * returns its input untouched — which is the module's documented fallback, and pinning hex
 * output would be pinning jsdom's behaviour rather than the app's.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import {
  chartColors,
  chartCategoricalPalette,
  chartColor,
  resetChartColorCache,
} from './chartTheme';

let getPropertyValue: ReturnType<typeof vi.fn>;
let computedStyle: ReturnType<typeof vi.spyOn>;
let canvasContext: ReturnType<typeof vi.spyOn>;

beforeEach(() => {
  // The cache is module state and outlives any single test.
  resetChartColorCache();

  // jsdom ships no 2d context and logs "Not implemented: HTMLCanvasElement's getContext()" for
  // every attempt — once per token, since the module only caches a context it actually got.
  // Returning null explicitly reaches the same documented fallback without the noise, and makes
  // the assumption these tests rest on visible instead of incidental.
  canvasContext = vi
    .spyOn(HTMLCanvasElement.prototype, 'getContext')
    .mockReturnValue(null as unknown as RenderingContext);

  getPropertyValue = vi.fn((name: string) => `resolved(${name})`);
  computedStyle = vi
    .spyOn(window, 'getComputedStyle')
    .mockReturnValue({ getPropertyValue } as unknown as CSSStyleDeclaration);
});

afterEach(() => {
  computedStyle.mockRestore();
  canvasContext.mockRestore();
  resetChartColorCache();
});

describe('token resolution', () => {
  it('reads the named custom property off the document root', () => {
    expect(chartColors.primary).toBe('resolved(--mm-primary)');
    expect(getPropertyValue).toHaveBeenCalledWith('--mm-primary');
  });

  it('gives every named colour its own token', () => {
    // Guards against the copy-paste failure this shape invites: two getters pointing at the
    // same custom property, which renders as two series in identical colours.
    const requested = Object.values(chartColors);

    expect(new Set(requested).size).toBe(requested.length);
  });

  it('resolves an unset token to an empty string rather than inventing one', () => {
    getPropertyValue.mockReturnValue('   ');

    // Trimmed to empty, so the falsy branch skips the canvas conversion entirely. Chart.js
    // treats an empty string as "use your default", which is the right failure for a missing
    // token — a fabricated colour would look deliberate.
    expect(chartColors.accent).toBe('');
  });
});

describe('caching', () => {
  it('resolves a given token only once', () => {
    void chartColors.primary;
    void chartColors.primary;
    void chartColors.primary;

    expect(getPropertyValue).toHaveBeenCalledTimes(1);
  });

  it('resolves distinct tokens separately', () => {
    void chartColors.primary;
    void chartColors.danger;

    expect(getPropertyValue).toHaveBeenCalledTimes(2);
  });

  it('re-reads after the cache is cleared', () => {
    void chartColors.primary;
    resetChartColorCache();
    void chartColors.primary;

    // The theme-switch path depends on this: Chart.js copies these values into its own config
    // when a dataset is built, so a stale cache would survive a theme change indefinitely.
    expect(getPropertyValue).toHaveBeenCalledTimes(2);
  });
});

describe('the categorical palette', () => {
  it('has six entries, drawn from the numbered tokens', () => {
    expect(chartCategoricalPalette()).toEqual([
      'resolved(--mm-chart-1)',
      'resolved(--mm-chart-2)',
      'resolved(--mm-chart-3)',
      'resolved(--mm-chart-4)',
      'resolved(--mm-chart-5)',
      'resolved(--mm-chart-6)',
    ]);
  });

  it('wraps around rather than running off the end', () => {
    // A breakdown with more slices than the palette has entries is the ordinary case, not an
    // edge one — an index past the end must reuse a colour, not return undefined.
    expect(chartColor(6)).toBe(chartColor(0));
    expect(chartColor(7)).toBe(chartColor(1));
    expect(chartColor(13)).toBe(chartColor(1));
  });

  it('is defined for every index a chart could ask for', () => {
    for (let index = 0; index < 20; index++) {
      expect(chartColor(index)).toBeTruthy();
    }
  });
});
