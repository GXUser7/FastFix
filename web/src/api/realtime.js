// Подключение к хабу /hubs/orders и подписка на событие OrderUpdated
import * as signalR from '@microsoft/signalr'
import { session } from '../stores/session.js'

let connection = null
const listeners = new Set()

export function onOrderUpdated(fn) {
  listeners.add(fn)
  return () => listeners.delete(fn)
}

export async function startRealtime() {
  if (connection || !session.token) return
  connection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/orders', { accessTokenFactory: () => session.token })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .configureLogging(signalR.LogLevel.Warning)
    .build()
  connection.on('OrderUpdated', e => listeners.forEach(fn => fn(e)))
  try {
    await connection.start()
  } catch {
    // сервер недоступен — повторная попытка через 10 с
    connection = null
    setTimeout(startRealtime, 10000)
  }
}

export async function stopRealtime() {
  if (!connection) return
  const c = connection
  connection = null
  try { await c.stop() } catch { /* */ }
}
