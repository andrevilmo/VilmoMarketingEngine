/* global ZXing */
(function (root, factory) {
  const api = factory();
  if (typeof module === "object" && module.exports) module.exports = api;
  root.VilmoNfeScan = api;
})(typeof globalThis !== "undefined" ? globalThis : this, function () {
  "use strict";

  function dv(first43) {
    let weight = 2;
    let sum = 0;
    for (let i = first43.length - 1; i >= 0; i--) {
      sum += (first43.charCodeAt(i) - 48) * weight;
      weight = weight === 9 ? 2 : weight + 1;
    }
    const r = 11 - (sum % 11);
    return r === 0 || r === 1 || r === 10 || r === 11 ? 0 : r;
  }

  function isChave(c) {
    return /^\d{44}$/.test(c) && dv(c.slice(0, 43)) === Number(c[43]);
  }

  function extractChave(raw) {
    if (raw == null) return null;
    const s = String(raw);
    const ch = s.match(/chNFe=(\d{44})/i);
    if (ch && isChave(ch[1])) return ch[1];
    const p = s.match(/[?&]p=([^&]+)/i);
    if (p) {
      let val = p[1];
      try { val = decodeURIComponent(val.replace(/\+/g, "%20")); } catch { /* keep */ }
      const head = val.split("|")[0];
      const headDigits = head.replace(/\D/g, "");
      if (isChave(headDigits.slice(0, 44))) return headDigits.slice(0, 44);
    }
    const digits = s.replace(/\D/g, "");
    for (let i = 0; i + 44 <= digits.length; i++) {
      const cand = digits.slice(i, i + 44);
      if (isChave(cand)) return cand;
    }
    return null;
  }

  let running = false;
  let raf = 0;
  let timer = 0;
  let stream = null;
  let detector = null;
  let zxingReader = null;
  let lastInvalidAt = 0;
  let deviceIds = [];
  let deviceIndex = 0;

  async function nativeDetector() {
    if (typeof BarcodeDetector !== "function") return null;
    const wanted = ["qr_code", "code_128", "itf"];
    let formats = wanted;
    try {
      if (typeof BarcodeDetector.getSupportedFormats === "function") {
        const supported = await BarcodeDetector.getSupportedFormats();
        formats = wanted.filter((f) => supported.includes(f));
      }
      if (!formats.length) return null;
      return new BarcodeDetector({ formats });
    } catch {
      try { return new BarcodeDetector({ formats: ["qr_code"] }); } catch { return null; }
    }
  }

  function zxingDecodeCanvas(canvas) {
    if (typeof ZXing === "undefined") return null;
    try {
      if (!zxingReader) {
        const hints = new Map();
        hints.set(ZXing.DecodeHintType.POSSIBLE_FORMATS, [
          ZXing.BarcodeFormat.QR_CODE,
          ZXing.BarcodeFormat.CODE_128,
          ZXing.BarcodeFormat.ITF
        ]);
        hints.set(ZXing.DecodeHintType.TRY_HARDER, true);
        zxingReader = new ZXing.MultiFormatReader();
        zxingReader.setHints(hints);
      }
      const src = new ZXing.HTMLCanvasElementLuminanceSource(canvas);
      const bmp = new ZXing.BinaryBitmap(new ZXing.HybridBinarizer(src));
      const result = zxingReader.decode(bmp);
      zxingReader.reset && zxingReader.reset();
      return result ? result.getText() : null;
    } catch {
      try { zxingReader && zxingReader.reset && zxingReader.reset(); } catch { /* ignore */ }
      return null;
    }
  }

  function drawFrame(video, canvas) {
    const w = video.videoWidth;
    const h = video.videoHeight;
    if (!w || !h) return false;
    const maxW = 1280;
    const scale = w > maxW ? maxW / w : 1;
    canvas.width = Math.round(w * scale);
    canvas.height = Math.round(h * scale);
    const ctx = canvas.getContext("2d", { willReadFrequently: true });
    ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
    return true;
  }

  async function payloadsFromCanvas(canvas) {
    const out = [];
    if (detector) {
      try {
        const codes = await detector.detect(canvas);
        for (const c of codes) if (c.rawValue) out.push(c.rawValue);
      } catch { /* native detector often lacks Code 128 */ }
    }
    const zx = zxingDecodeCanvas(canvas);
    if (zx) out.push(zx);
    return out;
  }

  async function decodeImageSource(img) {
    const canvas = document.createElement("canvas");
    const w = img.naturalWidth || img.width;
    const h = img.naturalHeight || img.height;
    canvas.width = w;
    canvas.height = h;
    canvas.getContext("2d").drawImage(img, 0, 0);
    const payloads = await payloadsFromCanvas(canvas);
    for (const p of payloads) {
      const chave = extractChave(p);
      if (chave) return chave;
    }
    const fromAlt = extractChave(img.alt || "");
    return fromAlt;
  }

  async function listVideoInputs() {
    try {
      const all = await navigator.mediaDevices.enumerateDevices();
      return all.filter((d) => d.kind === "videoinput").map((d) => d.deviceId).filter(Boolean);
    } catch { return []; }
  }

  async function openStream(facing) {
    const constraints = deviceIds.length
      ? { video: { deviceId: { exact: deviceIds[deviceIndex] }, width: { ideal: 1280 }, height: { ideal: 720 } } }
      : { video: { facingMode: { ideal: facing || "environment" }, width: { ideal: 1280 }, height: { ideal: 720 } } };
    return navigator.mediaDevices.getUserMedia(constraints);
  }

  async function start(opts) {
    const video = opts.video;
    const canvas = opts.canvas || document.createElement("canvas");
    const onChave = opts.onChave;
    const onInvalid = opts.onInvalid;
    const facing = opts.facing || "environment";
    stop();
    running = true;
    detector = await nativeDetector();
    stream = await openStream(facing);
    video.srcObject = stream;
    video.setAttribute("playsinline", "true");
    video.muted = true;
    await video.play();
    deviceIds = await listVideoInputs();
    const tick = async () => {
      if (!running) return;
      if (drawFrame(video, canvas)) {
        const payloads = await payloadsFromCanvas(canvas);
        let sawCode = false;
        for (const p of payloads) {
          sawCode = true;
          const chave = extractChave(p);
          if (chave) {
            onChave(chave);
            stop();
            return;
          }
        }
        if (sawCode && onInvalid && Date.now() - lastInvalidAt > 1200) {
          lastInvalidAt = Date.now();
          onInvalid();
        }
      }
      timer = setTimeout(() => { raf = requestAnimationFrame(tick); }, 120);
    };
    raf = requestAnimationFrame(tick);
  }

  async function flip(opts) {
    if (!deviceIds.length) deviceIds = await listVideoInputs();
    if (deviceIds.length < 2) return start(opts);
    deviceIndex = (deviceIndex + 1) % deviceIds.length;
    return start(opts);
  }

  function stop() {
    running = false;
    if (raf) cancelAnimationFrame(raf);
    raf = 0;
    if (timer) clearTimeout(timer);
    timer = 0;
    if (stream) {
      stream.getTracks().forEach((t) => t.stop());
      stream = null;
    }
    if (typeof document !== "undefined") {
      const video = document.getElementById("cam");
      if (video) video.srcObject = null;
    }
  }

  async function fromFile(file) {
    const url = URL.createObjectURL(file);
    try {
      const img = new Image();
      img.src = url;
      await img.decode();
      return decodeImageSource(img);
    } finally {
      URL.revokeObjectURL(url);
    }
  }

  return { extractChave, isChave, dv, start, stop, flip, fromFile, decodeImageSource, payloadsFromCanvas };
});
