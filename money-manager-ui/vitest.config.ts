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
      // No thresholds yet, deliberately. A floor picked before the baseline is known either
      // sits below reality and gates nothing, or above it and teaches people to write
      // assertion-free tests. Measure first, then set it just under the current number so it
      // works as a regression detector.
    },
  },
});
