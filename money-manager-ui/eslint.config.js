import js from '@eslint/js';
import pluginVue from 'eslint-plugin-vue';
import { defineConfigWithVueTs, vueTsConfigs } from '@vue/eslint-config-typescript';

/**
 * ESLint rather than Biome, for one specific reason.
 *
 * Biome is faster and needs almost no configuration, but it parses `<script>` blocks and does
 * not lint `<template>`. Template defects are this codebase's demonstrated failure mode:
 * TenancyWidget once shipped a template referencing BaseInput, BaseButton and ListRow with no
 * imports, and both `vue-tsc` and `vite build` passed. `vue/no-undef-components` catches exactly
 * that, statically, before anything has to be mounted. At 60 files Biome's speed edge is
 * irrelevant; that one rule is not.
 *
 * Every rule here is an error, and `npm run lint` carries --max-warnings 0 in ci.yml's required
 * UI job. Rules started as warnings while the initial backlog was cleared; that finished, so
 * the warning tier is gone rather than left as a place for new findings to accumulate quietly.
 *
 * The rules that were switched off are switched off with a reason, below. A rule left permanently
 * at "warn" that nobody intends to satisfy is indistinguishable from noise, and it is what makes
 * --max-warnings 0 unreachable.
 */
export default defineConfigWithVueTs(
  {
    name: 'app/files-to-lint',
    files: ['**/*.{ts,mts,tsx,vue}'],
  },

  {
    name: 'app/files-to-ignore',
    ignores: [
      '**/dist/**',
      '**/coverage/**',
      '**/playwright-report/**',
      '**/test-results/**',
      '**/node_modules/**',
    ],
  },

  js.configs.recommended,
  ...pluginVue.configs['flat/recommended'],
  vueTsConfigs.recommended,

  {
    name: 'app/rules',

    // An unused disable directive is a lie about the code that survives long after the problem
    // it silenced is gone. Treat it as an error so suppressions have to stay justified.
    linterOptions: {
      reportUnusedDisableDirectives: 'error',
    },

    rules: {
      // Bugs, not style. These stay errors.
      'vue/no-unused-components': 'error',
      'vue/require-v-for-key': 'error',
      'vue/no-mutating-props': 'error',
      'vue/no-side-effects-in-computed-properties': 'error',

      // The rule this whole setup is for. router-link and router-view are registered globally
      // by vue-router, so they are defined everywhere and must be excluded or the rule reports
      // six false positives and gets switched off by the first person who reads it.
      'vue/no-undef-components': ['error', { ignorePatterns: ['router-link', 'router-view'] }],

      // Importing defineProps/defineEmits produces a compiler warning on every build and test
      // run, which is the kind of persistent noise that trains people to ignore output. The
      // build passes regardless, so only a linter catches it — the same argument as above.
      'vue/no-import-compiler-macros': 'error',

      // tsconfig.app.json already sets noUnusedLocals and noUnusedParameters, and vue-tsc runs
      // in CI. Reporting the same finding from two tools teaches people to ignore both.
      '@typescript-eslint/no-unused-vars': 'off',

      // Pure formatting, switched off on purpose.
      //
      // These four accounted for 474 of the 509 warnings on first run, and not one of them can
      // catch a defect — the codebase already has a consistent hand style. Leaving them on would
      // have meant either a 60-file reformat that destroys git blame and makes every subsequent
      // agent diff noisier, or an advisory job showing ~500 findings forever, which is how a
      // signal becomes wallpaper. Formatting belongs to a formatter, if it is adopted at all.
      'vue/max-attributes-per-line': 'off',
      'vue/singleline-html-element-content-newline': 'off',
      'vue/multiline-html-element-content-newline': 'off',
      'vue/html-indent': 'off',
      'vue/html-self-closing': 'off',
      'vue/html-closing-bracket-spacing': 'off',

      // Two rules that do not fit this codebase, switched off rather than left as permanent
      // warnings. A rule nobody intends to satisfy is indistinguishable from noise.
      //
      // require-default-prop predates <script setup lang="ts">. With type-based props, `label?:
      // string` already says the prop is optional and undefined is its default; adding a runtime
      // default would change behaviour to satisfy a linter. All 10 hits were the ui/ primitives
      // doing the correct modern thing.
      'vue/require-default-prop': 'off',
      //
      // multi-word-component-names exists to avoid colliding with current and future HTML
      // elements. Badge, Dashboard, Login, Menu and Register do not collide, and renaming them
      // would churn imports across the app for no defect caught.
      'vue/multi-word-component-names': 'off',

      // Kept on and now at zero. Left as errors below rather than warnings so a regression
      // fails the build instead of joining a backlog.
      'vue/attributes-order': 'error',
      'vue/require-explicit-emits': 'error',
      // Every component now has a typed script block. This is what keeps it that way.
      'vue/block-lang': ['error', { script: { lang: 'ts' } }],
      '@typescript-eslint/no-explicit-any': 'error',
    },
  },

  {
    name: 'app/tests',
    files: ['**/*.test.ts', 'src/__tests__/**'],
    rules: {
      // Test fixtures are shaped like API responses, and asserting against them sometimes needs
      // a cast the production code would never justify.
      '@typescript-eslint/no-explicit-any': 'off',
    },
  }
);
