<script setup>
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { api, downloadDocument } from '../api/http.js'
import { session, logout, setUser } from '../stores/session.js'
import { date, money, phone } from '../format.js'
import { toast } from '../components/ToastHost.vue'

const router = useRouter()
const history = ref([])
const loading = ref(true)

const initials = computed(() => ((session.user?.lastName?.[0] || '') + (session.user?.firstName?.[0] || '')).toUpperCase())

onMounted(async () => {
  try {
    const [me, orders] = await Promise.all([api.get('/profile'), api.get('/orders?active=false')])
    setUser(me)
    history.value = orders
  } catch (e) {
    toast(e.message, 'error')
  } finally {
    loading.value = false
  }
})

async function act(o) {
  try {
    await downloadDocument(o.id, 'act')
  } catch (e) {
    toast(e.message, 'error')
  }
}

function exit() {
  logout()
  router.replace('/login')
}
</script>

<template>
  <section class="card profile">
    <div class="avatar">{{ initials }}</div>
    <div>
      <h1>{{ session.user?.lastName }} {{ session.user?.firstName }}</h1>
      <p class="muted">{{ phone(session.user?.phone) }}<template v-if="session.user?.email"> · {{ session.user.email }}</template></p>
    </div>
  </section>
  <div class="actions">
    <RouterLink to="/profile/edit" class="btn outline block">Редактировать профиль</RouterLink>
    <button class="btn ghost block" @click="exit">Выйти из аккаунта</button>
  </div>

  <h2 class="section">История заявок</h2>
  <div v-if="loading" class="spinner"></div>
  <div v-else-if="!history.length" class="card empty">Завершённых заявок пока нет</div>
  <div v-for="o in history" :key="o.id" class="card item">
    <RouterLink :to="{ name: 'order', params: { id: o.id } }" class="info">
      <div>№ {{ o.id }} · {{ o.device }}</div>
      <div class="muted small">{{ o.statusName }} · {{ date(o.createdAt) }}</div>
    </RouterLink>
    <div class="right">
      <b>{{ money(o.totalCost) }}<template v-if="o.isWarranty"> (гарантия)</template></b>
      <button v-if="o.status === 'issued'" class="link small" @click="act(o)">Акт выполненных работ</button>
    </div>
  </div>
</template>

<style scoped>
.profile { display: flex; align-items: center; gap: 20px; }
.profile p { margin: 4px 0 0; }
.avatar { width: 78px; height: 78px; border-radius: 50%; background: #2D6CDF; color: #fff; font-size: 28px; font-weight: 700; display: flex; align-items: center; justify-content: center; flex: none; }
.actions { display: flex; flex-direction: column; gap: 10px; margin: 18px 0 28px; }
.section { margin-bottom: 12px; padding-top: 22px; border-top: 1px solid #DDE3EA; }
.item { display: flex; justify-content: space-between; gap: 12px; align-items: center; margin-bottom: 10px; padding: 14px 18px; }
.info { color: inherit; }
.right { text-align: right; display: flex; flex-direction: column; gap: 4px; align-items: flex-end; }
.link { background: none; border: 0; color: #2D6CDF; cursor: pointer; padding: 0; font: inherit; font-size: 13px; }
@media (max-width: 480px) {
  .avatar { width: 60px; height: 60px; font-size: 22px; }
  .item { flex-direction: column; align-items: flex-start; }
  .right { align-items: flex-start; text-align: left; }
}
</style>
