<template>
  <BaseCard :title="t('event.upcomingTitle')">
    <LoadingSkeleton v-if="loading" :rows="4" />
    <ErrorState
        v-else-if="error"
        :title="t('event.loadFailed')"
        :description="error ?? undefined"
      />
    <div v-else>
      <table v-if="upcoming.length" class="w-full text-sm">
        <thead>
          <tr class="border-b border-border text-left font-semibold text-text-muted">
            <th class="py-2">{{ t('event.title') }}</th>
            <th class="py-2">{{ t('event.description') }}</th>
            <th class="py-2">{{ t('event.date') }}</th>
            <th class="py-2">{{ t('event.recurring') }}</th>
            <th class="py-2">{{ t('event.notified') }}</th>
            <th class="py-2">{{ t('event.actions') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="ev in upcoming" :key="ev.id" class="border-b border-border hover:bg-surface-2">
            <td class="py-2">{{ ev.title }}</td>
            <td class="py-2 text-text-muted">{{ ev.description }}</td>
            <td class="py-2 font-mono text-text-muted tabular-nums">{{ formatDate(ev.eventDate) }}</td>
            <td class="py-2">{{ ev.isRecurring ? t('event.yes') : t('event.no') }}</td>
            <td class="py-2">
              <Badge :variant="ev.isNotified ? 'primary' : 'neutral'">
                {{ ev.isNotified ? t('event.notifiedBadge') : t('event.pendingBadge') }}
              </Badge>
            </td>
            <td class="py-2">
              <BaseButton
                size="sm"
                variant="primary"
                class="mr-2"
                :disabled="ev.isNotified || updatingIds.has(ev.id)"
                @click="markNotified(ev)"
              >
                {{ ev.isNotified ? '✓' : t('event.notify') }}
              </BaseButton>

              <BaseButton
                size="sm"
                variant="danger"
                :disabled="updatingIds.has(ev.id)"
                @click="deleteEvent(ev.id)"
              >
                {{ t('event.delete') }}
              </BaseButton>
            </td>
          </tr>
        </tbody>
      </table>

      <EmptyState v-else :title="t('event.empty')" />
    </div>
  </BaseCard>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { fetchUpcomingEvents, updateUpcomingEvent, deleteUpcomingEvent } from '../../../services/api';
import type { UpcomingEvent } from '../../../models/models';
import BaseCard from '../../ui/BaseCard.vue';
import BaseButton from '../../ui/BaseButton.vue';
import Badge from '../../ui/Badge.vue';
import EmptyState from '../../ui/EmptyState.vue';
import ErrorState from '../../ui/ErrorState.vue';
import LoadingSkeleton from '../../ui/LoadingSkeleton.vue';
import { useI18n } from 'vue-i18n';
import { intlLocale } from '../../../i18n/locale';

const { t } = useI18n();

const events = ref<UpcomingEvent[]>([]);
const loading = ref(true);
const error = ref<string | null>(null);

// Track ids currently being updated to disable actions while request in flight
const updatingIds = ref(new Set<number>());

onMounted(async () => {
  try {
    const data = await fetchUpcomingEvents();
    events.value = data;
  } catch (err) {
    console.error(err);
    error.value = t('event.loadFailed');
  } finally {
    loading.value = false;
  }
});

// Filter to future events (today or later) and sort ascending
const upcoming = computed(() => {
  const now = new Date();
  return events.value
    .filter(e => {
      const d = new Date(e.eventDate);
      return d >= now;
    })
    .sort((a, b) => new Date(a.eventDate).getTime() - new Date(b.eventDate).getTime());
});

function formatDate(iso: string) {
  const d = new Date(iso);
  return d.toLocaleString(intlLocale(), {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  });
}

async function markNotified(ev: UpcomingEvent) {
  if (ev.isNotified) return;
  updatingIds.value.add(ev.id);
  try {
    const updated: UpcomingEvent = { ...ev, isNotified: true };
    await updateUpcomingEvent(ev.id, updated);
    // Update local copy
    const idx = events.value.findIndex(x => x.id === ev.id);
    if (idx !== -1) events.value[idx] = updated;
  } catch (err) {
    console.error('Failed to mark notified', err);
  } finally {
    updatingIds.value.delete(ev.id);
  }
}

async function deleteEvent(id: number) {
  if (!confirm(t('event.confirmDelete'))) return;
  updatingIds.value.add(id);
  try {
    await deleteUpcomingEvent(id);
    events.value = events.value.filter(e => e.id !== id);
  } catch (err) {
    console.error('Failed to delete event', err);
  } finally {
    updatingIds.value.delete(id);
  }
}
</script>
