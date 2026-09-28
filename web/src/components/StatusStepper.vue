<script setup>
import { computed } from 'vue'
import { STEPS, stepIndex } from '../format.js'

const props = defineProps({ status: String, color: String })
const current = computed(() => stepIndex(props.status))
const cancelled = computed(() => props.status === 'cancelled')
</script>

<template>
  <div class="stepper" :class="{ cancelled }">
    <div v-for="(s, i) in STEPS" :key="s.code" class="step" :class="{ done: !cancelled && i < current, current: !cancelled && i === current }">
      <div class="dot" :style="!cancelled && i === current ? { background: color, boxShadow: `0 0 0 4px ${color}33` } : {}"></div>
      <div class="bar" v-if="i < STEPS.length - 1"></div>
      <div class="label">{{ s.title }}</div>
    </div>
  </div>
  <p v-if="status === 'waiting_parts'" class="note muted small">Ремонт приостановлен до поступления запчастей</p>
  <p v-if="cancelled" class="note small cancel">Заявка отменена</p>
</template>

<style scoped>
.stepper { display: flex; margin: 18px 0 6px; }
.step { flex: 1; position: relative; display: flex; flex-direction: column; align-items: flex-start; }
.dot { width: 16px; height: 16px; border-radius: 50%; background: #D5DCE4; position: relative; z-index: 1; transition: .2s; }
.bar { position: absolute; top: 6px; left: 16px; right: 0; height: 4px; background: #D5DCE4; }
.done .dot, .done .bar { background: #2D6CDF; }
.label { margin-top: 8px; font-size: 12px; color: var(--muted); padding-right: 4px; }
.current .label { color: var(--text); font-weight: 600; }
.cancelled .dot, .cancelled .bar { background: #E9EDF2; }
.note { margin: 6px 0 0; }
.cancel { color: #E74C3C; font-weight: 600; }
@media (max-width: 480px) {
  .label { font-size: 10.5px; }
  .dot { width: 14px; height: 14px; }
  .bar { top: 5px; left: 14px; }
}
</style>
