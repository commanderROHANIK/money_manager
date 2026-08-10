import { config } from '@vue/test-utils';
import { i18n } from '../i18n';

/**
 * Installs the translation plugin for every mount in the suite.
 *
 * <p>Once a component calls `useI18n()`, mounting it without the plugin throws — and
 * `widgets.smoke.test.ts` mounts every widget in the tree and fails on any Vue warning, which is
 * exactly the behaviour that makes it worth having. Registering the plugin here rather than in
 * each file means a newly translated component does not also require its test to be found and
 * amended, which is the kind of chore that ends with someone deleting the assertion instead.</p>
 *
 * <p>The locale is deliberately not pinned here. It defaults to Hungarian, the same as the
 * application, so a test that wants English says so — see the content suites, which do.</p>
 */
config.global.plugins = [...(config.global.plugins ?? []), i18n];
