import type { MaybeRefOrGetter } from 'vue'

// Centralised SEO meta for the storefront. Wraps useSeoMeta() + a canonical <link>, fills
// the full OpenGraph/Twitter set (so FB/LINE/Twitter scrapers get a title/description/image),
// and falls back to a site logo when a page has no image. Pass a getter so it stays reactive
// while the page's `await useXxxData()` resolves.

export interface SeoInput {
  title?: string | null
  /** Raw text or HTML; stripped + truncated to a meta description. */
  description?: string | null
  /** Absolute (blob) URL or site-relative path; absolutised + defaulted to the site logo. */
  image?: string | null
  /** Canonical absolute URL; defaults to siteUrl + current path. */
  url?: string | null
  /** og:type — 'website' (default) | 'article' | 'product'. */
  type?: 'website' | 'article' | 'product'
  noindex?: boolean
}

export function useSeo(source: MaybeRefOrGetter<SeoInput>) {
  const config = useRuntimeConfig()
  const siteUrl = String(config.public.siteUrl).replace(/\/+$/, '')
  const reqUrl = useRequestURL()
  const defaultImage = `${siteUrl}/content/images/logo.png`

  const resolved = computed(() => {
    const s = toValue(source)
    return {
      title: s.title ?? undefined,
      description: s.description ? metaDescription(s.description) : undefined,
      image: absoluteUrl(s.image, siteUrl) || defaultImage,
      url: s.url || `${siteUrl}${reqUrl.pathname}`,
      type: s.type ?? 'website',
      noindex: s.noindex ?? false,
    }
  })

  useSeoMeta({
    title: () => resolved.value.title,
    description: () => resolved.value.description,
    ogTitle: () => resolved.value.title,
    ogDescription: () => resolved.value.description,
    ogImage: () => resolved.value.image,
    ogType: () => resolved.value.type as 'website',
    ogUrl: () => resolved.value.url,
    ogSiteName: '食在呼 TFoodies',
    ogLocale: 'zh_TW',
    twitterCard: 'summary_large_image',
    twitterTitle: () => resolved.value.title,
    twitterDescription: () => resolved.value.description,
    twitterImage: () => resolved.value.image,
    robots: () => (resolved.value.noindex ? 'noindex, nofollow' : undefined),
  })

  useHead(() => ({
    link: [{ rel: 'canonical', href: resolved.value.url }],
  }))
}

/**
 * Inject one or more JSON-LD structured-data blocks. Pass a getter returning the node(s);
 * returns null/[] until the page data resolves. Build nodes with the helpers in utils/jsonLd.
 */
export function useJsonLd(
  source: MaybeRefOrGetter<Record<string, unknown> | Record<string, unknown>[] | null | undefined>,
) {
  useHead(() => {
    const value = toValue(source)
    const nodes = (Array.isArray(value) ? value : value ? [value] : []).filter(Boolean)
    return {
      script: nodes.map((node, i) => ({
        key: `ld-json-${i}`,
        type: 'application/ld+json',
        innerHTML: JSON.stringify(node),
      })),
    }
  })
}
