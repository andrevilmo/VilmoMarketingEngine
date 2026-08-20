#!/usr/bin/env python3
"""CDP e2e: login, Ingerir NF-e, attach DANFE QR photo, assert chave filled."""
import json
import os
import subprocess
import time
import urllib.request
from pathlib import Path

import websocket  # type: ignore

CHAVE = "42260868431371000161555001000000001123456788"
PNG = str(Path("/workspace/tests/nfe-scan-fixtures/danfe_qr_p.png").resolve())
BASE = "http://127.0.0.1:8088"
DEBUG = 9335
USER_DATA = "/tmp/chrome-nfe-e2e-" + str(os.getpid())


def http_json(url):
    with urllib.request.urlopen(url, timeout=5) as r:
        return json.loads(r.read().decode())


def login_token():
    req = urllib.request.Request(
        BASE + "/api/auth/login",
        data=json.dumps({"email": "admin@vilmomkt.com", "password": os.environ.get("BOOTSTRAP_ADMIN_PASSWORD", "VilmoAdmin!2026")}).encode(),
        headers={"content-type": "application/json"},
    )
    with urllib.request.urlopen(req, timeout=10) as r:
        body = json.loads(r.read().decode())
    return body["accessToken"], body["user"]["memberships"][0]["companyId"]


class Cdp:
    def __init__(self, ws_url):
        self.ws = websocket.create_connection(ws_url, timeout=20)
        self.n = 0

    def call(self, method, **params):
        self.n += 1
        self.ws.send(json.dumps({"id": self.n, "method": method, "params": params}))
        while True:
            msg = json.loads(self.ws.recv())
            if msg.get("id") == self.n:
                if "error" in msg:
                    raise RuntimeError(msg["error"])
                return msg.get("result", {})

    def eval(self, expr, await_promise=True):
        r = self.call(
            "Runtime.evaluate",
            expression=expr,
            awaitPromise=await_promise,
            returnByValue=True,
        )
        return r.get("result", {}).get("value")


def main():
    token, company = login_token()
    chrome = subprocess.Popen(
        [
            "google-chrome",
            "--headless=new",
            "--disable-gpu",
            "--no-sandbox",
            f"--user-data-dir={USER_DATA}",
            f"--remote-debugging-port={DEBUG}", "--remote-allow-origins=*",
            "--use-fake-ui-for-media-stream",
            "--use-fake-device-for-media-stream",
            "--use-file-for-fake-video-capture=/tmp/danfe_qr.y4m",
            f"{BASE}/web/login.html",
        ],
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )
    try:
        ws_url = None
        for _ in range(40):
            try:
                ver = http_json(f"http://127.0.0.1:{DEBUG}/json/version")
                tabs = http_json(f"http://127.0.0.1:{DEBUG}/json")
                ws_url = (tabs[0].get("webSocketDebuggerUrl") if tabs else None) or ver.get(
                    "webSocketDebuggerUrl"
                )
                if ws_url:
                    break
            except Exception:
                time.sleep(0.25)
        if not ws_url:
            raise SystemExit("no cdp websocket")
        cdp = Cdp(ws_url)
        cdp.call("Page.enable")
        cdp.call("Runtime.enable")
        cdp.call("DOM.enable")
        cdp.eval(
            f"""(async () => {{
          sessionStorage.setItem('vilmo_token', {json.dumps(token)});
          sessionStorage.setItem('vilmo_company', {json.dumps(company)});
          location.href = '/web/index.html#/nfe';
        }})()"""
        )
        for _ in range(40):
            ready = cdp.eval("!!document.getElementById('scan-photo')", await_promise=False)
            if ready:
                break
            time.sleep(0.25)
        else:
            raise SystemExit("nfe form not ready: " + str(cdp.eval("document.body.innerText.slice(0,400)", await_promise=False)))
        doc = cdp.call("DOM.getDocument")
        node = cdp.call("DOM.querySelector", nodeId=doc["root"]["nodeId"], selector="#scan-photo")
        cdp.call("DOM.setFileInputFiles", nodeId=node["nodeId"], files=[PNG])
        chave = None
        for _ in range(30):
            chave = cdp.eval("document.getElementById('chave') && document.getElementById('chave').value", await_promise=False)
            if chave == CHAVE:
                break
            time.sleep(0.2)
        print("PHOTO_CHAVE", chave)
        if chave != CHAVE:
            raise SystemExit("photo path did not fill chave")
        print("PHOTO_OK")
        cdp.eval("document.getElementById('chave').value = ''", await_promise=False)
        cdp.eval("document.getElementById('scan-btn').click()", await_promise=False)
        opened = False
        for _ in range(40):
            overlay = cdp.eval(
                "document.getElementById('camera-overlay') && !document.getElementById('camera-overlay').classList.contains('hidden')",
                await_promise=False,
            )
            if overlay:
                opened = True
                break
            time.sleep(0.15)
        print("LIVE_OVERLAY", opened)
        if not opened:
            raise SystemExit("camera overlay did not open")
        live = None
        for _ in range(80):
            live = cdp.eval(
                "document.getElementById('chave') && document.getElementById('chave').value",
                await_promise=False,
            )
            overlay = cdp.eval(
                "document.getElementById('camera-overlay') && !document.getElementById('camera-overlay').classList.contains('hidden')",
                await_promise=False,
            )
            if live == CHAVE:
                print("LIVE_CHAVE", live, "overlay_open", overlay)
                break
            time.sleep(0.2)
        else:
            raise SystemExit("live camera did not fill chave, last=" + str(live))
        print("LIVE_OK")
    finally:
        chrome.terminate()
        try:
            chrome.wait(timeout=5)
        except Exception:
            chrome.kill()


if __name__ == "__main__":
    main()
