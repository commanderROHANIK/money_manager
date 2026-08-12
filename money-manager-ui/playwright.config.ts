import { defineConfig, devices } from '@playwright/test';

/**
 * End-to-end configuration.
 *
 * <p>The suite runs against a <em>running deployment image</em>, not against `dotnet run` plus a
 * Vite dev server. That is the whole point of it: the container is where the two halves meet on
 * one origin, and the failure modes that only appear there — the SPA fallback's `{*path:nonfile}`
 * pattern colliding with the deny-by-default authorization policy, static files served after
 * `UseAuthorization`, `wwwroot` resolving off a mismatched ContentRoot — are invisible to a dev
 * server that serves the bundle itself. In CI the image is the one the API job has just built.</p>
 *
 * <p>Point it somewhere with `E2E_BASE_URL`. There is deliberately no `webServer` block: starting
 * the container is CI's job (or the reader's), and a config that shelled out to `docker run` would
 * silently do nothing on a machine where the suite was pointed at an already-running instance.</p>
 */

const baseURL = process.env.E2E_BASE_URL ?? 'http://localhost:8080';

export default defineConfig({
  testDir: './e2e',

  // Vitest owns `src/**/*.test.ts`; this owns `e2e/**/*.spec.ts`. The two globs cannot overlap,
  // so neither runner can ever pick up the other's files — which matters because a Playwright
  // spec collected by vitest fails in a way that reads like a broken component.
  testMatch: '**/*.spec.ts',

  // One worker against one container holding one SQLite file. These specs only read, so parallel
  // execution would be safe today — but the first spec that writes would make it unsafe silently,
  // and finding that out through a flake is worse than the ~10s this costs.
  fullyParallel: false,
  workers: 1,

  forbidOnly: !!process.env.CI,

  // No retries, on purpose. A retry turns "the demo is intermittently broken" into a green check,
  // and an intermittently broken demo is precisely what this suite exists to report. If a spec
  // here is flaky, the flake is the finding.
  retries: 0,

  timeout: 30_000,
  expect: { timeout: 10_000 },

  // `list` is in the CI set on purpose, alongside the other two. The `github` reporter emits
  // annotations and the `html` one writes a file you have to download; neither puts a legible
  // account of the failure in the job log itself, which is the first and often only place anyone
  // looks. Leaving it out once already cost a full cycle diagnosing a red run.
  reporter: process.env.CI
    ? [['list'], ['github'], ['html', { open: 'never' }]]
    : [['list']],

  use: {
    baseURL,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'off',
  },

  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
});
