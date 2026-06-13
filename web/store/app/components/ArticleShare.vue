<script setup lang="ts">
// 文章分享列：沿用舊系統 _Addthis 的分享功能（FB / LINE / 複製連結），
// 介面改為與 Recipe 詳細頁一致的圓形按鈕 + 複製提示。
const props = defineProps<{ url: string; title?: string }>()

const fbShare = computed(() =>
  `https://www.facebook.com/sharer/sharer.php?u=${encodeURIComponent(props.url)}`)
const lineShare = computed(() =>
  `https://social-plugins.line.me/lineit/share?url=${encodeURIComponent(props.url)}`)

const copied = ref(false)
async function copyLink() {
  try {
    await navigator.clipboard.writeText(props.url)
    copied.value = true
    setTimeout(() => (copied.value = false), 1800)
  } catch { /* clipboard 不可用時靜默忽略 */ }
}
</script>

<template>
  <div class="share">
    <span class="share__label">分享</span>
    <a :href="fbShare" target="_blank" rel="noopener" class="share__btn" aria-label="分享到 Facebook">
      <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M14 8.5V7c0-.8.4-1.2 1.3-1.2H17V3h-2.6C11.8 3 11 4.6 11 6.6v1.9H9V11h2v9h3v-9h2.2l.4-2.5z" fill="currentColor" stroke="none" /></svg>
    </a>
    <a :href="lineShare" target="_blank" rel="noopener" class="share__btn" aria-label="分享到 LINE">
      <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 4C7 4 3 7.3 3 11.3c0 3.6 3.2 6.6 7.5 7.2.3.1.7.2.8.5.1.3 0 .7 0 1l-.1.8c0 .2-.2.9.8.5s5.4-3.2 7.4-5.5c1.3-1.4 2-2.9 2-4.5C21.5 7.3 17.5 4 12 4z" fill="currentColor" stroke="none" /></svg>
    </a>
    <button type="button" class="share__btn" aria-label="複製連結" @click="copyLink">
      <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M9 9h9v9a2 2 0 0 1-2 2H9a2 2 0 0 1-2-2V9z" /><path d="M5 15V6a2 2 0 0 1 2-2h9" /></svg>
    </button>

    <Teleport to="body">
      <Transition name="copy-toast">
        <div v-if="copied" class="copy-toast">已複製連結</div>
      </Transition>
    </Teleport>
  </div>
</template>

<style scoped>
.share { display: flex; align-items: center; gap: 0.55rem; }
.share__label { font-size: 0.82rem; letter-spacing: 0.1em; color: #9b9b9b; margin-right: 0.15rem; }
.share__btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 34px; height: 34px;
  padding: 0;
  border: 1px solid #e8eaea;
  border-radius: 50%;
  background: #fff;
  color: #26b7bc;
  cursor: pointer;
  transition: background 0.18s ease, color 0.18s ease, border-color 0.18s ease;
}
.share__btn:hover { background: #26b7bc; color: #fff; border-color: #26b7bc; }
.share__btn svg { width: 17px; height: 17px; fill: none; stroke: currentColor; stroke-width: 1.7; stroke-linecap: round; stroke-linejoin: round; }

.copy-toast {
  position: fixed;
  left: 50%; bottom: 2.5rem;
  transform: translateX(-50%);
  z-index: 1000;
  padding: 0.7rem 1.5rem;
  font-size: 0.9rem;
  letter-spacing: 0.05em;
  color: #fff;
  background: rgba(38, 183, 188, 0.96);
  border-radius: 999px;
  box-shadow: 0 6px 20px rgba(0, 0, 0, 0.18);
}
.copy-toast-enter-active,
.copy-toast-leave-active { transition: opacity 0.3s ease, transform 0.3s ease; }
.copy-toast-enter-from,
.copy-toast-leave-to { opacity: 0; transform: translate(-50%, 0.75rem); }
</style>
