<script setup>
import { ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { api } from '../api/http.js'
import { setSession } from '../stores/session.js'

const route = useRoute()
const router = useRouter()
const login = ref('')
const password = ref('')
const error = ref(route.query.expired ? 'Сеанс истёк — войдите снова' : '')
const busy = ref(false)

async function submit() {
  error.value = ''
  if (!login.value.trim() || !password.value) {
    error.value = 'Введите телефон или email и пароль'
    return
  }
  busy.value = true
  try {
    const auth = await api.post('/auth/login', { login: login.value.trim(), password: password.value })
    if (auth.user.role !== 'client') {
      error.value = 'Сотрудники работают в настольном приложении «СервисДеск»'
      return
    }
    setSession(auth)
    router.replace(route.query.next || '/')
  } catch (e) {
    error.value = e.message
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <div class="auth">
    <form class="panel" @submit.prevent="submit" novalidate>
      <div class="logo"><span class="w">Сервис</span><span class="b">Деск</span></div>
      <h1>Вход в личный кабинет</h1>
      <p class="sub">Отслеживайте статус заявки и историю ремонта</p>

      <div v-if="error" class="error-box" role="alert">{{ error }}</div>
      <input class="dark-input" v-model="login" placeholder="Телефон или email" autocomplete="username" autofocus />
      <input class="dark-input" v-model="password" type="password" placeholder="Пароль" autocomplete="current-password" />
      <button class="btn block" :disabled="busy">{{ busy ? 'Входим…' : 'Войти' }}</button>
      <p class="links">Нет учётной записи? <RouterLink to="/register">Зарегистрироваться</RouterLink></p>
      <p class="hint">Если вы уже сдавали устройство в ремонт, зарегистрируйтесь по тому же номеру телефона — заявки появятся в кабинете автоматически.</p>
    </form>
  </div>
</template>

<style scoped>
.auth { min-height: 100vh; display: flex; align-items: center; justify-content: center; padding: 24px 16px; background: #F4F6F8; }
.panel { width: 100%; max-width: 480px; background: #1B2A41; border-radius: 8px; padding: 44px 40px; box-shadow: 0 10px 40px rgba(27, 42, 65, .25); color: #fff; }
.logo { font-size: 26px; font-weight: 700; margin-bottom: 44px; }
.logo .b { color: #2D6CDF; }
h1 { font-size: 24px; margin-bottom: 8px; }
.sub { color: #9FB0C8; margin: 0 0 28px; }
.dark-input {
  display: block; width: 100%; height: 50px; margin-bottom: 16px; padding: 0 16px; border: 1px solid transparent;
  border-radius: 6px; background: #24344F; color: #fff; font: inherit; font-size: 15px; outline: none;
}
.dark-input::placeholder { color: #8394AD; }
.dark-input:focus { border-color: #2D6CDF; }
.btn { height: 50px; font-size: 15px; margin-top: 12px; }
.links { text-align: center; color: #9FB0C8; margin: 18px 0 0; }
.links a { color: #fff; font-weight: 600; }
.hint { color: #7F90A8; font-size: 12px; text-align: center; margin: 24px 0 0; line-height: 1.5; }
@media (max-width: 480px) { .panel { padding: 32px 22px; } .logo { margin-bottom: 30px; } }
</style>
