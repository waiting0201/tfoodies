<script setup lang="ts">
// 訂單列表已整併進會員中心（/Member/Center 的「我的訂單」分頁）。
// 保留此路由以相容舊連結/書籤，直接導向會員中心。
await navigateTo('/Member/Center', { redirectCode: 301 })
</script>

<template>
  <div />
</template>
