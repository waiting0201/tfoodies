// SEO text/URL helpers. Auto-imported by Nuxt (app/utils).
// Content fields (intro/summary/memo) often contain legacy HTML, so meta descriptions must
// be stripped to plain text and capped for Google's ~160-char snippet.

/** Strip HTML tags + entities down to a single line of plain text. */
export function stripHtml(html?: string | null): string {
  if (!html) return ''
  return html
    .replace(/<[^>]*>/g, ' ')
    .replace(/&nbsp;/g, ' ')
    .replace(/&amp;/g, '&')
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/\s+/g, ' ')
    .trim()
}

/** Truncate to `max` chars on a word-ish boundary, appending an ellipsis. */
export function truncate(text: string, max = 160): string {
  if (text.length <= max) return text
  return text.slice(0, max - 1).trimEnd() + '…'
}

/** Build a plain-text meta description from raw (possibly HTML) content. */
export function metaDescription(raw?: string | null, max = 160): string {
  return truncate(stripHtml(raw), max)
}

/** Resolve a path/relative URL to an absolute URL against `base`. Pass-through for http(s). */
export function absoluteUrl(path: string | null | undefined, base: string): string {
  if (!path) return ''
  if (/^https?:\/\//i.test(path)) return path
  const b = base.replace(/\/+$/, '')
  return path.startsWith('/') ? b + path : `${b}/${path}`
}
