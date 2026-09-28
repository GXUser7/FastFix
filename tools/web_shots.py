# Скриншоты веб-кабинета для руководства пользователя (Edge headless).
# 1) через DevTools-протокол выполняется вход демо-клиента, токен сохраняется в localStorage профиля;
# 2) страницы снимаются командой msedge --screenshot с тем же профилем.
# Требуется запущенный сервер API (:5080) и веб (:5173), демо-данные из db/02_seed.sql.
import json
import os
import socket
import subprocess
import sys
import tempfile
import time
import urllib.request

import websocket

EDGE = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
WEB = "http://localhost:5173"
OUT = os.path.abspath(sys.argv[1] if len(sys.argv) > 1 else ".")
PROFILE = tempfile.mkdtemp(prefix="sd-edge-")


def free_port():
    with socket.socket() as s:
        s.bind(("127.0.0.1", 0))
        return s.getsockname()[1]


def login(login_, password):
    port = free_port()
    proc = subprocess.Popen([EDGE, "--headless=new", f"--remote-debugging-port={port}", "--remote-allow-origins=*",
                             f"--user-data-dir={PROFILE}", "--no-first-run", "about:blank"])
    try:
        time.sleep(3)
        page = next(t for t in json.load(urllib.request.urlopen(f"http://127.0.0.1:{port}/json")) if t["type"] == "page")
        ws = websocket.create_connection(page["webSocketDebuggerUrl"], timeout=30, suppress_origin=True)
        n = 0

        def cdp(method, **params):
            nonlocal n
            n += 1
            ws.send(json.dumps({"id": n, "method": method, "params": params}))
            while True:
                m = json.loads(ws.recv())
                if m.get("id") == n:
                    return m.get("result", {})

        cdp("Page.navigate", url=WEB + "/login")
        time.sleep(2)
        cdp("Runtime.evaluate", awaitPromise=True, expression=f"""
            fetch('/api/auth/login', {{method:'POST', headers:{{'Content-Type':'application/json'}},
              body: JSON.stringify({{login:{json.dumps(login_)}, password:{json.dumps(password)}}})}})
            .then(r => r.json()).then(a => localStorage.setItem('servicedesk.session', JSON.stringify(a)))""")
        time.sleep(1)
        try:
            cdp("Browser.close")
        except Exception:
            pass
        ws.close()
    finally:
        time.sleep(1)
        proc.terminate()
        time.sleep(1)


def shot(path, name, w=1280, h=800, scale=1.5):
    out = os.path.join(OUT, name)
    if os.path.exists(out):
        os.remove(out)
    subprocess.run([EDGE, "--headless=new", f"--user-data-dir={PROFILE}", "--no-first-run", "--hide-scrollbars",
                    f"--window-size={w},{h}", f"--force-device-scale-factor={scale}", "--virtual-time-budget=6000",
                    f"--screenshot={out}", WEB + path], timeout=90, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    print(("saved " if os.path.exists(out) else "FAILED ") + name)


shot("/login", "w_login.png")
shot("/register", "w_register.png")
login("+79001234567", "Client2026")
shot("/", "w_orders.png")
shot("/orders/10236", "w_order_approval.png", h=1500)
shot("/profile", "w_profile.png")
shot("/profile/edit", "w_profile_edit.png")
