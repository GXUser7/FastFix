<script setup>
import { computed, watch } from 'vue'
import { useRoute } from 'vue-router'
import AppHeader from './components/AppHeader.vue'
import ToastHost from './components/ToastHost.vue'
import { session } from './stores/session.js'
import { startRealtime, stopRealtime } from './api/realtime.js'

const route = useRoute()
const guest = computed(() => route.meta.guest)

// подключение к уведомлениям, пока пользователь вошёл
watch(() => session.token, t => (t ? startRealtime() : stopRealtime()), { immediate: true })
</script>

<template>
  <div :class="guest ? 'guest-layout' : 'app-layout'">
    <AppHeader v-if="!guest" />
    <main :class="{ page: !guest }">
      <RouterView />
    </main>
    <ToastHost />
  </div>
</template>
