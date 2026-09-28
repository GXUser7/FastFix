# Функциональные тест-кейсы протокола тестирования, выполняемые через REST API.
# Запуск (сервер на http://localhost:5080, БД заполнена db/02_seed.sql):
#   python tools/e2e_api.py [--out results.json]
# После прогона рекомендуется заново загрузить 02_seed.sql, чтобы вернуть демо-данные.
import json
import sys
import time
import urllib.error
import urllib.request

BASE = "http://localhost:5080"
DEMO = {
    "receptionist": ("+79000000001", "Priem2026"),
    "master": ("+79000000003", "Master2026"),
    "master2": ("+79000000004", "Master2026"),
    "storekeeper": ("sklad@servicedesk.ru", "Sklad2026"),
    "client": ("+79001234567", "Client2026"),
    "client2": ("+79005551122", "Client2026"),
}
results = []


def call(method, path, token=None, body=None, raw=False):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(BASE + path, data=data, method=method)
    req.add_header("Accept", "application/json")
    if data is not None:
        req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    t0 = time.perf_counter()
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            payload = r.read()
            ms = (time.perf_counter() - t0) * 1000
            ctype = r.headers.get("Content-Type", "")
            if raw:
                return r.status, payload, ctype, ms
            return r.status, (json.loads(payload) if payload and "json" in ctype else None), ctype, ms
    except urllib.error.HTTPError as e:
        payload = e.read()
        ms = (time.perf_counter() - t0) * 1000
        try:
            j = json.loads(payload)
        except Exception:
            j = None
        return e.code, j, e.headers.get("Content-Type", ""), ms


def case(cid, title, ok, details=""):
    results.append({"id": cid, "title": title, "passed": bool(ok), "details": details})
    print(("PASS " if ok else "FAIL ") + cid + " " + title + (" — " + details if details else ""))


def login(role):
    s, j, _, _ = call("POST", "/api/auth/login", body={"login": DEMO[role][0], "password": DEMO[role][1]})
    assert s == 200, (role, s, j)
    return j["token"]


def msg(j):
    return (j or {}).get("message", "")


# ---------------------------------------------------------------- безопасность
s, j, _, _ = call("POST", "/api/auth/login", body={"login": "8 (900) 000-00-01", "password": "Priem2026"})
case("ТК-01", "Вход сотрудника по телефону в любом формате", s == 200 and j["user"]["role"] == "receptionist", f"HTTP {s}, роль {j and j['user']['role']}")
s, j, _, _ = call("POST", "/api/auth/login", body={"login": "sklad@servicedesk.ru", "password": "Sklad2026"})
case("ТК-02", "Вход по e-mail", s == 200 and j["user"]["role"] == "storekeeper", f"HTTP {s}")
s, j, _, _ = call("POST", "/api/auth/login", body={"login": "+79001234567", "password": "bad"})
case("ТК-03", "Неверный пароль отклоняется с кодом 401", s == 401, f"HTTP {s}: {msg(j)}")

codes = []
for _ in range(5):
    s, j, _, _ = call("POST", "/api/auth/login", body={"login": "+79161234003", "password": "Wrong0000"})
    codes.append(s)
s6, j6, _, _ = call("POST", "/api/auth/login", body={"login": "+79161234003", "password": "Client2026"})
case("ТК-04", "Блокировка на 15 минут после 5 неудачных попыток (423)", codes[:4] == [401] * 4 and codes[4] == 423 and s6 == 423,
     f"коды: {codes}, верный пароль во время блокировки → {s6}: {msg(j6)}")

s, j, _, _ = call("GET", "/api/orders")
case("ТК-05", "Запрос без токена отклоняется (401)", s == 401, f"HTTP {s}")

phone = "+7916" + str(int(time.time()))[-7:]
s, j, _, _ = call("POST", "/api/auth/register", body={"lastName": "Тестов", "firstName": "Тест", "phone": phone, "password": "short"})
case("ТК-06", "Регистрация: пароль не соответствует политике (400)", s == 400, msg(j))
s, j, _, _ = call("POST", "/api/auth/register", body={"lastName": "Тестов", "firstName": "Тест", "phone": phone, "email": f"t{phone[-7:]}@test.ru", "password": "Test2026pass"})
case("ТК-07", "Регистрация клиента и автоматический вход", s == 201 and j["user"]["role"] == "client", f"HTTP {s}")
s, j, _, _ = call("POST", "/api/auth/register", body={"lastName": "Тестов", "firstName": "Тест", "phone": phone, "password": "Test2026pass"})
case("ТК-08", "Повторная регистрация по тому же телефону (409)", s == 409, msg(j))

R, M, M2, K, C, C2 = (login(r) for r in ["receptionist", "master", "master2", "storekeeper", "client", "client2"])

s, j, _, _ = call("GET", "/api/parts", C)
case("ТК-09", "Клиент не имеет доступа к складу (403)", s == 403, f"HTTP {s}")
s, j, _, _ = call("GET", "/api/orders", K)
case("ТК-10", "Кладовщик не видит заявки (403)", s == 403, f"HTTP {s}")
s, j, _, _ = call("GET", "/api/orders/10231", C)
case("ТК-11", "Клиент не видит чужую заявку (404)", s == 404, f"HTTP {s}")
s, j, _, _ = call("GET", "/api/orders/10229", M)
case("ТК-12", "Мастер не видит заявку другого мастера (403)", s == 403, f"HTTP {s}: {msg(j)}")

# ---------------------------------------------------------------- приём заявок
s, j, _, ms = call("GET", "/api/orders?search=1122", R)
case("ТК-13", "Поиск заявки по части телефона клиента", s == 200 and any(o["id"] == 10231 for o in j) and ms < 2000, f"найдено {len(j)}, {ms:.0f} мс")
s, j, _, ms = call("GET", "/api/orders?search=10198", R)
case("ТК-14", "Поиск заявки по номеру", s == 200 and len(j) == 1 and j[0]["id"] == 10198, f"{ms:.0f} мс")
s, j, _, _ = call("GET", "/api/orders?status=in_work", R)
case("ТК-15", "Фильтр списка по статусу", s == 200 and j and all(o["status"] == "in_work" for o in j), f"найдено {len(j)}")
s, j, _, _ = call("GET", "/api/orders?overdue=true", R)
case("ТК-16", "Выделение просроченных заявок", s == 200 and j and all(o["isOverdue"] for o in j), f"просрочено: {[o['id'] for o in j]}")
s, j, _, _ = call("GET", "/api/clients?phone=89001234567", R)
case("ТК-17", "Поиск клиента по телефону", s == 200 and j["lastName"] == "Иванов", j and j.get("fullName", ""))
s, j, _, _ = call("GET", "/api/clients?phone=+79990001122", R)
case("ТК-18", "Клиент не найден (404) — форма быстрой регистрации", s == 404, msg(j))

new_phone = "+7917" + str(int(time.time()))[-7:]
s, j, _, ms = call("POST", "/api/orders", R, {
    "clientPhone": new_phone, "clientLastName": "Новиков", "clientFirstName": "Пётр",
    "deviceTypeId": 1, "brand": "Lenovo", "model": "IdeaPad 3", "serialNumber": "PF3ABC12",
    "declaredFault": "Не включается после обновления BIOS", "completenessIds": [1, 3, 8], "masterId": 3, "isWarranty": False,
})
order = j and j.get("id")
case("ТК-19", "Оформление заявки с быстрой регистрацией клиента", s == 201 and order, f"заявка № {order}, {ms:.0f} мс")
s, j, _, _ = call("GET", f"/api/orders/{order}", R)
case("ТК-20", "Новая заявка в статусе «Принята», история и квитанция", s == 200 and j["status"] == "accepted" and len(j["history"]) == 1
     and j["completeness"] and any(d["type"] == "receipt" and d["available"] for d in j["documents"]), f"комплектность: {', '.join(j['completeness'])}")
s, pdf, ctype, _ = call("GET", f"/api/orders/{order}/documents/receipt", R, raw=True)
case("ТК-21", "Квитанция о приёме формируется в PDF", s == 200 and "pdf" in ctype and pdf[:4] == b"%PDF", f"{len(pdf) // 1024} КБ")
s, j, _, _ = call("POST", "/api/orders", R, {"clientPhone": "+79001234567", "deviceTypeId": 1, "brand": "", "model": "", "declaredFault": ""})
case("ТК-22", "Валидация обязательных полей заявки (400)", s == 400, msg(j))

# ---------------------------------------------------------------- управление ремонтом
s, j, _, _ = call("POST", f"/api/orders/{order}/status", R, {"status": "diagnostics"})
case("ТК-23", "Приёмщик не может начать диагностику (403/409)", s in (403, 409), f"HTTP {s}: {msg(j)}")
s, j, _, _ = call("POST", f"/api/orders/{order}/status", M, {"status": "ready"})
case("ТК-24", "Недопустимый переход «Принята → Готова» отклоняется (409)", s == 409, msg(j))
s, _, _, _ = call("POST", f"/api/orders/{order}/status", M, {"status": "diagnostics"})
case("ТК-25", "Мастер начинает диагностику", s == 204, f"HTTP {s}")
s, j, _, _ = call("POST", f"/api/orders/{order}/status", M, {"status": "approval"})
case("ТК-26", "Смета не отправляется без заключения и работ (409)", s == 409, msg(j))
s, _, _, _ = call("PUT", f"/api/orders/{order}/diagnosis", M, {"diagnosis": "Повреждена прошивка BIOS, неисправен разъём питания", "warrantyMonths": 40})
case("ТК-27", "Гарантийный срок вне диапазона 0–36 отклоняется (400)", s == 400, f"HTTP {s}")
s, _, _, _ = call("PUT", f"/api/orders/{order}/diagnosis", M, {"diagnosis": "Повреждена прошивка BIOS, неисправен разъём питания", "warrantyMonths": 6})
s2, _, _, _ = call("POST", f"/api/orders/{order}/works", M, {"serviceId": 15})
s3, _, _, _ = call("POST", f"/api/orders/{order}/works", M, {"serviceId": 3})
s4, res, _, _ = call("POST", f"/api/orders/{order}/parts", M, {"partId": 9, "quantity": 1})
s, j, _, _ = call("GET", f"/api/orders/{order}", M)
case("ТК-28", "Заключение, работы из прайс-листа и резерв запчасти", (s, s2, s3, s4) == (200, 201, 201, 201) and res["status"] == "reserved",
     f"работы: {len(j['works'])}, запчасть: {res['statusName']}")
case("ТК-29", "Автоматический расчёт стоимости S = Σработ + Σзапчастей", j["totalCost"] == 1000 + 1800 + 350,
     f"{j['worksTotal']} + {j['partsTotal']} = {j['totalCost']}")
s, _, _, _ = call("POST", f"/api/orders/{order}/status", M, {"status": "approval", "comment": "Смета отправлена"})
case("ТК-30", "Передача сметы на согласование клиенту", s == 204, f"HTTP {s}")

# клиент заявки — новый, без пароля: регистрируется по тому же телефону
s, j, _, _ = call("POST", "/api/auth/register", body={"lastName": "Новиков", "firstName": "Пётр", "phone": new_phone, "password": "Novikov2026"})
NC = j["token"] if s == 201 else None
s2, lst, _, _ = call("GET", "/api/orders", NC)
case("ТК-31", "Клиент, заведённый приёмщиком, регистрируется и видит свои заявки", s == 201 and any(o["id"] == order for o in lst), f"заявок в кабинете: {len(lst)}")
s, j, _, _ = call("GET", f"/api/orders/{order}", NC)
case("ТК-32", "Клиенту доступно согласование и смета", j["canDecide"] and any(d["type"] == "estimate" and d["available"] for d in j["documents"]),
     f"статус {j['statusName']}")
s, _, _, _ = call("POST", f"/api/orders/{order}/decision", NC, {"approve": True})
s2, j, _, _ = call("GET", f"/api/orders/{order}", R)
case("ТК-33", "Согласование стоимости клиентом → «В работе», дата решения", s == 204 and j["status"] == "in_work" and j["decisionAt"], f"{j['statusName']}, {j['decisionAt']}")

s, res2, _, _ = call("POST", f"/api/orders/{order}/parts", M, {"partId": 19, "quantity": 1})
s2, j, _, _ = call("GET", f"/api/orders/{order}", M)
case("ТК-34", "Нехватка запчасти → запрос «ожидает поступления», заявка → «Ожидание запчастей»",
     res2["status"] == "requested" and j["status"] == "waiting_parts", f"{res2['statusName']}, {j['statusName']}")
s, j, _, _ = call("POST", f"/api/orders/{order}/status", M, {"status": "in_work"})
case("ТК-35", "Возврат в работу до поступления детали запрещён (409)", s == 409, msg(j))

# ---------------------------------------------------------------- склад
s, reqs, _, _ = call("GET", "/api/parts/requests", K)
case("ТК-36", "Кладовщик видит запросы мастеров", s == 200 and any(r["orderId"] == order for r in reqs), f"активных запросов: {len(reqs)}")
s, p, _, _ = call("POST", "/api/parts/19/receipt", K, {"quantity": 5, "comment": "Накладная № 777 (тест)"})
s2, j, _, _ = call("GET", f"/api/orders/{order}", M)
case("ТК-37", "Оприходование: резерв под ожидающий запрос и автоматический возврат заявки в работу",
     s == 200 and p["onHand"] == 5 and p["reserved"] == 1 and j["status"] == "in_work", f"на складе {p['onHand']}, резерв {p['reserved']}, заявка: {j['statusName']}")
s, j, _, _ = call("POST", f"/api/orders/{order}/status", M, {"status": "ready"})
case("ТК-38", "Нельзя завершить ремонт, пока запчасти не выданы со склада (409)", s == 409, msg(j))
ok = True
for r in call("GET", "/api/parts/requests?status=reserved", K)[1]:
    if r["orderId"] == order:
        ok &= call("POST", f"/api/parts/requests/{r['id']}/issue", K)[0] == 204
s, mv, _, _ = call("GET", "/api/parts/movements?partId=9&type=issue", K)
case("ТК-39", "Выдача деталей мастеру со списанием и записью в журнал движения", ok and any(m["orderId"] == order and m["quantity"] == -1 for m in mv),
     f"операций выдачи по позиции: {len(mv)}")
s, j, _, _ = call("POST", "/api/parts/requests/1/issue", K)
case("ТК-40", "Повторная выдача уже выданной детали запрещена (409)", s == 409, msg(j))
s, inv, _, _ = call("POST", "/api/inventories", K, {"comment": "Тест", "items": [{"partId": 17, "actualQuantity": 18}, {"partId": 18, "actualQuantity": 10}]})
s2, p17, _, _ = call("GET", "/api/parts/17", K)
case("ТК-41", "Инвентаризация: расчёт расхождений и корректировка остатка", s == 201 and inv["discrepancies"] == 1 and p17["onHand"] == 18,
     f"расхождений {inv['discrepancies']}, Δ = {[i['difference'] for i in inv['items'] if i['difference']]}")
s, low, _, _ = call("GET", "/api/parts?belowMinimum=true", K)
case("ТК-42", "Сигнализация о снижении остатка ниже минимального", s == 200 and low and all(x["belowMinimum"] for x in low), f"позиций: {', '.join(x['sku'] for x in low)}")
s, j, _, _ = call("POST", "/api/parts", K, {"sku": "PWR-ASUS-65W", "name": "Дубль", "price": 1})
case("ТК-43", "Уникальность артикула (409)", s == 409, msg(j))

# ---------------------------------------------------------------- расчёты и документы
s, _, _, _ = call("POST", f"/api/orders/{order}/status", M, {"status": "ready"})
s2, j, _, _ = call("GET", f"/api/orders/{order}", R)
total = j["totalCost"]
case("ТК-44", "Завершение ремонта → «Готова к выдаче», акт выполненных работ", s == 204 and j["status"] == "ready"
     and any(d["type"] == "act" and d["available"] for d in j["documents"]), f"итог {total} ₽")
s, j, _, _ = call("POST", f"/api/orders/{order}/issue", R)
case("ТК-45", "Выдача без полной оплаты запрещена (409)", s == 409, msg(j))
s, j, _, _ = call("POST", f"/api/orders/{order}/payments", R, {"amount": total + 100, "method": "cash"})
case("ТК-46", "Оплата сверх остатка отклоняется (409)", s == 409, msg(j))
s, _, _, _ = call("POST", f"/api/orders/{order}/payments", R, {"amount": 1000, "method": "cash"})
s2, _, _, _ = call("POST", f"/api/orders/{order}/payments", R, {"amount": total - 1000, "method": "sbp"})
s3, j, _, _ = call("GET", f"/api/orders/{order}", R)
case("ТК-47", "Частичная оплата наличными и СБП, контроль остатка", (s, s2) == (201, 201) and j["due"] == 0 and len(j["payments"]) == 2, f"оплачено {j['paid']} ₽")
s, _, _, _ = call("POST", f"/api/orders/{order}/issue", R)
s2, j, _, _ = call("GET", f"/api/orders/{order}", R)
case("ТК-48", "Выдача устройства после оплаты, акт выдачи", s == 204 and j["status"] == "issued" and j["issuedAt"]
     and any(d["type"] == "issue" and d["available"] for d in j["documents"]), j["statusName"])
hist = [h["status"] for h in j["history"]]
case("ТК-49", "100 % смен статусов в истории с сотрудником и временем",
     hist == ["accepted", "diagnostics", "approval", "in_work", "waiting_parts", "in_work", "ready", "issued"] and all(h["changedBy"] and h["changedAt"] for h in j["history"]),
     " → ".join(hist))
ok = True
for t in ["receipt", "estimate", "act", "issue"]:
    s, pdf, ctype, _ = call("GET", f"/api/orders/{order}/documents/{t}", R, raw=True)
    ok &= s == 200 and pdf[:4] == b"%PDF"
case("ТК-50", "Все четыре документа формируются в PDF", ok)
s, j, _, _ = call("GET", f"/api/orders/{order}/documents/issue", NC)
case("ТК-51", "Акт выдачи недоступен клиенту по матрице прав (403)", s == 403, msg(j))
s, j, _, _ = call("POST", f"/api/orders/{order}/status", R, {"status": "cancelled"})
case("ТК-52", "Выданная заявка — конечный статус, изменения отклоняются (409)", s == 409, msg(j))

# отказ клиента
s, j, _, _ = call("GET", "/api/orders/10236", C)
s1, _, _, _ = call("POST", "/api/orders/10236/decision", C, {"approve": False, "comment": "Дорого"})
s2, j2, _, _ = call("GET", "/api/orders/10236", R)
s3, p1, _, _ = call("GET", "/api/parts/1", K)
case("ТК-53", "Отказ клиента → «Отменена», резерв запчасти снят", j["canDecide"] and s1 == 204 and j2["status"] == "cancelled" and p1["reserved"] == 0,
     f"{j2['statusName']}, резерв блока питания: {p1['reserved']}")
s, _, _, _ = call("POST", "/api/orders/10236/issue", R)
case("ТК-54", "Возврат устройства по отменённой заявке без оплаты", s == 204, f"HTTP {s}")

# гарантийная заявка
s, j, _, _ = call("POST", "/api/orders", R, {"clientPhone": "+79005551122", "deviceTypeId": 1, "brand": "ASUS", "model": "X515",
                                              "declaredFault": "Повторная неисправность", "masterId": 3, "isWarranty": True})
w = j["id"]
call("POST", f"/api/orders/{w}/status", M, {"status": "diagnostics"})
call("PUT", f"/api/orders/{w}/diagnosis", M, {"diagnosis": "Гарантийный случай"})
call("POST", f"/api/orders/{w}/works", M, {"serviceId": 1})
s1, _, _, _ = call("POST", f"/api/orders/{w}/status", M, {"status": "in_work"})
s2, _, _, _ = call("POST", f"/api/orders/{w}/status", M, {"status": "ready"})
s3, _, _, _ = call("POST", f"/api/orders/{w}/issue", R)
s4, j, _, _ = call("GET", f"/api/orders/{w}", R)
case("ТК-55", "Гарантийная заявка: без согласования, стоимость 0, выдача без оплаты", (s1, s2, s3) == (204, 204, 204) and j["totalCost"] == 0 and j["status"] == "issued",
     f"S = {j['totalCost']}")

# ---------------------------------------------------------------- профиль и производительность
s, j, _, _ = call("PUT", "/api/profile", C2, {"lastName": "Смирнов", "firstName": "Алексей", "email": "smirnov.ap@yandex.ru", "phone": "+7 900 555-11-22"})
case("ТК-56", "Изменение профиля клиента", s == 200 and j["phone"] == "+79005551122", j and j.get("fullName", ""))
s, j, _, _ = call("PUT", "/api/profile/password", C2, {"currentPassword": "bad", "newPassword": "Client2027"})
case("ТК-57", "Смена пароля с неверным текущим паролем (400)", s == 400, msg(j))
times = [call("GET", "/api/orders", R)[3] for _ in range(20)] + [call("GET", "/api/orders/10198", R)[3] for _ in range(20)]
case("ТК-58", "Время ответа API на типовые запросы < 1 с", max(times) < 1000, f"среднее {sum(times) / len(times):.0f} мс, максимум {max(times):.0f} мс")

passed = sum(r["passed"] for r in results)
print(f"\nИтого: {passed} из {len(results)} пройдено")
if "--out" in sys.argv:
    with open(sys.argv[sys.argv.index("--out") + 1], "w", encoding="utf-8") as f:
        json.dump(results, f, ensure_ascii=False, indent=1)
sys.exit(0 if passed == len(results) else 1)
