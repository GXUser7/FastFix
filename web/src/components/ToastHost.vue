<script>
import { reactive } from 'vue'

const toasts = reactive([])
let seq = 0

// Всплывающее уведомление: toast('текст') или toast('текст', 'error')
export function toast(text, kind = 'info', timeout = 4500) {
  const id = ++seq
  toasts.push({ id, text, kind })
  setTimeout(() => {
    const i = toasts.findIndex(t => t.id === id)
    if (i >= 0) toasts.splice(i, 1)
  }, timeout)
}
</script>

<script setup>
const list = toasts
</script>

<template>
  <div class="toasts">
    <TransitionGroup name="t">
      <div v-for="t in list" :key="t.id" class="toast" :class="t.kind">{{ t.text }}</div>
    </TransitionGroup>
  </div>
</template>

<style scoped>
.toasts { position: fixed; right: 20px; bottom: 20px; display: flex; flex-direction: column; gap: 10px; z-index: 100; max-width: calc(100vw - 40px); }
.toast { background: #1B2A41; color: #fff; padding: 12px 18px; border-radius: 8px; box-shadow: 0 6px 24px rgba(0,0,0,.2); font-size: 14px; max-width: 380px; }
.toast.error { background: #C0392B; }
.toast.success { background: #1E8449; }
.t-enter-active, .t-leave-active { transition: all .25s; }
.t-enter-from, .t-leave-to { opacity: 0; transform: translateY(10px); }
@media (max-width: 480px) { .toasts { left: 16px; right: 16px; bottom: 16px; } .toast { max-width: none; } }
</style>
