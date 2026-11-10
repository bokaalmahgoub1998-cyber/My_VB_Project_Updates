/**
 * إعداد قاعدة الشركة (Mahgoub_server_name) — نفس عقلية المتجر:
 * رابط Dropbox يبقى Secret على Cloudflare فقط، والعميل يستدعي هذا المسار بتوقيع Gate+HMAC.
 * لا يُرجع رابط Dropbox أبداً.
 *
 * Secrets (Pages → Environment variables → Encrypted):
 *   DROPBOX_SERVER_NAME_URL
 *   COMPANY_CONFIG_HMAC_SECRET
 *   COMPANY_CONFIG_GATE_TOKEN
 */
export async function onRequest(context) {
  const incoming = context.request;
  if (incoming.method === "OPTIONS") {
    return new Response(null, { status: 204, headers: cors(incoming) });
  }

  if (incoming.method !== "GET" && incoming.method !== "HEAD") {
    return json(incoming, { message: "طريقة غير مسموحة." }, 405);
  }

  const hmacSecret = String((context.env && context.env.COMPANY_CONFIG_HMAC_SECRET) || "").trim();
  const gateToken = String((context.env && context.env.COMPANY_CONFIG_GATE_TOKEN) || "").trim();
  const dropboxUrl = String((context.env && context.env.DROPBOX_SERVER_NAME_URL) || "").trim();
  if (!hmacSecret || !gateToken || !dropboxUrl) {
    const missing = [];
    if (!hmacSecret) missing.push("COMPANY_CONFIG_HMAC_SECRET");
    if (!gateToken) missing.push("COMPANY_CONFIG_GATE_TOKEN");
    if (!dropboxUrl) missing.push("DROPBOX_SERVER_NAME_URL");
    // أسماء الأسرار الناقصة فقط — بلا قيم
    return json(incoming, { message: "إعداد الحماية غير مكتمل.", missing }, 503);
  }

  const gate = incoming.headers.get("X-Mahgoub-Gate") || "";
  const ts = incoming.headers.get("X-Mahgoub-Ts") || "";
  const nonce = incoming.headers.get("X-Mahgoub-Nonce") || "";
  const sign = incoming.headers.get("X-Mahgoub-Sign") || "";

  if (!gate || !(await timingSafeEqual(gateToken, gate))) {
    return json(incoming, { message: "رمز البوابة مفقود أو غير صالح." }, 401);
  }
  if (!ts || !nonce || !sign) {
    return json(incoming, { message: "التوقيع مطلوب." }, 401);
  }

  const unix = Number(ts);
  if (!Number.isFinite(unix) || Math.abs(Date.now() / 1000 - unix) > 300) {
    return json(incoming, { message: "انتهت صلاحية التوقيع." }, 401);
  }

  const url = new URL(incoming.url);
  const pathAndQuery = url.pathname + url.search;
  const expected = await hmacSign(hmacSecret, incoming.method, pathAndQuery, ts, nonce, new Uint8Array());
  if (!(await timingSafeEqual(expected, sign))) {
    return json(incoming, { message: "توقيع غير صالح." }, 401);
  }

  let text;
  try {
    const resp = await fetch(withDirectDownload(dropboxUrl), { redirect: "follow" });
    if (!resp.ok) {
      return json(incoming, { message: "تعذر جلب إعداد الشركة." }, 503);
    }
    text = await resp.text();
  } catch {
    return json(incoming, { message: "تعذر جلب إعداد الشركة." }, 503);
  }

  if (!text || looksLikeHtml(text) || !looksLikeSqlConnection(text)) {
    return json(incoming, { message: "ملف إعداد الشركة غير صالح." }, 503);
  }

  if (incoming.method === "HEAD") {
    return new Response(null, {
      status: 200,
      headers: {
        "content-type": "text/plain; charset=utf-8",
        "cache-control": "no-store",
        "x-frame-options": "DENY",
        "x-content-type-options": "nosniff",
        ...cors(incoming)
      }
    });
  }

  return new Response(text, {
    status: 200,
    headers: {
      "content-type": "text/plain; charset=utf-8",
      "cache-control": "no-store",
      "x-frame-options": "DENY",
      "x-content-type-options": "nosniff",
      "referrer-policy": "strict-origin-when-cross-origin",
      ...cors(incoming)
    }
  });
}

function withDirectDownload(url) {
  if (!/dropbox\.com/i.test(url)) return url;
  if (/dl=0/i.test(url)) return url.replace(/dl=0/i, "dl=1");
  if (!/dl=1/i.test(url)) return url + (url.includes("?") ? "&dl=1" : "?dl=1");
  return url;
}

function looksLikeHtml(raw) {
  const t = String(raw || "").trim().slice(0, 200).toLowerCase();
  return t.startsWith("<!doctype") || t.startsWith("<html") || t.includes("<head");
}

function looksLikeSqlConnection(raw) {
  const first = String(raw || "")
    .replace(/\r/g, "\n")
    .split("\n")
    .map((x) => x.trim())
    .find((x) => x && !x.startsWith("#") && !/^store\./i.test(x));
  if (!first) return false;
  return /^(server\s*=|data source\s*=)/i.test(first) || /server\s*=/i.test(first);
}

function isAllowedOrigin(origin) {
  if (!origin) return true; // native apps often omit Origin
  try {
    const u = new URL(origin);
    const h = u.hostname.toLowerCase();
    if (h === "localhost" || h === "127.0.0.1") return true;
    if (h === "mahgoubonline.com" || h.endsWith(".mahgoubonline.com")) return true;
    if (h.endsWith(".pages.dev")) return true;
    return false;
  } catch {
    return false;
  }
}

function cors(request) {
  const origin = request && request.headers ? request.headers.get("Origin") || "" : "";
  const allow = isAllowedOrigin(origin) ? (origin || "https://mahgoubonline.com") : "https://mahgoubonline.com";
  return {
    "access-control-allow-origin": allow,
    "access-control-allow-headers":
      "content-type, x-mahgoub-gate, x-mahgoub-ts, x-mahgoub-nonce, x-mahgoub-sign, x-mahgoub-device",
    "access-control-allow-methods": "GET, HEAD, OPTIONS",
    vary: "Origin"
  };
}

function json(request, body, status) {
  return new Response(JSON.stringify(body), {
    status: status || 200,
    headers: { "content-type": "application/json; charset=utf-8", ...cors(request) }
  });
}

async function hmacSign(secret, method, pathAndQuery, ts, nonce, body) {
  const bodyHash = await sha256hex(body || new Uint8Array());
  const msg = `${ts}\n${nonce}\n${String(method).trim().toUpperCase()}\n${pathAndQuery}\n${bodyHash}`;
  const key = await crypto.subtle.importKey(
    "raw",
    new TextEncoder().encode(secret),
    { name: "HMAC", hash: "SHA-256" },
    false,
    ["sign"]
  );
  const sig = await crypto.subtle.sign("HMAC", key, new TextEncoder().encode(msg));
  return [...new Uint8Array(sig)].map((b) => b.toString(16).padStart(2, "0")).join("");
}

async function sha256hex(bytes) {
  const digest = await crypto.subtle.digest("SHA-256", bytes);
  return [...new Uint8Array(digest)].map((b) => b.toString(16).padStart(2, "0")).join("");
}

async function timingSafeEqual(a, b) {
  const left = new TextEncoder().encode(String(a || "").trim().toLowerCase());
  const right = new TextEncoder().encode(String(b || "").trim().toLowerCase());
  if (left.length !== right.length) return false;
  let diff = 0;
  for (let i = 0; i < left.length; i++) diff |= left[i] ^ right[i];
  return diff === 0;
}
