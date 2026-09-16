export async function onRequestGet(context) {
  const slug = String(context.params.slug || "").trim().toLowerCase();
  if (!slug) return json({ slug, enabled: false }, 400);

  const dropboxUrl =
    (context.env && context.env.DROPBOX_STORE_DATA_URL) ||
    DEFAULT_STORE_DATA_URL;

  try {
    const resp = await fetch(withDirectDownload(dropboxUrl), { redirect: "follow" });
    if (!resp.ok) return json({ slug, enabled: false }, 503);
    const text = await resp.text();
    if (looksLikeSqlConnection(text)) return json({ slug, enabled: false }, 404);
    const rec = findStore(text, slug);
    if (!rec) return json({ slug, enabled: false }, 404);
    return json(publicStore(rec));
  } catch {
    return json({ slug, enabled: false }, 503);
  }
}

const DEFAULT_STORE_DATA_URL =
  "https://www.dropbox.com/scl/fi/5rccqzhezx15nviq5zoyf/Mahgoub_store_data.txt?rlkey=7v930ywv3umwbnrvyhpn1p50k&dl=1";

function json(body, status) {
  return new Response(JSON.stringify(body), {
    status: status || 200,
    headers: { "content-type": "application/json; charset=utf-8" }
  });
}

function withDirectDownload(url) {
  if (!/dropbox\.com/i.test(url)) return url;
  if (/dl=0/i.test(url)) return url.replace(/dl=0/i, "dl=1");
  if (!/dl=1/i.test(url)) return url + (url.includes("?") ? "&dl=1" : "?dl=1");
  return url;
}

function looksLikeSqlConnection(raw) {
  const first = String(raw || "").replace(/\r/g, "\n").split("\n").map((x) => x.trim()).find((x) => x && !x.startsWith("#") && !/^store\./i.test(x));
  return first ? /^(server\s*=|data source\s*=)/i.test(first) : false;
}

function publicStore(rec) {
  return {
    slug: rec.slug,
    tunnel: rec.tunnel || "",
    enabled: true
  };
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
          return { slug, tunnel: hit.tunnel || "" };
        }
      } catch { /* تجاهل JSON غير صالح */ }
    }
    if (t.toLowerCase().startsWith("store.") && t.includes("=")) {
      const eq = t.indexOf("=");
      const s = t.slice(6, eq).trim().toLowerCase();
      if (s !== slug) continue;
      const parts = t.slice(eq + 1).split("|");
      return { slug, tunnel: (parts[0] || "").trim() };
    }
  }
  return null;
}
