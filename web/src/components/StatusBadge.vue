<script setup>
defineProps({ name: String, color: String, overdue: Boolean })
</script>

<template>
  <span class="badge" :style="{ background: color }">{{ name }}</span>
  <span v-if="overdue" class="badge overdue">Просрочена</span>
</template>

<style scoped>
.badge {
  display: inline-block; padding: 4px 12px; border-radius: 999px; color: #fff;
  font-size: 12px; font-weight: 600; white-space: nowrap; line-height: 1.4;
}
.overdue { background: #C0392B; margin-left: 6px; }
</style>
