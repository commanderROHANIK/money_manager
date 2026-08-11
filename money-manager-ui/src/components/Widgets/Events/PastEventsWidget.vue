<template>
  <BaseCard :title="t('event.pastTitle')">
    <ul v-if="pastEvents?.length > 0">
      <ListRow v-for="event in pastEvents" :key="event.id">
        <template #title>
          <h3 class="text-sm font-bold">{{ event.title }}</h3>
        </template>
        <template #subtitle>
          <p class="text-sm text-text-muted">{{ event.description }}</p>
        </template>
        <template #trailing>
          <span class="font-mono text-xs text-text-muted tabular-nums">{{ formatDate(event.eventDate) }}</span>
        </template>
      </ListRow>
    </ul>

    <EmptyState v-else :title="t('event.noPast')" />
  </BaseCard>
</template>

<script lang="ts" setup>
import { computed } from 'vue'
import BaseCard from '../../ui/BaseCard.vue'
import ListRow from '../../ui/ListRow.vue'
import EmptyState from '../../ui/EmptyState.vue'
import { useI18n } from 'vue-i18n';
import { intlLocale } from '../../../i18n/locale';

const { t } = useI18n();

export interface UpcomingEvent {
  id: number
  title: string
  description: string
  eventDate: string // ISO date string
  isRecurring: boolean
  isNotified: boolean
}

const props = defineProps<{
  events: UpcomingEvent[]
}>()

const pastEvents = computed(() =>
  props.events?.filter(e => new Date(e.eventDate) < new Date())
)

function formatDate(dateString: string): string {
  return new Date(dateString).toLocaleDateString(intlLocale(), {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  })
}
</script>
