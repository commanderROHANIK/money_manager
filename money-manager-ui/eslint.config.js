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
 * Severity is split deliberately. Rules that encode real bugs are errors from day one. Stylistic
 * rules start as warnings so the advisory Quality job reports a shrinking number instead of a
 * wall of red — see the promotion note in .github/workflows/quality.yml.
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

      // Real signal, but each needs a deliberate change rather than a reformat. Warnings for
      // now; promote to error one at a time as each reaches zero.
      'vue/attributes-order': 'warn',
      'vue/multi-word-component-names': 'warn',
      'vue/require-default-prop': 'warn',
      'vue/require-explicit-emits': 'warn',
      // Three components still have plain-JS script blocks, so they are not type-checked.
      'vue/block-lang': 'warn',
      '@typescript-eslint/no-explicit-any': 'warn',
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
