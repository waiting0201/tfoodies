// Dynamic sitemap source for @nuxtjs/sitemap (registered via `sitemap.sources` in
// nuxt.config). Runs on the server at request/prerender time, queries the Store API and
// returns one entry per content URL. Static routes (/, /Products, /News, ...) are collected
// automatically by the module from the pages directory, so only DB-driven URLs live here.
//
// Brand detail pages: enumerated from /store/brands (isdisplay=1, the same list that feeds
// the 「品牌系列」nav menu), so each on-sale brand micro-site gets indexed.

interface Paged<T> { items: T[]; totalPages: number }

// lastmod: the DB has no modified-date column, so articles use their publish/create date
// (they're rarely edited after publishing). Recipes/products have no date → omitted.
interface SitemapEntry { loc: string; lastmod?: string }

/** DB title (slashes) → URL segment (hyphens). Mirrors app/utils/slug.titleToUrlSlug. */
function toSlug(title: string): string {
  return title.replaceAll('/', '-')
}

export default defineSitemapEventHandler(async () => {
  const apiBase = String(useRuntimeConfig().public.apiBase).replace(/\/+$/, '')
  const urls: SitemapEntry[] = []

  const safe = async <T>(fn: () => Promise<T>, fallback: T): Promise<T> => {
    try { return await fn() }
    catch { return fallback }
  }

  // Walk a paginated list endpoint (?p=) and map each item to a URL.
  async function collect<T>(
    path: string,
    map: (item: T) => SitemapEntry | null,
    maxPages = 200,
  ): Promise<void> {
    const first = await safe(
      () => $fetch<Paged<T>>(`${apiBase}${path}`, { query: { p: 1 } }),
      { items: [], totalPages: 0 },
    )
    const pushAll = (items: T[]) => {
      for (const it of items) {
        const entry = map(it)
        if (entry) urls.push(entry)
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

  // Products + product-type listings (non-paginated single response, camelCase DTOs).
  const products = await safe(
    () => $fetch<{ products: { title: string }[]; productTypes: { title: string }[] }>(
      `${apiBase}/store/products`,
    ),
    { products: [], productTypes: [] },
  )
  // Raw title, no encodeURIComponent: @nuxtjs/sitemap percent-encodes <loc> itself, so
  // pre-encoding here double-encodes (%25E5...) and the page can't resolve the type.
  for (const t of products.productTypes) urls.push({ loc: `/Products/${t.title}` })
  for (const p of products.products) urls.push({ loc: `/Product/${toSlug(p.title)}` })

  // Brand micro-sites (on-sale only; same source as the 品牌系列 nav menu).
  const brands = await safe(
    () => $fetch<{ title: string }[]>(`${apiBase}/store/brands`),
    [],
  )
  for (const b of brands) urls.push({ loc: `/Brand/${toSlug(b.title)}` })

  await Promise.all([
    collect<{ newId: string; publishDate?: string }>('/store/news',
      (n) => ({ loc: `/NewsDetail/${n.newId}/1`, lastmod: n.publishDate })),
    collect<{ recipeId: string }>('/store/recipes', (r) => ({ loc: `/Recipe/${r.recipeId}/1` })),
    collect<{ title: string; createDate?: string }>('/store/issues',
      (i) => ({ loc: `/Issue/${toSlug(i.title)}/1`, lastmod: i.createDate })),
    collect<{ knowledgeId: string; createDate?: string }>('/store/knowledges',
      (k) => ({ loc: `/Knowledge/${k.knowledgeId}/1`, lastmod: k.createDate })),
    collect<{ eventId: string; createDate?: string }>('/store/events',
      (e) => ({ loc: `/Events/${e.eventId}`, lastmod: e.createDate })),
  ])

  return urls
})
