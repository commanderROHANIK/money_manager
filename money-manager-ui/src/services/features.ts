import { readonly, ref } from 'vue';
import type { DeepReadonly, Ref } from 'vue';
import { api } from './api';

/**
 * Which sections this deployment presents. Mirrors `FeaturesDto` on the API.
 */
export interface Features {
  banking: boolean;
  stocks: boolean;
  loans: boolean;
  events: boolean;
}

export type FeatureName = keyof Features;

/**
 * What the UI believes before the server has answered, and what it falls back to if the request
 * fails.
 *
 * <p>Closed rather than open, deliberately. Guessing "on" and being wrong renders a section whose
 * every endpoint answers 404 — a screen of failed requests with no explanation. Guessing "off"
 * and being wrong hides a link for the moment it takes one request to resolve, and the router
 * waits for that request before the first authenticated view renders, so in practice nobody sees
 * the closed state at all.</p>
 */
const closed: Features = { banking: false, stocks: false, loans: false, events: false };

const features = ref<Features>({ ...closed });

/**
 * The in-flight or resolved load. Held so that the router guard, the navigation and the dashboard
 * share one request rather than three, and so a second navigation does not refetch.
 */
let pending: Promise<Features> | null = null;

/**
 * The flags, for components to render from. Read-only because there is exactly one writer — a
 * component that could assign here would be inventing a section rather than reading one.
 */
export const featureFlags: DeepReadonly<Ref<Features>> = readonly(features);

/** How many times a single navigation asks before it gives up and holds the closed default. */
const ATTEMPTS = 2;

/** Long enough for a dropped connection to re-establish, short enough not to stall a navigation. */
const RETRY_DELAY_MS = 400;

const wait = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms));

/**
 * Resolves once the flags are known, fetching them at most once per navigation.
 *
 * <p>A failed request is deliberately not cached: the flags decide what the whole application
 * shows, so a blip during the first navigation would otherwise leave the session stuck in the
 * closed state until a reload. Clearing `pending` on failure means the next navigation tries
 * again, and the closed default holds in the meantime.</p>
 *
 * <p>Not caching the failure was not enough on its own, though, and a review said so: the router
 * guard consumes the *same* navigation's answer, so a single dropped request bounced someone off
 * `/loans` to the dashboard with the sidebar collapsed to two links, and the retry only happened
 * if they navigated again — which, having just been thrown somewhere they did not ask for, they
 * have no reason to do. One immediate retry covers the blip, which is the failure that actually
 * happens; two failures in a row is an outage, and holding the closed default is the right answer
 * to that.</p>
 *
 * <p>A 401 is not retried. The api interceptor is already redirecting to the login screen, and a
 * second request would only race that redirect with a second guaranteed failure.</p>
 */
export function ensureFeaturesLoaded(): Promise<Features> {
  pending ??= load();

  return pending;
}

async function load(): Promise<Features> {
  for (let attempt = 1; attempt <= ATTEMPTS; attempt += 1) {
    try {
      const response = await api.get<Features>('/Features');
      features.value = response.data;
      return features.value;
    } catch (error: unknown) {
      const status = (error as { response?: { status?: number } })?.response?.status;
      const worthRetrying = attempt < ATTEMPTS && status !== 401;

      if (!worthRetrying) {
        // Not cached, so the next navigation starts again from a clean slate.
        pending = null;
        console.error(
          'Failed to load feature flags; sections stay hidden until this succeeds.',
          error
        );
        return features.value;
      }

      await wait(RETRY_DELAY_MS);
    }
  }

  return features.value;
}

/**
 * Forgets the flags, so the next navigation asks the server again. Called on logout: the flags
 * belong to the deployment rather than to the account, but a stale copy surviving a logout would
 * mean the answer to "what does this app contain" depends on how long the tab has been open.
 */
export function clearFeatures(): void {
  pending = null;
  features.value = { ...closed };
}
