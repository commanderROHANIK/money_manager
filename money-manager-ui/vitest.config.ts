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
      // Set from the measured baseline (69.2 / 68.1 / 69.5 / 69.0 at the time of writing),
      // rounded down a couple of points. Deliberately NOT an aspirational number: the job of a
      // floor is to catch a pull request that deletes coverage, and a target nobody can hit
      // just teaches people to write assertion-free tests to clear it.
      //
      // Raise these as part of any change that raises real coverage. That is the ratchet.
      thresholds: {
        statements: 67,
        branches: 66,
        functions: 67,
        lines: 67,
      },
    },
  },
});
