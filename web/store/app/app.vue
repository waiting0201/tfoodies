<template>
  <div>
    <NuxtRouteAnnouncer />
    <!-- Legacy layout frame + file-based routing (reproduces old URL structure for SEO). -->
    <NuxtLayout>
      <NuxtPage />
    </NuxtLayout>
  </div>
</template>
