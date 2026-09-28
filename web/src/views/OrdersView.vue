<script setup>
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { api } from '../api/http.js'
import { onOrderUpdated } from '../api/realtime.js'
import { session } from '../stores/session.js'
import { plural } from '../format.js'
import OrderCard from '../components/OrderCard.vue'
import { toast } from '../components/ToastHost.vue'

const orders = ref([])
const loading = ref(true)
const error = ref('')

const active = computed(() => orders.value.filter(o => o.status !== 'issued' && o.status !== 'cancelled'))
const closed = computed(() => orders.value.filter(o => o.status === 'issued' || o.status === 'cancelled'))
const needApproval = computed(() => active.value.filter(o => o.status === 'approval').length)

async function load() {
  try {
    orders.value = await api.get('/orders')
    error.value = ''
  } catch (e) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}

let off
onMounted(() => {
  load()
  // изменение статуса приходит по WebSocket — список обновляется без перезагрузки страницы
  off = onOrderUpdated(e => {
    toast(`Заявка № ${e.orderId}: ${e.statusName}`)
    load()
  })
})
onUnmounted(() => off?.())
</script>

<template>
  <section class="hero">
    <h1>Здравствуйте, {{ session.user?.firstName }}!</h1>
    <p v-if="loading">Загружаем ваши заявки…</p>
    <p v-else-if="active.length">
      У вас {{ active.length }} {{ plural(active.length, 'заявка', 'заявки', 'заявок') }} в работе{{ needApproval ? `, ${needApproval} ожидает согласования стоимости` : '' }}
    </p>
    <p v-else>Активных заявок нет</p>
  </section>

  <div v-if="error" class="error-box">{{ error }}</div>
  <div v-if="loading" class="spinner"></div>
  <template v-else>
    <h2 class="section">Мои заявки</h2>
    <OrderCard v-for="o in active" :key="o.id" :order="o" />
    <div v-if="!active.length" class="card empty">
      Сейчас у вас нет устройств в ремонте.<br />
      <span class="small">Заявка появится здесь, как только приёмщик оформит её на ваш номер телефона.</span>
    </div>

    <template v-if="closed.length">
      <h2 class="section">Завершённые</h2>
      <OrderCard v-for="o in closed" :key="o.id" :order="o" />
    </template>
  </template>
</template>

<style scoped>
.hero { background: #2D6CDF; color: #fff; border-radius: 8px; padding: 26px 28px; margin-bottom: 24px; box-shadow: var(--shadow); }
.hero h1 { font-size: 22px; margin-bottom: 6px; }
.hero p { margin: 0; color: #DCE6FA; }
.section { margin: 22px 0 12px; }
@media (max-width: 480px) { .hero { padding: 20px; } }
</style>
