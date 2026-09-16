export async function onRequest(context) {
  const url = new URL(context.request.url);
  if (url.pathname.startsWith("/api/"))
    return context.next();

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
    return new Response(index.body, { status: 200, headers });
  }

  return new Response("Not found", { status: 404, headers: { "cache-control": "no-store" } });
}
