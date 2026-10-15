/**
 * Chart.js draws its legend, tooltip and axis text on a canvas, which has no CSS cascade — a
 * widget that regressed back to the default sans-serif would render correctly in every other
 * test and be invisible to Playwright too, since there is no DOM text node to inspect. Capturing
 * the `options` object each chart component actually receives is the only way to catch that, so
 * this stubs `vue-chartjs` itself (rather than mocking chartTheme) and reads the real options.
 *
 * Only 3 widgets are left on Chart.js after the pie/donut widgets moved to `PieChart.vue`'s
 * CSS conic-gradient — see `src/utils/pieChart.test.ts` for those.
 */
import { describe, it, expect, vi } from 'vitest';
import { mount } from '@vue/test-utils';
import { nextTick } from 'vue';
import * as f from '../../__tests__/fixtures';

const received: Record<string, unknown>[] = [];

vi.mock('vue-chartjs', async () => {
  const { defineComponent, h } = await import('vue');
  const stub = (name: string) =>
    defineComponent({
      name,
      props: { data: { type: Object }, options: { type: Object } },
      setup(props) {
        return () => {
          received.push(props.options as Record<string, unknown>);
          return h('canvas');
        };
      },
    });
  return { Bar: stub('Bar'), Line: stub('Line'), Pie: stub('Pie'), Doughnut: stub('Doughnut') };
});

vi.mock('../../services/api', async () => {
  const f = await import('../../__tests__/fixtures');
  return { fetchStocks: () => Promise.resolve(f.stocks) };
});

const { default: RentByMonthChartWidget } = await import('./Properties/RentByMonthChartWidget.vue');
const { default: RentOverTimeChartWidget } = await import('./Properties/RentOverTimeChartWidget.vue');
const { default: PortfolioPerformanceChartWidget } = await import('./Stocks/PortfolioPerformanceChartWidget.vue');

/** Every `family` value Chart.js would actually see, wherever in the options tree it sits. */
function fontFamilies(options: unknown): string[] {
  return JSON.stringify(options).match(/"family":"[^"]*"/g) ?? [];
}

describe('chart widgets set a font family on every text-producing option', () => {
  it.each([
    [
      'RentByMonthChartWidget',
      () =>
        mount(RentByMonthChartWidget, {
          props: {
            properties: f.properties,
            payments: [{ datePaid: '2026-01-15T00:00:00Z', amount: 195000, currencyCode: 'HUF' }],
          },
        }),
    ],
    [
      'RentOverTimeChartWidget',
      () => mount(RentOverTimeChartWidget, { props: { history: f.rentHistory, currencyCode: 'HUF' } }),
    ],
  ])('%s hands Chart.js a non-empty font family', async (_name, mountWidget) => {
    received.length = 0;
    const wrapper = mountWidget();
    await nextTick();

    expect(received.length).toBeGreaterThan(0);
    for (const options of received) {
      expect(fontFamilies(options).length).toBeGreaterThan(0);
      for (const entry of fontFamilies(options)) {
        expect(entry).toContain('Inter');
      }
    }

    wrapper.unmount();
  });

  it('PortfolioPerformanceChartWidget hands Chart.js a non-empty font family once its fetch resolves', async () => {
    received.length = 0;
    const wrapper = mount(PortfolioPerformanceChartWidget);
    await new Promise((resolve) => setTimeout(resolve, 0));
    await nextTick();

    expect(received.length).toBeGreaterThan(0);
    for (const options of received) {
      expect(fontFamilies(options).length).toBeGreaterThan(0);
      for (const entry of fontFamilies(options)) {
        expect(entry).toContain('Inter');
      }
    }

    wrapper.unmount();
  });
});
