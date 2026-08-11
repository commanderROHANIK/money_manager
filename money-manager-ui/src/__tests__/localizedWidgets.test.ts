/**
 * Mounts the widgets whose text depends on a condition, in Hungarian, on both sides of each
 * condition.
 *
 * These branches are the ones translation created and nothing else covers. A widget that picks
 * between two messages — a tenancy with an end date or without, a rent above or below market, a
 * schedule up to date or behind — has a path through it that no existing suite walks, because the
 * content suites are pinned to English and assert one case each.
 *
 * They earn their place twice over. Rendering in Hungarian is what proves a component actually
 * reads the locale rather than holding an English string it happens to pass through, and picking
 * a branch is what proves the *other* message exists in the locale files at all — a key that only
 * the rarer branch reaches is exactly the one a translator forgets.
 */
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { mount } from '@vue/test-utils';
import type { PortfolioAnalytics, PropertyMetrics, RentSchedule } from '../models/models';
import { setLocale } from '../i18n';
import { DEFAULT_LOCALE } from '../i18n/locale';
import * as f from './fixtures';
import { FROZEN_NOW } from './fixtures';

import MostExpensivePropertyWidget from '../components/Widgets/Properties/MostExpensivePropertyWidget.vue';
import PortfolioSummaryWidget from '../components/Widgets/Properties/PortfolioSummaryWidget.vue';
import RentCollectionWidget from '../components/Widgets/Properties/RentCollectionWidget.vue';
import RentVsMarketWidget from '../components/Widgets/Properties/RentVsMarketWidget.vue';
import TenancyWidget from '../components/Widgets/Properties/TenancyWidget.vue';

beforeEach(() => {
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(FROZEN_NOW);
  setLocale('hu');
});

afterEach(() => {
  vi.useRealTimers();
  setLocale(DEFAULT_LOCALE);
});

/** Nothing rendered in Hungarian should contain an unresolved key or a leftover plural form. */
function assertFullyTranslated(text: string) {
  expect(text).not.toMatch(/property\.|event\.|loan\.|settings\./);
  expect(text).not.toContain('{');
  expect(text).not.toContain('|');
}

describe('rent vs market', () => {
  const withGap = (gap: number | null): PropertyMetrics =>
    ({ ...f.propertyMetrics, rentGapPercent: gap }) as unknown as PropertyMetrics;

  it('says below market, above market and at market, each in Hungarian', () => {
    // Three branches behind one headline, and only one of them is on the common path. The other
    // two are where a missing key hides.
    const below = mount(RentVsMarketWidget, { props: { metrics: withGap(0.12) } }).text();
    const above = mount(RentVsMarketWidget, { props: { metrics: withGap(-0.08) } }).text();
    const at = mount(RentVsMarketWidget, { props: { metrics: withGap(0) } }).text();

    expect(below).toContain('piaci alatt');
    expect(above).toContain('piaci fölött');
    expect(at).toContain('Piaci szinten');

    for (const text of [below, above, at]) assertFullyTranslated(text);
  });

  it('offers the estimate form when no market rent is on record', () => {
    const wrapper = mount(RentVsMarketWidget, {
      props: { metrics: { ...f.propertyMetrics, marketMonthlyRent: null } as unknown as PropertyMetrics },
    });

    expect(wrapper.text()).toContain('nincs rögzített piaci becslés');
    assertFullyTranslated(wrapper.text());
  });
});

describe('tenancy', () => {
  it('words an open-ended tenancy differently from one with an end date', () => {
    // The case the old template could not express: Hungarian joins the two dates with a dash and
    // no connecting word, so "Since X" and "Since X until Y" are separate sentences rather than
    // one with a tail.
    const open = mount(TenancyWidget, {
      props: { leases: [{ ...f.leases[0], endDate: null }], propertyId: 1 },
    }).text();

    const closed = mount(TenancyWidget, {
      props: { leases: [{ ...f.leases[0], endDate: '2027-01-31' }], propertyId: 1 },
    }).text();

    expect(open).not.toEqual(closed);
    assertFullyTranslated(open);
    assertFullyTranslated(closed);
  });

  it('says the property is vacant when no tenancy is running', () => {
    const wrapper = mount(TenancyWidget, { props: { leases: [], propertyId: 1 } });

    expect(wrapper.text()).toContain('Üres');
    assertFullyTranslated(wrapper.text());
  });
});

describe('rent collection', () => {
  const mountWith = (schedule: RentSchedule) =>
    mount(RentCollectionWidget, {
      props: { schedule, currencyCode: 'HUF', recording: null, error: null },
    });

  it('distinguishes a schedule in arrears from one that is up to date', () => {
    const behind = mountWith(f.rentSchedule as unknown as RentSchedule).text();
    const current = mountWith({
      ...f.rentSchedule,
      arrears: 0,
      overduePeriodCount: 0,
    } as unknown as RentSchedule).text();

    expect(current).toContain('Nincs elmaradás');
    expect(behind).toContain('elmaradás');
    expect(behind).not.toContain('Nincs elmaradás');

    // The arrears badge is pluralised, and Hungarian takes a single form where English takes two.
    // Asserting no leftover pipe is what catches the form separator reaching the screen.
    assertFullyTranslated(behind);
    assertFullyTranslated(current);
  });

  it('renders the empty state when there is no tenancy at all', () => {
    const wrapper = mountWith({ ...f.rentSchedule, periods: [] } as unknown as RentSchedule);

    expect(wrapper.text()).toContain('Még nincs rögzített bérlet');
    assertFullyTranslated(wrapper.text());
  });

  it('swaps the show-all label when the table is expanded', async () => {
    // The toggle only exists past COLLAPSED_ROWS (12), and the shared fixture holds six months —
    // so the schedule is padded here rather than the fixture being changed, which would alter
    // what every other suite using it is asserting.
    const padded = {
      ...f.rentSchedule,
      periods: Array.from({ length: 14 }, (_, i) => ({
        ...f.rentSchedule.periods[i % f.rentSchedule.periods.length],
        period: `2025-${String((i % 12) + 1).padStart(2, '0')}`,
      })),
    };

    const wrapper = mountWith(padded as unknown as RentSchedule);
    // Selected by its own class: the first button in the table is a per-row "mark
    // received" action, and finding that instead compares one label against itself.
    const toggle = wrapper.find('button.text-primary-strong');

    // The collapsed label carries a count and the expanded one does not, so the two are separate
    // messages rather than one with an optional number — and only one of them is ever on screen
    // at a time, which is what leaves the other uncovered.
    const collapsed = toggle.text();
    await toggle.trigger('click');
    const expanded = toggle.text();

    expect(collapsed).not.toEqual(expanded);
    assertFullyTranslated(collapsed);
    assertFullyTranslated(expanded);
  });

  it('shows a vacant month as unknown rather than as zero received', () => {
    // The product rule the schedule has to keep in every language: nothing was owed for a vacant
    // month, which is not the same as everything owed having been paid.
    const wrapper = mountWith(f.rentScheduleWithVacancy as unknown as RentSchedule);

    expect(wrapper.text()).toContain('—');
    assertFullyTranslated(wrapper.text());
  });

  it('names the month being recorded while the request is in flight', () => {
    const period = f.rentSchedule.periods.find((p) => p.status !== 3)?.period;

    const wrapper = mount(RentCollectionWidget, {
      props: { schedule: f.rentSchedule as unknown as RentSchedule, currencyCode: 'HUF', recording: period ?? null, error: null },
    });

    assertFullyTranslated(wrapper.text());
  });
});

describe('highest rent', () => {
  it('labels a let property and a vacant one differently', () => {
    const let_ = mount(MostExpensivePropertyWidget, {
      props: { properties: [{ ...f.properties[0], isRented: true }] },
    }).text();

    const vacant = mount(MostExpensivePropertyWidget, {
      props: { properties: [{ ...f.properties[0], isRented: false }] },
    }).text();

    expect(let_).toContain('Kiadva');
    expect(vacant).toContain('Üres');
    assertFullyTranslated(let_);
    assertFullyTranslated(vacant);
  });

  it('says there is nothing to show when the portfolio is empty', () => {
    const wrapper = mount(MostExpensivePropertyWidget, { props: { properties: [] } });

    expect(wrapper.text()).toContain('Nincs rögzített ingatlan');
    assertFullyTranslated(wrapper.text());
  });
});

describe('portfolio conversion note', () => {
  const withRate = (source: number, asOf: string) =>
    ({
      ...f.portfolioConverted,
      appliedRates: [{ from: 'HUF', to: 'EUR', rate: 0.0025, asOf, inverted: true, source }],
    }) as unknown as PortfolioAnalytics;

  it('attributes an entered rate and a fetched one differently, in Hungarian', () => {
    // The branch translation created and nothing else walks. Before this change there was one
    // sentence, so the Hungarian file had one key — and the version that names the ECB is the one
    // a translator never sees, because it only appears once a deployment starts fetching.
    const entered = mount(PortfolioSummaryWidget, { props: { portfolio: withRate(0, '2026-07-01T00:00:00') } }).text();
    const fetched = mount(PortfolioSummaryWidget, { props: { portfolio: withRate(1, '2026-08-10T00:00:00') } }).text();

    expect(entered).toContain('általad megadott árfolyam');
    expect(fetched).toContain('EKB-referenciaárfolyam');
    expect(entered).not.toEqual(fetched);

    for (const text of [entered, fetched]) assertFullyTranslated(text);
  });

  it('says only the arithmetic when no stored row backs the rate', () => {
    // asOf and source are null together, for a conversion nothing was stored for. Attributing it
    // to anybody would be inventing a provenance.
    const portfolio = {
      ...f.portfolioConverted,
      appliedRates: [{ from: 'HUF', to: 'EUR', rate: 0.0025, asOf: null, inverted: true, source: null }],
    } as unknown as PortfolioAnalytics;

    const text = mount(PortfolioSummaryWidget, { props: { portfolio } }).text();

    expect(text).toContain('1 HUF = 0.0025 EUR');
    expect(text).not.toContain('EKB');
    assertFullyTranslated(text);
  });
});
