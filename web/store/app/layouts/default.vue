<script setup lang="ts">
// Verbatim port of reference/old/tfoodies/Views/Shared/_Layout.cshtml body structure:
// #sb-site > #wrapper > (header, body, footer), then the Slidebars mobile menu.
// 品牌系列下拉清單為全站共用（舊 BaseController.ViewBag.Brands），於 layout 一次載入後
// 餵給桌面導覽列與行動側欄；cart/login 由各自的 store 驅動。
const { data: brands } = await useBrandsMenu()
</script>

<template>
  <div id="sb-site">
    <div id="wrapper">
      <SiteHeader :brands="brands ?? []" />
      <slot />
      <SiteFooter />
    </div>
  </div>
  <MobileMenu :brands="brands ?? []" />
</template>
