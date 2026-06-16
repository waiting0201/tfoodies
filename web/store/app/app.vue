<script setup lang="ts">
// Site-wide structured data (Organization + WebSite) for search/AI engines. Injected once
// here so it appears on every route with a stable @id other nodes reference. Namespaced key
// prefix so it never collides with a page's own useJsonLd() blocks.
const siteUrl = String(useRuntimeConfig().public.siteUrl).replace(/\/+$/, '')
useJsonLd([organizationJsonLd(siteUrl), websiteJsonLd(siteUrl)], 'ld-global')
</script>

<template>
  <div>
    <NuxtRouteAnnouncer />
    <!-- Legacy layout frame + file-based routing (reproduces old URL structure for SEO). -->
    <NuxtLayout>
      <NuxtPage />
    </NuxtLayout>
  </div>
</template>
