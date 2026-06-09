<script setup lang="ts">
// Verbatim port of @Html.PagedListPager output from PagedList.css.
// Generates <ul class="pagination"> links exactly as the legacy MVC helper did.
const props = defineProps<{
  currentPage: number
  totalPages: number
  buildUrl: (p: number) => string
}>()

const pages = computed(() => {
  if (props.totalPages <= 1) return []
  const list: { page: number; label: string }[] = []
  for (let i = 1; i <= props.totalPages; i++) list.push({ page: i, label: String(i) })
  return list
})
</script>

<template>
  <div v-if="totalPages > 1" class="centered more">
    <ul class="pagination">
      <li v-if="currentPage > 1" class="PagedList-skipToPrevious">
        <a :href="buildUrl(currentPage - 1)">«</a>
      </li>
      <li v-for="item in pages" :key="item.page" :class="item.page === currentPage ? 'active' : ''">
        <a :href="buildUrl(item.page)">{{ item.label }}</a>
      </li>
      <li v-if="currentPage < totalPages" class="PagedList-skipToNext">
        <a :href="buildUrl(currentPage + 1)">»</a>
      </li>
    </ul>
  </div>
</template>
