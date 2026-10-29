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
  sku?: string
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
    ...(opts.sku ? { sku: opts.sku } : {}),
    ...(opts.brand ? { brand: { '@type': 'Brand', name: opts.brand } } : {}),
    offers: {
      '@type': 'Offer',
      ...(opts.url ? { url: opts.url } : {}),
      price: Math.trunc(opts.price),
      priceCurrency: 'TWD',
      availability: opts.inStock
        ? 'https://schema.org/InStock'
        : 'https://schema.org/OutOfStock',
      shippingDetails: shippingDetailsJsonLd(opts.price),
      hasMerchantReturnPolicy: RETURN_POLICY,
    },
  }
}

/**
 * Shipping for a single-item order: free at/over FREIGHT_THRESHOLD, else FREIGHT_FEE (utils/shipping).
 * Times mirror 購物說明「配送」: ships in 3~7 business days, arrives ~3 days after dispatch.
 */
function shippingDetailsJsonLd(price: number): Json {
  const days = (minValue: number, maxValue: number) =>
    ({ '@type': 'QuantitativeValue', minValue, maxValue, unitCode: 'DAY' })
  return {
    '@type': 'OfferShippingDetails',
    shippingRate: {
      '@type': 'MonetaryAmount',
      value: price >= FREIGHT_THRESHOLD ? 0 : FREIGHT_FEE,
      currency: 'TWD',
    },
    shippingDestination: { '@type': 'DefinedRegion', addressCountry: 'TW' },
    deliveryTime: {
      '@type': 'ShippingDeliveryTime',
      handlingTime: days(3, 7),
      transitTime: days(1, 3),
    },
  }
}

/** 購物說明「退貨」: report damage within 7 days of receipt; opened items not returnable. */
const RETURN_POLICY: Json = {
  '@type': 'MerchantReturnPolicy',
  applicableCountry: 'TW',
  returnPolicyCategory: 'https://schema.org/MerchantReturnFiniteReturnWindow',
  merchantReturnDays: 7,
  returnMethod: 'https://schema.org/ReturnByMail',
}

/** schema.org/Article for news / recipes / issues. */
export function articleJsonLd(opts: {
  headline: string
  description?: string | null
  image?: string | null
  url?: string
  datePublished?: string | null
  /** No modified-date column in the DB; defaults to datePublished. */
  dateModified?: string | null
}): Json {
  const dateModified = opts.dateModified ?? opts.datePublished
  return {
    '@context': 'https://schema.org',
    '@type': 'Article',
    headline: opts.headline,
    ...(opts.description ? { description: metaDescription(opts.description, 300) } : {}),
    ...(opts.image ? { image: opts.image } : {}),
    ...(opts.url ? { mainEntityOfPage: opts.url } : {}),
    ...(opts.datePublished ? { datePublished: opts.datePublished } : {}),
    ...(dateModified ? { dateModified } : {}),
    author: { '@type': 'Organization', name: '食在呼 TFoodies' },
    publisher: { '@type': 'Organization', name: '食在呼 TFoodies' },
  }
}

/**
 * schema.org/Recipe — the format Google recipe cards and AI answer engines consume for
 * cooking content (ingredients, steps, time, yield). Ingredients + seasonings are merged
 * into recipeIngredient; steps become HowToStep nodes. duration is minutes → ISO-8601.
 */
export function recipeJsonLd(opts: {
  name: string
  description?: string | null
  image?: string | null
  url?: string
  /** Cooking time in minutes (legacy `duration`). */
  durationMinutes?: number | null
  /** Servings (legacy `portion`). */
  yield?: number | null
  ingredients?: string[]
  steps?: { title?: string | null; text?: string | null }[]
  videoUrl?: string | null
}): Json {
  const stripHtml = (v?: string | null) => (v ? metaDescription(v, 1000) : '')
  const instructions = (opts.steps ?? [])
    .map((s) => stripHtml(s.text) || stripHtml(s.title))
    .filter(Boolean)
    .map((text, i) => ({
      '@type': 'HowToStep',
      position: i + 1,
      text,
      ...(opts.steps?.[i]?.title ? { name: opts.steps[i]!.title } : {}),
    }))
  return {
    '@context': 'https://schema.org',
    '@type': 'Recipe',
    name: opts.name,
    ...(opts.description ? { description: metaDescription(opts.description, 300) } : {}),
    ...(opts.image ? { image: opts.image } : {}),
    ...(opts.url ? { mainEntityOfPage: opts.url } : {}),
    ...(opts.durationMinutes ? { totalTime: `PT${Math.trunc(opts.durationMinutes)}M` } : {}),
    ...(opts.yield ? { recipeYield: `${Math.trunc(opts.yield)} 人份` } : {}),
    ...(opts.ingredients?.length ? { recipeIngredient: opts.ingredients } : {}),
    ...(instructions.length ? { recipeInstructions: instructions } : {}),
    ...(opts.videoUrl ? { video: { '@type': 'VideoObject', contentUrl: opts.videoUrl } } : {}),
    author: { '@type': 'Organization', name: '食在呼 TFoodies' },
    publisher: { '@type': 'Organization', name: '食在呼 TFoodies' },
  }
}

/**
 * schema.org/Organization — the publisher entity. Injected once globally (app.vue) with a
 * stable @id so other nodes (Article/Recipe publisher, WebSite) can reference it. Contact +
 * address mirror the 聯絡資訊 page. siteUrl must be absolute, no trailing slash.
 */
export function organizationJsonLd(siteUrl: string): Json {
  return {
    '@context': 'https://schema.org',
    '@type': 'Organization',
    '@id': `${siteUrl}/#organization`,
    name: '食在呼 TFoodies',
    legalName: '庭富國際有限公司',
    url: `${siteUrl}/`,
    logo: `${siteUrl}/content/images/logo.png`,
    email: 'hi@tfoodies.com',
    contactPoint: {
      '@type': 'ContactPoint',
      contactType: 'customer service',
      telephone: '+886-4-2436-6659',
      email: 'hi@tfoodies.com',
      areaServed: 'TW',
      availableLanguage: 'zh-Hant',
    },
    address: {
      '@type': 'PostalAddress',
      addressCountry: 'TW',
      addressRegion: '台中市',
      addressLocality: '北屯區',
      postalCode: '406',
      streetAddress: '東山路一段192巷56弄18號',
    },
  }
}

/**
 * schema.org/WebSite — site identity for search engines / AI engines. Injected once globally
 * (app.vue); publisher references the Organization @id. siteUrl absolute, no trailing slash.
 */
export function websiteJsonLd(siteUrl: string): Json {
  return {
    '@context': 'https://schema.org',
    '@type': 'WebSite',
    '@id': `${siteUrl}/#website`,
    name: '食在呼 TFoodies',
    url: `${siteUrl}/`,
    inLanguage: 'zh-Hant-TW',
    publisher: { '@id': `${siteUrl}/#organization` },
  }
}

/**
 * schema.org/FAQPage — Q&A pairs for 購物說明. Answers are rich-text HTML, stripped to plain
 * text. Only pass the questions actually rendered on the page (Google requires visibility).
 */
export function faqPageJsonLd(items: { question: string; answer: string }[]): Json {
  return {
    '@context': 'https://schema.org',
    '@type': 'FAQPage',
    mainEntity: items.map((it) => ({
      '@type': 'Question',
      name: it.question,
      acceptedAnswer: { '@type': 'Answer', text: stripHtml(it.answer) },
    })),
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
