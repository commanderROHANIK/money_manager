import { defineConfig } from 'vitest/config';
import vue from '@vitejs/plugin-vue';

export default defineConfig({
  plugins: [vue()],
  test: {
    environment: 'jsdom',
    include: ['src/**/*.test.ts'],
    // Installs vue-i18n for every mount. Without it, the first component to call useI18n()
    // fails its test for a reason that has nothing to do with what the test asserts.
    setupFiles: ['src/__tests__/setup.ts'],
    coverage: {
      // Must track the vitest major: a v3 provider fails silently against vitest 4.
      provider: 'v8',
      // json-summary is what CI reads to put a number on the run summary; text-summary is for
      // whoever runs this locally.
      reporter: ['text-summary', 'json-summary', 'cobertura', 'html'],
      include: ['src/**/*.{ts,vue}'],
      exclude: [
        'src/**/*.test.ts',
        'src/__tests__/**',
        'src/main.ts',
        'src/vite-env.d.ts',
        // Type declarations only — covering them measures nothing.
        'src/models/**',
      ],
      // Set from the measured baseline, rounded down a couple of points. Deliberately NOT an
      // aspirational number: the job of a floor is to catch a pull request that deletes
      // coverage, and a target nobody can hit just teaches people to write assertion-free
      // tests to clear it.
      //
      // Raise these as part of any change that raises real coverage. That is the ratchet.
      //
      // Last measured 76.85 / 74.33 / 74.45 / 76.73, after the onboarding checklist.
      //
      // Functions moves 73 -> 74, and it is the only one that moves. It gained 1.19 — the largest
      // single jump these floors have seen — because the feature arrived as two pure functions
      // with a table of cases against them rather than as a component with logic buried in it.
      //
      // Statements and lines gained about six tenths each and stay at 76: the next rung up is
      // above the measurement, so raising them would fail the run that just passed. Branches did
      // not move at all — 74.34 to 74.33 — which is exactly the rounding these floors are written
      // to absorb, and ratcheting on it would be ratcheting on noise.
      //
      // Note this is the second look at functions. The previous pass measured 74.11 and declined
      // 74, because 0.11 of headroom fails an honest pull request on rounding. The extra tests
      // for the dismissal path and the failed-request path put it at 74.45, which is the margin
      // this file has accepted before.
      //
      // Before that: 76.25 / 74.34 / 73.26 / 76.10, after the demo seed and its browser suite.
      //
      // Before that: 75.66 / 73.46 / 72.69 / 75.48, after the exchange-rate provenance work.
      //
      // Statements, lines and branches move up — 73 -> 75, 73 -> 75 and 71 -> 73 — keeping the
      // roughly half-point-to-two-point margin these floors carry. Functions stays at 70: it
      // measures 72.69 and gained a third of a point, which is the rounding the floor already
      // absorbs; 72 would be a ratchet on noise.
      //
      // Branches is the interesting one again. The disclosure is branching by nature — a rate is
      // attributed to the ECB, to the user, or to nobody; an introduction describes fetching or
      // says it does not happen — and those paths came with the tests that walk them, in both
      // English and Hungarian, rather than after the floor failed.
      //
      // Before that: 75.26 / 72.62 / 72.34 / 75.2, after the localization work.
      //
      // Statements, lines and functions move up — 71 -> 73, 71 -> 73 and 69 -> 70 — keeping the
      // roughly two-point margin the floors below were set with. Branches stays at 71: it
      // measures 72.62, and 72 would leave 0.62 of headroom.
      //
      // Branches is worth a note, because the floor did its job here rather than merely passing.
      // Translation *added* branches — every `condition ? t(a) : t(b)`, every message chosen per
      // case — and the run that finished the sweep came in at 69.69%, below the floor and failing
      // the build. That was correct: the branches were real and nothing exercised them, because
      // the widget content suites are pinned to English and assert one case each. The answer was
      // localizedWidgets.test.ts, which mounts the conditional widgets in Hungarian on both sides
      // of each condition — not a lower floor.
      //
      // Before that: 72.81 / 73.13 / 71.23 / 72.7, after the feature flags brought the feature
      // service, the navigation and the section guard under test.
      //
      // Statements and lines move 70 -> 71. That is a one-point ratchet on a 1.2-point gain, and
      // it is deliberately the smaller of the two moves available: 72 was measured first, on a
      // run where the router test imported the page components for real instead of stubbing
      // them, and it evaporated the moment that test went back to stubs. A floor set from the
      // higher number would have been a floor set from a mistake.
      //
      // Branches and functions stay where they are. Both gained under a point, and the next rung
      // up would leave 1.13 and 1.23 of headroom — the margin this file has already declined
      // once, because a floor that close fails an honest pull request on rounding rather than on
      // a regression.
      //
      // Before that: 71.6 / 72.18 / 70.32 / 71.36, after the validation work brought the API
      // error extractor and the add-property form under test.
      //
      // The floors move with it, which they did not last time: the vite 8 basis now has several
      // runs behind it and the numbers have risen twice on it, so this is a real gain rather
      // than a denominator shifting. Statements and lines go to 70, branches to 71 — each a
      // point or so under measured reality, the same margin the earlier floors carried.
      //
      // Functions stays at 69 on purpose. It measures 70.32, which is only 1.3 above the floor
      // where the others clear 3, and it is the metric the toolchain upgrade moved most. A floor
      // set that close to the measurement would fail an honest pull request on noise, which is
      // the failure mode these are written to avoid.
      //
      // Before that: 71.24 / 71.61 / 69.63 / 70.95, on vite 8 + @vitest/coverage-v8 against the
      // upgraded toolchain. The floors do not move, and the reason is worth writing down: this
      // measurement is not comparable to the ones below it. The function *denominator* jumped
      // from 471 to 517 on identical source — the newer build pipeline emits different function
      // boundaries, so v8 counts more of them — which dropped functions to 67.69% on the same
      // tests that had measured 71.54% the day before.
      //
      // That was a measurement change, not a coverage loss, and the honest fix was neither to
      // lower the floor nor to pretend nothing happened: chartTheme.ts got the colocated unit
      // test the testing table already asked for, which put functions back to 69.63% on the new
      // basis. Statements and lines gained about a point in the process. The floors stay where
      // they are until this basis has a couple of runs behind it — ratcheting onto a denominator
      // that has just moved is how you get a floor nobody can reproduce.
      //
      // Before that: 70.35 / 71.2 / 71.54 / 69.97, after the registration-disabled branch in
      // Register.vue arrived with its own test: branches moved a sixth of a point and nothing
      // else moved at all, which is the rounding these floors already absorb.
      //
      // Before that: 70.35 / 71.04 / 71.54 / 69.97, from 70.0 / 71.14 / 70.98 / 69.6, after the
      // rent-collection work. The floors do not move with it: the new widget and service arrived
      // with their own tests, so the ratio barely shifted — branches even dipped a tenth — and
      // ratcheting on noise would fail the next honest pull request for no gain. The floors move
      // when the measured number moves by more than the rounding they already allow for.
      //
      // Before that: 69.2 / 68.1 / 69.5 / 69.0, when the multi-currency rollup work brought the
      // two bank-balance widgets, the settings page and the exchange-rate service under test.
      thresholds: {
        statements: 76,
        branches: 74,
        functions: 74,
        lines: 76,
      },
    },
  },
});
