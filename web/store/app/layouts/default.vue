<script setup lang="ts">
// Verbatim port of reference/old/tfoodies/Views/Shared/_Layout.cshtml body structure:
// #sb-site > #wrapper > (header, body, footer), then the Slidebars mobile menu.
// Brands/cart/login will be fed from composables/stores as those land; defaults render
// the static chrome so the legacy main.css produces the identical frame.
</script>

<template>
  <div id="sb-site">
    <div id="wrapper">
      <SiteHeader />
      <slot />
      <SiteFooter />
    </div>
  </div>
  <MobileMenu />
</template>
