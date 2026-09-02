<template>
  <BaseCard v-if="visible" :title="t('onboarding.title')">
    <template #actions>
      <button class="text-sm text-text-muted hover:text-text" @click="dismiss">
        {{ t('onboarding.dismiss') }}
      </button>
    </template>

    <p class="mb-3 text-sm text-text-muted">{{ t('onboarding.intro') }}</p>

    <ul>
      <ListRow v-for="step in steps" :key="step.id">
        <template #title>
          <span :class="step.done ? 'text-text-muted line-through' : 'font-medium'">
            {{ t(`onboarding.steps.${step.id}.title`) }}
          </span>
        </template>
        <template #subtitle>
          <div class="text-sm text-text-muted">
            {{ t(`onboarding.steps.${step.id}.hint`) }}
            <span v-if="step.optional">· {{ t('onboarding.optional') }}</span>
          </div>
        </template>
        <template #trailing>
          <Badge :variant="step.done ? 'primary' : 'neutral'">
            {{ step.done ? t('onboarding.done') : t('onboarding.todo') }}
          </Badge>
          <router-link
            v-if="!step.done"
            :to="step.to"
            class="text-sm text-primary-strong hover:underline whitespace-nowrap"
          >
            {{ t('onboarding.go') }}
          </router-link>
        </template>
      </ListRow>
    </ul>
  </BaseCard>
</template>

<script setup lang="ts">
/**
 * A checklist on the dashboard, not a modal wizard.
 *
 * <p>Non-blocking and resumable: it survives a refresh because nothing about it is session state,
 * and it lets someone look around first rather than trapping them in a flow before they have seen
 * what they are being asked to fill in.</p>
 *
 * <p>It renders nothing at all once the required steps are done — see `isChecklistNeeded`. An
 * established landlord has never seen this component and never will, which is why it is safe to
 * put on the dashboard unconditionally.</p>
 */
import { useI18n } from 'vue-i18n';
import BaseCard from '../../ui/BaseCard.vue';
import Badge from '../../ui/Badge.vue';
import ListRow from '../../ui/ListRow.vue';
import { useOnboarding } from '../../../composables/useOnboarding';

const { t } = useI18n();
const { steps, visible, dismiss } = useOnboarding();
</script>
