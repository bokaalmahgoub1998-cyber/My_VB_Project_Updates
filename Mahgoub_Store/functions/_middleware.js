const LOADERIO_TOKEN = "loaderio-5e05aad08ec1aef8efd4f91ad375ed2a";

export async function onRequest(context) {
  const url = new URL(context.request.url);
  if (url.pathname.startsWith("/api/"))
    return context.next();

  // loader.io verification (with or without .txt)
  const path = url.pathname.replace(/\/+$/, "") || "/";
  if (path === `/${LOADERIO_TOKEN}` || path === `/${LOADERIO_TOKEN}.txt`) {
    return new Response(LOADERIO_TOKEN, {
      status: 200,
      headers: {
        "content-type": "text/plain; charset=utf-8",
        "cache-control": "no-store"
      }
    });
  }

  try {
    const asset = await context.env.ASSETS.fetch(context.request);
    if (asset.status !== 404)
      return asset;
  } catch {
    /* نكمل إلى index.html */
  }

  const index = await context.env.ASSETS.fetch(new URL("/index.html", url.origin));
  if (index.ok) {
    const headers = new Headers(index.headers);
    headers.set("content-type", "text/html; charset=utf-8");
    headers.set("cache-control", "no-cache");
    if (!headers.has("permissions-policy"))
      headers.set("Permissions-Policy", "geolocation=(self), camera=(), microphone=()");
    return new Response(index.body, { status: 200, headers });
  }

  return new Response("Not found", { status: 404, headers: { "cache-control": "no-store" } });
}
