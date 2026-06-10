// JSON-LD structured-data builders (schema.org). Pure functions — auto-imported by Nuxt.
// Inject the result with useJsonLd() (composables/useSeo). Descriptions are stripped to
// plain text; images should be passed as absolute (blob) URLs.

type Json = Record<string, unknown>

/** schema.org/Product with an offer (price + TWD + availability from stock). */
export function productJsonLd(opts: {
  name: string
  description?: string | null
  image?: string | string[]
  url?: string
  price: number
  inStock: boolean
  brand?: string | null
}): Json {
  return {
    '@context': 'https://schema.org',
    '@type': 'Product',
    name: opts.name,
    ...(opts.description ? { description: metaDescription(opts.description, 300) } : {}),
    ...(opts.image ? { image: opts.image } : {}),
    ...(opts.brand ? { brand: { '@type': 'Brand', name: opts.brand } } : {}),
    offers: {
      '@type': 'Offer',
      ...(opts.url ? { url: opts.url } : {}),
      price: Math.trunc(opts.price),
      priceCurrency: 'TWD',
      availability: opts.inStock
        ? 'https://schema.org/InStock'
        : 'https://schema.org/OutOfStock',
    },
  }
}

/** schema.org/Article for news / recipes / issues. */
export function articleJsonLd(opts: {
  headline: string
  description?: string | null
  image?: string | null
  url?: string
  datePublished?: string | null
}): Json {
  return {
    '@context': 'https://schema.org',
    '@type': 'Article',
    headline: opts.headline,
    ...(opts.description ? { description: metaDescription(opts.description, 300) } : {}),
    ...(opts.image ? { image: opts.image } : {}),
    ...(opts.url ? { mainEntityOfPage: opts.url } : {}),
    ...(opts.datePublished ? { datePublished: opts.datePublished } : {}),
    author: { '@type': 'Organization', name: '食在呼 TFoodies' },
    publisher: { '@type': 'Organization', name: '食在呼 TFoodies' },
  }
}

/** schema.org/BreadcrumbList from ordered { name, url } crumbs (url absolute or path). */
export function breadcrumbJsonLd(items: { name: string; url?: string }[]): Json {
  return {
    '@context': 'https://schema.org',
    '@type': 'BreadcrumbList',
    itemListElement: items.map((it, i) => ({
      '@type': 'ListItem',
      position: i + 1,
      name: it.name,
      ...(it.url ? { item: it.url } : {}),
    })),
  }
}
