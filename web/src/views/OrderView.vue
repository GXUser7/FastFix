<script setup>
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { api, downloadDocument } from '../api/http.js'
import { onOrderUpdated } from '../api/realtime.js'
import { date, dateTime, money } from '../format.js'
import StatusBadge from '../components/StatusBadge.vue'
import StatusStepper from '../components/StatusStepper.vue'
import { toast } from '../components/ToastHost.vue'

const props = defineProps({ id: Number })
const order = ref(null)
const error = ref('')
const busy = ref(false)
const confirmReject = ref(false)
const comment = ref('')

const parts = computed(() => order.value?.parts.filter(p => p.status !== 'cancelled') || [])
const hasEstimate = computed(() => order.value && (order.value.works.length || parts.value.length))

async function load() {
  try {
    order.value = await api.get(`/orders/${props.id}`)
    error.value = ''
  } catch (e) {
    error.value = e.message
  }
}

async function decide(approve) {
  busy.value = true
  try {
    await api.post(`/orders/${props.id}/decision`, { approve, comment: comment.value || null })
    toast(approve ? 'Стоимость согласована — мастер приступает к ремонту' : 'Вы отказались от ремонта', approve ? 'success' : 'info')
    confirmReject.value = false
    await load()
  } catch (e) {
    toast(e.message, 'error')
  } finally {
    busy.value = false
  }
}

async function download(doc) {
  try {
    await downloadDocument(props.id, doc.type)
  } catch (e) {
    toast(e.message, 'error')
  }
}

let off
onMounted(() => {
  load()
  off = onOrderUpdated(e => {
    if (e.orderId === props.id) {
      toast(e.message || `Статус: ${e.statusName}`)
      load()
    }
  })
})
onUnmounted(() => off?.())
watch(() => props.id, load)
</script>

<template>
  <RouterLink to="/" class="back">← Все заявки</RouterLink>
  <div v-if="error" class="error-box">{{ error }}</div>
  <div v-if="!order && !error" class="spinner"></div>

  <template v-if="order">
    <section class="card">
      <div class="head">
        <div>
          <h1>{{ order.device.title }}</h1>
          <p class="muted small">
            Заявка № {{ order.id }} · принята {{ date(order.createdAt) }}
            <template v-if="order.masterName"> · мастер: {{ order.masterName }}</template>
          </p>
        </div>
        <div><StatusBadge :name="order.statusName" :color="order.statusColor" :overdue="order.isOverdue" /></div>
      </div>
      <StatusStepper :status="order.status" :color="order.statusColor" />
      <div class="facts">
        <div><span class="muted small">Плановый срок</span><b>{{ date(order.dueDate) }}</b></div>
        <div v-if="order.readyAt"><span class="muted small">Готова</span><b>{{ date(order.readyAt) }}</b></div>
        <div v-if="order.issuedAt"><span class="muted small">Выдана</span><b>{{ date(order.issuedAt) }}</b></div>
        <div v-if="order.isWarranty"><span class="muted small">Вид ремонта</span><b>Гарантийный</b></div>
        <div v-if="order.device.serialNumber"><span class="muted small">Серийный номер</span><b>{{ order.device.serialNumber }}</b></div>
      </div>
    </section>

    <!-- согласование стоимости -->
    <section v-if="order.canDecide" class="card decision">
      <h2>Требуется ваше решение</h2>
      <p class="muted">Мастер провёл диагностику и подготовил смету. Ремонт начнётся после вашего согласования.</p>
      <div class="total"><span>Итоговая стоимость</span><b>{{ money(order.totalCost) }}</b></div>
      <textarea v-model="comment" class="input" rows="2" placeholder="Комментарий для мастера (необязательно)"></textarea>
      <div class="buttons" v-if="!confirmReject">
        <button class="btn success" :disabled="busy" @click="decide(true)">Согласовать</button>
        <button class="btn danger" :disabled="busy" @click="confirmReject = true">Отказаться</button>
      </div>
      <div v-else class="confirm">
        <p>Отказаться от ремонта? Заявка будет отменена, устройство можно будет забрать в сервисном центре.</p>
        <div class="buttons">
          <button class="btn danger" :disabled="busy" @click="decide(false)">Да, отказаться</button>
          <button class="btn ghost" :disabled="busy" @click="confirmReject = false">Назад</button>
        </div>
      </div>
    </section>

    <section class="card">
      <h2>Неисправность</h2>
      <p>{{ order.declaredFault }}</p>
      <template v-if="order.completeness.length || order.appearanceNote">
        <p class="muted small">Комплектность и внешний вид при приёме: {{ [...order.completeness, order.appearanceNote].filter(Boolean).join(', ') }}</p>
      </template>
      <template v-if="order.diagnosis">
        <h2 class="mt">Заключение мастера</h2>
        <div class="diagnosis">{{ order.diagnosis }}</div>
        <p v-if="order.warrantyMonths" class="muted small">Гарантия на работы: {{ order.warrantyMonths }} мес.</p>
      </template>
    </section>

    <section v-if="hasEstimate" class="card">
      <h2>Смета</h2>
      <table class="table">
        <thead><tr><th>Наименование</th><th class="r">Кол.</th><th class="r">Сумма</th></tr></thead>
        <tbody>
          <tr v-for="w in order.works" :key="'w' + w.id"><td>{{ w.description }}</td><td class="r">1</td><td class="r">{{ money(w.price) }}</td></tr>
          <tr v-for="p in parts" :key="'p' + p.id">
            <td>{{ p.partName }} <span v-if="p.status === 'requested'" class="muted small">(ожидает поступления)</span></td>
            <td class="r">{{ p.quantity }}</td><td class="r">{{ money(p.sum) }}</td>
          </tr>
        </tbody>
      </table>
      <div class="total"><span>{{ order.isWarranty ? 'Гарантийный ремонт' : 'Итого' }}</span><b>{{ money(order.totalCost) }}</b></div>
      <div v-if="order.paid > 0" class="paid muted small">Оплачено {{ money(order.paid) }}<template v-if="order.due > 0">, к оплате {{ money(order.due) }}</template></div>
      <p v-if="order.clientDecision" class="muted small">
        {{ order.clientDecision === 'approved' ? 'Стоимость согласована' : 'Вы отказались от ремонта' }} {{ dateTime(order.decisionAt) }}
      </p>
    </section>

    <section class="card">
      <h2>Документы</h2>
      <div class="docs">
        <button v-for="d in order.documents" :key="d.type" class="doc" :disabled="!d.available" @click="download(d)">
          <span class="ico">PDF</span>
          <span><b>{{ d.name }}</b><br /><span class="muted small">{{ d.available ? d.number : 'будет доступен позже' }}</span></span>
        </button>
      </div>
    </section>

    <section class="card">
      <h2>История заявки</h2>
      <ul class="history">
        <li v-for="(h, i) in order.history" :key="i">
          <span class="dot" :style="{ background: h.statusColor }"></span>
          <div>
            <b>{{ h.statusName }}</b> <span class="muted small">· {{ dateTime(h.changedAt) }}</span>
            <div v-if="h.comment" class="small">{{ h.comment }}</div>
          </div>
        </li>
      </ul>
    </section>
  </template>
</template>

<style scoped>
.back { display: inline-block; margin-bottom: 14px; font-weight: 500; }
.card { margin-bottom: 16px; }
.head { display: flex; justify-content: space-between; gap: 12px; align-items: flex-start; }
.head p { margin: 6px 0 0; }
.facts { display: flex; flex-wrap: wrap; gap: 12px 32px; margin-top: 16px; padding-top: 14px; border-top: 1px solid var(--line); }
.facts div { display: flex; flex-direction: column; gap: 2px; }
h2 { font-size: 17px; margin-bottom: 10px; }
.mt { margin-top: 18px; }
.diagnosis { background: #F4F6F8; border: 1px solid var(--line); border-radius: 8px; padding: 12px 14px; white-space: pre-line; }
.decision { border: 2px solid #F39C12; }
.decision p { margin-top: 0; }
.total { display: flex; justify-content: space-between; align-items: baseline; margin: 14px 0; }
.total span { color: var(--muted); }
.total b { font-size: 22px; }
.buttons { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; margin-top: 14px; }
.confirm p { margin: 14px 0 0; }
.table { width: 100%; border-collapse: collapse; font-size: 13px; }
.table th { text-align: left; color: var(--muted); font-weight: 600; padding: 8px 6px; border-bottom: 1px solid var(--line); }
.table td { padding: 9px 6px; border-bottom: 1px solid var(--line); }
.r { text-align: right; white-space: nowrap; }
.paid { text-align: right; margin-top: -8px; }
.docs { display: grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap: 12px; }
.doc { display: flex; gap: 12px; align-items: center; text-align: left; border: 1px solid var(--line); background: #fff; border-radius: 8px; padding: 12px; cursor: pointer; font: inherit; color: inherit; }
.doc:hover:not(:disabled) { border-color: #2D6CDF; background: #F6F9FF; }
.doc:disabled { opacity: .5; cursor: default; }
.ico { background: #E74C3C; color: #fff; font-size: 11px; font-weight: 700; border-radius: 4px; padding: 6px 5px; }
.history { list-style: none; margin: 0; padding: 0; }
.history li { display: flex; gap: 12px; padding: 8px 0; }
.history .dot { width: 12px; height: 12px; border-radius: 50%; margin-top: 4px; flex: none; }
@media (max-width: 480px) {
  .head { flex-direction: column; }
  .buttons { grid-template-columns: 1fr; }
  .total b { font-size: 20px; }
}
</style>
