<script setup lang="ts">
// 文章側欄：列出「其他消息／其他文章」。沿用舊系統 article-right 的功能，
// 介面改為與全站一致的卡片清單（圖片 + 標題）。
defineProps<{
  heading: string
  items: { href: string; photo?: string; label: string }[]
  blobUrl: string
}>()
</script>

<template>
  <aside v-if="items.length" class="aside">
    <h2 class="aside__title"><span class="aside__bar"></span>{{ heading }}</h2>
    <ul class="aside__list">
      <li v-for="(it, i) in items" :key="i">
        <a :href="it.href" class="aside__item">
          <span class="aside__thumb">
            <img :src="blobUrl + (it.photo ?? '')" :alt="it.label" loading="lazy">
          </span>
          <span class="aside__label">{{ it.label }}</span>
        </a>
      </li>
    </ul>
  </aside>
</template>

<style scoped>
.aside { color: #393939; }
.aside__title {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  margin: 0 0 1.1rem;
  font-size: 1.12rem;
  font-weight: 600;
  letter-spacing: 0.04em;
  color: #156467;
}
.aside__bar { width: 4px; height: 1.05em; border-radius: 2px; background: #26b7bc; }
.aside__list { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 0.75rem; }
.aside__item {
  display: flex;
  align-items: center;
  gap: 0.85rem;
  padding: 0.5rem;
  border: 1px solid #e8eaea;
  border-radius: 12px;
  background: #fff;
  text-decoration: none;
  transition: border-color 0.18s ease, box-shadow 0.18s ease, transform 0.18s ease;
}
.aside__item:hover {
  border-color: #b9e7e9;
  box-shadow: 0 8px 20px rgba(21, 100, 103, 0.1);
  transform: translateY(-1px);
}
.aside__thumb {
  flex: 0 0 auto;
  width: 76px; height: 76px;
  border-radius: 9px;
  overflow: hidden;
  background: #f6f9f9;
}
.aside__thumb img { display: block; width: 100%; height: 100%; object-fit: cover; }
.aside__label {
  font-size: 0.92rem;
  line-height: 1.55;
  color: #393939;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}
.aside__item:hover .aside__label { color: #156467; }
</style>
