// Dynamic sitemap source for @nuxtjs/sitemap (registered via `sitemap.sources` in
// nuxt.config). Runs on the server at request/prerender time, queries the Store API and
// returns one entry per content URL. Static routes (/, /Products, /News, ...) are collected
// automatically by the module from the pages directory, so only DB-driven URLs live here.
//
// Brand detail pages are intentionally omitted: the Store API has no brand-list endpoint
// (only /store/brands/detail), and brand pages are reachable via product pages.

interface Paged<T> { items: T[]; totalPages: number }

/** DB title (slashes) → URL segment (hyphens). Mirrors app/utils/slug.titleToUrlSlug. */
function toSlug(title: string): string {
  return title.replaceAll('/', '-')
}

export default defineSitemapEventHandler(async () => {
  const apiBase = String(useRuntimeConfig().public.apiBase).replace(/\/+$/, '')
  const urls: { loc: string }[] = []

  const safe = async <T>(fn: () => Promise<T>, fallback: T): Promise<T> => {
    try { return await fn() }
    catch { return fallback }
  }

  // Walk a paginated list endpoint (?p=) and map each item to a URL.
  async function collect<T>(
    path: string,
    map: (item: T) => string | null,
    maxPages = 200,
  ): Promise<void> {
    const first = await safe(
      () => $fetch<Paged<T>>(`${apiBase}${path}`, { query: { p: 1 } }),
      { items: [], totalPages: 0 },
    )
    const pushAll = (items: T[]) => {
      for (const it of items) {
        const loc = map(it)
        if (loc) urls.push({ loc })
      }
    }
    pushAll(first.items)
    const total = Math.min(first.totalPages || 1, maxPages)
    for (let p = 2; p <= total; p++) {
      const page = await safe(
        () => $fetch<Paged<T>>(`${apiBase}${path}`, { query: { p } }),
        { items: [] as T[], totalPages: total },
      )
      pushAll(page.items)
    }
  }

  // Products + product-type listings (non-paginated single response).
  const products = await safe(
    () => $fetch<{ products: { title: string }[]; producttypes: { title: string }[] }>(
      `${apiBase}/store/products`,
    ),
    { products: [], producttypes: [] },
  )
  for (const t of products.producttypes) urls.push({ loc: `/Products/${encodeURIComponent(t.title)}` })
  for (const p of products.products) urls.push({ loc: `/Product/${toSlug(p.title)}` })

  await Promise.all([
    collect<{ newid: string }>('/store/news', (n) => `/NewsDetail/${n.newid}/1`),
    collect<{ recipeid: string }>('/store/recipes', (r) => `/Recipe/${r.recipeid}/1`),
    collect<{ title: string }>('/store/issues', (i) => `/Issue/${toSlug(i.title)}/1`),
    collect<{ eventid: string }>('/store/events', (e) => `/Events/${e.eventid}`),
  ])

  return urls
})
