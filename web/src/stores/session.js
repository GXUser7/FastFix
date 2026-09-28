// Токен и данные пользователя (localStorage)
import { reactive } from 'vue'

const KEY = 'servicedesk.session'

function load() {
  try {
    const s = JSON.parse(localStorage.getItem(KEY) || 'null')
    if (s && new Date(s.expiresAt) > new Date()) return s
  } catch { /* повреждённые данные */ }
  return null
}

const saved = load()

export const session = reactive({
  token: saved?.token || null,
  expiresAt: saved?.expiresAt || null,
  user: saved?.user || null,
})

export function isLoggedIn() {
  return !!session.token && new Date(session.expiresAt) > new Date()
}

export function setSession(auth) {
  session.token = auth.token
  session.expiresAt = auth.expiresAt
  session.user = auth.user
  persist()
}

export function setUser(user) {
  session.user = user
  persist()
}

export function logout() {
  session.token = null
  session.expiresAt = null
  session.user = null
  try { localStorage.removeItem(KEY) } catch { /* */ }
}

function persist() {
  try {
    localStorage.setItem(KEY, JSON.stringify({ token: session.token, expiresAt: session.expiresAt, user: session.user }))
  } catch { /* приватный режим */ }
}
