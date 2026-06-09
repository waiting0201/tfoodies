import { test, expect } from '@playwright/test'

// SEO/revenue-critical routes that must look identical to the legacy site (plan §7.1 + 路由保留).
// Slug routes need a real slug at runtime; swap in known products/brands before enabling.
const ROUTES: { name: string; path: string }[] = [
  { name: 'home', path: '/' },
  { name: 'products', path: '/Products' },
  { name: 'news', path: '/News' },
  { name: 'recipes', path: '/Recipes' },
  { name: 'issues', path: '/Issues' },
  { name: 'about', path: '/TFoodies' },
  // { name: 'product-detail', path: '/Product/<known-slug>' },
  // { name: 'brand', path: '/Brand/<known-slug>' },
]

for (const route of ROUTES) {
  test(`visual parity: ${route.name}`, async ({ page }) => {
    await page.goto(route.path, { waitUntil: 'networkidle' })
    // Mask volatile regions (e.g. captcha image) if/when present.
    await expect(page).toHaveScreenshot(`${route.name}.png`, { fullPage: true })
  })
}
