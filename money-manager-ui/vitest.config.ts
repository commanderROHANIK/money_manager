import { defineConfig } from 'vitest/config';
import vue from '@vitejs/plugin-vue';

export default defineConfig({
  plugins: [vue()],
  test: {
    environment: 'jsdom',
    include: ['src/**/*.test.ts'],
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
      // Last measured 70.35 / 71.2 / 71.54 / 69.97, after the registration-disabled branch in
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
        statements: 68,
        branches: 69,
        functions: 69,
        lines: 68,
      },
    },
  },
});
