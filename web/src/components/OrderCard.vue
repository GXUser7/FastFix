<script setup>
import StatusBadge from './StatusBadge.vue'
import { date, money } from '../format.js'

defineProps({ order: Object })
</script>

<template>
  <RouterLink :to="{ name: 'order', params: { id: order.id } }" class="order card" :class="{ approval: order.status === 'approval' }">
    <div class="top">
      <div>
        <div class="title">№ {{ order.id }} · {{ order.deviceType }} {{ order.device }}</div>
        <div class="muted small">Принята {{ date(order.createdAt) }} · срок {{ date(order.dueDate) }}</div>
      </div>
      <div class="sum" v-if="order.totalCost > 0 || order.isWarranty">
        {{ order.isWarranty ? 'Гарантия' : money(order.totalCost) }}
      </div>
    </div>
    <div class="bottom">
      <StatusBadge :name="order.statusName" :color="order.statusColor" :overdue="order.isOverdue" />
      <span v-if="order.status === 'approval'" class="action">Требуется ваше согласование →</span>
      <span v-else-if="order.status === 'ready'" class="action ready">Можно забирать</span>
    </div>
  </RouterLink>
</template>

<style scoped>
.order { display: block; color: inherit; margin-bottom: 12px; transition: transform .12s, box-shadow .12s; padding: 16px 20px; }
.order:hover { color: inherit; transform: translateY(-1px); box-shadow: 0 4px 18px rgba(27, 42, 65, .12); }
.approval { border-left: 4px solid #F39C12; }
.top { display: flex; justify-content: space-between; gap: 12px; }
.title { font-weight: 600; font-size: 15px; margin-bottom: 4px; }
.sum { font-weight: 700; white-space: nowrap; }
.bottom { margin-top: 10px; display: flex; align-items: center; gap: 12px; flex-wrap: wrap; }
.action { color: #D68910; font-size: 13px; font-weight: 600; }
.action.ready { color: #27AE60; }
</style>
