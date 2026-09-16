export async function onRequest(context) {
  if (context.request.method === "OPTIONS") {
    return new Response(null, { status: 204, headers: cors() });
  }

  const slug = String(context.params.slug || "").trim().toLowerCase();
  const rest = joinPath(context.params.path);
  if (!slug || !rest) return json({ message: "طلب غير صالح." }, 400);

  const rec = await lookupStore(context, slug);
  if (!rec) return json({ message: "المتجر غير متاح." }, 404);
  const tunnel = String(rec.tunnel || "").replace(/\/+$/, "");
  if (!/^https:\/\//i.test(tunnel)) return json({ message: "النفق غير جاهز." }, 503);

  const incoming = new URL(context.request.url);
  const targetPath = "/api/webstore/" + rest;
  const target = tunnel + targetPath + incoming.search;
  const bodyBuf = await context.request.arrayBuffer();
  const secret = (context.env && context.env.WEBSTORE_HMAC_SECRET) || "";
  const gate = (context.env && context.env.WEBSTORE_GATE_TOKEN) || "";
  const ts = String(Math.floor(Date.now() / 1000));
  const nonce = crypto.randomUUID().replace(/-/g, "");
  const headers = new Headers();
  headers.set("content-type", context.request.headers.get("content-type") || "application/json");
  headers.set("Origin", "https://mahgoubonline.com");
  headers.set("X-Mahgoub-Slug", slug);
  const device = context.request.headers.get("X-Mahgoub-Device");
  if (device) headers.set("X-Mahgoub-Device", device);
  const visitorIp = context.request.headers.get("CF-Connecting-IP") || context.request.headers.get("True-Client-IP");
  if (visitorIp) headers.set("CF-Connecting-IP", visitorIp);
  if (gate) headers.set("X-Mahgoub-Gate", gate);
  if (secret) {
    const sign = await hmacSign(secret, context.request.method, targetPath + incoming.search, ts, nonce, new Uint8Array(bodyBuf));
    headers.set("X-Mahgoub-Ts", ts);
    headers.set("X-Mahgoub-Nonce", nonce);
    headers.set("X-Mahgoub-Sign", sign);
  }

  let upstream;
  try {
    upstream = await fetch(target, {
      method: context.request.method,
      headers,
      body: ["GET", "HEAD"].includes(context.request.method.toUpperCase()) ? undefined : bodyBuf
    });
  } catch {
    return json({ message: "المتجر غير متاح حالياً، يرجى الزيارة لاحقاً" }, 503);
  }

  const out = new Headers(upstream.headers);
  out.set("x-frame-options", "DENY");
  out.set("x-content-type-options", "nosniff");
  out.set("referrer-policy", "strict-origin-when-cross-origin");
  Object.entries(cors()).forEach(([k, v]) => out.set(k, v));
  return new Response(upstream.body, { status: upstream.status, headers: out });
}

function joinPath(path) {
  if (Array.isArray(path)) return path.filter(Boolean).join("/");
  return String(path || "").replace(/^\/+/, "");
}

function cors() {
  return {
    "access-control-allow-origin": "*",
    "access-control-allow-headers": "content-type, x-mahgoub-slug, x-mahgoub-device",
    "access-control-allow-methods": "GET, POST, OPTIONS"
  };
}

function json(body, status) {
  return new Response(JSON.stringify(body), {
    status: status || 200,
    headers: { "content-type": "application/json; charset=utf-8", ...cors() }
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

const DEFAULT_STORE_DATA_URL =
  "https://www.dropbox.com/scl/fi/5rccqzhezx15nviq5zoyf/Mahgoub_store_data.txt?rlkey=7v930ywv3umwbnrvyhpn1p50k&dl=1";

async function lookupStore(context, slug) {
  const dropboxUrl =
    (context.env && context.env.DROPBOX_STORE_DATA_URL) || DEFAULT_STORE_DATA_URL;
  const resp = await fetch(withDirectDownload(dropboxUrl), { redirect: "follow" });
  if (!resp.ok) return null;
  const text = await resp.text();
  return findStore(text, slug);
}

function withDirectDownload(url) {
  if (!/dropbox\.com/i.test(url)) return url;
  if (/dl=0/i.test(url)) return url.replace(/dl=0/i, "dl=1");
  if (!/dl=1/i.test(url)) return url + (url.includes("?") ? "&dl=1" : "?dl=1");
  return url;
}

function findStore(raw, slug) {
  const lines = String(raw || "").replace(/\r/g, "\n").split("\n");
  for (const line of lines) {
    const t = line.trim();
    if (/^#\s*STORES/i.test(t)) {
      const i = t.indexOf("{");
      if (i < 0) continue;
      try {
        const data = JSON.parse(t.slice(i));
        const hit = (data.stores || []).find((s) => String(s.slug || "").toLowerCase() === slug);
        if (hit) {
          return {
            slug,
            tunnel: hit.tunnel || ""
          };
        }
      } catch { /* تجاهل */ }
    }
    if (t.toLowerCase().startsWith("store.") && t.includes("=")) {
      const eq = t.indexOf("=");
      const s = t.slice(6, eq).trim().toLowerCase();
      if (s !== slug) continue;
      const parts = t.slice(eq + 1).split("|");
      return {
        slug,
        tunnel: (parts[0] || "").trim()
      };
    }
  }
  return null;
}
