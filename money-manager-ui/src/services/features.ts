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

/**
 * Resolves once the flags are known, fetching them at most once.
 *
 * <p>A failed request is deliberately not cached: the flags decide what the whole application
 * shows, so a blip during the first navigation would otherwise leave the session stuck in the
 * closed state until a reload. Clearing `pending` on failure means the next navigation tries
 * again, and the closed default holds in the meantime.</p>
 *
 * <p>A 401 needs no handling here — the api interceptor already sends an expired session back to
 * the login screen, and the closed default is the right thing to be holding when it does.</p>
 */
export function ensureFeaturesLoaded(): Promise<Features> {
  pending ??= api
    .get<Features>('/Features')
    .then((response) => {
      features.value = response.data;
      return features.value;
    })
    .catch((error: unknown) => {
      pending = null;
      console.error('Failed to load feature flags; sections stay hidden until this succeeds.', error);
      return features.value;
    });

  return pending;
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
