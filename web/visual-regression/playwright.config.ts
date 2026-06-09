import { defineConfig } from '@playwright/test'

// Visual-regression gate (plan §7.1): the new store must match the legacy site pixel-for-pixel.
// LEGACY_URL = the source of truth (https://www.tfoodies.com or a snapshot).
// BASE_URL    = the new store under test (preview/staging).
// Baselines are captured from LEGACY_URL via `npm run test:update`, then BASE_URL is diffed.
export default defineConfig({
  testDir: './tests',
  snapshotPathTemplate: '{testDir}/__legacy_baseline__/{testFilePath}/{arg}{ext}',
  expect: {
    // ≤ ~0.1% pixel difference tolerance.
    toHaveScreenshot: { maxDiffPixelRatio: 0.001, animations: 'disabled' },
  },
  use: {
    baseURL: process.env.BASE_URL ?? 'http://localhost:3000',
  },
  projects: [
    { name: 'desktop', use: { viewport: { width: 1280, height: 900 } } },
    { name: 'laptop', use: { viewport: { width: 1024, height: 800 } } },
    { name: 'tablet', use: { viewport: { width: 768, height: 1024 } } },
    { name: 'mobile', use: { viewport: { width: 375, height: 812 } } },
  ],
})
