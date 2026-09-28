// Запросы к REST API: JWT в заголовке, единая обработка ошибок { message }
import { session, logout } from '../stores/session.js'
import router from '../router.js'

export class ApiError extends Error {
  constructor(status, message) {
    super(message)
    this.status = status
  }
}

async function request(method, url, body) {
  const headers = { Accept: 'application/json' }
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  if (session.token) headers.Authorization = `Bearer ${session.token}`

  let res
  try {
    res = await fetch('/api' + url, { method, headers, body: body !== undefined ? JSON.stringify(body) : undefined })
  } catch {
    throw new ApiError(0, 'Сервер недоступен. Проверьте подключение к интернету')
  }

  if (res.status === 401 && session.token) {
    logout()
    router.push({ name: 'login', query: { expired: '1' } })
  }
  if (!res.ok) {
    let message = `Ошибка ${res.status}`
    try { message = (await res.json()).message || message } catch { /* тело не JSON */ }
    throw new ApiError(res.status, message)
  }
  if (res.status === 204 || res.headers.get('content-length') === '0') return null
  const type = res.headers.get('content-type') || ''
  return type.includes('application/json') ? res.json() : null
}

export const api = {
  get: url => request('GET', url),
  post: (url, body = {}) => request('POST', url, body),
  put: (url, body = {}) => request('PUT', url, body),
  del: url => request('DELETE', url),
}

// Скачивание PDF-документа с авторизацией
export async function downloadDocument(orderId, type) {
  const res = await fetch(`/api/orders/${orderId}/documents/${type}`, {
    headers: { Authorization: `Bearer ${session.token}` },
  })
  if (!res.ok) {
    let message = 'Документ недоступен'
    try { message = (await res.json()).message || message } catch { /* */ }
    throw new ApiError(res.status, message)
  }
  const blob = await res.blob()
  const disposition = res.headers.get('content-disposition') || ''
  const match = /filename\*=UTF-8''([^;]+)/i.exec(disposition) || /filename="?([^";]+)"?/i.exec(disposition)
  const name = match ? decodeURIComponent(match[1]) : `${type}-${orderId}.pdf`
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = name
  document.body.appendChild(a)
  a.click()
  a.remove()
  setTimeout(() => URL.revokeObjectURL(url), 10000)
}
