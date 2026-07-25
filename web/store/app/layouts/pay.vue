<script setup lang="ts">
// 收款連結專用極簡外殼。刻意不用 default.vue：這位訪客不是來逛街的，是被動收到一支連結、
// 目的單一（確認金額、付款）。SiteHeader 的社群列、免運跑馬燈、品牌導覽、mini-cart
// （此情境購物車必為空，顯示 0 反而讓人懷疑走錯頁）全是離開此頁的出口。
// 但完全裸表單又會讓人起疑「這是誰的頁面」——尤其常在 LINE 內建瀏覽器開啟、網址列被摺疊，
// 故只保留兩個訊號：品牌識別（logo）＋安全（SSL 提示）。
</script>

<template>
  <div class="pay-shell">
    <header class="pay-header">
      <a href="/" class="pay-header__logo" title="食在呼 TFoodies">
        <img src="/content/images/common/logo-main.png" alt="食在呼 TFoodies">
      </a>
      <div class="pay-header__trust">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
          <rect x="5" y="11" width="14" height="9" rx="2" />
          <path d="M8 11V8a4 4 0 0 1 8 0v3" />
        </svg>
        <span>SSL 加密連線</span>
      </div>
    </header>

    <main class="pay-main">
      <div class="pay-wrap">
        <slot />
      </div>
    </main>

    <footer class="pay-footer">
      © {{ new Date().getFullYear() }} 食在呼 TFoodies｜客服信箱
      <a href="mailto:service@tfoodies.com">service@tfoodies.com</a>
    </footer>
  </div>
</template>

<style scoped>
.pay-shell { min-height: 100vh; display: flex; flex-direction: column; background: #fafbfb; }

.pay-header {
  flex: 0 0 auto; height: 56px; background: #fff; border-bottom: 1px solid #ececec;
  display: flex; align-items: center; justify-content: space-between; padding: 0 1.2em;
}
.pay-header__logo { display: flex; align-items: center; height: 100%; }
.pay-header__logo img { height: 26px; display: block; }
.pay-header__trust { display: flex; align-items: center; gap: .35em; color: #9aa3a3; font-size: .78em; letter-spacing: .04em; }
.pay-header__trust svg { width: 13px; height: 13px; flex: 0 0 auto; }

.pay-main { flex: 1 1 auto; padding: 2.4em 1em 3em; }
.pay-wrap { max-width: 480px; margin: 0 auto; }

.pay-footer {
  flex: 0 0 auto; padding: 1.4em 1em; text-align: center;
  color: #b3b3b3; font-size: .78em; letter-spacing: .02em; border-top: 1px solid #f0f0f0;
}
.pay-footer a { color: #8a9292; text-decoration: underline; }

@media (max-width: 767px) {
  /* 手機有 sticky 底部確認列，留出空間避免遮住表單尾端 */
  .pay-main { padding-bottom: 6.5em; }
}
@media (max-width: 480px) {
  .pay-header__trust span { display: none; }   /* 極窄螢幕只留鎖頭圖示 */
}
</style>
