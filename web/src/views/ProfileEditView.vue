<script setup>
import { reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { api } from '../api/http.js'
import { session, setUser } from '../stores/session.js'
import { phone } from '../format.js'
import { toast } from '../components/ToastHost.vue'

const router = useRouter()
const u = session.user || {}
const f = reactive({ lastName: u.lastName || '', firstName: u.firstName || '', email: u.email || '', phone: phone(u.phone) })
const p = reactive({ currentPassword: '', newPassword: '' })
const error = ref('')
const pwdError = ref('')
const busy = ref(false)

async function save() {
  error.value = ''
  if (!f.lastName.trim() || !f.firstName.trim()) return (error.value = 'Укажите фамилию и имя')
  busy.value = true
  try {
    const me = await api.put('/profile', { lastName: f.lastName, firstName: f.firstName, email: f.email.trim() || null, phone: f.phone })
    setUser(me)
    toast('Изменения сохранены', 'success')
    router.push('/profile')
  } catch (e) {
    error.value = e.message
  } finally {
    busy.value = false
  }
}

async function changePassword() {
  pwdError.value = ''
  if (!p.currentPassword || !p.newPassword) return (pwdError.value = 'Заполните оба поля')
  busy.value = true
  try {
    await api.put('/profile/password', { ...p })
    p.currentPassword = p.newPassword = ''
    toast('Пароль изменён', 'success')
  } catch (e) {
    pwdError.value = e.message
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <RouterLink to="/profile" class="back">← Профиль</RouterLink>
  <form class="card" @submit.prevent="save" novalidate>
    <h2>Личные данные</h2>
    <div v-if="error" class="error-box">{{ error }}</div>
    <div class="row">
      <div class="field"><label>Фамилия</label><input class="input" v-model="f.lastName" autocomplete="family-name" /></div>
      <div class="field"><label>Имя</label><input class="input" v-model="f.firstName" autocomplete="given-name" /></div>
    </div>
    <div class="field"><label>Email</label><input class="input" v-model="f.email" type="email" autocomplete="email" /></div>
    <div class="field"><label>Телефон</label><input class="input" v-model="f.phone" type="tel" autocomplete="tel" /></div>
    <button class="btn block" :disabled="busy">Сохранить изменения</button>
  </form>

  <form class="card" @submit.prevent="changePassword" novalidate>
    <h2>Смена пароля</h2>
    <div v-if="pwdError" class="error-box">{{ pwdError }}</div>
    <div class="row">
      <div class="field"><label>Текущий пароль</label><input class="input" v-model="p.currentPassword" type="password" autocomplete="current-password" /></div>
      <div class="field"><label>Новый пароль</label><input class="input" v-model="p.newPassword" type="password" autocomplete="new-password" /></div>
    </div>
    <p class="muted small">Не менее 8 символов, обязательно буквы и цифры.</p>
    <button class="btn outline block" :disabled="busy">Изменить пароль</button>
  </form>
</template>

<style scoped>
.back { display: inline-block; margin-bottom: 14px; font-weight: 500; }
.card { margin-bottom: 18px; }
h2 { margin-bottom: 16px; }
.btn { margin-top: 6px; }
</style>
